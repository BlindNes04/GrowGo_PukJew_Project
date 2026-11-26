// =====================================================================================
//  GrowGoLoadingFX.cs  🍅✨ ทรานซิชัน หน้าโหลด -> MainMenu (สำหรับ Open House)
// -------------------------------------------------------------------------------------
//  หน้าโหลด (LoadingScene)
//    - ระหว่างโหลด: ตัวอักษร "Loading..." (object ชื่อ LoadingText) กระโดดขึ้นลงทีละตัวเป็นคลื่น
//    - โหลดเสร็จ: ตัวหนังสือย่อตัว -> เด้งขึ้น + แสงวาบ + ประกายดาว + เสียงกริ๊ง แล้วเปลี่ยนเป็น "Let's Grow!"
//    - แล้วโลโก้ (GameLogo) ย่อนิดนึง แล้วพุ่งขยายเข้าไปในลูกมะเขือเทศจนจอแดงเต็มจอ (มีเสียงวูบ)
//    - LoadingManager รอจนอนิเมชันจบก่อนค่อยสลับฉาก (ผ่าน hook LoadingManager.BeforeSceneSwitch)
//  หน้า MainMenu (เฉพาะครั้งแรกที่มาจากหน้าโหลด)
//    - ข้ามอนิเมชันเลื่อนหน้าของ MainMenuPanel (ครั้งแรกเท่านั้น กดไป login แล้วกลับมายังเล่นปกติ)
//    - จอแดงจางออก + โลโก้หดกลับไปอยู่ตำแหน่ง GameLogo ของ MainMenuPanel แล้วส่งต่อให้ตัวจริง
//    - พอโลโก้ลงที่ ชื่อเกม (GameName) เด้งโผล่ขึ้นมา
//
//  สร้างตัวเองอัตโนมัติ (RuntimeInitializeOnLoadMethod) + อยู่ข้ามฉาก (DontDestroyOnLoad)
//  ไม่ต้องลากใส่ฉาก ไม่ต้องผูก Inspector
//  ปิดเอฟเฟกต์นี้: ตั้ง Enabled = false หรือลบไฟล์นี้ทิ้ง (LoadingManager จะสลับฉากทันทีเหมือนเดิม)
// =====================================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GrowGoLoadingFX : MonoBehaviour
{
    public static GrowGoLoadingFX I;
    public static bool Enabled = true;

    // ==================== ⚙️ ค่าที่ปรับได้ ====================
    public static string DoneText = "Let's Grow!";        // คำที่เปลี่ยนตอนโหลดเสร็จ
    public static float BounceHeight = 0.22f;             // ความสูงที่ตัวอักษรกระโดด (สัดส่วนของขนาดฟอนต์)
    public static float BounceSpeed = 6f;                 // ความเร็วกระโดด
    public static float LetterDelay = 0.35f;              // ตัวถัดไปกระโดดช้ากว่ากันเท่านี้ (เรเดียน) -> เป็นคลื่น
    public static float HoldAfterPop = 0.55f;             // โชว์ "Let's Grow!" ค้างไว้กี่วินาทีก่อนโลโก้พุ่ง
    public static float ZoomTime = 0.6f;                  // เวลาโลโก้พุ่งเต็มจอ
    public static float ShrinkTime = 0.75f;               // เวลาโลโก้หดกลับที่ MainMenu

    // จุดกลางลูกมะเขือเทศในรูป game logo (วัดจากรูปจริง: 0..1 จากซ้ายล่าง) + รัศมีส่วนที่แดงทึบ
    static readonly Vector2 TomatoPivot = new Vector2(0.503f, 0.44f);
    const float TomatoRadius = 0.15f;
    static readonly Color TomatoRed = new Color32(237, 109, 94, 255);

    const string LoadingSceneName = "LoadingScene", MenuSceneName = "MainMenu";
    const int SR = 44100;

    // ==================== 🚀 สร้างตัวเองตอนเกมเริ่ม ====================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || I != null) return;
        GameObject go = new GameObject("[GrowGoLoadingFX]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<GrowGoLoadingFX>();
    }

    TMP_Text loadingText;
    float waveAmp = 1f;
    bool comingFromLoading;

    Canvas overlay;
    RectTransform flyLogo;
    Image fill;
    float flyBaseWidth, coverScale;

    AudioSource sfx;
    AudioListener bridge;   // หูชั่วคราวช่วงสลับฉาก (กล้องฉากเก่าหาย -> ไม่มีหู -> เสียงตัดดังป๊อก)
    AudioClip clipSparkle, clipWhoosh, clipLand;
    Sprite softSprite, starSprite;

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.ignoreListenerPause = true;
        BuildSounds();
        softSprite = MakeSprite(64, SoftDot);
        starSprite = MakeSprite(64, Star);
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == LoadingSceneName) SetupLoading();
    }

    // ==================== 🧪 DEBUG: วัดความดังเสียงที่ออกลำโพงจริงช่วงทรานซิชัน (เขียนลง Logs/growgo_audio.csv) ====================
    public static bool DebugAudio = false;   // true = บันทึกความดังลง Logs/growgo_audio.csv ไว้ตรวจเสียง
    System.Text.StringBuilder audioLog;
    float audioLogStart, audioLogUntil = -1f;
    readonly float[] outL = new float[1024];

    void Update()
    {
        if (audioLog == null) return;
        float now = Time.realtimeSinceStartup;
        AudioListener.GetOutputData(outL, 0);
        double sum = 0; float pk = 0;
        for (int i = 0; i < outL.Length; i++) { sum += outL[i] * outL[i]; pk = Mathf.Max(pk, Mathf.Abs(outL[i])); }
        float db = 20f * Mathf.Log10(Mathf.Sqrt((float)(sum / outL.Length)) + 1e-6f);
        int nl = 0; foreach (AudioListener l in FindObjectsByType<AudioListener>()) if (l.isActiveAndEnabled) nl++;
        string bgm = "";
        if (MAudioManager.instance != null && MAudioManager.instance.bgmSource != null)
        {
            AudioSource b = MAudioManager.instance.bgmSource;
            bgm = b.isPlaying + ";" + b.time.ToString("0.000") + ";" + b.volume.ToString("0.00") + ";" + (b.clip ? b.clip.loadState.ToString() : "-");
        }
        audioLog.AppendLine((now - audioLogStart).ToString("0.000") + "," + SceneManager.GetActiveScene().name + "," + db.ToString("0.0") + "," + pk.ToString("0.000") + "," + nl + "," + AudioListener.volume + "," + bgm);
        if (audioLogUntil > 0 && now > audioLogUntil)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/growgo_audio.csv"), audioLog.ToString()); } catch (Exception e) { Debug.LogWarning(e); }
            Debug.Log("[GrowGoLoadingFX] 🧪 เขียน Logs/growgo_audio.csv แล้ว");
            audioLog = null;
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (I == this) I = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        ReleaseListener();
        if (s.name == LoadingSceneName) { SetupLoading(); return; }
        loadingText = null;
        if (s.name == MenuSceneName && comingFromLoading)
        {
            comingFromLoading = false;
            StartCoroutine(Intro(s));
        }
        else KillOverlay();   // กันจอแดงค้าง ถ้าไปฉากอื่น
    }

    // =====================================================================================
    //  ⏳ หน้าโหลด: หา LoadingText + ฝาก hook ไว้กับ LoadingManager
    // =====================================================================================
    void SetupLoading()
    {
        Scene s = SceneManager.GetActiveScene();
        Transform t = FindInScene(s, "LoadingText");
        loadingText = t ? t.GetComponent<TMP_Text>() : null;
        waveAmp = 1f;
        LoadingManager.BeforeSceneSwitch = Outro;
    }

    // ตัวอักษรกระโดดเป็นคลื่น (แก้ตำแหน่ง vertex ของ TMP ทุกเฟรม)
    void LateUpdate()
    {
        if (loadingText == null || !loadingText.isActiveAndEnabled) return;
        loadingText.ForceMeshUpdate();
        if (waveAmp <= 0.001f) return;
        TMP_TextInfo ti = loadingText.textInfo;
        float h = loadingText.fontSize * BounceHeight * waveAmp;
        float now = Time.unscaledTime * BounceSpeed;
        for (int i = 0; i < ti.characterCount; i++)
        {
            TMP_CharacterInfo ci = ti.characterInfo[i];
            if (!ci.isVisible) continue;
            float hop = Mathf.Sin(now - i * LetterDelay);
            float y = Mathf.Max(0f, hop) * h;                      // ครึ่งบนของคลื่น = กระโดด
            float squash = hop < -0.85f ? (hop + 0.85f) * 0.6f : 0f; // ตอนแตะพื้นยุบนิดๆ
            Vector3[] v = ti.meshInfo[ci.materialReferenceIndex].vertices;
            int vi = ci.vertexIndex;
            float baseY = (v[vi].y + v[vi + 1].y) * 0.5f;
            for (int j = 0; j < 4; j++)
            {
                Vector3 p = v[vi + j];
                p.y = baseY + (p.y - baseY) * (1f + squash) + y;
                v[vi + j] = p;
            }
        }
        for (int m = 0; m < ti.meshInfo.Length; m++)
        {
            ti.meshInfo[m].mesh.vertices = ti.meshInfo[m].vertices;
            loadingText.UpdateGeometry(ti.meshInfo[m].mesh, m);
        }
    }

    // =====================================================================================
    //  🎉 โหลดเสร็จ: ตัวหนังสือเด้ง -> Let's Grow! -> โลโก้พุ่งเต็มจอ (LoadingManager รอจนจบ)
    // =====================================================================================
    IEnumerator Outro()
    {
        LoadingManager.BeforeSceneSwitch = null;
        comingFromLoading = true;
        if (DebugAudio) { audioLog = new System.Text.StringBuilder("t,scene,rmsDb,peak,listeners,listenerVol,bgm(playing;time;vol;load)\n"); audioLogStart = Time.realtimeSinceStartup; audioLogUntil = -1f; }
        if (loadingText != null) yield return PopText();
        yield return ZoomLogo();
    }

    IEnumerator PopText()
    {
        RectTransform rt = loadingText.rectTransform;
        Vector3 s0 = rt.localScale;

        // ย่อตัวเตรียมกระโดด + คลื่นหยุด
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
        {
            waveAmp = 1f - t;
            rt.localScale = Vector3.Scale(s0, new Vector3(1f + 0.08f * t, 1f - 0.18f * t, 1f));
            yield return null;
        }
        waveAmp = 0f;

        // เด้ง! เปลี่ยนคำ + แสงวาบ + ประกาย + เสียง
        loadingText.text = DoneText;
        Play(clipSparkle, 0.9f);
        Burst(rt, 12, 1f);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
        {
            float k = t < 0.2f ? Mathf.Lerp(0.9f, 1.35f, EaseOut(t / 0.2f))
                               : Mathf.LerpUnclamped(1.35f, 1f, ElasticOut((t - 0.2f) / 0.8f));
            float sx = k * (t < 0.2f ? 0.95f : 1f);
            rt.localScale = Vector3.Scale(s0, new Vector3(sx, k, 1f));
            yield return null;
        }
        rt.localScale = s0;

        // โชว์ค้างไว้ ตัวอักษรกระโดดเบาๆ
        for (float t = 0; t < HoldAfterPop; t += Time.unscaledDeltaTime)
        {
            waveAmp = Mathf.Min(0.55f, t * 3f);
            yield return null;
        }
    }

    IEnumerator ZoomLogo()
    {
        Scene s = SceneManager.GetActiveScene();
        Transform lt = FindInScene(s, "GameLogo");
        Image src = lt ? lt.GetComponent<Image>() : null;

        BuildOverlay();
        Vector2 startPos, size;
        if (src != null && src.sprite != null)
        {
            ScreenRect(src.rectTransform, out Vector2 bl, out size);
            flyLogo = NewImage("FlyLogo", overlay.transform, src.sprite, Color.white);
            flyLogo.anchorMin = flyLogo.anchorMax = Vector2.zero;
            flyLogo.pivot = TomatoPivot;
            flyLogo.sizeDelta = size;
            startPos = bl + Vector2.Scale(TomatoPivot, size);
            flyLogo.anchoredPosition = startPos;
            flyLogo.SetSiblingIndex(0);
            src.enabled = false;
        }
        else
        {
            size = new Vector2(Screen.width, Screen.width) * 0.5f;
            startPos = new Vector2(Screen.width, Screen.height) * 0.5f;
        }
        flyBaseWidth = size.x;

        // ต้องขยายเท่าไหร่ ส่วนแดงของมะเขือเทศถึงจะคลุมทั้งจอ
        float far = 0f;
        foreach (Vector2 c in new[] { Vector2.zero, new Vector2(Screen.width, 0), new Vector2(0, Screen.height), new Vector2(Screen.width, Screen.height) })
            far = Mathf.Max(far, Vector2.Distance(c, startPos));
        coverScale = far / Mathf.Max(1f, TomatoRadius * size.x) * 1.08f;

        // ย่อตัวเตรียมพุ่ง
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.16f)
        {
            if (flyLogo) flyLogo.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, EaseOut(t));
            yield return null;
        }

        // พุ่งเข้าไปในลูกมะเขือเทศ จอแดงเต็ม
        Play(clipWhoosh, 0.8f);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / ZoomTime)
        {
            float e = t * t * t;
            if (flyLogo) flyLogo.localScale = Vector3.one * Mathf.Lerp(0.88f, coverScale, e);
            SetFill(Mathf.Clamp01((t - 0.55f) / 0.4f));
            yield return null;
        }
        if (flyLogo) flyLogo.localScale = Vector3.one * coverScale;
        SetFill(1f);
        yield return null;   // ให้เฟรมแดงเต็มจอขึ้นจอก่อนสลับฉาก
        HoldListener();
    }

    // =====================================================================================
    //  🏠 MainMenu (ครั้งแรกจากหน้าโหลด): ข้ามอนิเมชันเลื่อนหน้า + โลโก้หดกลับที่ + ชื่อเกมเด้งโผล่
    // =====================================================================================
    IEnumerator Intro(Scene s)
    {
        if (audioLog != null) audioLogUntil = Time.realtimeSinceStartup + 5f;
        Transform panel = FindInScene(s, "MainMenuPanel");
        SkipPanelAnimation(panel);

        Transform lt = panel ? FindChildDeep(panel, "GameLogo") : FindInScene(s, "GameLogo");
        Image target = lt ? lt.GetComponent<Image>() : null;
        Transform nt = panel ? FindChildDeep(panel, "GameName") : FindInScene(s, "GameName");
        CanvasGroup nameCg = null;
        if (nt != null)
        {
            nameCg = nt.GetComponent<CanvasGroup>();
            if (nameCg == null) nameCg = nt.gameObject.AddComponent<CanvasGroup>();
            nameCg.alpha = 0f;
        }
        if (target != null) target.enabled = false;

        yield return null;               // รอ layout ของฉากใหม่เข้าที่
        SkipPanelAnimation(panel);       // กันไว้อีกรอบ (เผื่อ Animator เพิ่งเริ่มทำงาน)
        Canvas.ForceUpdateCanvases();

        if (flyLogo == null || target == null)
        {
            // ไม่มีโลโก้ให้ส่งต่อ -> แค่จางจอแดงออก
            if (target) target.enabled = true;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.4f) { SetFill(1f - t); yield return null; }
            if (nameCg) nameCg.alpha = 1f;
            KillOverlay();
            yield break;
        }

        ScreenRect(target.rectTransform, out Vector2 bl, out Vector2 tsize);
        Vector2 endPos = bl + Vector2.Scale(TomatoPivot, tsize);
        Vector2 startPos = flyLogo.anchoredPosition;
        float endScale = tsize.x / Mathf.Max(1f, flyBaseWidth);

        Play(clipWhoosh, 0.45f, 1.25f);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / ShrinkTime)
        {
            float ep = EaseOutCubic(t);
            // หดแบบ log (ขนาดเปลี่ยนเยอะมาก ใช้ log ให้ดูนุ่มเท่ากันตลอด) + เลยเป้านิดแล้วเด้งกลับ
            float es = BackOut(t, 1.1f);
            float sc = Mathf.Exp(Mathf.LerpUnclamped(Mathf.Log(coverScale), Mathf.Log(endScale), es));
            flyLogo.localScale = Vector3.one * sc;
            flyLogo.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, ep);
            SetFill(1f - Mathf.Clamp01(t / 0.35f));
            yield return null;
        }

        // ลงที่แล้ว: ส่งต่อให้โลโก้ตัวจริง
        target.enabled = true;
        KillOverlay();
        Play(clipLand, 0.8f);
        StartCoroutine(Punch(target.rectTransform, 0.1f, 0.35f));

        // ชื่อเกมเด้งโผล่
        if (nt != null)
        {
            RectTransform nrt = (RectTransform)nt;
            Vector3 n0 = nrt.localScale;
            Burst(nrt, 8, 0.7f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.55f)
            {
                nameCg.alpha = Mathf.Clamp01(t / 0.25f);
                nrt.localScale = n0 * Mathf.LerpUnclamped(0.4f, 1f, ElasticOut(t));
                yield return null;
            }
            nrt.localScale = n0;
            nameCg.alpha = 1f;
        }
    }

    // =====================================================================================
    //  🎧 หูชั่วคราวข้ามฉาก: ปิดหูของกล้องฉากโหลด ใช้หูของตัวนี้แทน จนฉากใหม่มีหูของตัวเอง
    //  (ไม่งั้นช่วงสลับฉากไม่มี AudioListener เลย Unity ตัดเสียงทั้งหมดทันที = เสียงลำโพงตัด)
    // =====================================================================================
    void HoldListener()
    {
        foreach (AudioListener l in FindObjectsByType<AudioListener>())
            if (l.gameObject != gameObject) l.enabled = false;
        if (bridge == null) bridge = gameObject.AddComponent<AudioListener>();
        bridge.enabled = true;
    }

    void ReleaseListener()
    {
        if (bridge == null || !bridge.enabled) return;
        foreach (AudioListener l in FindObjectsByType<AudioListener>())
            if (l != bridge && l.isActiveAndEnabled) { bridge.enabled = false; return; }
        // ฉากใหม่ไม่มีหูของตัวเอง -> ใช้หูของตัวนี้ต่อไป
    }

    // ข้ามอนิเมชันของ MainMenuPanel ไปเฟรมสุดท้าย (ครั้งนี้ครั้งเดียว ครั้งหน้าเปิดแผงใหม่ Animator เริ่มเล่นใหม่เอง)
    static void SkipPanelAnimation(Transform panel)
    {
        if (panel == null) return;
        Animator an = panel.GetComponent<Animator>();
        if (an == null || !an.isActiveAndEnabled || an.runtimeAnimatorController == null) return;
        an.Update(0f);
        int hash = an.GetCurrentAnimatorStateInfo(0).fullPathHash;
        an.Play(hash, 0, 1f);
        an.Update(0f);
    }

    IEnumerator Punch(RectTransform rt, float amount, float dur)
    {
        Vector3 s0 = rt.localScale;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / dur)
        {
            if (rt == null) yield break;
            rt.localScale = s0 * (1f + amount * Mathf.Sin(t * Mathf.PI) * (1f - t));
            yield return null;
        }
        if (rt != null) rt.localScale = s0;
    }

    // =====================================================================================
    //  ✨ แสงวาบ + ประกายดาวรอบ UI
    // =====================================================================================
    void Burst(RectTransform around, int count, float power)
    {
        Transform parent = around.parent;
        if (parent == null) return;
        Rect r = around.rect;
        float size = Mathf.Max(r.height, 60f);

        // แสงวาบด้านหลัง
        RectTransform glow = NewImage("Glow", parent, softSprite, new Color(1f, 0.95f, 0.6f, 0.9f));
        glow.SetSiblingIndex(around.GetSiblingIndex());
        glow.localPosition = around.localPosition + (Vector3)(r.center);
        glow.sizeDelta = new Vector2(Mathf.Max(r.width, size) * 1.5f, size * 2.6f);
        StartCoroutine(GlowAnim(glow, power));

        // ดาวกระจาย
        for (int i = 0; i < count; i++)
        {
            RectTransform st = NewImage("Sparkle", parent, starSprite, i % 3 == 0 ? Color.white : new Color(1f, 0.9f, 0.45f, 1f));
            st.SetAsLastSibling();
            Vector2 from = r.center + new Vector2(UnityEngine.Random.Range(-0.4f, 0.4f) * r.width, UnityEngine.Random.Range(-0.3f, 0.3f) * r.height);
            st.localPosition = around.localPosition + (Vector3)from;
            float s = size * UnityEngine.Random.Range(0.35f, 0.6f) * power;
            st.sizeDelta = new Vector2(s, s);
            float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector2 dir = new Vector2(Mathf.Cos(ang) * 1.6f, Mathf.Sin(ang));
            StartCoroutine(SparkleAnim(st, dir * size * UnityEngine.Random.Range(1.2f, 2.2f) * power, UnityEngine.Random.Range(0.5f, 0.8f)));
        }
    }

    IEnumerator GlowAnim(RectTransform g, float power)
    {
        Image im = g.GetComponent<Image>();
        Color c = im.color;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.55f)
        {
            if (g == null) yield break;
            g.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, EaseOut(t));
            im.color = new Color(c.r, c.g, c.b, c.a * power * (1f - t) * Mathf.Clamp01(t * 8f));
            yield return null;
        }
        if (g != null) Destroy(g.gameObject);
    }

    IEnumerator SparkleAnim(RectTransform st, Vector2 move, float dur)
    {
        Image im = st.GetComponent<Image>();
        Vector3 p0 = st.localPosition;
        float spin = UnityEngine.Random.Range(-260f, 260f);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / dur)
        {
            if (st == null) yield break;
            st.localPosition = p0 + (Vector3)(move * EaseOut(t));
            st.localRotation = Quaternion.Euler(0, 0, spin * t);
            float k = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            st.localScale = Vector3.one * Mathf.Max(0f, k);
            yield return null;
        }
        if (st != null) Destroy(st.gameObject);
    }

    // =====================================================================================
    //  🧱 overlay ข้ามฉาก (โลโก้บิน + จอแดง)
    // =====================================================================================
    void BuildOverlay()
    {
        KillOverlay();
        GameObject go = new GameObject("GrowGoTransitionCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);   // ลูกของตัวนี้ = อยู่ข้ามฉากด้วย
        overlay = go.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 5000;
        go.AddComponent<GraphicRaycaster>();        // บล็อกการกดระหว่างทรานซิชัน
        fill = NewImage("Fill", go.transform, null, new Color(TomatoRed.r, TomatoRed.g, TomatoRed.b, 0f)).GetComponent<Image>();
        RectTransform f = fill.rectTransform;
        f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = f.offsetMax = Vector2.zero;
        fill.raycastTarget = true;
    }

    void SetFill(float a)
    {
        if (fill != null) fill.color = new Color(TomatoRed.r, TomatoRed.g, TomatoRed.b, a);
    }

    void KillOverlay()
    {
        if (overlay != null) Destroy(overlay.gameObject);
        overlay = null; flyLogo = null; fill = null;
    }

    // มุมซ้ายล่าง + ขนาด ของ UI บนจอ (หน่วย pixel)
    static void ScreenRect(RectTransform rt, out Vector2 bottomLeft, out Vector2 size)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Canvas cv = rt.GetComponentInParent<Canvas>();
        Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        bottomLeft = a;
        size = b - a;
    }

    static RectTransform NewImage(string name, Transform parent, Sprite sp, Color col)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image im = go.AddComponent<Image>();
        im.sprite = sp;
        im.color = col;
        im.raycastTarget = false;
        if (sp != null) im.preserveAspect = true;
        return (RectTransform)go.transform;
    }

    static Transform FindInScene(Scene s, string name)
    {
        if (!s.IsValid()) return null;
        foreach (GameObject root in s.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            Transform t = FindChildDeep(root.transform, name);
            if (t != null) return t;
        }
        return null;
    }

    static Transform FindChildDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.name == name) return c;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform t = FindChildDeep(parent.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    // ==================== 📈 easing ====================
    static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
    static float BackOut(float t, float s) { t = Mathf.Clamp01(t) - 1f; return t * t * ((s + 1f) * t + s) + 1f; }
    static float ElasticOut(float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0f || t >= 1f) return t;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
    }

    // ==================== 🖼️ รูปเล็กๆ ที่สร้างเอง (ไม่ต้องมีไฟล์) ====================
    static Sprite MakeSprite(int n, Func<float, float, float> alpha)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static float SoftDot(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        return Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
    }

    static float Star(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        float rays = Mathf.Exp(-Mathf.Abs(u) * 9f) + Mathf.Exp(-Mathf.Abs(v) * 9f);
        float core = Mathf.Exp(-d * d * 18f);
        return (rays * 0.8f + core) * Mathf.Clamp01(1f - d);
    }

    // =====================================================================================
    //  🔊 เสียง (สังเคราะห์เอง ใช้ระดับเสียง SFX เดียวกับหน้าตั้งค่า PlayerPrefs "SfxVolume")
    // =====================================================================================
    void Play(AudioClip clip, float vol = 1f, float pitch = 1f)
    {
        if (clip == null || sfx == null) return;
        float master = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (master <= 0.001f) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, vol * master);
    }

    void BuildSounds()
    {
        // กริ๊ง~ ไล่โน้ตขึ้น (โหลดเสร็จ)
        clipSparkle = Synth("sparkle", 1.1f, t =>
            Bell(t, 1047f, 0f, 6f) + Bell(t, 1319f, 0.06f, 6f) + Bell(t, 1568f, 0.12f, 6f)
            + Bell(t, 2093f, 0.18f, 5f) * 1.1f + Bell(t, 3136f, 0.26f, 7f) * 0.5f + Bell(t, 4186f, 0.32f, 9f) * 0.3f);

        // วูบ (โลโก้พุ่ง): noise ผ่าน low-pass ที่ค่อยๆ เปิด
        {
            float dur = 0.7f;
            int n = Mathf.CeilToInt(SR * dur);
            float[] d = new float[n];
            System.Random rnd = new System.Random(7);
            float lp = 0f, lp2 = 0f, peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, k = t / dur;
                float cutoff = Mathf.Lerp(0.01f, 0.25f, k * k);
                float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (nz - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f))), 1.5f);   // Max กันค่าติดลบ -> Pow ได้ NaN -> ลำโพงแตก
                d[i] = lp2 * env;
                peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            }
            clipWhoosh = ToClip("whoosh", d, peak);
        }

        // ป๊อป + ติ๊ง (โลโก้ลงที่)
        clipLand = Synth("land", 0.6f, t =>
            Bloop(t, 380f, 820f, 0.09f) * 1.2f + Bell(t, 1568f, 0.04f, 8f) * 0.6f + Bell(t, 2349f, 0.09f, 8f) * 0.45f);
    }

    static float Bloop(float t, float f0, float f1, float dur)
    {
        if (t < 0 || t > dur * 1.6f) return 0f;
        float f = Mathf.Lerp(f0, f1, Mathf.Clamp01(t / dur));
        return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 30f) * Mathf.Min(1f, t * 400f);
    }

    static float Bell(float t, float f, float start, float decay)
    {
        t -= start;
        if (t < 0) return 0f;
        float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 300f);
        return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t) + 0.12f * Mathf.Sin(2 * Mathf.PI * f * 3.02f * t)) * env * 0.5f;
    }

    static AudioClip Synth(string name, float dur, Func<float, float> f)
    {
        int n = Mathf.CeilToInt(SR * dur);
        float[] d = new float[n];
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            d[i] = f(i / (float)SR);
            if (float.IsNaN(d[i])) d[i] = 0f;
            peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        }
        return ToClip(name, d, peak);
    }

    static AudioClip ToClip(string name, float[] d, float peak)
    {
        int n = d.Length;
        float norm = 0.8f / peak;
        int fade = Mathf.Min(n, 400);
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(d[i]) || float.IsInfinity(d[i])) d[i] = 0f;   // กันเสียงพังทั้งระบบ
            d[i] *= norm;
            if (i > n - fade) d[i] *= (n - i) / (float)fade;   // กันเสียงคลิกตอนจบ
        }
        AudioClip c = AudioClip.Create("loadfx_" + name, n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }
}
