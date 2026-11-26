// =====================================================================================
//  GrowGoAmbience.cs  🌳🌊🐚🌧️ เสียงบรรยากาศในฉาก (สำหรับ Open House)
// -------------------------------------------------------------------------------------
//  - MainForest / FriendVisit = เสียงตามสภาพอากาศ (WeatherManager.currentWeather)
//        ปกติ/แดดจัด = เสียงป่า, หมอก = ป่าเงียบมีหยดน้ำ, ฝน = ฝนตกเบาๆ, พายุ = ฝนหนัก + ฟ้าร้อง/ฟ้าผ่า
//  - BeachIsland = เสียงคลื่นซัดชายหาด + เสียงหอยเขย่าเบาๆ ตอนหอยกำลังเขย่า (เป็น hint) สุ่มเสียง/pitch/ซ้ายขวา ไม่จำเจ
//  ไฟล์เสียงเจนจาก ElevenLabs Sound Effects อยู่ใน Resources/GrowGoAmbience/
//     ชื่อไฟล์ = กลุ่ม + เลขเวอร์ชัน เช่น forest, beach, shell_1, mist_2, thunder_far_3
//     กลุ่มเดียวกันมีหลายไฟล์ = สุ่มเล่น (ไม่มีไฟล์ = ข้ามเสียงนั้นไปเฉยๆ ไม่ error)
//     (ไฟล์ต้นฉบับจาก ElevenLabs เบามาก เลยเร่งเสียงด้วย ffmpeg ก่อนใส่ ต้นฉบับยังอยู่ข้างนอกโฟลเดอร์ Resources)
//  ไม่ต้องลากใส่ฉาก ไม่ต้องผูก Inspector สร้างตัวเองตอนเกมเริ่ม แล้วอยู่ข้ามฉาก
//  ความดังตามระดับ SFX ในหน้าตั้งค่า (PlayerPrefs "SfxVolume") ปรับระหว่างเล่นได้ทันที
//
//  ปิดเสียงนี้: ลบไฟล์นี้ทิ้งไฟล์เดียว (ไม่กระทบ GrowGoJuice ตัวอื่น)
// =====================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GrowGoAmbience : MonoBehaviour
{
    public static GrowGoAmbience I;
    public static bool Enabled = true;

    // ==================== ⚙️ ความดังแต่ละเสียง (คูณกับ SfxVolume) ====================
    public static float ForestVolume = 0.6f;
    public static float BeachVolume = 0.5f;
    public static float MistVolume = 0.6f;
    public static float RainVolume = 0.5f;
    public static float StormVolume = 0.55f;
    public static float ThunderVolume = 0.6f;
    public static float ShellVolume = 0.45f;
    public static int ShellMinGap = 1, ShellMaxGap = 1;   // เล่นเสียงทุกกี่รอบเขย่า (1 = ทุกรอบ ตรงกับหอยเขย่า, ถ้าอยากให้ห่างขึ้นใส่ 2-4)
    private const float SceneFade = 2f;         // ค่อยๆ ดัง/เบา ตอนเข้า-ออกฉาก หรืออากาศเปลี่ยน (วินาที)
    private const float LoopCrossfade = 3f;     // ไฟล์ไม่ได้ทำมาให้วนเนียน -> เกลี่ยท้ายไฟล์เข้าต้นไฟล์ 3 วินาที

    // ==================== 🔊 ตัวเล่นเสียง ====================
    // วนเสียงด้วย AudioSource 2 ตัวสลับกัน: ใกล้จบไฟล์ ตัวที่ 2 เริ่มจากต้นไฟล์แล้วค่อยๆ ดังขึ้น ตัวแรกค่อยๆ เบาลง
    private AudioSource[] loopSrc = new AudioSource[2];
    private int cur;                 // ตัวที่กำลังเป็นตัวหลัก
    private float xfade = 1f;        // 0..1 ความคืบหน้าการเกลี่ยรอยต่อ (1 = ไม่ได้เกลี่ยอยู่)
    private AudioSource shellSrc, thunderSrc;

    // เสียงทั้งหมด จัดกลุ่มตามชื่อไฟล์ (ตัดเลขเวอร์ชันท้ายชื่อออก)
    private readonly Dictionary<string, List<AudioClip>> groups = new Dictionary<string, List<AudioClip>>();
    private readonly Dictionary<string, AudioClip> lastPicked = new Dictionary<string, AudioClip>();

    private string currentKey = null;  // เสียงวนที่ต้องการตอนนี้ เช่น "forest", "rain"
    private AudioClip loopClip;      // คลิปที่กำลังวนอยู่
    private float targetBase;        // ความดังที่ต้องการ (0 = ไม่มีเสียงบรรยากาศ)
    private float fade;              // 0..1 เฟดตอนเข้า-ออกฉาก
    private AudioClip pendingClip;   // คลิปที่รอเล่นหลังเฟดของเก่าออกจนหมด

    // ---- หอยเขย่า (อ่านค่าจาก BeachShellManager ด้วย reflection ไม่แก้สคริปต์เพื่อน) ----
    private FieldInfo fActiveShell, fShellRot, fCooldown;
    private bool shellWasTilted;
    private int shakeBursts, nextShellBurst;

    // ==================== 🚀 สร้างตัวเองตอนเกมเริ่ม ====================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (I != null) return;
        GameObject go = new GameObject("[GrowGoAmbience]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<GrowGoAmbience>();
    }

    private void Awake()
    {
        for (int i = 0; i < 2; i++)
        {
            AudioSource a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = false; a.spatialBlend = 0f; a.volume = 0f;
            loopSrc[i] = a;
        }
        shellSrc = gameObject.AddComponent<AudioSource>();
        shellSrc.playOnAwake = false; shellSrc.spatialBlend = 0f;
        thunderSrc = gameObject.AddComponent<AudioSource>();
        thunderSrc.playOnAwake = false; thunderSrc.spatialBlend = 0f;

        // โหลดไฟล์เสียงทั้งหมดใน Resources/GrowGoAmbience/ แล้วจัดกลุ่ม: "thunder_far_2" -> กลุ่ม "thunder_far"
        foreach (AudioClip c in Resources.LoadAll<AudioClip>("GrowGoAmbience"))
        {
            if (c == null) continue;
            string key = Regex.Replace(c.name.ToLowerInvariant(), "_\\d+$", "");
            if (!groups.ContainsKey(key)) groups[key] = new List<AudioClip>();
            groups[key].Add(c);
        }

        fActiveShell = typeof(BeachShellManager).GetField("activeShell", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fShellRot = typeof(BeachShellManager).GetField("activeShellOriginalRotation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fCooldown = typeof(BeachShellManager).GetField("isCooldown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        shellWasTilted = false; shakeBursts = 0; nextShellBurst = 0;
    }

    // ==================== 🎚️ ฉาก + อากาศ -> ต้องเล่นเสียงอะไร ====================
    private static string DesiredKey()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "BeachIsland") return "beach";
        if (scene == "MainForest" || scene == "FriendVisit")
        {
            switch (GrowGoWeather.CurrentWeatherId())
            {
                case "WTH_MIST": return "mist";
                case "WTH_RAIN": return "rain";
                case "WTH_STORM": return "storm";
                default: return "forest";    // WTH_CLEAR / WTH_HOT
            }
        }
        return "";
    }

    private static float VolumeOf(string key)
    {
        switch (key)
        {
            case "forest": return ForestVolume;
            case "beach": return BeachVolume;
            case "mist": return MistVolume;
            case "rain": return RainVolume;
            case "storm": return StormVolume;
            default: return 0f;
        }
    }

    // สุ่มคลิปในกลุ่ม ไม่ให้ซ้ำกับครั้งก่อน (ถ้ามีมากกว่า 1 ไฟล์)
    private AudioClip Pick(string key)
    {
        List<AudioClip> list;
        if (!groups.TryGetValue(key, out list) || list.Count == 0) return null;
        AudioClip last; lastPicked.TryGetValue(key, out last);
        AudioClip c = list[UnityEngine.Random.Range(0, list.Count)];
        if (list.Count > 1 && c == last) c = list[(list.IndexOf(c) + 1 + UnityEngine.Random.Range(0, list.Count - 1)) % list.Count];
        lastPicked[key] = c;
        return c;
    }

    private void StartLoop(AudioClip c)
    {
        pendingClip = null;
        loopClip = c;
        cur = 0; xfade = 1f;
        loopSrc[1].Stop();
        loopSrc[0].clip = c;
        // เริ่มตรงจุดสุ่ม (ไม่ให้เหมือนเดิมทุกครั้งที่เข้าฉาก) แต่ไม่ให้ใกล้ท้ายไฟล์เกินไป
        float maxStart = Mathf.Max(0f, c.length - LoopCrossfade * 2f - 1f);
        loopSrc[0].time = UnityEngine.Random.Range(0f, maxStart);
        loopSrc[0].Play();
        fade = 0f;
    }

    // ==================== 🔁 ทุกเฟรม ====================
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float master = PlayerPrefs.GetFloat("SfxVolume", 1f);

        // ---- ฉาก/อากาศเปลี่ยน -> เปลี่ยนเสียง ----
        string key = Enabled ? DesiredKey() : "";
        if (key != currentKey)
        {
            currentKey = key;
            AudioClip want = key == "" ? null : Pick(key);
            targetBase = want != null ? VolumeOf(key) : 0f;
            if (want == null) pendingClip = null;
            else if (loopClip != null && fade > 0.01f) pendingClip = want;   // มีเสียงเก่าเล่นอยู่ เฟดออกก่อนแล้วค่อยเปลี่ยน
            else StartLoop(want);
        }

        // ---- เฟดเข้า-ออก ----
        bool wantOn = targetBase > 0f && pendingClip == null && loopClip != null;
        fade = Mathf.MoveTowards(fade, wantOn ? 1f : 0f, dt / SceneFade);
        if (fade <= 0f)
        {
            if (pendingClip != null) StartLoop(pendingClip);
            else if (loopClip != null && targetBase <= 0f) { loopSrc[0].Stop(); loopSrc[1].Stop(); loopClip = null; }
        }

        // ---- วนไฟล์แบบเกลี่ยรอยต่อ ----
        if (loopClip != null)
        {
            AudioSource a = loopSrc[cur];
            if (xfade >= 1f && a.isPlaying && a.time >= loopClip.length - LoopCrossfade)
            {
                AudioSource b = loopSrc[1 - cur];   // ตัวใหม่เริ่มจากต้นไฟล์
                b.clip = loopClip; b.time = 0f; b.Play();
                cur = 1 - cur; xfade = 0f;
            }
            if (xfade < 1f) xfade = Mathf.Min(1f, xfade + dt / LoopCrossfade);
            // equal-power: เสียงไม่วูบตรงกลางช่วงเกลี่ย
            float vNew = Mathf.Sin(xfade * Mathf.PI * 0.5f), vOld = Mathf.Cos(xfade * Mathf.PI * 0.5f);
            float baseVol = targetBase * master * fade * fade;
            loopSrc[cur].volume = baseVol * vNew;
            loopSrc[1 - cur].volume = baseVol * vOld;
            if (xfade >= 1f && loopSrc[1 - cur].isPlaying) loopSrc[1 - cur].Stop();
        }

        WatchShell(master);
    }

    // =====================================================================================
    //  ⚡ ฟ้าร้อง/ฟ้าผ่า : GrowGoWeather เรียกตอนแฟลช เพื่อให้เสียงตรงกับภาพ
    //  strike = ฟ้าผ่าใกล้ (เสียงมาแทบทันที), ไม่ใช่ = ฟ้าร้องไกล (แสงมาก่อน เสียงตามมาทีหลัง เหมือนของจริง)
    // =====================================================================================
    public void PlayThunder(bool strike, float delay)
    {
        if (!Enabled) return;
        StartCoroutine(ThunderRoutine(strike ? "thunder_strike" : "thunder_far", delay));
    }

    private IEnumerator ThunderRoutine(string key, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        AudioClip c = Pick(key);
        if (c == null && key == "thunder_strike") c = Pick("thunder_far");   // ไม่มีไฟล์ฟ้าผ่า ใช้ฟ้าร้องแทน
        if (c == null) yield break;
        float master = PlayerPrefs.GetFloat("SfxVolume", 1f);
        thunderSrc.pitch = UnityEngine.Random.Range(0.93f, 1.05f);
        thunderSrc.PlayOneShot(c, ThunderVolume * master * UnityEngine.Random.Range(0.8f, 1f));
    }

    // =====================================================================================
    //  🐚 เสียงหอยตอนหอยเขย่า
    //  BeachShellManager หมุนหอยไปมา 4 ครั้งแล้วพัก 1.5 วิ วนไปเรื่อยๆ
    //  เราดูว่าหอยเอียงออกจากมุมเดิมเมื่อไหร่ = เริ่มเขย่ารอบใหม่ แล้วเล่นเสียงทันที (ไฟล์ตัดให้ยาว 0.4 วิ พอดีกับเขย่า 0.32 วิ)
    // =====================================================================================
    private void WatchShell(float master)
    {
        List<AudioClip> shells;
        if (!groups.TryGetValue("shell", out shells) || shells.Count == 0 || fActiveShell == null) return;
        BeachShellManager m = BeachShellManager.Instance;
        if (m == null || !m.isActiveAndEnabled) { shellWasTilted = false; return; }
        try
        {
            Button shell = fActiveShell.GetValue(m) as Button;
            bool cooldown = fCooldown != null && (bool)fCooldown.GetValue(m);
            if (shell == null || cooldown || !shell.gameObject.activeInHierarchy) { shellWasTilted = false; return; }

            Quaternion orig = fShellRot != null ? (Quaternion)fShellRot.GetValue(m) : Quaternion.identity;
            bool tilted = Quaternion.Angle(shell.transform.localRotation, orig) > 1f;
            if (tilted && !shellWasTilted)
            {
                if (shakeBursts >= nextShellBurst)
                {
                    PlayShell(shell.transform, master);
                    nextShellBurst = shakeBursts + UnityEngine.Random.Range(ShellMinGap, ShellMaxGap + 1);
                }
                shakeBursts++;
            }
            // ช่วงเขย่ามุมกลับมาตรงเดิมแค่ตอนจบรอบ (พัก 1.5 วิ) จึงนับ "เอียง" ต่อเนื่องจนพัก
            if (tilted) { shellWasTilted = true; straightSince = -1f; }
            else if (shellWasTilted && !IsMidBurst()) shellWasTilted = false;
        }
        catch (Exception e) { Debug.LogWarning("[GrowGoAmbience] shell: " + e.Message); fActiveShell = null; }
    }

    // ---- ให้เสียงหอยไม่จำเจ ----
    // 1) สุ่มไฟล์ (ไม่ซ้ำกับครั้งก่อน)  2) สุ่ม pitch สูง-ต่ำนิดหน่อย  3) สุ่มความดัง
    // 4) แพนเสียงซ้าย-ขวาตามตำแหน่งหอยบนจอ (หอยอยู่ซ้าย เสียงมาทางซ้าย)
    private void PlayShell(Transform shell, float master)
    {
        AudioClip c = Pick("shell");
        if (c == null) return;
        shellSrc.pitch = UnityEngine.Random.Range(0.95f, 1.06f);   // สุ่มนิดเดียว ให้ความยาวเสียงยังพอดีกับจังหวะเขย่า
        float x = 0.5f;
        Camera cam = Camera.main;
        Canvas cv = shell.GetComponentInParent<Canvas>();
        if (cv != null && cv.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) x = shell.position.x / Mathf.Max(1, Screen.width);
        else if (cam != null) x = cam.WorldToViewportPoint(shell.position).x;
        shellSrc.panStereo = Mathf.Clamp((x - 0.5f) * 0.8f, -0.4f, 0.4f);
        shellSrc.PlayOneShot(c, ShellVolume * master * UnityEngine.Random.Range(0.75f, 1f));
    }

    // ระหว่างเขย่า หอยจะเอียงทุก 0.04 วิ ถ้าตรงอยู่นานกว่า 0.2 วิ = จบรอบเขย่าแล้ว
    private float straightSince = -1f;
    private bool IsMidBurst()
    {
        if (straightSince < 0f) straightSince = Time.unscaledTime;
        bool mid = Time.unscaledTime - straightSince < 0.2f;
        if (!mid) straightSince = -1f;
        return mid;
    }
}
