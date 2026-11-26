// =====================================================================================
//  GrowGoJuice.cs  ✨ ระบบเอฟเฟกต์ทั้งเกม (สำหรับ Open House)
// -------------------------------------------------------------------------------------
//  ไฟล์เดียวจบ ไม่ต้องลากใส่ฉาก ไม่ต้องผูก Inspector ไม่แก้สคริปต์เดิมสักบรรทัด
//  - สร้างตัวเองอัตโนมัติตอนเกมเริ่ม (RuntimeInitializeOnLoadMethod) แล้วอยู่ข้ามฉาก (DontDestroyOnLoad)
//  - วาดเอฟเฟกต์ทั้งหมดบน Canvas ของตัวเอง (Overlay ชั้นบนสุด) ไม่รับการแตะ จึงไม่บังปุ่มของเกม
//  - รูป particle และเสียง สร้างด้วยโค้ดตอนเริ่มเกม (ไม่มีไฟล์ asset เพิ่ม)
//  - "ดู" สถานะเกมจากข้างนอก (PlantStatus, AlertPopupManager, หน้ารางวัล ฯลฯ) แล้วเล่นเอฟเฟกต์ตอนค่าเปลี่ยน
//
//  ปิดเอฟเฟกต์ทั้งหมด: ลบไฟล์นี้ทิ้งไฟล์เดียว เกมกลับเป็นเหมือนเดิม 100%
//  หรือตั้ง GrowGoJuice.Enabled = false
// =====================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GrowGoJuice : MonoBehaviour
{
    public static GrowGoJuice I;
    public static bool Enabled = true;

    // ==================== 🚀 สร้างตัวเองตอนเกมเริ่ม ====================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (I != null) return;
        GameObject go = new GameObject("[GrowGoJuice]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<GrowGoJuice>();
    }

    // ==================== 🎨 สีหลักของเอฟเฟกต์ (พาสเทลโทนสวน) ====================
    private static readonly Color C_Yellow = new Color(1f, 0.88f, 0.4f);
    private static readonly Color C_Pink = new Color(1f, 0.56f, 0.69f);
    private static readonly Color C_Green = new Color(0.49f, 0.85f, 0.34f);
    private static readonly Color C_Sky = new Color(0.43f, 0.78f, 1f);
    private static readonly Color C_Orange = new Color(1f, 0.7f, 0.28f);
    private static readonly Color C_Purple = new Color(0.76f, 0.65f, 1f);
    private static readonly Color C_White = Color.white;
    private static readonly Color[] Confetti = { C_Yellow, C_Pink, C_Green, C_Sky, C_Orange, C_Purple, C_White };
    private static readonly Color[] Pastel = { C_Yellow, C_Pink, C_Green, C_Sky, C_Purple, C_White };

    // ==================== 🧱 ของที่สร้างตอนเริ่ม ====================
    private Canvas fxCanvas;
    private RectTransform fxRoot;
    private Image flashImage;
    private Sprite sprGlow, sprStar, sprRing, sprHeart, sprLeaf, sprDrop, sprSquare, sprRays, sprFlower, sprPlus;

    private AudioSource[] sfxPool;
    private int sfxIndex;
    private AudioClip clipClick, clipBubble, clipWater, clipSparkle, clipChime, clipFanfare, clipWhoosh, clipPop, clipDing, clipSad, clipShield;

    private float U = 1f;   // หน่วยขนาด (ปรับตามขนาดจอ ออกแบบที่ 1080x1920)

    // ==================== ✨ particle (Image ธรรมดาบน Canvas) ====================
    private class P
    {
        public RectTransform rt;
        public Image img;
        public bool alive;
        public bool ambient;
        public Vector2 pos, vel;
        public float gravity, drag, life, maxLife, rot, spin, size0, size1, aspect;
        public float wobbleAmp, wobbleFreq, seed;
        public Color c0, c1;
        public float fadeIn;
    }
    private readonly List<P> pool = new List<P>();
    private const int MaxParticles = 450;

    // ==================== 👀 ตัวเฝ้าดูสถานะเกม ====================
    private float lastTapTime = -99f;
    private float armedAt;                        // ก่อนเวลานี้ = เพิ่งเข้าฉาก/เพิ่งโหลดข้อมูล ยังไม่เล่นเอฟเฟกต์
    private string sceneName = "";

    private PlantStatus plant;
    private MCController plantMC;
    private string pId; private int pStage; private string pStageId; private int pFertFx; private bool pSick;
    private string pWatered; private int pShield; private float plantArmedAt;

    private AlertPopupManager alerts;
    private bool[] alertLast = new bool[4];

    private class Watch { public GameObject go; public bool last; public Transform anchor; public Component owner; public bool mega; }
    private readonly List<Watch> rewardWatches = new List<Watch>();

    private Component friendVisit; private FieldInfo friendHelpedField; private int friendHelpedLast = -1;
    private FieldInfo friendBusyField; private bool friendSawBusy;   // isBusy = กำลังรอ DB ตอนกดรดน้ำ
    private MCController friendPlantMC;

    private float scanTimer;
    private int rewardScans;
    private float ambientTimer, leafTimer;
    private int ambientCount;

    // =====================================================================================
    //  เริ่มต้น
    // =====================================================================================
    private void Awake()
    {
        try
        {
            BuildCanvas();
            BuildSprites();
            BuildAudio();
        }
        catch (Exception e) { Debug.LogWarning("[GrowGoJuice] init error: " + e); Enabled = false; }

        SceneManager.sceneLoaded += OnSceneLoaded;
        try { SupabaseManager.OnPlayerLevelUp += OnLevelUp; } catch { }
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        try { SupabaseManager.OnPlayerLevelUp -= OnLevelUp; } catch { }
    }

    private void BuildCanvas()
    {
        GameObject cgo = new GameObject("JuiceCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        fxCanvas = cgo.AddComponent<Canvas>();
        fxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fxCanvas.sortingOrder = 30000;          // อยู่บนสุดเหนือ UI ทุกอัน
        // ไม่ใส่ GraphicRaycaster -> แตะทะลุ ไม่บังปุ่มของเกม

        GameObject root = new GameObject("FX", typeof(RectTransform));
        root.transform.SetParent(cgo.transform, false);
        fxRoot = root.GetComponent<RectTransform>();
        Stretch(fxRoot);

        GameObject fl = new GameObject("Flash", typeof(RectTransform));
        fl.transform.SetParent(cgo.transform, false);
        flashImage = fl.AddComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = new Color(1, 1, 1, 0);
        flashImage.enabled = false;
        Stretch(flashImage.rectTransform);
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    // =====================================================================================
    //  🖼️ สร้างรูป particle ด้วยโค้ด (ไม่ต้องมีไฟล์รูป)
    // =====================================================================================
    private void BuildSprites()
    {
        sprGlow = MakeSprite(64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f); });
        sprStar = MakeSprite(64, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float cross = Mathf.Clamp01(1f - (ax * ay * 18f + Mathf.Max(ax, ay) * 1.05f));
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2.6f);
            return Mathf.Clamp01(Mathf.Pow(cross, 1.4f) + core);
        });
        sprRing = MakeSprite(64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) * 9f); });
        sprHeart = MakeSprite(64, (x, y) =>
        {
            float hx = x * 1.25f, hy = y * 1.25f + 0.25f;
            float v = Mathf.Pow(hx * hx + hy * hy - 1f, 3f) - hx * hx * hy * hy * hy;
            return Mathf.Clamp01(-v * 18f);
        });
        sprLeaf = MakeSprite(64, (x, y) =>
        {
            // ใบไม้ = ส่วนที่ทับกันของวงกลม 2 วง + เส้นกลางใบ
            float r = 1.0f, off = 0.62f;
            float d1 = Mathf.Sqrt((x - off) * (x - off) + y * y), d2 = Mathf.Sqrt((x + off) * (x + off) + y * y);
            float a = Mathf.Clamp01((r - Mathf.Max(d1, d2)) * 14f);
            float vein = Mathf.Abs(x) < 0.035f && Mathf.Abs(y) < 0.7f ? 0.55f : 1f;
            return a * vein;
        });
        sprDrop = MakeSprite(64, (x, y) =>
        {
            float cy = y + 0.3f;
            float circle = Mathf.Clamp01((0.6f - Mathf.Sqrt(x * x + cy * cy)) * 14f);
            float tri = (y > -0.3f && y < 0.95f) ? Mathf.Clamp01(((0.6f * (0.95f - y) / 1.25f) - Mathf.Abs(x)) * 14f) : 0f;
            return Mathf.Max(circle, tri);
        });
        sprSquare = MakeSprite(16, (x, y) => 1f);
        sprRays = MakeSprite(256, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x);
            float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f)), 6f);
            return ray * Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 4f) + Mathf.Pow(Mathf.Clamp01(1f - d * 2.2f), 2f) * 0.7f;
        });
        sprFlower = MakeSprite(64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x);
            float petal = 0.62f + 0.33f * Mathf.Cos(ang * 5f);
            float a = Mathf.Clamp01((petal - d) * 12f);
            float center = d < 0.22f ? 0.65f : 1f;     // กลางดอกเข้มกว่า
            return a * center;
        });
        sprPlus = MakeSprite(64, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            bool arm = (ax < 0.24f && ay < 0.8f) || (ay < 0.24f && ax < 0.8f);
            return arm ? 1f : 0f;
        });
    }

    private Sprite MakeSprite(int size, Func<float, float, float> alpha)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f;
                float y = (j + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(alpha(x, y));
                px[j * size + i] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // =====================================================================================
    //  🔊 สร้างเสียงด้วยโค้ด (synth) ไม่มีไฟล์เสียงเพิ่ม
    // =====================================================================================
    private const int SR = 44100;

    private void BuildAudio()
    {
        sfxPool = new AudioSource[8];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            AudioSource a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            sfxPool[i] = a;
        }

        System.Random rng = new System.Random(7);

        // ปุ่ม: ติ๊กสั้นๆ นุ่มๆ
        clipClick = Synth("click", 0.07f, t =>
        {
            float f = Mathf.Lerp(1500f, 800f, t / 0.07f);
            return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 70f);
        });
        // ฟองน้ำ
        clipBubble = Synth("bubble", 0.12f, t => Bloop(t, 380f, 1100f, 0.09f));
        // รดน้ำ: ฟอง 4 ลูก + เสียงน้ำซ่า
        float lp = 0f;
        clipWater = Synth("water", 0.6f, t =>
        {
            float s = Bloop(t - 0.00f, 300f, 900f, 0.09f) + Bloop(t - 0.08f, 420f, 1200f, 0.08f) * 0.8f
                    + Bloop(t - 0.19f, 350f, 1000f, 0.09f) * 0.7f + Bloop(t - 0.3f, 500f, 1400f, 0.08f) * 0.6f;
            float n = (float)(rng.NextDouble() * 2 - 1);
            lp += (n - lp) * 0.18f;
            float splash = lp * Mathf.Exp(-t * 7f) * 0.6f;
            return s + splash;
        });
        // ประกายวิบวับ
        clipSparkle = Synth("sparkle", 0.6f, t =>
            Bell(t, 2093f, 0.0f, 9f) + Bell(t, 2637f, 0.06f, 9f) + Bell(t, 3136f, 0.12f, 9f) + Bell(t, 4186f, 0.18f, 8f) * 0.7f);
        // กริ๊งใส่ปุ๋ย (C6 E6 G6 C7)
        clipChime = Synth("chime", 0.9f, t =>
            Bell(t, 1046.5f, 0f, 5f) + Bell(t, 1318.5f, 0.07f, 5f) + Bell(t, 1568f, 0.14f, 5f) + Bell(t, 2093f, 0.21f, 4f));
        // แฟนแฟร์วิวัฒนาการ / เลเวลอัป
        clipFanfare = Synth("fanfare", 1.7f, t =>
        {
            float s = Brass(t, 392f, 0.00f, 0.13f) + Brass(t, 523.25f, 0.12f, 0.13f) + Brass(t, 659.25f, 0.24f, 0.13f);
            float chordT = t - 0.38f;
            if (chordT > 0)
            {
                float env = Mathf.Min(1f, chordT * 40f) * Mathf.Exp(-chordT * 1.8f);
                float vib = 1f + 0.006f * Mathf.Sin(2 * Mathf.PI * 5.5f * chordT);
                s += (Soft(523.25f * vib, chordT) + Soft(659.25f * vib, chordT) + Soft(783.99f * vib, chordT) + Soft(1046.5f * vib, chordT) * 0.8f) * env * 0.55f;
                s += Bell(t, 2093f, 0.38f, 4f) * 0.5f + Bell(t, 3136f, 0.5f, 4f) * 0.35f;
            }
            return s;
        });
        // วูชช
        float lp2 = 0f;
        clipWhoosh = Synth("whoosh", 0.7f, t =>
        {
            float n = (float)(rng.NextDouble() * 2 - 1);
            float k = Mathf.Lerp(0.02f, 0.35f, t / 0.7f);
            lp2 += (n - lp2) * k;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.7f));
            return lp2 * env * 1.6f;
        });
        // ป๊อป (ป๊อปอัปเปิด)
        clipPop = Synth("pop", 0.08f, t => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(450f, 1000f, t / 0.08f) * t) * Mathf.Exp(-t * 45f));
        // ติ๊ง (หัวใจ/ไอเทม)
        clipDing = Synth("ding", 0.5f, t => Bell(t, 1568f, 0f, 7f) + Bell(t, 2349f, 0.05f, 7f) * 0.8f);
        // เศร้า (ป่วย)
        clipSad = Synth("sad", 0.6f, t =>
        {
            float f = Mathf.Lerp(330f, 165f, t / 0.6f);
            return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * t)) * Mathf.Exp(-t * 4f) * Mathf.Min(1f, t * 60f);
        });
        // เกราะ
        clipShield = Synth("shield", 0.6f, t =>
        {
            float f = Mathf.Lerp(600f, 1400f, Mathf.Clamp01(t / 0.25f));
            return (Mathf.Sin(2 * Mathf.PI * f * t) * 0.6f + Bell(t, 2637f, 0.2f, 6f)) * Mathf.Exp(-t * 3.5f);
        });
    }

    private static float Bloop(float t, float f0, float f1, float dur)
    {
        if (t < 0 || t > dur * 1.6f) return 0f;
        float f = Mathf.Lerp(f0, f1, Mathf.Clamp01(t / dur));
        return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 30f) * Mathf.Min(1f, t * 400f);
    }

    private static float Bell(float t, float f, float start, float decay)
    {
        t -= start;
        if (t < 0) return 0f;
        float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 300f);
        return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t) + 0.12f * Mathf.Sin(2 * Mathf.PI * f * 3.02f * t)) * env * 0.5f;
    }

    private static float Soft(float f, float t)
    {
        return Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2f * t) + 0.12f * Mathf.Sin(2 * Mathf.PI * f * 3f * t);
    }

    private static float Brass(float t, float f, float start, float dur)
    {
        t -= start;
        if (t < 0 || t > dur + 0.25f) return 0f;
        float env = Mathf.Min(1f, t * 60f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 20f));
        return Soft(f, t) * env * 0.5f;
    }

    private AudioClip Synth(string name, float dur, Func<float, float> f)
    {
        int n = Mathf.CeilToInt(SR * dur);
        float[] d = new float[n];
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float v = f(i / (float)SR);
            d[i] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }
        float norm = 0.8f / peak;
        int fade = Mathf.Min(n, 400);
        for (int i = 0; i < n; i++)
        {
            d[i] *= norm;
            if (i > n - fade) d[i] *= (n - i) / (float)fade;   // กันเสียงคลิกตอนจบ
        }
        AudioClip c = AudioClip.Create("juice_" + name, n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    // เล่นเสียง (ใช้ระดับเสียง SFX เดียวกับหน้าตั้งค่า PlayerPrefs "SfxVolume")
    private void Play(AudioClip clip, float vol = 1f, float pitch = 1f)
    {
        if (clip == null || sfxPool == null) return;
        float master = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (master <= 0.001f) return;
        AudioSource a = sfxPool[sfxIndex];
        sfxIndex = (sfxIndex + 1) % sfxPool.Length;
        a.pitch = pitch;
        a.PlayOneShot(clip, vol * master);
    }

    // =====================================================================================
    //  🎬 เปลี่ยนฉาก: หาของในฉากใหม่ใหม่หมด
    // =====================================================================================
    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        sceneName = SceneManager.GetActiveScene().name;
        armedAt = Time.unscaledTime + 1.2f;
        plant = null; plantMC = null; alerts = null; friendVisit = null; friendPlantMC = null; friendHelpedLast = -1; friendSawBusy = false;
        rewardWatches.Clear(); rewardScans = 0; lastTapTime = -99f;
        // ล้าง particle ที่ค้างจากฉากก่อน
        for (int i = 0; i < pool.Count; i++) Kill(pool[i]);
        ambientCount = 0;
        scanTimer = 0.1f;

        if (sceneName == "MainForest") StartCoroutine(LeafSweep(0.8f));
    }

    // สแกนของในฉาก (ทุก 1 วิ) : ปุ่มใหม่, ต้นไม้, หน้ารางวัล
    private void Scan()
    {
        // --- ต้นไม้ของเรา ---
        if (plant == null)
        {
            plant = FindAnyObjectByType<PlantStatus>();
            if (plant != null) { plantMC = plant.GetComponent<MCController>(); BaselinePlant(); plantArmedAt = Time.unscaledTime + 3f; }
        }
        if (plantMC != null) AttachScale(plantMC.gameObject, true);

        // --- ป๊อปอัป +EXP/+ดอกไม้/+หัวใจ ---
        if (alerts == null)
        {
            alerts = FindAnyObjectByType<AlertPopupManager>(FindObjectsInactive.Include);
            if (alerts != null) for (int i = 0; i < 4; i++) alertLast[i] = AlertGO(i) != null && AlertGO(i).activeInHierarchy;
        }

        // --- สวนเพื่อน ---
        if (friendVisit == null)
        {
            FriendVisitController fv = FindAnyObjectByType<FriendVisitController>(FindObjectsInactive.Include);
            if (fv != null)
            {
                friendVisit = fv;
                friendPlantMC = fv.plantCharacter;
                friendHelpedField = typeof(FriendVisitController).GetField("helpedTodayPlantIds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                friendBusyField = typeof(FriendVisitController).GetField("isBusy", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                friendHelpedLast = FriendHelpedCount();
            }
        }
        if (friendPlantMC != null) AttachScale(friendPlantMC.gameObject, true);

        // --- ปุ่มทุกปุ่ม: เด้งตอนกด + เสียงติ๊ก ---
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null || !b.gameObject.scene.IsValid() || !b.gameObject.scene.isLoaded) continue;
            if (b.GetComponent<GrowGoJuiceScale>() != null) continue;
            if (b.transition == Selectable.Transition.Animation || b.GetComponent<Animator>() != null) continue;
            GrowGoJuiceScale js = b.gameObject.AddComponent<GrowGoJuiceScale>();
            js.isButton = true;
            // เสียงติ๊กเล่นจาก GrowGoJuicePress.OnPointerClick (ไม่ผูกกับ onClick เพราะบางสคริปต์ RemoveAllListeners ทิ้ง)
            b.gameObject.AddComponent<GrowGoJuicePress>().clickSound = true;
        }

        // --- หน้ารางวัล (หาจากชื่อ field ใน component: sunburst / dailySunburst / rewardBox / greenPointBox) ---
        if (rewardScans < 3) { rewardScans++; rewardWatches.Clear(); FindRewardWatches(); }   // สแกนแค่ 3 รอบแรกของฉาก (หนัก)
    }

    private void FindRewardWatches()
    {
        string[] names = { "sunburst", "dailySunburst", "rewardBox", "greenPointBox" };
        MonoBehaviour[] all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour mb = all[i];
            if (mb == null || mb is GrowGoJuice || mb is GrowGoJuiceScale || mb is GrowGoJuicePress) continue;
            if (!mb.gameObject.scene.IsValid() || !mb.gameObject.scene.isLoaded) continue;
            Type t = mb.GetType();
            for (int n = 0; n < names.Length; n++)
            {
                FieldInfo f = t.GetField(names[n], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) continue;
                object v = null;
                try { v = f.GetValue(mb); } catch { }
                GameObject go = null;
                if (v is Component comp && comp != null) go = comp.gameObject;
                else if (v is GameObject g && g != null) go = g;
                if (go == null) continue;

                Transform anchor = go.transform;
                // กล่องรางวัลร้านค้า: ใช้ตำแหน่งรูปไอเทม
                if (names[n] == "rewardBox")
                {
                    FieldInfo icon = t.GetField("rewardIcon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (icon != null && icon.GetValue(mb) is Component ic && ic != null) anchor = ic.transform;
                }
                rewardWatches.Add(new Watch { go = go, last = go.activeInHierarchy, anchor = anchor, owner = mb, mega = names[n] == "greenPointBox" });
            }
        }
    }

    private int FriendHelpedCount()
    {
        if (friendVisit == null || friendHelpedField == null) return -1;
        try
        {
            object v = friendHelpedField.GetValue(friendVisit);
            if (v is HashSet<string> hs) return hs.Count;
        }
        catch { }
        return -1;
    }

    private GameObject AlertGO(int i)
    {
        if (alerts == null) return null;
        switch (i)
        {
            case 0: return alerts.moisturePopup;
            case 1: return alerts.expPopup;
            case 2: return alerts.flowerPopup;
            default: return alerts.relationshipPopup;
        }
    }

    private void AttachScale(GameObject go, bool breathe)
    {
        if (go == null) return;
        GrowGoJuiceScale js = go.GetComponent<GrowGoJuiceScale>();
        if (js == null)
        {
            js = go.AddComponent<GrowGoJuiceScale>();
            if (go.GetComponent<Button>() != null && go.GetComponent<GrowGoJuicePress>() == null) go.AddComponent<GrowGoJuicePress>();
        }
        js.breathe = breathe;
    }

    private void OnAnyButtonClick()
    {
        if (!Enabled) return;
        Play(clipClick, 0.45f, UnityEngine.Random.Range(0.95f, 1.08f));
    }

    // =====================================================================================
    //  🔌 ให้สคริปต์ Juice ตัวอื่นเรียกใช้ (เช่น GrowGoQuizFX)
    // =====================================================================================
    public void PlayClick() { OnAnyButtonClick(); }

    public void PlaySfx(string id, float vol = 1f, float pitch = 1f)
    {
        if (!Enabled) return;
        AudioClip c = null;
        switch (id)
        {
            case "click": c = clipClick; break;
            case "bubble": c = clipBubble; break;
            case "sparkle": c = clipSparkle; break;
            case "chime": c = clipChime; break;
            case "fanfare": c = clipFanfare; break;
            case "whoosh": c = clipWhoosh; break;
            case "pop": c = clipPop; break;
            case "ding": c = clipDing; break;
        }
        Play(c, vol, pitch);
    }

    public Vector2 ScreenPosOf(Transform t) { return ScreenPos(t); }
    public void FxSparkles(Vector2 p, int n, Color tint, float speed) { if (Enabled) Sparkles(p, n, tint, speed); }
    public void FxConfetti(Vector2 p, int n, float speed) { if (Enabled) ConfettiBurst(p, n, speed); }
    public void FxCannons(int perSide) { if (Enabled) ConfettiCannons(perSide); }
    public void FxTap(Vector2 p) { if (Enabled) TapFX(p); }
    public void FxFlash(Color c, float alpha, float dur) { if (Enabled) Flash(c, alpha, dur); }

    // =====================================================================================
    //  🔁 Update: แตะจอ, ตัวเฝ้าดู, ambient, particle
    // =====================================================================================
    private void Update()
    {
        if (!Enabled || fxRoot == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        U = Mathf.Max(0.4f, Mathf.Min(Screen.width / 1080f, Screen.height / 1920f));

        try
        {
            scanTimer -= dt;
            if (scanTimer <= 0f) { scanTimer = 1f; Scan(); }

            // --- แตะจอ = ประกาย ---
            if (Input.GetMouseButtonDown(0))
            {
                lastTapTime = Time.unscaledTime;
                Vector2 mp = Input.mousePosition;
                TapFX(mp);
                if (plantMC != null && IsOver(plantMC.transform as RectTransform, mp) && !CoveredByUI(plantMC.transform, mp)) PlantTapFX(plantMC.transform);
                else if (friendPlantMC != null && IsOver(friendPlantMC.transform as RectTransform, mp) && !CoveredByUI(friendPlantMC.transform, mp)) PlantTapFX(friendPlantMC.transform);
            }

            if (Time.unscaledTime > armedAt)
            {
                WatchPlant();
                WatchAlerts();
                WatchRewards();
                WatchFriend();
            }

            Ambient(dt);
        }
        catch (Exception e)
        {
            // เอฟเฟกต์พังต้องไม่ทำให้เกมพัง
            Debug.LogWarning("[GrowGoJuice] " + e.Message);
        }

        StepParticles(dt);
    }

    private bool RecentTap(float window = 2.5f) { return Time.unscaledTime - lastTapTime < window; }

    // ---------- 💧 เวลารดน้ำเพิ่งเกิดขึ้นจริงมั้ย (ภายใน 15 วิ และไม่ใช่เวลาเดิมที่แค่เขียนคนละรูปแบบ) ----------
    private static bool JustWatered(string wateredAt, string previous)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        DateTimeOffset t, old;
        if (!DateTimeOffset.TryParse(wateredAt, ci, System.Globalization.DateTimeStyles.None, out t)) return false;
        if (!string.IsNullOrEmpty(previous) && DateTimeOffset.TryParse(previous, ci, System.Globalization.DateTimeStyles.None, out old)
            && Math.Abs((t - old).TotalSeconds) < 2.0) return false;   // เวลาเดียวกัน แค่โหลดใหม่จาก DB
        return Math.Abs((DateTimeOffset.Now - t).TotalSeconds) < 15.0;
    }

    // ---------- 🌱 ดูค่าต้นไม้ ----------
    private void BaselinePlant()
    {
        if (plant == null) return;
        pId = plant.playerPlantId; pStage = plant.currentStageLevel; pStageId = plant.currentStageId; pFertFx = plant.fxFertilizeCount;
        pSick = plant.isSick; pWatered = plant.lastWateredAt; pShield = plant.immunityShields;
    }

    private void WatchPlant()
    {
        if (plant == null) return;
        if (plant.playerPlantId != pId) { BaselinePlant(); plantArmedAt = Time.unscaledTime + 3f; return; }   // สลับต้น
        Transform t = plantMC != null ? plantMC.transform : plant.transform;

        // รดน้ำ: เช็คก่อนช่วงกันเอฟเฟกต์ 3 วิหลังสลับต้น (แก้บัค 2026-11-26: สลับต้นแล้วกดรดน้ำเร็วๆ เอฟเฟกต์หาย)
        //   ปลอดภัยเพราะ JustWatered กันเวลาเดิมที่แค่โหลดจาก DB มาคนละรูปแบบไว้แล้ว
        //   ไม่ต้องรอแตะจอ (RecentTap) เพราะ JustWatered เช็คแล้วว่าเวลารดน้ำเป็นของภายใน 15 วิจริง
        bool watered = plant.lastWateredAt != pWatered && plant.currentMoisture >= plant.maxMoisture - 0.5f && JustWatered(plant.lastWateredAt, pWatered);
        if (watered) StartCoroutine(WaterFX(t));

        // ใส่ปุ๋ย: ดูจากตัวนับที่ PlantStatus เพิ่มตอนใส่ปุ๋ยจริง (แก้บัค 2026-11-26: สลับต้นแล้วใส่ปุ๋ยเร็วๆ เอฟเฟกต์หาย)
        //   ตัวนับนี้ไม่โหลดจาก DB เลยไม่มีเอฟเฟกต์หลอกตอนโหลดข้อมูลต้นใหม่ ไม่ต้องรอ 3 วิ และไม่ต้องรอแตะจอ
        if (plant.fxFertilizeCount != pFertFx) FertilizeFX(t);

        if (Time.unscaledTime < plantArmedAt) { BaselinePlant(); return; }

        // วิวัฒนาการ (ขึ้นร่าง)
        if (plant.currentStageLevel == pStage + 1) StartCoroutine(EvolveFX(t, plant.currentStageLevel >= 4));
        // เปลี่ยนสายพันธุ์เชอร์รี่ (ร่างเท่าเดิม แต่ stage_id เปลี่ยน)
        else if (plant.currentStageId != pStageId && plant.currentStageLevel == pStage && RecentTap(4f)) MagicFX(t);

        // หายป่วย / ป่วย
        if (pSick && !plant.isSick) HealFX(t);
        else if (!pSick && plant.isSick) SickFX(t);
        // ได้เกราะ
        if (plant.immunityShields > pShield) ShieldFX(t);

        BaselinePlant();
    }

    // ---------- 💬 ป๊อปอัป +ค่าต่างๆ ----------
    private void WatchAlerts()
    {
        if (alerts == null) return;
        for (int i = 0; i < 4; i++)
        {
            GameObject g = AlertGO(i);
            bool on = g != null && g.activeInHierarchy;
            if (on && !alertLast[i])
            {
                if (g.GetComponent<Animator>() == null)
                {
                    GrowGoJuiceScale js = g.GetComponent<GrowGoJuiceScale>();
                    if (js == null) js = g.AddComponent<GrowGoJuiceScale>();
                    js.Kick(0.35f);
                }
                Transform from = plantMC != null ? plantMC.transform : (friendPlantMC != null ? friendPlantMC.transform : null);
                Vector2 o = from != null ? ScreenPos(from) : new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
                switch (i)
                {
                    case 0: Fountain(sprDrop, o, 7, C_Sky, 0.9f); break;
                    case 1: Fountain(sprStar, o, 8, C_Yellow, 1f); break;
                    case 2: Fountain(sprFlower, o, 7, C_Pink, 1.1f); break;
                    default: Fountain(sprHeart, o, 7, new Color(1f, 0.42f, 0.55f), 1.1f); Play(clipDing, 0.5f, 1.1f); break;
                }
            }
            alertLast[i] = on;
        }
    }

    // ---------- 🎁 หน้ารางวัล ----------
    private void WatchRewards()
    {
        for (int i = 0; i < rewardWatches.Count; i++)
        {
            Watch w = rewardWatches[i];
            if (w.go == null) continue;
            bool on = w.go.activeInHierarchy;
            if (on && !w.last)
            {
                bool mega = w.mega || IsGradeS(w.owner);
                StartCoroutine(RewardFX(w.anchor, mega));
            }
            w.last = on;
        }
    }

    // ผสมปุ๋ยได้เกรด S = เอฟเฟกต์ใหญ่พิเศษ
    private bool IsGradeS(Component owner)
    {
        if (owner == null) return false;
        FieldInfo f = owner.GetType().GetField("rewardGradeText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return false;
        TMP_Text txt = f.GetValue(owner) as TMP_Text;
        if (txt == null || string.IsNullOrEmpty(txt.text)) return false;
        string s = txt.text.Trim().ToUpperInvariant();
        return s == "S" || s.EndsWith(" S") || s.EndsWith(":S") || s.Contains("เกรด S") || s.Contains("GRADE S");
    }

    // ---------- 🤝 รดน้ำสวนเพื่อน ----------
    // แก้บัค (2026-11-26): กดเพื่อนคนถัดไปแล้วเอฟเฟกต์รดน้ำขึ้นเอง
    //   สาเหตุ: เปลี่ยนเพื่อน -> LoadHelpedToday() ล้าง helpedTodayPlantIds (เหลือ 0) แล้วโหลดจาก DB ใหม่ (กลับมา N)
    //   ตัวนับเลย "เพิ่มขึ้น" ทั้งที่ไม่ได้รดน้ำ บวกกับเพิ่งแตะปุ่มถัดไป -> RecentTap ผ่าน
    //   วิธีแก้: เล่นเอฟเฟกต์เฉพาะเมื่อก่อนหน้านั้นเห็น isBusy = true (WaterRoutine ตั้งไว้ตอนรอ DB รดน้ำ)
    //   ตอนโหลดเพื่อนใหม่ isBusy ไม่ถูกตั้งเป็น true เลยไม่เล่น
    private void WatchFriend()
    {
        if (friendVisit == null) return;
        if (FriendBusy()) friendSawBusy = true;
        int c = FriendHelpedCount();
        if (c < 0) return;
        if (c < friendHelpedLast) friendSawBusy = false;   // ถูกล้าง = กำลังโหลดเพื่อนใหม่ ไม่ใช่การรดน้ำ
        if (friendHelpedLast >= 0 && c > friendHelpedLast && friendSawBusy && !FriendBusy() && friendPlantMC != null)
        {
            StartCoroutine(WaterFX(friendPlantMC.transform));
            friendSawBusy = false;
        }
        friendHelpedLast = c;
    }

    private bool FriendBusy()
    {
        if (friendVisit == null || friendBusyField == null) return false;
        try { return friendBusyField.GetValue(friendVisit) is bool b && b; } catch { return false; }
    }

    // ---------- ⭐ เลเวลอัป ----------
    private void OnLevelUp(int newLevel)
    {
        if (!Enabled || I == null) return;
        try { StartCoroutine(LevelUpFX(newLevel)); } catch { }
    }

    // =====================================================================================
    //  💥 เอฟเฟกต์แต่ละแบบ
    // =====================================================================================
    private void TapFX(Vector2 p)
    {
        Spawn(sprRing, p, Vector2.zero, 0.38f, 20f, 150f, new Color(1, 1, 1, 0.75f), new Color(1, 1, 1, 0f));
        Spawn(sprGlow, p, Vector2.zero, 0.25f, 90f, 30f, new Color(1f, 0.95f, 0.7f, 0.6f), new Color(1f, 1f, 1f, 0f));
        int n = 6;
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(300f, 520f) * U;
            Color c = Pastel[UnityEngine.Random.Range(0, Pastel.Length)];
            P q = Spawn(sprStar, p, v, UnityEngine.Random.Range(0.35f, 0.55f), UnityEngine.Random.Range(26f, 40f), 0f, c, c);
            if (q != null) { q.drag = 5f; q.spin = UnityEngine.Random.Range(-360f, 360f); }
        }
    }

    private void PlantTapFX(Transform t)
    {
        Vector2 p = ScreenPos(t) + new Vector2(0, 120f * U);
        for (int i = 0; i < 4; i++)
        {
            Vector2 v = new Vector2(UnityEngine.Random.Range(-180f, 180f), UnityEngine.Random.Range(350f, 600f)) * U;
            P q = Spawn(sprHeart, p + UnityEngine.Random.insideUnitCircle * 60f * U, v, UnityEngine.Random.Range(0.9f, 1.3f), 50f, 30f,
                        new Color(1f, 0.45f, 0.6f, 1f), new Color(1f, 0.6f, 0.75f, 0f));
            if (q != null) { q.drag = 2f; q.gravity = -150f; q.wobbleAmp = 40f; q.wobbleFreq = 5f; }
        }
        Play(clipBubble, 0.4f, UnityEngine.Random.Range(1.1f, 1.35f));
    }

    private IEnumerator WaterFX(Transform t)
    {
        Vector2 p = ScreenPos(t);
        Play(clipWater, 0.8f);
        // หยดน้ำตกลงมาจากข้างบน
        for (int i = 0; i < 14; i++)
        {
            Vector2 s = p + new Vector2(UnityEngine.Random.Range(-170f, 170f), UnityEngine.Random.Range(380f, 560f)) * U;
            P q = Spawn(sprDrop, s, new Vector2(0, -UnityEngine.Random.Range(400f, 700f) * U), 0.5f, UnityEngine.Random.Range(34f, 50f), 30f,
                        new Color(0.55f, 0.85f, 1f, 1f), new Color(0.7f, 0.9f, 1f, 0.2f));
            if (q != null) { q.gravity = 2600f; q.rot = 180f; q.fadeIn = 0.05f; }
        }
        yield return Wait(0.33f);
        if (t == null) yield break;
        p = ScreenPos(t);
        Kick(t, 0.22f);
        // น้ำกระเซ็น
        Spawn(sprRing, p, Vector2.zero, 0.55f, 60f, 520f, new Color(0.6f, 0.88f, 1f, 0.9f), new Color(0.6f, 0.88f, 1f, 0f));
        for (int i = 0; i < 18; i++)
        {
            float a = UnityEngine.Random.Range(0.15f, Mathf.PI - 0.15f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(400f, 900f) * U;
            P q = Spawn(sprDrop, p, v, UnityEngine.Random.Range(0.5f, 0.8f), UnityEngine.Random.Range(18f, 30f), 10f,
                        new Color(0.6f, 0.88f, 1f, 1f), new Color(0.8f, 0.95f, 1f, 0f));
            if (q != null) { q.gravity = 1800f; q.rot = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f; }
        }
        Sparkles(p, 10, C_Sky, 300f);
        yield return Wait(0.25f);
        // ฟองอากาศลอยขึ้นช้าๆ
        for (int i = 0; i < 8; i++)
        {
            if (t == null) yield break;
            Vector2 s = ScreenPos(t) + new Vector2(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(-40f, 80f)) * U;
            P q = Spawn(sprRing, s, new Vector2(0, UnityEngine.Random.Range(120f, 220f) * U), UnityEngine.Random.Range(1.0f, 1.6f), UnityEngine.Random.Range(22f, 40f), 50f,
                        new Color(0.75f, 0.93f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f));
            if (q != null) { q.wobbleAmp = 30f; q.wobbleFreq = 4f; q.fadeIn = 0.15f; }
            yield return Wait(0.05f);
        }
    }

    private void FertilizeFX(Transform t)
    {
        Vector2 p = ScreenPos(t);
        Play(clipChime, 0.7f);
        Kick(t, 0.28f);
        Spawn(sprRing, p, Vector2.zero, 0.6f, 60f, 600f, new Color(0.6f, 1f, 0.5f, 0.9f), new Color(1f, 0.95f, 0.5f, 0f));
        Spawn(sprGlow, p, Vector2.zero, 0.7f, 200f, 650f, new Color(0.7f, 1f, 0.5f, 0.55f), new Color(1f, 1f, 0.6f, 0f));
        // ประกายหมุนเป็นเกลียวขึ้น
        for (int i = 0; i < 26; i++)
        {
            float a = i * 0.55f;
            Vector2 off = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.35f) * 170f * U;
            Vector2 v = new Vector2(-Mathf.Sin(a) * 160f, UnityEngine.Random.Range(380f, 750f)) * U;
            Color c = (i % 3 == 0) ? C_Yellow : (i % 3 == 1 ? C_Green : C_White);
            P q = Spawn(sprStar, p + off - new Vector2(0, 60f * U), v, UnityEngine.Random.Range(0.8f, 1.3f), UnityEngine.Random.Range(28f, 46f), 0f, c, c);
            if (q != null) { q.drag = 1.4f; q.spin = UnityEngine.Random.Range(-300f, 300f); q.wobbleAmp = 35f; q.wobbleFreq = 6f; }
        }
        // ใบไม้ปลิว
        for (int i = 0; i < 8; i++)
        {
            Vector2 v = new Vector2(UnityEngine.Random.Range(-500f, 500f), UnityEngine.Random.Range(500f, 900f)) * U;
            P q = Spawn(sprLeaf, p, v, UnityEngine.Random.Range(1.0f, 1.5f), UnityEngine.Random.Range(40f, 60f), 30f,
                        new Color(0.45f, 0.82f, 0.32f, 1f), new Color(0.55f, 0.9f, 0.4f, 0f));
            if (q != null) { q.gravity = 900f; q.drag = 1.2f; q.spin = UnityEngine.Random.Range(-400f, 400f); q.aspect = 0.6f; }
        }
    }

    private void HealFX(Transform t)
    {
        Vector2 p = ScreenPos(t);
        Play(clipSparkle, 0.7f, 1.1f);
        Kick(t, 0.25f);
        Spawn(sprRing, p, Vector2.zero, 0.7f, 80f, 700f, new Color(1f, 1f, 1f, 0.9f), new Color(0.6f, 1f, 0.6f, 0f));
        for (int i = 0; i < 12; i++)
        {
            Vector2 s = p + new Vector2(UnityEngine.Random.Range(-200f, 200f), UnityEngine.Random.Range(-80f, 120f)) * U;
            P q = Spawn(sprPlus, s, new Vector2(0, UnityEngine.Random.Range(250f, 450f) * U), UnityEngine.Random.Range(0.9f, 1.4f), UnityEngine.Random.Range(30f, 50f), 20f,
                        new Color(0.5f, 0.95f, 0.5f, 1f), new Color(0.8f, 1f, 0.8f, 0f));
            if (q != null) { q.fadeIn = 0.1f; q.wobbleAmp = 20f; q.wobbleFreq = 3f; }
        }
        Sparkles(p, 14, C_Green, 500f);
        BigText(t, "หายป่วยแล้ว!", "HEALED!", C_Green, 0.9f);
    }

    private void SickFX(Transform t)
    {
        Vector2 p = ScreenPos(t);
        Play(clipSad, 0.6f);
        for (int i = 0; i < 12; i++)
        {
            Vector2 s = p + new Vector2(UnityEngine.Random.Range(-150f, 150f), UnityEngine.Random.Range(0f, 150f)) * U;
            P q = Spawn(sprGlow, s, new Vector2(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(80f, 200f)) * U, UnityEngine.Random.Range(1.0f, 1.8f),
                        UnityEngine.Random.Range(50f, 90f), 140f, new Color(0.55f, 0.4f, 0.75f, 0.55f), new Color(0.4f, 0.35f, 0.5f, 0f));
            if (q != null) { q.fadeIn = 0.2f; q.wobbleAmp = 25f; q.wobbleFreq = 2f; }
        }
    }

    private void ShieldFX(Transform t)
    {
        Vector2 p = ScreenPos(t);
        Play(clipShield, 0.6f);
        for (int i = 0; i < 3; i++)
        {
            Spawn(sprRing, p, Vector2.zero, 0.7f + i * 0.15f, 150f, 520f + i * 80f, new Color(0.5f, 0.85f, 1f, 0.9f), new Color(0.7f, 0.95f, 1f, 0f));   // ซ้อน 3 วง
        }
        Sparkles(p, 12, C_Sky, 400f);
    }

    private void MagicFX(Transform t)
    {
        Vector2 p = ScreenPos(t);
        Play(clipSparkle, 0.7f);
        Kick(t, 0.3f);
        Spawn(sprGlow, p, Vector2.zero, 0.5f, 300f, 700f, new Color(1f, 0.8f, 0.95f, 0.7f), new Color(1f, 1f, 1f, 0f));
        Sparkles(p, 24, C_Pink, 650f);
        Petals(p, 14);
    }

    // ---------- 🌟 วิวัฒนาการ (ไฮไลต์ของเดโม) ----------
    private IEnumerator EvolveFX(Transform t, bool finalForm)
    {
        if (t == null) yield break;
        Play(clipWhoosh, 0.8f);
        // 1) พลังงานดูดเข้าต้น + ต้นสั่น
        float dur = 0.75f, e = 0f;
        GrowGoJuiceScale js = t.GetComponent<GrowGoJuiceScale>();
        while (e < dur)
        {
            if (t == null) yield break;
            Vector2 c = ScreenPos(t);
            for (int k = 0; k < 2; k++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float r = UnityEngine.Random.Range(380f, 560f) * U;
                Vector2 s = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Color col = finalForm ? C_Yellow : Pastel[UnityEngine.Random.Range(0, Pastel.Length)];
                P q = Spawn(sprGlow, s, (c - s) / 0.45f, 0.45f, 40f, 12f, new Color(col.r, col.g, col.b, 0.2f), new Color(1f, 1f, 1f, 1f));
                if (q != null) q.fadeIn = 0.1f;
            }
            if (js != null) js.jitter = Mathf.Lerp(0.0f, 0.06f, e / dur);
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        if (js != null) js.jitter = 0f;
        if (t == null) yield break;

        // 2) บูม!
        Vector2 p = ScreenPos(t);
        Play(clipFanfare, 0.9f);
        Play(clipSparkle, 0.6f, 1.2f);
        Flash(finalForm ? new Color(1f, 0.95f, 0.75f) : Color.white, 0.75f, 0.6f);
        Shake(0.45f, 22f);
        Kick(t, 0.45f);

        Color rayCol = finalForm ? new Color(1f, 0.85f, 0.35f, 0.55f) : new Color(1f, 1f, 0.8f, 0.45f);
        P rays = Spawn(sprRays, p, Vector2.zero, 2.4f, 200f, 1500f, rayCol, new Color(rayCol.r, rayCol.g, rayCol.b, 0f));
        if (rays != null) { rays.spin = 35f; rays.fadeIn = 0.1f; }
        if (finalForm)
        {
            P rays2 = Spawn(sprRays, p, Vector2.zero, 2.8f, 300f, 1900f, new Color(1f, 0.6f, 0.8f, 0.35f), new Color(1f, 0.6f, 0.8f, 0f));
            if (rays2 != null) { rays2.spin = -22f; rays2.fadeIn = 0.15f; }
        }
        Spawn(sprRing, p, Vector2.zero, 0.7f, 100f, 1300f, new Color(1, 1, 1, 1), new Color(1, 1, 1, 0));
        Spawn(sprRing, p, Vector2.zero, 0.9f, 60f, 900f, new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.9f, 0.5f, 0f));
        Spawn(sprGlow, p, Vector2.zero, 0.9f, 900f, 300f, new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0));
        ConfettiBurst(p, finalForm ? 110 : 70, 1300f);
        Sparkles(p, finalForm ? 40 : 26, C_Yellow, 900f);
        Petals(p, finalForm ? 24 : 12);

        BigText(t, finalForm ? "ร่างสุดท้าย!!" : "โตขึ้นแล้ว!", finalForm ? "FINAL FORM!!" : "EVOLVED!", finalForm ? C_Yellow : C_Green, finalForm ? 1.3f : 1.1f);

        // 3) ฝนประกายตามหลัง
        for (int i = 0; i < (finalForm ? 30 : 16); i++)
        {
            yield return Wait(0.05f);
            Vector2 s = new Vector2(UnityEngine.Random.Range(0f, Screen.width), Screen.height + 40f * U);
            Color col = Confetti[UnityEngine.Random.Range(0, Confetti.Length)];
            P q = Spawn(sprStar, s, new Vector2(UnityEngine.Random.Range(-60f, 60f), -UnityEngine.Random.Range(300f, 600f)) * U, 3f, UnityEngine.Random.Range(30f, 50f), 20f, col, col);
            if (q != null) { q.spin = UnityEngine.Random.Range(-200f, 200f); q.wobbleAmp = 50f; q.wobbleFreq = 2.5f; }
        }
        if (finalForm)
        {
            yield return Wait(0.3f);
            Play(clipSparkle, 0.6f, 0.9f);
            ConfettiCannons(60);
        }
    }

    // ---------- 🎁 หน้ารางวัล ----------
    private IEnumerator RewardFX(Transform anchor, bool mega)
    {
        yield return Wait(0.12f);    // รอให้หน้ารางวัลของเกมเริ่มเด้งก่อน
        if (anchor == null) yield break;
        Vector2 p = ScreenPos(anchor);
        Play(clipSparkle, 0.8f);
        if (mega) { Play(clipFanfare, 0.8f); Flash(Color.white, 0.6f, 0.5f); Shake(0.3f, 14f); }
        else Play(clipChime, 0.5f, 1.2f);

        Spawn(sprRing, p, Vector2.zero, 0.6f, 80f, 1000f, new Color(1, 1, 1, 1), new Color(1, 1, 1, 0));
        Spawn(sprGlow, p, Vector2.zero, 0.7f, 700f, 200f, new Color(1f, 1f, 0.85f, 0.8f), new Color(1, 1, 1, 0));
        Sparkles(p, mega ? 36 : 22, C_Yellow, 800f);
        ConfettiCannons(mega ? 70 : 40);
        if (mega)
        {
            P r = Spawn(sprRays, p, Vector2.zero, 2.2f, 200f, 1500f, new Color(1f, 0.85f, 0.4f, 0.5f), new Color(1f, 0.85f, 0.4f, 0f));
            if (r != null) r.spin = 30f;
            BigText(anchor, "สุดยอด!!", "AMAZING!!", C_Yellow, 1.2f);
        }
        // ประกายวิบวับรอบๆ ต่ออีกพักนึง
        for (int i = 0; i < 14; i++)
        {
            yield return Wait(0.09f);
            if (anchor == null || !anchor.gameObject.activeInHierarchy) yield break;
            Vector2 s = ScreenPos(anchor) + UnityEngine.Random.insideUnitCircle * 260f * U;
            P q = Spawn(sprStar, s, Vector2.zero, 0.6f, 10f, 70f, C_White, new Color(1f, 0.95f, 0.6f, 0f));
            if (q != null) q.spin = 180f;
        }
    }

    // ---------- ⭐ เลเวลอัป ----------
    private IEnumerator LevelUpFX(int lv)
    {
        yield return Wait(0.2f);
        Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        Play(clipFanfare, 0.9f, 1.06f);
        Flash(new Color(1f, 0.95f, 0.7f), 0.6f, 0.55f);
        Shake(0.35f, 16f);
        P r = Spawn(sprRays, c, Vector2.zero, 2.4f, 300f, 1700f, new Color(1f, 0.85f, 0.4f, 0.5f), new Color(1f, 0.85f, 0.4f, 0f));
        if (r != null) r.spin = 25f;
        Spawn(sprRing, c, Vector2.zero, 0.8f, 100f, 1400f, new Color(1, 1, 1, 1), new Color(1, 1, 1, 0));
        ConfettiCannons(80);
        Sparkles(c, 30, C_Yellow, 900f);
        BigTextAt(c, "LEVEL UP!", "LEVEL UP!", C_Yellow, 1.4f);
        yield return Wait(0.35f);
        BigTextAt(c - new Vector2(0, 150f * U), "Lv." + lv, "Lv." + lv, C_White, 1.0f);
        // ฝนคอนเฟตติจากบนจอ
        for (int i = 0; i < 50; i++)
        {
            Vector2 s = new Vector2(UnityEngine.Random.Range(0f, Screen.width), Screen.height + 30f * U);
            Color col = Confetti[UnityEngine.Random.Range(0, Confetti.Length)];
            P q = Spawn(sprSquare, s, new Vector2(UnityEngine.Random.Range(-100f, 100f), -UnityEngine.Random.Range(200f, 500f)) * U, 3.5f, UnityEngine.Random.Range(18f, 28f), 18f, col, col);
            if (q != null) { q.aspect = 0.55f; q.spin = UnityEngine.Random.Range(-600f, 600f); q.wobbleAmp = 60f; q.wobbleFreq = 3f; q.gravity = 120f; }
            if (i % 5 == 0) yield return null;
        }
    }

    // ---------- 🍃 ใบไม้ปลิวผ่านจอตอนเข้าสวน ----------
    private IEnumerator LeafSweep(float delay)
    {
        yield return Wait(delay);
        for (int i = 0; i < 14; i++)
        {
            Vector2 s = new Vector2(-40f * U, UnityEngine.Random.Range(Screen.height * 0.2f, Screen.height * 0.95f));
            Color col = Color.Lerp(C_Green, C_Yellow, UnityEngine.Random.Range(0f, 0.4f));
            P q = Spawn(sprLeaf, s, new Vector2(UnityEngine.Random.Range(700f, 1100f), UnityEngine.Random.Range(-150f, 80f)) * U, 2.2f, UnityEngine.Random.Range(40f, 64f), 40f,
                        col, new Color(col.r, col.g, col.b, 0f));
            if (q != null) { q.spin = UnityEngine.Random.Range(-300f, 300f); q.aspect = 0.6f; q.wobbleAmp = 70f; q.wobbleFreq = 2.5f; q.fadeIn = 0.1f; q.c0.a = 0.9f; }
            yield return Wait(0.06f);
        }
    }

    // ---------- 🌼 ละอองเกสร/หิ่งห้อยลอยในสวน (บรรยากาศ) ----------
    private void Ambient(float dt)
    {
        bool garden = sceneName == "MainForest" || sceneName == "FriendVisit" || sceneName == "MainMenu";
        if (!garden) return;
        // ==================== 🌧️ ฝน/พายุ/หมอก ไม่ปล่อยละอองเกสร (GrowGoWeather ทำเอฟเฟกต์แทน) ====================
        if (GrowGoWeather.GloomyNow()) return;

        ambientTimer -= dt;
        if (ambientTimer <= 0f && ambientCount < 22)
        {
            ambientTimer = UnityEngine.Random.Range(0.35f, 0.7f);
            Vector2 s = new Vector2(UnityEngine.Random.Range(0f, Screen.width), UnityEngine.Random.Range(-20f, Screen.height * 0.75f));
            Color col = UnityEngine.Random.value < 0.6f ? new Color(1f, 0.95f, 0.65f, 0.6f) : new Color(0.8f, 1f, 0.7f, 0.55f);
            P q = Spawn(sprGlow, s, new Vector2(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(30f, 80f)) * U, UnityEngine.Random.Range(5f, 9f),
                        UnityEngine.Random.Range(14f, 30f), UnityEngine.Random.Range(14f, 30f), col, new Color(col.r, col.g, col.b, 0f));
            if (q != null) { q.ambient = true; q.fadeIn = 0.25f; q.wobbleAmp = 40f; q.wobbleFreq = UnityEngine.Random.Range(0.6f, 1.4f); ambientCount++; }
        }

        leafTimer -= dt;
        if (leafTimer <= 0f && sceneName != "MainMenu")
        {
            leafTimer = UnityEngine.Random.Range(2.5f, 5f);
            Vector2 s = new Vector2(UnityEngine.Random.Range(0f, Screen.width), Screen.height + 40f * U);
            Color col = Color.Lerp(C_Green, C_Orange, UnityEngine.Random.Range(0f, 0.5f));
            col.a = 0.75f;
            P q = Spawn(sprLeaf, s, new Vector2(UnityEngine.Random.Range(-40f, 40f), -UnityEngine.Random.Range(90f, 150f)) * U, 12f, UnityEngine.Random.Range(36f, 52f), 36f,
                        col, new Color(col.r, col.g, col.b, 0f));
            if (q != null) { q.ambient = true; q.aspect = 0.6f; q.spin = UnityEngine.Random.Range(-90f, 90f); q.wobbleAmp = 120f; q.wobbleFreq = 0.5f; q.fadeIn = 0.05f; ambientCount++; }
        }
    }

    // =====================================================================================
    //  🧩 ตัวช่วยสร้างเอฟเฟกต์
    // =====================================================================================
    private void Sparkles(Vector2 p, int n, Color tint, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(speed * 0.4f, speed) * U;
            Color c = UnityEngine.Random.value < 0.5f ? tint : C_White;
            P q = Spawn(sprStar, p, v, UnityEngine.Random.Range(0.5f, 1.0f), UnityEngine.Random.Range(30f, 56f), 0f, c, c);
            if (q != null) { q.drag = 3f; q.spin = UnityEngine.Random.Range(-400f, 400f); }
        }
    }

    private void Petals(Vector2 p, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f + 0.6f) * UnityEngine.Random.Range(300f, 800f) * U;
            Color c = UnityEngine.Random.value < 0.5f ? C_Pink : new Color(1f, 0.8f, 0.9f);
            P q = Spawn(sprFlower, p, v, UnityEngine.Random.Range(1.3f, 2f), UnityEngine.Random.Range(36f, 56f), 30f, c, new Color(c.r, c.g, c.b, 0f));
            if (q != null) { q.gravity = 500f; q.drag = 1.5f; q.spin = UnityEngine.Random.Range(-300f, 300f); q.wobbleAmp = 40f; q.wobbleFreq = 3f; }
        }
    }

    private void ConfettiBurst(Vector2 p, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            float a = UnityEngine.Random.Range(0.1f, Mathf.PI - 0.1f);
            if (UnityEngine.Random.value < 0.25f) a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(speed * 0.35f, speed) * U;
            Color c = Confetti[UnityEngine.Random.Range(0, Confetti.Length)];
            P q = Spawn(sprSquare, p, v, UnityEngine.Random.Range(1.6f, 2.6f), UnityEngine.Random.Range(18f, 30f), 18f, c, c);
            if (q != null) { q.aspect = 0.55f; q.gravity = 1300f; q.drag = 2.2f; q.spin = UnityEngine.Random.Range(-720f, 720f); q.wobbleAmp = 40f; q.wobbleFreq = 4f; }
        }
    }

    // ปืนคอนเฟตติ 2 ข้างล่างจอ
    private void ConfettiCannons(int perSide)
    {
        for (int side = 0; side < 2; side++)
        {
            Vector2 o = new Vector2(side == 0 ? -10f : Screen.width + 10f, Screen.height * 0.08f);
            float dir = side == 0 ? 1f : -1f;
            for (int i = 0; i < perSide; i++)
            {
                float a = UnityEngine.Random.Range(55f, 80f) * Mathf.Deg2Rad;
                Vector2 v = new Vector2(Mathf.Cos(a) * dir, Mathf.Sin(a)) * UnityEngine.Random.Range(1300f, 2300f) * U;
                Color c = Confetti[UnityEngine.Random.Range(0, Confetti.Length)];
                Sprite s = UnityEngine.Random.value < 0.8f ? sprSquare : sprStar;
                P q = Spawn(s, o, v, UnityEngine.Random.Range(2.2f, 3.2f), UnityEngine.Random.Range(20f, 32f), 20f, c, c);
                if (q != null) { q.aspect = s == sprSquare ? 0.55f : 1f; q.gravity = 1500f; q.drag = 1.8f; q.spin = UnityEngine.Random.Range(-720f, 720f); q.wobbleAmp = 50f; q.wobbleFreq = 4f; }
            }
        }
    }

    // ไอคอนพุ่งขึ้นเป็นน้ำพุเล็กๆ (+น้ำ +EXP +ดอกไม้ +หัวใจ)
    private void Fountain(Sprite s, Vector2 p, int n, Color c, float scale)
    {
        for (int i = 0; i < n; i++)
        {
            Vector2 v = new Vector2(UnityEngine.Random.Range(-320f, 320f), UnityEngine.Random.Range(700f, 1100f)) * U;
            P q = Spawn(s, p + new Vector2(0, 80f * U), v, UnityEngine.Random.Range(1.0f, 1.4f), 46f * scale, 30f * scale, c, new Color(c.r, c.g, c.b, 0f));
            if (q != null) { q.gravity = 1500f; q.drag = 0.8f; q.spin = UnityEngine.Random.Range(-90f, 90f); }
        }
    }

    // =====================================================================================
    //  📝 ข้อความใหญ่เด้งกลางจอ
    // =====================================================================================
    private TMP_FontAsset thaiFont;

    private TMP_FontAsset FindFont(bool needThai)
    {
        if (needThai)
        {
            if (thaiFont != null) return thaiFont;
            TextMeshProUGUI[] all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            for (int i = 0; i < all.Length; i++)
            {
                TMP_FontAsset f = all[i] != null ? all[i].font : null;
                if (f == null) continue;
                bool has = false;
                try { has = f.HasCharacter('ก', true); } catch { }
                if (has) { thaiFont = f; return f; }
            }
            return null;
        }
        return TMP_Settings.defaultFontAsset;
    }

    private void BigText(Transform t, string thai, string eng, Color col, float scale)
    {
        if (t == null) return;
        BigTextAt(ScreenPos(t) + new Vector2(0, 320f * U), thai, eng, col, scale);
    }

    private void BigTextAt(Vector2 p, string thai, string eng, Color col, float scale)
    {
        try
        {
            TMP_FontAsset f = FindFont(true);
            string txt = thai;
            if (f == null) { f = FindFont(false); txt = eng; }
            if (f == null) return;

            GameObject go = new GameObject("JuiceText", typeof(RectTransform));
            go.transform.SetParent(fxRoot, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = f;
            tmp.text = txt;
            tmp.fontSize = 110f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.color = Color.white;
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(Color.white, Color.white, col, col);
            try { tmp.outlineWidth = 0.22f; tmp.outlineColor = new Color32(70, 45, 20, 255); } catch { }
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1400f, 260f);
            p.x = Mathf.Clamp(p.x, Screen.width * 0.3f, Screen.width * 0.7f);
            p.y = Mathf.Clamp(p.y, Screen.height * 0.15f, Screen.height * 0.85f);
            rt.position = p;
            StartCoroutine(BigTextRoutine(rt, tmp, scale * U));
        }
        catch (Exception e) { Debug.LogWarning("[GrowGoJuice] text: " + e.Message); }
    }

    private IEnumerator BigTextRoutine(RectTransform rt, TextMeshProUGUI tmp, float s)
    {
        float e = 0f, total = 1.6f;
        Vector3 start = rt.position;
        while (e < total && rt != null)
        {
            float k;
            if (e < 0.25f) { float x = e / 0.25f; k = BackOut(x) ; }
            else k = 1f + Mathf.Sin((e - 0.25f) * 6f) * 0.03f;
            rt.localScale = Vector3.one * s * k;
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(e * 5f) * 3f);
            rt.position = start + new Vector3(0, e * 60f * U, 0);
            float a = e < total - 0.4f ? 1f : Mathf.Clamp01((total - e) / 0.4f);
            tmp.alpha = a;
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private static float BackOut(float x)
    {
        const float c1 = 2.2f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // =====================================================================================
    //  ⚡ แฟลชจอ + จอสั่น
    // =====================================================================================
    private Coroutine flashCo;
    private void Flash(Color c, float alpha, float dur)
    {
        if (flashImage == null) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine(c, alpha, dur));
    }

    private IEnumerator FlashRoutine(Color c, float alpha, float dur)
    {
        flashImage.enabled = true;
        float e = 0f;
        while (e < dur)
        {
            float a = alpha * (1f - e / dur);
            flashImage.color = new Color(c.r, c.g, c.b, a * a / Mathf.Max(alpha, 0.01f));
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        flashImage.color = new Color(1, 1, 1, 0);
        flashImage.enabled = false;
        flashCo = null;
    }

    private bool shaking;
    private void Shake(float dur, float strength)
    {
        if (shaking) return;
        StartCoroutine(ShakeRoutine(dur, strength));
    }

    // สั่น = เลื่อนลูกชั้นแรกของ Canvas ทุกอัน (ไม่ใช่ของเรา) ไปมาเล็กน้อย แล้วคืนตำแหน่งเดิมแบบหักลบ (ไม่ทับค่าที่สคริปต์อื่นตั้ง)
    private IEnumerator ShakeRoutine(float dur, float strength)
    {
        shaking = true;
        List<RectTransform> targets = new List<RectTransform>();
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas cv = canvases[i];
            if (cv == null || cv == fxCanvas || !cv.isRootCanvas || !cv.gameObject.activeInHierarchy) continue;
            if (!cv.gameObject.scene.IsValid() || !cv.gameObject.scene.isLoaded) continue;
            foreach (Transform ch in cv.transform)
            {
                RectTransform r = ch as RectTransform;
                if (r != null && r.gameObject.activeInHierarchy) targets.Add(r);
            }
        }
        Camera cam = Camera.main;
        Vector3 camLast = Vector3.zero;
        Vector2 last = Vector2.zero;
        float e = 0f;
        while (e < dur)
        {
            float k = 1f - e / dur;
            Vector2 off = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * strength * U * k;
            for (int i = 0; i < targets.Count; i++) if (targets[i] != null) targets[i].anchoredPosition += off - last;
            if (cam != null)
            {
                Vector3 co = new Vector3(off.x, off.y, 0) * 0.004f;
                cam.transform.localPosition += co - camLast;
                camLast = co;
            }
            last = off;
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        for (int i = 0; i < targets.Count; i++) if (targets[i] != null) targets[i].anchoredPosition -= last;
        if (cam != null) cam.transform.localPosition -= camLast;
        shaking = false;
    }

    private void Kick(Transform t, float amount)
    {
        if (t == null) return;
        GrowGoJuiceScale js = t.GetComponent<GrowGoJuiceScale>();
        if (js != null) js.Kick(amount);
    }

    // =====================================================================================
    //  📍 ตำแหน่งบนจอ
    // =====================================================================================
    private Vector2 ScreenPos(Transform t)
    {
        if (t == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Canvas c = t.GetComponentInParent<Canvas>();
        if (c != null)
        {
            Canvas root = c.rootCanvas;
            Camera cam = (root.renderMode == RenderMode.ScreenSpaceOverlay) ? null : root.worldCamera;
            Vector3 world = t.position;
            RectTransform rt = t as RectTransform;
            if (rt != null)
            {
                // ใช้จุดกึ่งกลางของกรอบ (เผื่อ pivot ไม่อยู่ตรงกลาง)
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                world = (corners[0] + corners[2]) * 0.5f;
            }
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }
        Camera mc = Camera.main;
        if (mc != null) return mc.WorldToScreenPoint(t.position);
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private bool IsOver(RectTransform rt, Vector2 screen)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;
        Canvas c = rt.GetComponentInParent<Canvas>();
        Camera cam = (c == null || c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : c.rootCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screen, cam);
    }

    // ==================== 🚫 มีป๊อปอัป/ปุ่มทับต้นไม้ตรงจุดที่แตะ -> ไม่นับว่าแตะต้น ====================
    // ยิง raycast ของ UI ดูว่าอะไรอยู่บนสุด ถ้าเจอของที่วาด "ทับ" ต้นไม้ก่อนเจอต้นไม้ = โดนบัง
    // (พื้นหลังที่อยู่ใต้ต้นไม้ไม่นับ)
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private bool CoveredByUI(Transform plant, Vector2 screen)
    {
        EventSystem es = EventSystem.current;
        if (es == null || plant == null) return false;
        PointerEventData ped = new PointerEventData(es) { position = screen };
        uiHits.Clear();
        es.RaycastAll(ped, uiHits);
        for (int i = 0; i < uiHits.Count; i++)
        {
            GameObject g = uiHits[i].gameObject;
            if (g == null) continue;
            Transform t = g.transform;
            if (t == plant || t.IsChildOf(plant)) return false;   // เจอต้นไม้ก่อน = ไม่โดนบัง
            if (plant.IsChildOf(t)) continue;                     // พ่อของต้นไม้ (เช่น พื้นที่กดของสวน) ไม่นับ
            if (DrawnAbove(t, plant)) return true;               // ของที่วาดทับต้นไม้ = ป๊อปอัป/ปุ่ม
        }
        return false;
    }

    // a วาดทับ b มั้ย (ต่าง Canvas ดู sortingOrder, Canvas เดียวกันดูลำดับใน Hierarchy)
    private static bool DrawnAbove(Transform a, Transform b)
    {
        Canvas ca = a.GetComponentInParent<Canvas>(), cb = b.GetComponentInParent<Canvas>();
        if (ca == null || cb == null) return false;
        Canvas ra = ca.rootCanvas, rb = cb.rootCanvas;
        if (ra != rb)
        {
            bool oa = ra.renderMode == RenderMode.ScreenSpaceOverlay, ob = rb.renderMode == RenderMode.ScreenSpaceOverlay;
            if (oa != ob) return oa;                              // Overlay วาดทับ Camera/World เสมอ
            return ra.sortingOrder > rb.sortingOrder;
        }
        if (ca != cb && ca.overrideSorting && ca.sortingOrder != cb.sortingOrder) return ca.sortingOrder > cb.sortingOrder;
        return HierarchyOrder(a, ra.transform).CompareTo(HierarchyOrder(b, ra.transform)) > 0;
    }

    // ลำดับการวาดใน Canvas เดียวกัน: ไล่ sibling index จากราก (ตัวที่อยู่ล่างใน Hierarchy = วาดทีหลัง = อยู่บน)
    private static PathKey HierarchyOrder(Transform t, Transform root)
    {
        List<int> path = new List<int>();
        while (t != null && t != root) { path.Add(t.GetSiblingIndex()); t = t.parent; }
        path.Reverse();
        return new PathKey(path);
    }

    private struct PathKey : IComparable<PathKey>
    {
        private readonly List<int> p;
        public PathKey(List<int> path) { p = path; }
        public int CompareTo(PathKey o)
        {
            int n = Mathf.Min(p.Count, o.p.Count);
            for (int i = 0; i < n; i++) if (p[i] != o.p[i]) return p[i].CompareTo(o.p[i]);
            return p.Count.CompareTo(o.p.Count);                  // ลูกวาดทีหลังพ่อ
        }
    }

    private static WaitForSecondsRealtime Wait(float s) { return new WaitForSecondsRealtime(s); }

    // =====================================================================================
    //  ⚙️ ระบบ particle
    // =====================================================================================
    private P Spawn(Sprite s, Vector2 pos, Vector2 vel, float life, float size0, float size1, Color c0, Color c1)
    {
        if (!Enabled || fxRoot == null || s == null) return null;
        P q = null;
        for (int i = 0; i < pool.Count; i++) if (!pool[i].alive) { q = pool[i]; break; }
        if (q == null)
        {
            if (pool.Count >= MaxParticles) return null;   // เต็มแล้ว ข้ามไป (กันเครื่องหน่วง)
            GameObject go = new GameObject("p", typeof(RectTransform));
            go.transform.SetParent(fxRoot, false);
            q = new P();
            q.rt = go.GetComponent<RectTransform>();
            q.img = go.AddComponent<Image>();
            q.img.raycastTarget = false;
            pool.Add(q);
        }
        q.alive = true; q.ambient = false;
        q.img.sprite = s;
        q.img.enabled = true;
        q.pos = pos; q.vel = vel;
        q.life = q.maxLife = Mathf.Max(0.01f, life);
        q.size0 = size0; q.size1 = size1; q.aspect = 1f;
        q.c0 = c0; q.c1 = c1;
        q.gravity = 0f; q.drag = 0f; q.rot = UnityEngine.Random.Range(0f, 360f); q.spin = 0f;
        q.wobbleAmp = 0f; q.wobbleFreq = 0f; q.seed = UnityEngine.Random.Range(0f, 100f);
        q.fadeIn = 0f;
        if (s == sprRays || s == sprRing || s == sprGlow) q.rot = 0f;
        if (s == sprDrop || s == sprHeart) q.rot = 0f;
        q.rt.SetAsLastSibling();
        ApplyP(q, 0f);
        return q;
    }

    private void Kill(P q)
    {
        if (q == null) return;
        q.alive = false;
        if (q.img != null) q.img.enabled = false;
    }

    private void StepParticles(float dt)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            P q = pool[i];
            if (!q.alive) continue;
            q.life -= dt;
            if (q.life <= 0f)
            {
                if (q.ambient) ambientCount = Mathf.Max(0, ambientCount - 1);
                Kill(q);
                continue;
            }
            q.vel.y -= q.gravity * U * dt;
            if (q.drag > 0f) q.vel /= (1f + q.drag * dt);
            q.pos += q.vel * dt;
            q.rot += q.spin * dt;
            ApplyP(q, 1f - q.life / q.maxLife);
        }
    }

    private void ApplyP(P q, float t)
    {
        float size = Mathf.Lerp(q.size0, q.size1, t) * U;
        float wob = q.wobbleAmp > 0f ? Mathf.Sin((Time.unscaledTime + q.seed) * q.wobbleFreq * 2f * Mathf.PI) * q.wobbleAmp * U : 0f;
        q.rt.position = new Vector3(q.pos.x + wob, q.pos.y, 0f);
        // กระดาษคอนเฟตติพลิกไปมา (ย่อแกน x ตามเวลา)
        float flip = q.aspect < 1f ? Mathf.Abs(Mathf.Cos((Time.unscaledTime + q.seed) * 7f)) * 0.8f + 0.2f : 1f;
        q.rt.sizeDelta = new Vector2(size * q.aspect * flip, size);
        q.rt.localRotation = Quaternion.Euler(0, 0, q.rot);
        Color c = Color.Lerp(q.c0, q.c1, t);
        if (q.fadeIn > 0f)
        {
            float age = q.maxLife - q.life;
            if (age < q.fadeIn * q.maxLife) c.a *= age / (q.fadeIn * q.maxLife);
        }
        q.img.color = c;
    }
}
