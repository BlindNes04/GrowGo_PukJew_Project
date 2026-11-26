// =====================================================================================
//  GrowGoWeather.cs  🌧️🌫️⚡ เอฟเฟกต์ภาพสภาพอากาศ (สำหรับ Open House)
// -------------------------------------------------------------------------------------
//  ใช้ใน MainForest และ FriendVisit (สวนเพื่อน) อ่านอากาศจาก WeatherManager.Instance.currentWeather
//    - หมอก (WTH_MIST)  : รูปหมอกชุดเดียวกับ Mist Island ลอยไปมาช้าๆ + จาง-ทึบเป็นจังหวะ + ฉากขาวขึ้นนิดๆ
//    - ฝน   (WTH_RAIN)  : เม็ดฝนตกเฉียงๆ + กระเซ็นเล็กๆ + ฉากมืดลงนิดๆ
//    - พายุ  (WTH_STORM) : ฝนหนักกว่า เฉียงกว่า + ฉากมืดกว่า + ฟ้าแลบ/ฟ้าผ่า (เสียงตรงกับแสงผ่าน GrowGoAmbience)
//    - ปกติ/แดดจัด       : ไม่มีเอฟเฟกต์เพิ่ม
//  สวนเพื่อน: เปลี่ยนรูปพื้นหลังตามอากาศด้วย (ใช้รูปชุดเดียวกับที่ WeatherManager ใช้ในสวนเรา)
//  ต้นไม้ในรูปพื้นหลังไหวตามลม (ปกติเบาๆ, หมอกเบามาก, ฝนแรงขึ้น, พายุแรงสุด) ด้วย shader GrowGoBGWave
//  BeachIsland: น้ำทะเลในรูปพื้นหลังเป็นคลื่น + ประกายแดด (shader ตัวเดียวกัน)
//  หมอก: ชั้นหลัง (ใต้ต้นไม้) ยิ่งสูงยิ่งจาง + ชั้นหน้า (ทับเท้าพืช/บัดดี้บางๆ) กดทะลุได้
//
//  วางเอฟเฟกต์เป็น "ลูก" ของรูปพื้นหลัง -> อยู่เหนือพื้นหลัง แต่อยู่ใต้ต้นไม้/ปุ่ม/ป๊อปอัปทั้งหมด
//  ไม่ต้องลากใส่ฉาก ไม่ต้องผูก Inspector ไม่แก้ WeatherManager / FriendVisitController
//  ปิดเอฟเฟกต์นี้: ลบไฟล์นี้ทิ้ง (และ GrowGoAmbience จะใช้เสียงป่าปกติเสมอ ถ้าลบคู่กันต้องลบทั้ง 2 ไฟล์)
// =====================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GrowGoWeather : MonoBehaviour
{
    public static GrowGoWeather I;
    public static bool Enabled = true;

    // ==================== ⚙️ ค่าที่ปรับได้ ====================
    public static float RainPerSecond = 55f, StormPerSecond = 150f;     // จำนวนเม็ดฝนต่อวินาที
    public static Vector2 LightningEvery = new Vector2(9f, 22f);        // ฟ้าแลบทุกๆ กี่วินาที (สุ่มในช่วงนี้)
    public static float StrikeChance = 0.35f;                            // โอกาสเป็นฟ้าผ่าเห็นสายฟ้า (ที่เหลือ = ฟ้าแลบไกลๆ)
    public static float BeachLapDistance = 0.022f;                       // ระยะที่น้ำซัดขึ้นฝั่ง (สัดส่วนของรูป)

    // ==================== 🌦️ อ่านอากาศตอนนี้ (GrowGoAmbience ใช้ด้วย) ====================
    public static string CurrentWeatherId()
    {
        try
        {
            if (WeatherManager.Instance != null && WeatherManager.Instance.currentWeather != null)
                return WeatherManager.Instance.currentWeather.weather_id ?? "";
        }
        catch { }
        return "";
    }

    // ฝน/พายุ/หมอก -> ปิดละอองเกสรเรืองแสงของ GrowGoJuice (ไม่เข้ากับบรรยากาศ)
    public static bool GloomyNow()
    {
        string s = SceneManager.GetActiveScene().name;
        if (s != "MainForest" && s != "FriendVisit") return false;
        string w = CurrentWeatherId();
        return w == "WTH_RAIN" || w == "WTH_STORM" || w == "WTH_MIST";
    }

    // ==================== 🚀 สร้างตัวเองตอนเกมเริ่ม ====================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (I != null) return;
        GameObject go = new GameObject("[GrowGoWeather]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<GrowGoWeather>();
    }

    // ==================== 🧱 ของที่สร้าง ====================
    private Sprite sprStreak, sprSplash, sprWhite, sprGlow;
    private Sprite[] fogSprites = new Sprite[0];

    private Image bg;                    // รูปพื้นหลังของฉากนี้
    private RectTransform layer;         // ชั้นเอฟเฟกต์ (ลูกของพื้นหลัง)
    private Image tint, skyFlash;        // ย้อมสีฉาก / ฟ้าสว่างตอนฟ้าแลบ
    private Canvas topCanvas; private Image screenFlash;   // แฟลชจางๆ ทั้งจอตอนฟ้าผ่า (เหนือ UI)

    private class Drop { public RectTransform rt; public Image img; public bool alive; public Vector2 pos, vel; public float dieY, len; }
    private class Splash { public RectTransform rt; public Image img; public bool alive; public float t, life, w; }
    private class Fog { public RectTransform rt; public Image img; public Vector2 basePos; public float amp, speed, off, aMin, aMax; }
    private readonly List<Drop> drops = new List<Drop>();
    private readonly List<Splash> splashes = new List<Splash>();
    private readonly List<Fog> fogs = new List<Fog>();
    private const int MaxDrops = 260, MaxSplashes = 70;

    private string sceneName = "";
    private string weather = "";
    private float rainAmt, mistAmt, stormAmt;      // 0..1 ค่อยๆ เปลี่ยน (เฟด) ตอนอากาศเปลี่ยน
    private float spawnAcc;
    private float nextLightning;
    private float findTimer;

    // สวนเพื่อน: รูปพื้นหลังเดิม (ไว้คืนตอนอากาศปกติ)
    private Sprite friendOriginalBG;

    // หมอกชั้นหลัง: อยู่ในกล่อง RectMask2D ที่ขอบบนจางนุ่ม -> ยิ่งสูงยิ่งจาง ไม่ขึ้นไปเต็มฟ้า
    private RectTransform fogBack;
    private RectMask2D fogBackMask;
    // หมอกชั้นหน้า: วางทับพืช/บัดดี้ (แต่ใต้ปุ่ม/ป๊อปอัป) raycastTarget = false -> กดทะลุโดนพืช/บัดดี้ได้
    private class FrontFog { public RectTransform rt; public Image img; public Transform target; public float wMul, hMul, yMul, aMax, speed, off, amp; }
    private readonly List<FrontFog> frontFogs = new List<FrontFog>();
    private RectTransform fogFront;
    private float frontFindTimer;

    // ต้นไม้ไหว / คลื่นทะเล (shader GrowGoBGWave)
    private Shader waveShader;
    private Material swayMat, beachMat;
    private Image beachBg;
    private Sprite beachSprite;
    private bool beachReady;
    private float swayPhase, swayAmp, wavePhase;
    private Sprite swaySprite;   // รูปพื้นหลังที่โหลด mask ไว้แล้ว (พื้นหลังเปลี่ยนตามอากาศ -> โหลดใหม่)

    // =====================================================================================
    private void Awake()
    {
        try { BuildSprites(); BuildTopCanvas(); }
        catch (Exception e) { Debug.LogWarning("[GrowGoWeather] init: " + e.Message); Enabled = false; }
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        sceneName = SceneManager.GetActiveScene().name;
        bg = null; layer = null; tint = null; skyFlash = null; friendOriginalBG = null;
        drops.Clear(); splashes.Clear(); fogs.Clear();
        weather = ""; rainAmt = mistAmt = stormAmt = 0f;
        findTimer = 0f;
        fogBack = null; fogBackMask = null; fogFront = null; frontFogs.Clear(); frontFindTimer = 0f;
        beachBg = null; beachSprite = null; beachReady = false; swayAmp = 0f; swaySprite = null;
        nextLightning = Time.unscaledTime + UnityEngine.Random.Range(4f, 8f);
        if (screenFlash != null) { screenFlash.enabled = false; }
    }

    // ==================== 🖼️ รูปที่วาดด้วยโค้ด + รูปหมอกจาก Resources ====================
    private void BuildSprites()
    {
        // เม็ดฝน: เส้นแนวตั้ง หัวจาง ท้ายเข้ม ขอบนุ่ม
        sprStreak = MakeSprite(8, 64, (x, y) => Mathf.Clamp01(1f - Mathf.Abs(x) * 1.6f) * Mathf.Pow(Mathf.Clamp01((y + 1f) * 0.5f), 0.7f));
        // กระเซ็น: วงรีกลวงบางๆ
        sprSplash = MakeSprite(32, 32, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) * 7f); });
        sprWhite = MakeSprite(4, 4, (x, y) => 1f);
        // แสงเรืองรอบสายฟ้า: เข้มตรงกลาง จางไปด้านข้าง
        sprGlow = MakeSprite(32, 4, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x)), 2f));

        // รูปหมอก: ก๊อปจาก MistMap/mist_material/fog แล้วย่อเหลือกว้าง 1024 ใส่ใน Resources/GrowGoAmbience/fog/
        List<Sprite> list = new List<Sprite>();
        foreach (Texture2D t in Resources.LoadAll<Texture2D>("GrowGoAmbience/fog"))
            if (t != null) list.Add(Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f));
        fogSprites = list.ToArray();

        waveShader = Resources.Load<Shader>("GrowGoBGWave");
        if (waveShader != null && waveShader.isSupported)
        {
            swayMat = new Material(waveShader) { name = "GrowGoTreeSway" };
            swayMat.SetFloat("_Mode", 0f);
            beachMat = new Material(waveShader) { name = "GrowGoBeachWave" };
            beachMat.SetFloat("_Mode", 2f);
        }
        else Debug.LogWarning("[GrowGoWeather] ไม่เจอ shader GrowGoBGWave -> ต้นไม้/น้ำทะเลไม่ขยับ (เอฟเฟกต์อื่นยังทำงาน)");
    }

    private Sprite MakeSprite(int w, int h, Func<float, float, float> alpha)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float x = (i + 0.5f) / w * 2f - 1f, y = (j + 0.5f) / h * 2f - 1f;
                px[j * w + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void BuildTopCanvas()
    {
        GameObject cgo = new GameObject("WeatherTopCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        topCanvas = cgo.AddComponent<Canvas>();
        topCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        topCanvas.sortingOrder = 29990;      // ใต้ GrowGoJuice (30000) นิดนึง ไม่มี GraphicRaycaster = แตะทะลุ
        GameObject f = new GameObject("ScreenFlash", typeof(RectTransform));
        f.transform.SetParent(cgo.transform, false);
        screenFlash = f.AddComponent<Image>();
        screenFlash.sprite = sprWhite; screenFlash.raycastTarget = false; screenFlash.enabled = false;
        Stretch(screenFlash.rectTransform);
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    // =====================================================================================
    //  🔎 หารูปพื้นหลัง แล้วสร้างชั้นเอฟเฟกต์เป็นลูกของมัน
    // =====================================================================================
    private void FindBackground()
    {
        WeatherManager wm = WeatherManager.Instance;
        if (wm == null) return;
        if (sceneName == "MainForest")
        {
            if (wm.backgroundDisplayImage != null && wm.backgroundDisplayImage.gameObject.scene == SceneManager.GetActiveScene())
                bg = wm.backgroundDisplayImage;
        }
        else if (sceneName == "FriendVisit")
        {
            // ฉากสวนเพื่อนใช้รูปพื้นหลังเดียวกับสวนเรา (main forest normal) -> หา Image ที่ใช้รูปนั้น
            if (wm.clearBGSprite == null) return;
            Image[] all = Resources.FindObjectsOfTypeAll<Image>();
            for (int i = 0; i < all.Length; i++)
            {
                Image im = all[i];
                if (im == null || !im.gameObject.scene.IsValid() || im.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (im.sprite == wm.clearBGSprite) { bg = im; friendOriginalBG = im.sprite; break; }
            }
        }
        if (bg != null) BuildLayer();
    }

    private void BuildLayer()
    {
        GameObject go = new GameObject("GrowGoWeatherFX", typeof(RectTransform));
        go.transform.SetParent(bg.transform, false);
        layer = go.GetComponent<RectTransform>();
        Stretch(layer);
        layer.SetAsLastSibling();   // ถ้าพื้นหลังมีลูกอยู่แล้ว ให้เอฟเฟกต์อยู่บนสุดของพื้นหลัง

        tint = NewImage("Tint", sprWhite); Stretch(tint.rectTransform); tint.color = new Color(0, 0, 0, 0);
        // หมอก (ชั้นหลัง) อยู่ในกล่องที่เริ่มต่ำกว่าขอบล่างจอมากๆ (ขอบล่างที่จางอยู่นอกจอ) ถึง 75% ของความสูง ขอบบนจางนุ่ม
        GameObject fb = new GameObject("FogBack", typeof(RectTransform));
        fb.transform.SetParent(layer, false);
        fogBack = fb.GetComponent<RectTransform>();
        fogBack.anchorMin = new Vector2(-0.1f, -0.6f); fogBack.anchorMax = new Vector2(1.1f, 0.75f);
        fogBack.offsetMin = fogBack.offsetMax = Vector2.zero;
        fogBackMask = fb.AddComponent<RectMask2D>();
        for (int i = 0; i < (fogSprites.Length == 0 ? 0 : 4); i++)
        {
            Sprite s = fogSprites[i % fogSprites.Length];
            Image im = NewImage("Fog" + i, s);
            im.transform.SetParent(fogBack, false);
            im.preserveAspect = false;
            fogs.Add(new Fog
            {
                rt = im.rectTransform, img = im,
                basePos = new Vector2(0f, Mathf.Lerp(-0.42f, -0.1f, i / 3f)),   // สัดส่วนของความสูงฉาก (ล่าง -> บน) ต่ำกว่าเดิม
                amp = UnityEngine.Random.Range(0.06f, 0.12f), speed = UnityEngine.Random.Range(0.12f, 0.25f) * (i % 2 == 0 ? 1f : -1f),
                off = UnityEngine.Random.Range(0f, 10f), aMin = 0.25f, aMax = i == 0 ? 0.85f : 0.65f
            });
            im.color = new Color(1, 1, 1, 0);
        }
        skyFlash = NewImage("SkyFlash", sprWhite); Stretch(skyFlash.rectTransform); skyFlash.color = new Color(1, 1, 1, 0);
    }

    private Image NewImage(string name, Sprite s)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(layer, false);
        Image im = go.AddComponent<Image>();
        im.sprite = s; im.raycastTarget = false;
        return im;
    }

    // =====================================================================================
    //  🔁 ทุกเฟรม
    // =====================================================================================
    private void Update()
    {
        if (!Enabled) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (sceneName == "BeachIsland") { try { UpdateBeach(dt); } catch (Exception e) { Debug.LogWarning("[GrowGoWeather] beach: " + e.Message); } return; }
        if (sceneName != "MainForest" && sceneName != "FriendVisit") return;

        try
        {
            if (layer == null)
            {
                findTimer -= dt;
                if (findTimer <= 0f) { findTimer = 0.5f; bg = null; FindBackground(); }
                if (layer == null) return;
            }

            // ---- อากาศเปลี่ยน ----
            string w = CurrentWeatherId();
            if (w != weather)
            {
                weather = w;
                if (sceneName == "FriendVisit") ApplyFriendBackground();
                if (w == "WTH_STORM") nextLightning = Time.unscaledTime + UnityEngine.Random.Range(3f, 6f);
            }

            // ---- เฟดความแรงของแต่ละเอฟเฟกต์ ----
            float rainT = (w == "WTH_RAIN" || w == "WTH_STORM") ? 1f : 0f;
            float stormT = w == "WTH_STORM" ? 1f : 0f;
            float mistT = w == "WTH_MIST" ? 1f : 0f;
            rainAmt = Mathf.MoveTowards(rainAmt, rainT, dt / 1.5f);
            stormAmt = Mathf.MoveTowards(stormAmt, stormT, dt / 1.5f);
            mistAmt = Mathf.MoveTowards(mistAmt, mistT, dt / 2f);

            // ---- ย้อมสีฉาก: ฝนมืดลงอมฟ้า, พายุมืดกว่า, หมอกขาวขึ้น ----
            Color c = new Color(0.08f, 0.12f, 0.22f, 0.16f * rainAmt + 0.2f * stormAmt);
            if (mistAmt > 0f) c = Color.Lerp(c, new Color(0.92f, 0.95f, 0.97f, 0.16f), mistAmt);
            tint.color = c;

            UpdateRain(dt);
            UpdateFog();
            UpdateFrontFog(dt);
            UpdateSway(dt, w);
            if (stormAmt > 0.5f && Time.unscaledTime >= nextLightning)
            {
                nextLightning = Time.unscaledTime + UnityEngine.Random.Range(LightningEvery.x, LightningEvery.y);
                StartCoroutine(UnityEngine.Random.value < StrikeChance ? StrikeRoutine() : FarFlashRoutine());
            }
        }
        catch (Exception e) { Debug.LogWarning("[GrowGoWeather] " + e.Message); }
    }

    // =====================================================================================
    //  🌳 ต้นไม้ในรูปพื้นหลังไหวตามลม (แรงตามสภาพอากาศ ค่อยๆ เปลี่ยน)
    // =====================================================================================
    private void UpdateSway(float dt, string w)
    {
        if (swayMat == null || bg == null) return;
        if (bg.material != swayMat) bg.material = swayMat;
        if (bg.sprite != swaySprite) LoadTreeMask(bg.sprite);
        float target, speed;
        switch (w)
        {
            case "WTH_MIST": target = 0.001f; speed = 0.7f; break;     // หมอก: แทบไม่มีลม
            case "WTH_RAIN": target = 0.006f; speed = 1.8f; break;     // ฝน: ลมแรงขึ้น
            case "WTH_STORM": target = 0.012f; speed = 2.8f; break;    // พายุ: ไหวแรง
            case "WTH_HOT": target = 0.002f; speed = 1.0f; break;      // แดดจัด: ลมอ่อน
            default: target = 0.003f; speed = 1.2f; break;             // ปกติ: ลมพัดเบาๆ
        }
        swayAmp = Mathf.MoveTowards(swayAmp, target, dt * 0.004f);
        swayPhase += dt * speed;
        float t = Time.unscaledTime;
        // ลมกระโชกเป็นระลอก (พายุกระโชกแรงกว่า)
        float gust = 1f + (w == "WTH_STORM" ? 0.45f : 0.2f) * Mathf.Sin(t * 0.35f) * Mathf.Sin(t * 0.13f + 1f);
        swayMat.SetFloat("_Amp", swayAmp * gust);
        swayMat.SetFloat("_Phase", swayPhase);
    }

    // โหลด mask ต้นไม้ + รูปฟ้าที่ลบต้นไม้แล้ว ของรูปพื้นหลังนี้ (Resources/GrowGoSway/<ชื่อรูป>_mask, _sky)
    // ไม่มีไฟล์ = ไหวทั้งส่วนบนแบบเดิม
    private void LoadTreeMask(Sprite sp)
    {
        swaySprite = sp;
        Texture2D mask = null, sky = null;
        if (sp != null && sp.texture != null)
        {
            mask = Resources.Load<Texture2D>("GrowGoSway/" + sp.texture.name + "_mask");
            sky = Resources.Load<Texture2D>("GrowGoSway/" + sp.texture.name + "_sky");
        }
        bool ok = mask != null && sky != null;
        if (ok) { mask.wrapMode = TextureWrapMode.Clamp; sky.wrapMode = TextureWrapMode.Clamp; }
        swayMat.SetTexture("_MaskTex", ok ? mask : null);
        swayMat.SetTexture("_SkyTex", ok ? sky : null);
        swayMat.SetFloat("_HasMask", ok ? 1f : 0f);
    }

    // =====================================================================================
    //  🌊 BeachIsland: น้ำทะเลเป็นคลื่น + ประกายแดด
    // =====================================================================================
    private void UpdateBeach(float dt)
    {
        if (beachMat == null) return;
        if (beachBg == null)
        {
            findTimer -= dt;
            if (findTimer > 0f) return;
            findTimer = 0.5f;
            // หา Image พื้นหลังชายหาด: รูปชื่อมีคำว่า beach และใหญ่ที่สุดในฉาก
            float best = 0f;
            foreach (Image im in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (im == null || im.sprite == null || !im.gameObject.scene.IsValid() || im.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (!im.isActiveAndEnabled) continue;   // ในฉากมี BG (1) ที่ปิดอยู่ (รูป beach island) ต้องข้าม
                if (im.sprite.name.ToLowerInvariant().IndexOf("beach", StringComparison.Ordinal) < 0) continue;
                float area = Mathf.Abs(im.rectTransform.rect.width * im.rectTransform.rect.height);
                if (area > best) { best = area; beachBg = im; }
            }
            if (beachBg == null) return;
        }
        // ต้องมี mask น้ำ + รูปทรายที่ลบน้ำแล้ว (Resources/GrowGoSway/<ชื่อรูป>_mask, _sky) ไม่มี = ไม่ขยับ
        if (beachBg.sprite != beachSprite)
        {
            beachSprite = beachBg.sprite;
            Texture2D mask = Resources.Load<Texture2D>("GrowGoSway/" + beachSprite.texture.name + "_mask");
            Texture2D sand = Resources.Load<Texture2D>("GrowGoSway/" + beachSprite.texture.name + "_sky");
            beachReady = mask != null && sand != null;
            if (beachReady)
            {
                mask.wrapMode = TextureWrapMode.Clamp; sand.wrapMode = TextureWrapMode.Clamp;
                beachMat.SetTexture("_MaskTex", mask); beachMat.SetTexture("_SkyTex", sand);
            }
        }
        if (!beachReady) { if (beachBg.material == beachMat) beachBg.material = null; return; }
        if (beachBg.material != beachMat) beachBg.material = beachMat;
        // คลื่นซัด: ค่อยๆ ขึ้นฝั่ง แล้วถอยกลับ รอบละ ~6 วิ (ระลอกเล็กสลับใหญ่ ไม่ซ้ำจังหวะเดิม)
        wavePhase += dt;
        float lap = 0.5f - 0.5f * Mathf.Cos(wavePhase * 1.05f);              // 0..1
        float big = 0.75f + 0.25f * Mathf.Sin(wavePhase * 0.31f);             // ระลอกใหญ่/เล็ก
        beachMat.SetFloat("_Amp", BeachLapDistance * (lap * big - 0.15f));    // ติดลบนิดๆ = ถอยลงทะเลเลยจุดเดิมหน่อย
        beachMat.SetFloat("_Phase", wavePhase);
    }

    // =====================================================================================
    //  🌫️ หมอกชั้นหน้า: ก้อนบางๆ ทับเท้าพืชกับบัดดี้ ให้ดูเหมือนยืนอยู่ในหมอก (ไม่ลอย)
    //  วางไว้ "ถัดจาก" พืช/บัดดี้ใน Hierarchy -> ทับพืช/บัดดี้ แต่อยู่ใต้ปุ่มและป๊อปอัปที่อยู่ทีหลัง
    //  raycastTarget = false ทุกชิ้น -> แตะทะลุไปโดนพืช/บัดดี้ได้ปกติ
    // =====================================================================================
    private void UpdateFrontFog(float dt)
    {
        if (fogSprites.Length == 0) return;
        if (fogFront == null)
        {
            if (mistAmt <= 0f) return;            // สร้างตอนต้องใช้ครั้งแรก
            frontFindTimer -= dt;
            if (frontFindTimer > 0f) return;
            frontFindTimer = 1f;
            BuildFrontFog();
            if (fogFront == null) return;
        }
        float t = Time.unscaledTime;
        Vector3[] c = new Vector3[4];
        for (int i = 0; i < frontFogs.Count; i++)
        {
            FrontFog f = frontFogs[i];
            if (mistAmt <= 0f || f.target == null || !f.target.gameObject.activeInHierarchy) { if (f.img.enabled) f.img.enabled = false; continue; }
            RectTransform tr = f.target as RectTransform;
            if (tr == null) continue;
            // ขอบล่าง-กลาง และความกว้างของพืช/บัดดี้ ในพิกัดของชั้นหมอกหน้า
            tr.GetWorldCorners(c);
            Vector2 bl = fogFront.InverseTransformPoint(c[0]), br = fogFront.InverseTransformPoint(c[3]), tl = fogFront.InverseTransformPoint(c[1]);
            float w = Mathf.Abs(br.x - bl.x), h = Mathf.Abs(tl.y - bl.y);
            Vector2 bottom = (bl + br) * 0.5f;
            f.img.enabled = true;
            f.rt.sizeDelta = new Vector2(w * f.wMul, h * f.hMul);
            float x = Mathf.Sin((t + f.off) * f.speed) * w * f.amp;
            f.rt.anchoredPosition = new Vector2(bottom.x + x, bottom.y + h * f.yMul) - (Vector2)fogFront.rect.center;
            float wave = (Mathf.Sin((t + f.off) * 0.5f) + 1f) * 0.5f;
            f.img.color = new Color(1f, 1f, 1f, Mathf.Lerp(f.aMax * 0.6f, f.aMax, wave) * mistAmt);
        }
    }

    private void BuildFrontFog()
    {
        Transform plant = null, buddy = null;
        foreach (MCController mc in FindObjectsByType<MCController>())
            if (mc != null && mc.isActiveAndEnabled && mc.transform is RectTransform) { plant = mc.transform; break; }
        BuddyDisplayManager bdm = FindAnyObjectByType<BuddyDisplayManager>();
        if (bdm != null)
        {
            FieldInfo fi = typeof(BuddyDisplayManager).GetField("buddyUIImage", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Image bi = fi != null ? fi.GetValue(bdm) as Image : null;
            if (bi != null && bi.gameObject.activeInHierarchy) buddy = bi.transform;
        }
        if (buddy == null) { BuddyDisplay bd = FindAnyObjectByType<BuddyDisplay>(); if (bd != null && bd.transform is RectTransform) buddy = bd.transform; }
        if (plant == null) return;

        // หา "พ่อร่วม" ของพืชกับบัดดี้ (ต้องอยู่ Canvas เดียวกัน) แล้ววางชั้นหมอกหน้าต่อจากตัวที่อยู่ล่างสุดใน Hierarchy
        Transform lca = plant.parent;
        if (buddy != null && buddy.GetComponentInParent<Canvas>() != null && plant.GetComponentInParent<Canvas>() != null
            && buddy.GetComponentInParent<Canvas>().rootCanvas == plant.GetComponentInParent<Canvas>().rootCanvas)
        {
            HashSet<Transform> up = new HashSet<Transform>();
            for (Transform a = plant.parent; a != null; a = a.parent) up.Add(a);
            for (Transform b = buddy.parent; b != null; b = b.parent) if (up.Contains(b)) { lca = b; break; }
        }
        else buddy = null;
        if (lca == null) return;
        int idx = BranchIndex(plant, lca);
        if (buddy != null) idx = Mathf.Max(idx, BranchIndex(buddy, lca));

        GameObject go = new GameObject("GrowGoFogFront", typeof(RectTransform));
        go.transform.SetParent(lca, false);
        fogFront = go.GetComponent<RectTransform>();
        Stretch(fogFront);
        fogFront.SetSiblingIndex(idx + 1);

        AddFrontFog(plant, 1.7f, 0.32f, 0.08f, 0.6f, 0);    // คลุมโคนพืช
        AddFrontFog(plant, 2.6f, 0.22f, -0.04f, 0.4f, 1);   // แผ่นกว้างต่ำๆ ใต้พืช
        if (buddy != null) AddFrontFog(buddy, 1.8f, 0.35f, 0.06f, 0.5f, 2);   // คลุมเท้าบัดดี้
    }

    private static int BranchIndex(Transform t, Transform root)
    {
        while (t != null && t.parent != root) t = t.parent;
        return t != null ? t.GetSiblingIndex() : 0;
    }

    private void AddFrontFog(Transform target, float wMul, float hMul, float yMul, float aMax, int i)
    {
        GameObject go = new GameObject("FrontFog" + i, typeof(RectTransform));
        go.transform.SetParent(fogFront, false);
        Image im = go.AddComponent<Image>();
        im.sprite = fogSprites[(i + 1) % fogSprites.Length];
        im.raycastTarget = false;            // สำคัญ: กดทะลุหมอกได้
        im.preserveAspect = false;
        im.color = new Color(1, 1, 1, 0);
        im.enabled = false;
        frontFogs.Add(new FrontFog
        {
            rt = im.rectTransform, img = im, target = target, wMul = wMul, hMul = hMul, yMul = yMul, aMax = aMax,
            speed = UnityEngine.Random.Range(0.15f, 0.3f) * (i % 2 == 0 ? 1f : -1f), off = UnityEngine.Random.Range(0f, 10f), amp = 0.12f
        });
    }

    // ==================== 🖼️ สวนเพื่อน: เปลี่ยนรูปพื้นหลังตามอากาศ (map เดียวกับ WeatherManager) ====================
    private void ApplyFriendBackground()
    {
        WeatherManager wm = WeatherManager.Instance;
        if (bg == null || wm == null) return;
        Sprite s = friendOriginalBG;
        switch (weather)
        {
            case "WTH_HOT": if (wm.hotBGSprite != null) s = wm.hotBGSprite; break;
            case "WTH_MIST": s = wm.mistBGSprite != null ? wm.mistBGSprite : friendOriginalBG; break;
            case "WTH_RAIN": if (wm.rainBGSprite != null) s = wm.rainBGSprite; break;
            case "WTH_STORM": s = wm.stormBGSprite != null ? wm.stormBGSprite : (wm.rainBGSprite != null ? wm.rainBGSprite : friendOriginalBG); break;
        }
        if (s != null) bg.sprite = s;
    }

    // =====================================================================================
    //  🌧️ ฝน
    // =====================================================================================
    private void UpdateRain(float dt)
    {
        Rect r = layer.rect;
        float k = r.height / 1920f;     // หน่วยขนาด (ออกแบบที่ความสูง 1920)

        float rate = Mathf.Lerp(RainPerSecond, StormPerSecond, stormAmt) * rainAmt;
        spawnAcc += rate * dt;
        while (spawnAcc >= 1f) { spawnAcc -= 1f; SpawnDrop(r, k); }

        for (int i = 0; i < drops.Count; i++)
        {
            Drop d = drops[i];
            if (!d.alive) continue;
            d.pos += d.vel * dt;
            if (d.pos.y <= d.dieY)
            {
                d.alive = false; d.img.enabled = false;
                if (UnityEngine.Random.value < 0.45f) SpawnSplash(new Vector2(d.pos.x, d.dieY), k);
                continue;
            }
            d.rt.anchoredPosition = d.pos;
        }
        for (int i = 0; i < splashes.Count; i++)
        {
            Splash s = splashes[i];
            if (!s.alive) continue;
            s.t += dt;
            float x = s.t / s.life;
            if (x >= 1f) { s.alive = false; s.img.enabled = false; continue; }
            s.rt.sizeDelta = new Vector2(s.w * (0.3f + x), s.w * 0.35f * (0.3f + x));
            s.img.color = new Color(0.85f, 0.92f, 1f, 0.55f * (1f - x));
        }
    }

    private void SpawnDrop(Rect r, float k)
    {
        Drop d = null;
        for (int i = 0; i < drops.Count; i++) if (!drops[i].alive) { d = drops[i]; break; }
        if (d == null)
        {
            if (drops.Count >= MaxDrops) return;
            Image im = NewImage("drop", sprStreak);
            d = new Drop { rt = im.rectTransform, img = im };
            drops.Add(d);
            d.rt.SetSiblingIndex(skyFlash.rectTransform.GetSiblingIndex());   // ฝนอยู่ใต้แสงฟ้าแลบ
        }
        bool storm = UnityEngine.Random.value < stormAmt;
        float speed = (storm ? UnityEngine.Random.Range(2100f, 2600f) : UnityEngine.Random.Range(1300f, 1700f)) * k;
        float slant = storm ? UnityEngine.Random.Range(0.22f, 0.32f) : UnityEngine.Random.Range(0.06f, 0.12f);   // ความเอียง (ลมพัด)
        d.vel = new Vector2(-slant, -1f).normalized * speed;
        d.len = (storm ? UnityEngine.Random.Range(90f, 150f) : UnityEngine.Random.Range(55f, 95f)) * k;
        // เกิดเหนือขอบบน (เผื่อด้านขวาเพราะฝนเฉียงไปทางซ้าย)
        d.pos = new Vector2(UnityEngine.Random.Range(r.xMin, r.xMax + r.height * slant), r.yMax + d.len);
        d.dieY = Mathf.Lerp(r.yMin, r.yMin + r.height * 0.75f, Mathf.Pow(UnityEngine.Random.value, 1.6f));   // ส่วนมากตกถึงด้านล่าง
        d.rt.sizeDelta = new Vector2((storm ? 4f : 3f) * Mathf.Max(0.6f, k), d.len);
        d.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.vel.y, d.vel.x) * Mathf.Rad2Deg + 90f);
        d.img.color = new Color(0.8f, 0.88f, 1f, storm ? UnityEngine.Random.Range(0.4f, 0.65f) : UnityEngine.Random.Range(0.28f, 0.5f));
        d.img.enabled = true; d.alive = true;
        d.rt.anchoredPosition = d.pos;
    }

    private void SpawnSplash(Vector2 p, float k)
    {
        Splash s = null;
        for (int i = 0; i < splashes.Count; i++) if (!splashes[i].alive) { s = splashes[i]; break; }
        if (s == null)
        {
            if (splashes.Count >= MaxSplashes) return;
            Image im = NewImage("splash", sprSplash);
            s = new Splash { rt = im.rectTransform, img = im };
            splashes.Add(s);
            s.rt.SetSiblingIndex(skyFlash.rectTransform.GetSiblingIndex());
        }
        s.t = 0f; s.life = UnityEngine.Random.Range(0.2f, 0.32f); s.w = UnityEngine.Random.Range(26f, 44f) * k;
        s.rt.anchoredPosition = p; s.alive = true; s.img.enabled = true;
    }

    // =====================================================================================
    //  🌫️ หมอก: ลอยซ้าย-ขวาช้าๆ + จาง-ทึบเป็นจังหวะ (แบบเดียวกับ FogController ใน Mist Island)
    // =====================================================================================
    private void UpdateFog()
    {
        if (fogs.Count == 0 || fogBack == null) return;
        Rect r = layer.rect;
        float t = Time.unscaledTime;
        // ขอบบนของกล่องหมอกจางนุ่ม 40% ของความสูงฉาก: ล่าง 35% ของจอหมอกเต็มที่ แล้วจางลงจนหายที่ 75%
        // (ขอบล่างของกล่องก็จางเหมือนกัน แต่อยู่นอกจอ เลยไม่เห็น)
        int soft = Mathf.RoundToInt(r.height * 0.4f);
        if (fogBackMask.softness.y != soft) fogBackMask.softness = new Vector2Int(0, soft);
        Vector2 boxOff = fogBack.localPosition;   // ตำแหน่งกล่องหมอก เทียบกับชั้นเอฟเฟกต์
        for (int i = 0; i < fogs.Count; i++)
        {
            Fog f = fogs[i];
            if (mistAmt <= 0f) { if (f.img.enabled) f.img.enabled = false; continue; }
            f.img.enabled = true;
            Sprite s = f.img.sprite;
            float wid = r.width * 1.7f;
            float hgt = wid * s.rect.height / s.rect.width;
            if (hgt < r.height * 0.35f) hgt = r.height * 0.35f;      // หมอกแบบยาวแบน ยืดให้หนาขึ้นหน่อย
            f.rt.sizeDelta = new Vector2(wid, hgt);
            float x = Mathf.Sin((t + f.off) * f.speed) * r.width * f.amp + Mathf.Sin((t + f.off) * f.speed * 0.37f) * r.width * 0.05f;
            float y = f.basePos.y * r.height + Mathf.Sin((t + f.off) * 0.3f) * r.height * 0.01f;
            f.rt.anchoredPosition = new Vector2(r.center.x + x, r.center.y + y) - boxOff;
            float wave = (Mathf.Sin((t + f.off + i * 1.5f) * 0.6f) + 1f) * 0.5f;
            f.img.color = new Color(1f, 1f, 1f, Mathf.Lerp(f.aMin, f.aMax, wave) * mistAmt);
        }
    }

    // =====================================================================================
    //  ⚡ ฟ้าแลบไกล: ฟ้าสว่างวาบ 2 ที แล้วเสียงฟ้าร้องตามมา 0.8-1.8 วิ (แสงเร็วกว่าเสียง)
    // =====================================================================================
    private IEnumerator FarFlashRoutine()
    {
        if (GrowGoAmbience.I != null) GrowGoAmbience.I.PlayThunder(false, UnityEngine.Random.Range(0.8f, 1.8f));
        yield return FlashSky(0.32f, 0.12f);
        yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.05f, 0.15f));
        yield return FlashSky(0.22f, 0.4f);
    }

    // =====================================================================================
    //  ⚡ ฟ้าผ่า: สายฟ้าหยักๆ จากบนฟ้า กะพริบ 2 ที + ฟ้าสว่าง + แฟลชจางๆ ทั้งจอ + เสียงเปรี้ยงแทบพร้อมกัน
    // =====================================================================================
    private IEnumerator StrikeRoutine()
    {
        if (layer == null) yield break;
        Rect r = layer.rect;
        float k = r.height / 1920f;
        List<Image> parts = BuildBolt(r, k);
        if (GrowGoAmbience.I != null) GrowGoAmbience.I.PlayThunder(true, 0.06f);

        SetBolt(parts, 1f); SkyFlashNow(0.6f); ScreenFlashNow(0.18f);
        yield return new WaitForSecondsRealtime(0.07f);
        SetBolt(parts, 0f); SkyFlashNow(0.15f); ScreenFlashNow(0f);
        yield return new WaitForSecondsRealtime(0.06f);
        SetBolt(parts, 1f); SkyFlashNow(0.5f); ScreenFlashNow(0.12f);
        // ค่อยๆ จางหาย
        float e = 0f, dur = 0.45f;
        while (e < dur)
        {
            float a = 1f - e / dur;
            SetBolt(parts, a * a);
            SkyFlashNow(0.5f * a * a);
            ScreenFlashNow(0.12f * a * a);
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        SetBolt(parts, 0f); SkyFlashNow(0f); ScreenFlashNow(0f);
        for (int i = 0; i < parts.Count; i++) if (parts[i] != null) Destroy(parts[i].gameObject);
    }

    // สร้างสายฟ้า: เส้นหยักจากบนลงล่าง + กิ่งแยก 1-2 กิ่ง (แต่ละท่อน = Image สี่เหลี่ยมหมุนตามทิศ + แสงเรือง)
    private List<Image> BuildBolt(Rect r, float k)
    {
        List<Image> parts = new List<Image>();
        Vector2 p = new Vector2(Mathf.Lerp(r.xMin, r.xMax, UnityEngine.Random.Range(0.2f, 0.8f)), r.yMax + 20f);
        float endY = r.yMax - r.height * UnityEngine.Random.Range(0.45f, 0.65f);
        int segs = UnityEngine.Random.Range(9, 14);
        float step = (p.y - endY) / segs;
        List<Vector2> pts = new List<Vector2> { p };
        for (int i = 0; i < segs; i++)
        {
            p = new Vector2(p.x + UnityEngine.Random.Range(-70f, 70f) * k, p.y - step * UnityEngine.Random.Range(0.8f, 1.2f));
            pts.Add(p);
        }
        AddPolyline(parts, pts, 7f * Mathf.Max(0.6f, k), 1f);
        int branches = UnityEngine.Random.Range(1, 3);
        for (int b = 0; b < branches; b++)
        {
            int from = UnityEngine.Random.Range(2, pts.Count - 3);
            Vector2 q = pts[from];
            float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            List<Vector2> bp = new List<Vector2> { q };
            int n = UnityEngine.Random.Range(3, 6);
            for (int i = 0; i < n; i++) { q = new Vector2(q.x + dir * UnityEngine.Random.Range(30f, 80f) * k, q.y - step * UnityEngine.Random.Range(0.5f, 0.9f)); bp.Add(q); }
            AddPolyline(parts, bp, 4f * Mathf.Max(0.6f, k), 0.7f);
        }
        return parts;
    }

    private void AddPolyline(List<Image> parts, List<Vector2> pts, float width, float strength)
    {
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1];
            Vector2 mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b) + width;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            // แสงเรือง (กว้าง จาง สีฟ้าอ่อน)
            Image glow = NewImage("boltGlow", sprGlow);
            glow.rectTransform.anchoredPosition = mid; glow.rectTransform.sizeDelta = new Vector2(width * 7f, len);
            glow.rectTransform.localRotation = Quaternion.Euler(0, 0, ang - 90f);
            glow.color = new Color(0.7f, 0.8f, 1f, 0f); parts.Add(glow);
            // แกนสายฟ้า (บาง ขาว)
            Image core = NewImage("bolt", sprWhite);
            core.rectTransform.anchoredPosition = mid; core.rectTransform.sizeDelta = new Vector2(width, len);
            core.rectTransform.localRotation = Quaternion.Euler(0, 0, ang - 90f);
            core.color = new Color(1f, 1f, 1f, 0f); parts.Add(core);
            glow.transform.SetSiblingIndex(skyFlash.transform.GetSiblingIndex());
            core.transform.SetSiblingIndex(skyFlash.transform.GetSiblingIndex());
            glow.rectTransform.localScale = core.rectTransform.localScale = new Vector3(1f, 1f, 1f);
            glow.name = core.name = strength >= 1f ? "bolt" : "branch";
        }
    }

    private void SetBolt(List<Image> parts, float a)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            Image im = parts[i];
            if (im == null) continue;
            bool branch = im.name == "branch";
            bool glow = im.sprite == sprGlow;
            Color c = im.color;
            c.a = a * (glow ? 0.45f : 1f) * (branch ? 0.7f : 1f);
            im.color = c;
        }
    }

    private IEnumerator FlashSky(float peak, float dur)
    {
        float e = 0f;
        while (e < dur)
        {
            float a = 1f - e / dur;
            SkyFlashNow(peak * a);
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        SkyFlashNow(0f);
    }

    private void SkyFlashNow(float a) { if (skyFlash != null) skyFlash.color = new Color(0.9f, 0.93f, 1f, a); }

    private void ScreenFlashNow(float a)
    {
        if (screenFlash == null) return;
        screenFlash.enabled = a > 0.001f;
        screenFlash.color = new Color(0.92f, 0.95f, 1f, a);
    }
}
