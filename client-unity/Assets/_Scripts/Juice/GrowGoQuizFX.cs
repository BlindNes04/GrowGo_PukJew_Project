using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================================
//  GrowGoQuizFX : เอฟเฟกต์หน้า Quiz (สร้างตัวเอง ไม่ต้องลากใส่ฉาก, ทำงานเฉพาะฉาก "Quiz")
//  1) กดเลือกคำตอบ (ButtonA / ButtonB) = เสียง "ป๊อป-กริ๊ง" โทนสูงขึ้นทีละข้อ + ประกาย
//  2) หน้าผลคู่หู (BuddyCanvas) = แสงรัศมีหมุน + ตัวคู่หูเด้ง + คอนเฟตติ + แฟนแฟร์
//     ชื่อ/สกิลเด้งตอนโผล่ + ปุ่มถัดไปเด้งเรียกให้กด
//  ไม่แก้สคริปต์ของเกม: อ่านค่าจาก QuizManager / BuddyManager ด้วย reflection แล้วดู alpha ที่เกมเฟดเอง
// =====================================================================================
public class GrowGoQuizFX : MonoBehaviour
{
    private static GrowGoQuizFX I;

    // ==================== 🚀 สร้างตัวเองตอนเกมเริ่ม ====================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (I != null) return;
        GameObject go = new GameObject("[GrowGoQuizFX]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<GrowGoQuizFX>();
    }

    public static bool DebugLog = false;   // true = พิมพ์ log ตอนเทส
    private static void Log(string m) { if (DebugLog) Debug.Log("[GrowGoQuizFX] " + m); }

    private const int SR = 44100;
    private static float Dt { get { return Mathf.Min(Time.unscaledDeltaTime, 0.05f); } }   // กันเฟรมกระตุกแล้วแอนิเมชันข้าม
    private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    // ==================== 🧱 ของในฉาก ====================
    private bool inQuiz;
    private float scanTimer;
    private QuizManager quiz;
    private BuddyManager buddy;
    private FieldInfo fCurrentQuestion;

    private GameObject buddyCanvas;
    private RectTransform titleRT, imageRT, nameRT, skillRT;
    private Graphic titleG, imageG, nameG, skillG;
    private CanvasGroup nextCg;
    private GrowGoJuiceScale nextScale;
    private RectTransform nextRT;

    // ลำดับการโผล่ของหน้าคู่หู: 0 รอเปิด, 1 รอหัวข้อ, 2 รอรูป, 3 รอชื่อ, 4 รอสกิล, 5 รอปุ่ม, 6 จบ
    private int stage;
    private bool wasOpen = true;   // เริ่มเป็น true: ถ้าเปิดค้างมาตั้งแต่ต้นฉาก ไม่นับ
    private Vector3 imageBase = Vector3.one;
    private bool imageIdle;
    private float idleT, beckonTimer;

    private RectTransform raysRT, glowRT;
    private Image raysImg, glowImg;
    private float raysAlpha, raysSpin;

    // ==================== 🔊 เสียง ====================
    private AudioSource[] src;
    private int srcIndex;
    private AudioClip clipPick;
    private Sprite sprRays, sprGlow;

    // =====================================================================================
    //  เริ่มต้น
    // =====================================================================================
    private void Awake()
    {
        try
        {
            src = new AudioSource[3];
            for (int i = 0; i < src.Length; i++)
            {
                src[i] = gameObject.AddComponent<AudioSource>();
                src[i].playOnAwake = false;
                src[i].spatialBlend = 0f;
            }
            BuildClips();
            BuildSprites();
        }
        catch (Exception e) { Debug.LogWarning("[GrowGoQuizFX] init error: " + e.Message); }

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        inQuiz = SceneManager.GetActiveScene().name == "Quiz";
        scanTimer = 0f;
        quiz = null; buddy = null; buddyCanvas = null;
        titleRT = imageRT = nameRT = skillRT = nextRT = null;
        titleG = imageG = nameG = skillG = null;
        nextCg = null; nextScale = null;
        raysRT = glowRT = null; raysImg = glowImg = null;
        stage = 0; imageIdle = false; raysAlpha = 0f; wasOpen = true;
    }

    // =====================================================================================
    //  🔎 หา QuizManager / BuddyManager แล้วติดตัวรับการกดที่ปุ่มคำตอบ
    // =====================================================================================
    private void Scan()
    {
        if (quiz == null)
        {
            quiz = FindAnyObjectByType<QuizManager>(FindObjectsInactive.Include);
            if (quiz != null)
            {
                fCurrentQuestion = typeof(QuizManager).GetField("currentQuestion", BF);
                AttachAnswer(GetField<Button>(quiz, "buttonA"));
                AttachAnswer(GetField<Button>(quiz, "buttonB"));
            }
        }

        if (buddy == null)
        {
            buddy = FindAnyObjectByType<BuddyManager>(FindObjectsInactive.Include);
            if (buddy != null)
            {
                buddyCanvas = GetField<GameObject>(buddy, "buddyCanvas");
                Log("found buddy, canvas=" + (buddyCanvas ? buddyCanvas.name : "null"));
                titleG = GetField<Graphic>(buddy, "titleText");
                imageG = GetField<Graphic>(buddy, "resultCharacterImage");
                nameG = GetField<Graphic>(buddy, "resultBuddyNameText");
                skillG = GetField<Graphic>(buddy, "resultSkillNameText");
                Button nb = GetField<Button>(buddy, "nextButton");
                if (nb != null) nextRT = nb.transform as RectTransform;
                if (titleG != null) titleRT = titleG.rectTransform;
                if (imageG != null) imageRT = imageG.rectTransform;
                if (nameG != null) nameRT = nameG.rectTransform;
                if (skillG != null) skillRT = skillG.rectTransform;
            }
        }
    }

    private static T GetField<T>(object o, string name) where T : class
    {
        if (o == null) return null;
        FieldInfo f = o.GetType().GetField(name, BF);
        if (f == null) return null;
        object v = f.GetValue(o);
        if (v is T t) return t;
        if (v is Component c && typeof(T) == typeof(GameObject)) return c.gameObject as T;
        return null;
    }

    private void AttachAnswer(Button b)
    {
        if (b == null) return;
        GrowGoQuizAnswerTap tap = b.GetComponent<GrowGoQuizAnswerTap>();
        if (tap == null) tap = b.gameObject.AddComponent<GrowGoQuizAnswerTap>();
        tap.fx = this;
    }

    // ---------- กดเลือกคำตอบ ----------
    public void OnAnswer(Button b, Vector2 screenPos)
    {
        if (b == null || !b.IsInteractable()) return;
        int q = 0;
        try { if (fCurrentQuestion != null && quiz != null) q = (int)fCurrentQuestion.GetValue(quiz); } catch { }
        float pitch = 1f + Mathf.Min(q, 14) * 0.025f;        // ยิ่งตอบไปเยอะ ยิ่งเสียงสูงขึ้นนิดๆ
        Play(clipPick, 0.7f, pitch);

        GrowGoJuice j = GrowGoJuice.I;
        if (j != null)
        {
            Vector2 p = j.ScreenPosOf(b.transform);
            j.FxSparkles(p, 10, new Color(1f, 0.88f, 0.4f), 650f);
        }
        GrowGoJuiceScale s = b.GetComponent<GrowGoJuiceScale>();
        if (s != null) s.Kick(0.18f);
    }

    // =====================================================================================
    //  🔁 Update: เฝ้าดูหน้าคู่หู
    // =====================================================================================
    private void Update()
    {
        if (!inQuiz) return;
        try
        {
            scanTimer -= Dt;
            if (scanTimer <= 0f && (quiz == null || buddy == null)) { scanTimer = 0.5f; Scan(); }
            WatchBuddy();
        }
        catch (Exception e) { Debug.LogWarning("[GrowGoQuizFX] " + e.Message); }
    }

    private static bool Shown(Graphic g) { return g != null && g.color.a > 0.02f; }

    private void WatchBuddy()
    {
        if (buddy == null || buddyCanvas == null) return;
        bool open = buddyCanvas.activeInHierarchy;

        if (stage == 0)
        {
            // เริ่มนับตอน BuddyCanvas เพิ่งถูกเปิด (ปิด -> เปิด) และหัวข้อยังเฟดไม่เสร็จ
            // (เกมเริ่มเฟดทันทีในเฟรมที่กด เลยเช็ค alpha == 0 ไม่ได้)
            if (open && !wasOpen && titleG != null && titleG.color.a < 0.95f) { stage = 1; Log("armed"); }
            wasOpen = open;
            return;
        }
        if (!open) return;

        if (stage == 1 && Shown(titleG))
        {
            stage = 2;
            Log("title");
            GrowGoJuice j = GrowGoJuice.I;
            if (j != null) j.PlaySfx("whoosh", 0.35f, 1.2f);
            StartCoroutine(Punch(titleRT, 0.55f, 0.6f));
        }
        else if (stage == 2 && Shown(imageG))
        {
            stage = 3;
            RevealBuddy();
        }
        else if (stage == 3 && Shown(nameG))
        {
            stage = 4;
            GrowGoJuice j = GrowGoJuice.I;
            if (j != null)
            {
                j.PlaySfx("ding", 0.6f, 1f);
                j.FxSparkles(j.ScreenPosOf(nameRT), 14, new Color(1f, 0.56f, 0.69f), 700f);
            }
            StartCoroutine(Punch(nameRT, 0.45f, 0.55f));
        }
        else if (stage == 4 && Shown(skillG))
        {
            stage = 5;
            GrowGoJuice j = GrowGoJuice.I;
            if (j != null) j.PlaySfx("pop", 0.5f, 1.15f);
            StartCoroutine(Punch(skillRT, 0.7f, 0.45f));
        }
        else if (stage == 5)
        {
            if (nextCg == null && buddy != null) nextCg = buddy.GetType().GetField("nextButtonCanvasGroup", BF)?.GetValue(buddy) as CanvasGroup;
            if (nextCg != null && nextCg.alpha > 0.02f)
            {
                stage = 6;
                GrowGoJuice j = GrowGoJuice.I;
                if (j != null) j.PlaySfx("pop", 0.55f, 1.3f);
                beckonTimer = 1.2f;
            }
        }
        else if (stage == 6)
        {
            // ปุ่มถัดไปเด้งเรียกเบาๆ ทุก 2.2 วิ
            beckonTimer -= Dt;
            if (beckonTimer <= 0f)
            {
                beckonTimer = 2.2f;
                if (nextScale == null && nextRT != null) nextScale = nextRT.GetComponent<GrowGoJuiceScale>();
                if (nextScale != null) nextScale.Kick(0.12f);
            }
        }
    }

    // ---------- ✨ เปิดตัวคู่หู ----------
    private void RevealBuddy()
    {
        if (imageRT == null) return;
        imageBase = imageRT.localScale;
        BuildRays();
        Log("reveal buddy, rect=" + imageRT.rect + " scale=" + imageBase + " parent=" + imageRT.parent.name);

        GrowGoJuice j = GrowGoJuice.I;
        if (j != null)
        {
            j.PlaySfx("fanfare", 0.75f, 1f);
            j.PlaySfx("sparkle", 0.5f, 1f);
            j.FxFlash(Color.white, 0.45f, 0.45f);
            Vector2 p = j.ScreenPosOf(imageRT);
            j.FxConfetti(p, 46, 1700f);
            j.FxSparkles(p, 26, new Color(1f, 0.88f, 0.4f), 900f);
            j.FxCannons(22);
        }
        StartCoroutine(BuddyPop());
    }

    private IEnumerator BuddyPop()
    {
        float e = 0f, dur = 0.95f;
        imageIdle = false;
        while (e < dur && imageRT != null)
        {
            float x = e / dur;
            float k = Mathf.LerpUnclamped(0.25f, 1f, ElasticOut(x));
            float sq = 1f + Mathf.Sin(x * Mathf.PI * 3f) * 0.08f * (1f - x);   // ยืด-แบน นิดๆ
            imageRT.localScale = new Vector3(imageBase.x * k / sq, imageBase.y * k * sq, imageBase.z);
            e += Dt;
            yield return null;
        }
        if (imageRT != null) imageRT.localScale = imageBase;
        idleT = 0f;
        imageIdle = true;
    }

    private void LateUpdate()
    {
        if (!inQuiz || imageRT == null) return;
        bool open = buddyCanvas != null && buddyCanvas.activeInHierarchy;

        // หายใจเบาๆ หลังเด้งเสร็จ
        if (imageIdle && open)
        {
            idleT += Dt;
            float b = 1f + Mathf.Sin(idleT * 2.4f) * 0.025f;
            imageRT.localScale = new Vector3(imageBase.x / Mathf.Sqrt(b), imageBase.y * b, imageBase.z);
        }

        // แสงรัศมีหลังตัวคู่หู: ตามตำแหน่งรูป + หมุนช้าๆ
        if (raysRT != null && open)
        {
            raysAlpha = Mathf.MoveTowards(raysAlpha, 1f, Dt * 2.2f);
            raysSpin += Dt * 16f;
            Vector3 c = imageRT.TransformPoint(imageRT.rect.center);
            float size = Mathf.Max(imageRT.rect.width * imageBase.x, imageRT.rect.height * imageBase.y);
            float grow = Mathf.LerpUnclamped(0.3f, 1f, BackOut(raysAlpha));
            raysRT.position = c;
            raysRT.sizeDelta = Vector2.one * size * 1.75f * grow;
            raysRT.localRotation = Quaternion.Euler(0, 0, raysSpin);
            raysImg.color = new Color(1f, 0.82f, 0.35f, 0.75f * raysAlpha);
            if (glowRT != null)
            {
                float pulse = 1f + Mathf.Sin(raysSpin * 0.25f) * 0.06f;
                glowRT.position = c;
                glowRT.sizeDelta = Vector2.one * size * 1.25f * grow * pulse;
                glowImg.color = new Color(1f, 0.97f, 0.8f, 0.7f * raysAlpha);
            }
        }
    }

    private void BuildRays()
    {
        if (raysRT != null || imageRT == null || imageRT.parent == null) return;
        int idx = imageRT.GetSiblingIndex();
        glowRT = MakeBehind("BuddyGlow", sprGlow, out glowImg);
        raysRT = MakeBehind("BuddyRays", sprRays, out raysImg);
        // ลำดับ: รัศมี -> แสงนุ่ม -> รูปคู่หู
        raysRT.SetSiblingIndex(idx);
        glowRT.SetSiblingIndex(idx + 1);
        raysAlpha = 0f;
    }

    private RectTransform MakeBehind(string n, Sprite s, out Image img)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(imageRT.parent, false);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        img = go.AddComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        img.color = new Color(1, 1, 1, 0);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    // ---------- เด้งตัวหนังสือ (ขยายแล้วสั่นกลับ) ----------
    private IEnumerator Punch(RectTransform rt, float from, float dur)
    {
        if (rt == null) yield break;
        Vector3 b = rt.localScale;
        float e = 0f;
        while (e < dur && rt != null)
        {
            float k = Mathf.LerpUnclamped(from, 1f, ElasticOut(e / dur));
            rt.localScale = b * k;
            e += Dt;
            yield return null;
        }
        if (rt != null) rt.localScale = b;
    }

    private static float ElasticOut(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        const float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * c4) + 1f;
    }

    private static float BackOut(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

    // =====================================================================================
    //  🖼️ รูปรัศมี + แสงนุ่ม (สร้างด้วยโค้ด)
    // =====================================================================================
    private void BuildSprites()
    {
        const int N = 256;
        Texture2D rays = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Texture2D glow = new Texture2D(N, N, TextureFormat.RGBA32, false);
        rays.wrapMode = glow.wrapMode = TextureWrapMode.Clamp;
        Color32[] pr = new Color32[N * N], pg = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx);
                // 12 แฉก ขอบนุ่ม
                float ray = Mathf.Clamp01((Mathf.Cos(a * 12f) - 0.15f) * 2.2f);
                float fall = Mathf.Clamp01(1f - r);
                fall = fall * fall * Mathf.Clamp01(r * 5f);
                float ar = Mathf.Clamp01(ray * fall);
                float ag = Mathf.Exp(-r * r * 5f) * Mathf.Clamp01((1f - r) * 6f);
                pr[y * N + x] = new Color32(255, 255, 255, (byte)(ar * 255f));
                pg[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ag) * 255f));
            }
        }
        rays.SetPixels32(pr); rays.Apply();
        glow.SetPixels32(pg); glow.Apply();
        sprRays = Sprite.Create(rays, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        sprGlow = Sprite.Create(glow, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    // =====================================================================================
    //  🔊 เสียงเลือกคำตอบ: "บลู้ป" ขึ้น + กริ๊ง 2 โน้ต (E6 -> B6)
    // =====================================================================================
    private void BuildClips()
    {
        clipPick = Synth("quiz_pick", 0.5f, t =>
        {
            float s = 0f;
            if (t < 0.12f)
            {
                float f = Mathf.Lerp(480f, 980f, Mathf.Clamp01(t / 0.07f));
                s += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 32f) * Mathf.Min(1f, t * 500f);
            }
            s += Bell(t, 1318.5f, 0.035f, 9f) * 0.8f + Bell(t, 1975.5f, 0.09f, 8f) * 0.65f;
            return s;
        });
    }

    private static float Bell(float t, float f, float start, float decay)
    {
        t -= start;
        if (t < 0f) return 0f;
        float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 300f);
        return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t)) * env * 0.5f;
    }

    private static AudioClip Synth(string name, float dur, Func<float, float> f)
    {
        int n = Mathf.CeilToInt(SR * dur);
        float[] d = new float[n];
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float v = f(i / (float)SR);
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;   // กันเสียงพัง (NaN)
            d[i] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }
        float norm = 0.8f / peak;
        int fade = Mathf.Min(n, 400);
        for (int i = 0; i < n; i++)
        {
            d[i] *= norm;
            if (i > n - fade) d[i] *= (n - i) / (float)fade;
        }
        AudioClip c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    private void Play(AudioClip clip, float vol, float pitch)
    {
        if (clip == null || src == null) return;
        float master = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (master <= 0.001f) return;
        AudioSource a = src[srcIndex];
        srcIndex = (srcIndex + 1) % src.Length;
        a.pitch = pitch;
        a.PlayOneShot(clip, vol * master);
    }
}

// =====================================================================================
//  GrowGoQuizAnswerTap : ติดที่ปุ่มคำตอบ รับการกดเอง (ไม่โดน RemoveAllListeners ของ QuizManager)
// =====================================================================================
public class GrowGoQuizAnswerTap : MonoBehaviour, IPointerClickHandler
{
    [NonSerialized] public GrowGoQuizFX fx;
    private Button button;

    private void LateUpdate()
    {
        // บอก GrowGoJuicePress ว่าปุ่มนี้มีเสียงของตัวเองแล้ว (ไม่ต้องเล่นเสียงติ๊กซ้อน)
        GrowGoJuicePress p = GetComponent<GrowGoJuicePress>();
        if (p != null && !p.customSound) p.customSound = true;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || fx == null) return;
        if (button == null) button = GetComponent<Button>();
        fx.OnAnswer(button, e.position);
    }
}
