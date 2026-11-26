// =====================================================================================
//  GrowGoLeafTransition.cs  🍃 ทรานซิชัน "ม่านใบไม้" MainMenu/Login -> MainForest (สำหรับ Open House)
// -------------------------------------------------------------------------------------
//  ขาออก (MainMenu): กดเล่น / ล็อกอิน / สมัครเสร็จ -> ใบไม้ปลิวพัดจากซ้ายเข้ามาจนเต็มจอ (มีเสียงใบไม้ไหว)
//                   MainMenuManager รอจนจบก่อนค่อยเปลี่ยนฉาก (ผ่าน hook MainMenuManager.BeforeSceneChange)
//  ขาเข้า (MainForest): ใบไม้ปลิวต่อออกไปทางขวา เผยป่า
//                   -> แถบปุ่มรอบจอเลื่อนเข้ามาจากขอบ -> พืชเด้งโผล่ -> บัดดี้กระโดดลงมา
//  ไป Quiz (ผู้เล่นใหม่) ใช้ม่านใบไม้เหมือนกัน (ขาเข้าแค่ใบไม้ปลิวออก) และ Quiz -> MainForest ก็ใช้ (hook SeedManager.BeforeSceneChange)
//
//  ไม่แย่งอนิเมชันของเกม: ขยับตำแหน่งแบบ "บวกเพิ่ม" ทุกเฟรมแล้วถอดออก (Animator ของบัดดี้ยังเด้งตามปกติ)
//  สร้างตัวเองอัตโนมัติ + อยู่ข้ามฉาก ไม่ต้องลากใส่ฉาก
//  ปิดเอฟเฟกต์นี้: ตั้ง Enabled = false หรือลบไฟล์นี้ทิ้ง (MainMenuManager จะเปลี่ยนฉากทันทีเหมือนเดิม)
// =====================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GrowGoLeafTransition : MonoBehaviour
{
    public static GrowGoLeafTransition I;
    public static bool Enabled = true;

    // ==================== ⚙️ ค่าที่ปรับได้ ====================
    public static int LeafCount = 46;              // จำนวนใบไม้
    public static float CoverTime = 0.85f;          // เวลาใบไม้ปิดจอ (ขาออก)
    public static float RevealTime = 0.75f;         // เวลาใบไม้ปลิวออก (ขาเข้า)
    // แถบ UI ใน MainForest ที่จะเลื่อนเข้ามาจากขอบจอ (ไม่มีชื่อไหนในฉาก = ข้ามไป)
    public static string[] HudNames = { "PlayerStatusView", "TopRightMenuVIew", "BottomBarBG", "BottomBar", "PlantStatusView" };
    public static string PlantName = "MainCharacter", BuddyName = "BuddyCharacter";

    static readonly Color[] LeafColors =
    {
        new Color32(92, 158, 64, 255), new Color32(120, 184, 76, 255), new Color32(74, 133, 56, 255),
        new Color32(150, 199, 89, 255), new Color32(104, 170, 70, 255), new Color32(176, 206, 96, 255),
    };
    static readonly Color Backing = new Color32(84, 146, 60, 255);

    const string MenuScene = "MainMenu", ForestScene = "MainForest";
    const int SR = 44100;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || I != null) return;
        GameObject go = new GameObject("[GrowGoLeafTransition]");
        DontDestroyOnLoad(go);
        I = go.AddComponent<GrowGoLeafTransition>();
    }

    class Leaf { public RectTransform rt; public Vector2 from, to, wobble; public float rot0, rot1, delay, size; }

    Canvas overlay;
    Image backing;
    readonly List<Leaf> leaves = new List<Leaf>();
    bool pendingReveal;
    Sprite leafSprite, dotSprite;
    AudioSource sfx;
    AudioListener bridge;
    AudioClip clipRustle, clipRustleSoft, clipPop, clipLand;

    // ตัวที่กำลังเลื่อนแบบ "บวกเพิ่ม" (ไม่ทับค่าที่เกม/Animator เขียน)
    class Offset { public RectTransform rt; public Vector2 offset, lastOffset, lastWritten; public bool applied; }
    readonly List<Offset> offsets = new List<Offset>();

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.ignoreListenerPause = true;
        leafSprite = MakeLeafSprite(128);
        dotSprite = MakeDotSprite(48);
        BuildSounds();
        MainMenuManager.BeforeSceneChange = CoverScreen;
        SeedManager.BeforeSceneChange = CoverScreen;       // Quiz -> MainForest (ผู้เล่นใหม่)
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (MainMenuManager.BeforeSceneChange == (Func<string, IEnumerator>)CoverScreen) MainMenuManager.BeforeSceneChange = null;
        if (SeedManager.BeforeSceneChange == (Func<string, IEnumerator>)CoverScreen) SeedManager.BeforeSceneChange = null;
        if (I == this) I = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        ReleaseListener();
        if (s.name == MenuScene) MainMenuManager.BeforeSceneChange = CoverScreen;   // กลับมาเมนูใหม่ -> ผูก hook อีกรอบ
        SeedManager.BeforeSceneChange = CoverScreen;
        offsets.Clear();
        dirCache.Clear();
        if (pendingReveal)
        {
            pendingReveal = false;
            StopAllCoroutines();
            StartCoroutine(Reveal(s));
        }
        else KillOverlay();
    }

    // =====================================================================================
    //  🍃 ขาออก: ใบไม้พัดเข้ามาจนเต็มจอ
    // =====================================================================================
    IEnumerator CoverScreen(string nextScene)
    {
        BuildOverlay();
        float W = Screen.width, H = Screen.height, S = Mathf.Min(W, H);
        leaves.Clear();

        // วางเป้าหมายเป็นตารางสุ่มนิดๆ จะได้คลุมทั่วจอ
        int cols = 5, rows = Mathf.CeilToInt(LeafCount / (float)cols);
        for (int i = 0; i < LeafCount; i++)
        {
            int cx = i % cols, cy = i / cols;
            Vector2 to = new Vector2((cx + UnityEngine.Random.Range(0.1f, 0.9f)) / cols * W, (cy + UnityEngine.Random.Range(0.1f, 0.9f)) / rows * H);
            float size = S * UnityEngine.Random.Range(0.42f, 0.7f);
            Leaf lf = new Leaf
            {
                size = size,
                to = to,
                from = new Vector2(-size * UnityEngine.Random.Range(0.6f, 1.4f), to.y + H * UnityEngine.Random.Range(-0.25f, 0.2f)),
                rot0 = UnityEngine.Random.Range(-160f, 160f),
                rot1 = UnityEngine.Random.Range(-60f, 60f) + (UnityEngine.Random.value < 0.5f ? 0f : 180f),
                delay = to.x / W * 0.32f + UnityEngine.Random.Range(0f, 0.08f),
                wobble = new Vector2(UnityEngine.Random.Range(20f, 45f) * S / 1000f, UnityEngine.Random.Range(2f, 4f)),
            };
            lf.rt = NewImage("Leaf", overlay.transform, leafSprite, LeafColors[UnityEngine.Random.Range(0, LeafColors.Length)]);
            lf.rt.anchorMin = lf.rt.anchorMax = Vector2.zero;
            lf.rt.sizeDelta = new Vector2(size, size);
            lf.rt.anchoredPosition = lf.from;
            leaves.Add(lf);
        }
        // ใบที่มาทีหลังอยู่บนสุด
        leaves.Sort((a, b) => a.delay.CompareTo(b.delay));
        for (int i = 0; i < leaves.Count; i++) leaves[i].rt.SetSiblingIndex(i + 1);

        Play(clipRustle, 0.85f);
        float fly = CoverTime - 0.35f;
        for (float t = 0; t < CoverTime + 0.05f; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < leaves.Count; i++)
            {
                Leaf lf = leaves[i];
                float k = Mathf.Clamp01((t - lf.delay) / fly);
                float e = EaseOutCubic(k);
                Vector2 p = Vector2.LerpUnclamped(lf.from, lf.to, e);
                p.y += Mathf.Sin(k * Mathf.PI * lf.wobble.y) * lf.wobble.x * (1f - e);
                lf.rt.anchoredPosition = p;
                lf.rt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(lf.rot0, lf.rot1, e));
                lf.rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, e);
            }
            SetBacking(Mathf.Clamp01((t - CoverTime * 0.45f) / (CoverTime * 0.45f)));
            yield return null;
        }
        SetBacking(1f);
        pendingReveal = true;
        yield return null;          // ให้เฟรมที่ปิดจอเต็มขึ้นก่อน
        HoldListener();
    }

    // =====================================================================================
    //  🌳 ขาเข้า: ใบไม้ปลิวออก + UI เลื่อนเข้า + พืชเด้ง + บัดดี้กระโดด
    // =====================================================================================
    IEnumerator Reveal(Scene s)
    {
        bool forest = s.name == ForestScene;
        float W = Screen.width, H = Screen.height;

        // ---- เตรียมของในฉากก่อนเฟรมแรก: ซ่อนพืช/บัดดี้, ดัน UI ออกนอกจอ ----
        CanvasGroup plantCg = null, buddyCg = null;
        Transform plant = null, buddy = null;
        var hud = new List<Offset>();
        if (forest)
        {
            Canvas.ForceUpdateCanvases();
            plant = FindInScene(s, PlantName);
            buddy = FindInScene(s, BuddyName);
            plantCg = HideWithGroup(plant);
            buddyCg = HideWithGroup(buddy);
            foreach (string n in HudNames)
            {
                Transform t = FindInScene(s, n);
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                RectTransform rt = t as RectTransform;
                if (rt == null) continue;
                Offset o = new Offset { rt = rt, offset = OffscreenDir(rt) };
                offsets.Add(o);
                hud.Add(o);
            }
        }

        Play(clipRustleSoft, 0.7f, 1.08f);
        // ใบไม้ปลิวออกไปทางขวา (ซ้ายไปก่อน = จอเปิดจากซ้าย)
        for (int i = 0; i < leaves.Count; i++)
        {
            Leaf lf = leaves[i];
            lf.from = lf.rt.anchoredPosition;
            lf.to = new Vector2(W + lf.size * UnityEngine.Random.Range(0.7f, 1.5f), lf.from.y + H * UnityEngine.Random.Range(-0.1f, 0.25f));
            lf.rot0 = lf.rt.localEulerAngles.z;
            lf.rot1 = lf.rot0 + UnityEngine.Random.Range(-220f, 220f);
            lf.delay = lf.from.x / W * 0.28f + UnityEngine.Random.Range(0f, 0.08f);
        }

        float fly = RevealTime - 0.3f;
        bool plantDone = false, buddyStarted = false, landed = false;
        float t0 = 0f;
        for (float t = 0; t < 2.2f; t += Time.unscaledDeltaTime)
        {
            // ใบไม้
            if (overlay != null)
            {
                for (int i = 0; i < leaves.Count; i++)
                {
                    Leaf lf = leaves[i];
                    float k = Mathf.Clamp01((t - lf.delay) / fly);
                    float e = k * k * k;
                    lf.rt.anchoredPosition = Vector2.LerpUnclamped(lf.from, lf.to, e) + new Vector2(0, Mathf.Sin(k * 6f) * 18f * k);
                    lf.rt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(lf.rot0, lf.rot1, e));
                }
                SetBacking(1f - Mathf.Clamp01(t / 0.35f));
                if (t > RevealTime + 0.1f) KillOverlay();
            }

            if (forest)
            {
                // UI เลื่อนเข้าจากขอบ (ทีละชิ้น)
                for (int i = 0; i < hud.Count; i++)
                {
                    float k = Mathf.Clamp01((t - 0.25f - i * 0.07f) / 0.5f);
                    hud[i].offset = OffscreenDir(hud[i].rt, true) * (1f - BackOut(k, 1.3f));
                }

                // พืชเด้งโผล่
                if (!plantDone && t > 0.42f)
                {
                    plantDone = true;
                    if (plantCg) StartCoroutine(FadeGroup(plantCg, 0.12f));
                    if (plant)
                    {
                        GrowGoJuiceScale js = plant.GetComponent<GrowGoJuiceScale>();
                        if (js) js.Kick(0.45f);
                        Puff(plant as RectTransform, 10);
                    }
                    Play(clipPop, 0.8f);
                }

                // บัดดี้กระโดดลงมา
                if (buddy != null && t > 0.62f)
                {
                    RectTransform brt = buddy as RectTransform;
                    if (!buddyStarted)
                    {
                        buddyStarted = true; t0 = t;
                        if (buddyCg) buddyCg.alpha = 1f;
                        if (brt) offsets.Add(new Offset { rt = brt, offset = new Vector2(0, H * 0.45f) });
                    }
                    Offset bo = brt ? offsets.Find(o => o.rt == brt) : null;
                    float k = Mathf.Clamp01((t - t0) / 0.55f);
                    if (bo != null) bo.offset = new Vector2(0, H * 0.45f * (1f - BounceOut(k)));
                    if (!landed && k >= 1f / 2.75f) { landed = true; Play(clipLand, 0.7f); }   // แตะพื้นครั้งแรก
                    if (k >= 1f && bo != null) bo.offset = Vector2.zero;
                }
            }
            if (t > (forest ? 1.6f : RevealTime + 0.15f)) break;
            yield return null;
        }

        // คืนทุกอย่างให้เป๊ะ
        foreach (Offset o in offsets) o.offset = Vector2.zero;
        yield return null;
        offsets.Clear();
        if (plantCg) plantCg.alpha = 1f;
        if (buddyCg) buddyCg.alpha = 1f;
        KillOverlay();
    }

    // บวก offset หลังจากเกม/Animator เขียนตำแหน่งแล้ว (LateUpdate) -> แสดงผลรวม แต่ไม่ไปทับค่าของเกม
    void LateUpdate()
    {
        for (int i = 0; i < offsets.Count; i++)
        {
            Offset o = offsets[i];
            if (o.rt == null) continue;
            Vector2 cur = o.rt.anchoredPosition;
            // ถ้าไม่มีใครเขียนทับตั้งแต่เฟรมก่อน -> ถอด offset เก่าออกก่อน
            if (o.applied && (cur - o.lastWritten).sqrMagnitude < 0.0001f) cur -= o.lastOffset;
            cur += o.offset;
            o.rt.anchoredPosition = cur;
            o.lastWritten = cur; o.lastOffset = o.offset; o.applied = true;
        }
    }

    // ทิศที่ใกล้ขอบจอที่สุด * ระยะพอให้พ้นจอ (หน่วย anchoredPosition ของ parent)
    Vector2 OffscreenDir(RectTransform rt, bool cached = false)
    {
        if (cached && dirCache.TryGetValue(rt, out Vector2 v)) return v;
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(null, c[0]), b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
        Vector2 mid = (a + b) * 0.5f;
        float W = Screen.width, H = Screen.height;
        float dl = mid.x, dr = W - mid.x, db = mid.y, dt = H - mid.y;
        float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
        Vector2 screenMove;
        if (m == dt) screenMove = new Vector2(0, H - a.y + 20f);
        else if (m == db) screenMove = new Vector2(0, -(b.y + 20f));
        else if (m == dl) screenMove = new Vector2(-(b.x + 20f), 0);
        else screenMove = new Vector2(W - a.x + 20f, 0);
        // จอ (pixel) -> หน่วยของ parent (Canvas แบบ Overlay: world = pixel)
        Vector3 lossy = rt.parent != null ? rt.parent.lossyScale : Vector3.one;
        Vector2 local = screenMove / Mathf.Max(0.0001f, lossy.x);
        dirCache[rt] = local;
        return local;
    }
    readonly Dictionary<RectTransform, Vector2> dirCache = new Dictionary<RectTransform, Vector2>();

    static CanvasGroup HideWithGroup(Transform t)
    {
        if (t == null) return null;
        CanvasGroup cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        return cg;
    }

    IEnumerator FadeGroup(CanvasGroup cg, float dur)
    {
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / dur)
        {
            if (cg == null) yield break;
            cg.alpha = t;
            yield return null;
        }
        if (cg) cg.alpha = 1f;
    }

    // ฝุ่น/ประกายพุ่งออกรอบๆ ตอนพืชโผล่ (บน overlay ใหม่ชั่วคราว)
    void Puff(RectTransform around, int n)
    {
        if (around == null) return;
        Vector3[] c = new Vector3[4];
        around.GetWorldCorners(c);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(null, c[0]), b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
        Vector2 foot = new Vector2((a.x + b.x) * 0.5f, a.y + (b.y - a.y) * 0.15f);
        float size = Mathf.Abs(b.x - a.x);
        Canvas fx = MakeCanvas("GrowGoPuff", 4000);
        for (int i = 0; i < n; i++)
        {
            bool leaf = i % 2 == 0;
            RectTransform r = NewImage("Puff", fx.transform, leaf ? leafSprite : dotSprite,
                leaf ? LeafColors[UnityEngine.Random.Range(0, LeafColors.Length)] : new Color(1f, 0.96f, 0.7f, 0.9f));
            r.anchorMin = r.anchorMax = Vector2.zero;
            float s = size * (leaf ? UnityEngine.Random.Range(0.14f, 0.22f) : UnityEngine.Random.Range(0.08f, 0.14f));
            r.sizeDelta = new Vector2(s, s);
            r.anchoredPosition = foot + new Vector2(UnityEngine.Random.Range(-0.25f, 0.25f) * size, 0);
            float ang = Mathf.Lerp(20f, 160f, (i + UnityEngine.Random.value) / n) * Mathf.Deg2Rad;
            StartCoroutine(PuffAnim(r, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * size * UnityEngine.Random.Range(0.35f, 0.6f), UnityEngine.Random.Range(0.5f, 0.75f)));
        }
        Destroy(fx.gameObject, 1.2f);
    }

    IEnumerator PuffAnim(RectTransform r, Vector2 v, float dur)
    {
        Vector2 p0 = r.anchoredPosition;
        float spin = UnityEngine.Random.Range(-300f, 300f);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / dur)
        {
            if (r == null) yield break;
            float e = EaseOutCubic(t);
            r.anchoredPosition = p0 + v * e + new Vector2(0, -t * t * v.magnitude * 0.3f);
            r.localRotation = Quaternion.Euler(0, 0, spin * t);
            r.localScale = Vector3.one * (t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f);
            yield return null;
        }
        if (r) Destroy(r.gameObject);
    }

    // =====================================================================================
    //  🧱 overlay / หูชั่วคราว
    // =====================================================================================
    void BuildOverlay()
    {
        KillOverlay();
        overlay = MakeCanvas("GrowGoLeafCanvas", 5000);
        overlay.gameObject.AddComponent<GraphicRaycaster>();   // บล็อกการกดระหว่างทรานซิชัน
        backing = NewImage("Backing", overlay.transform, null, new Color(Backing.r, Backing.g, Backing.b, 0f)).GetComponent<Image>();
        RectTransform f = backing.rectTransform;
        f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = f.offsetMax = Vector2.zero;
        backing.raycastTarget = true;
    }

    Canvas MakeCanvas(string name, int order)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        return c;
    }

    void SetBacking(float a) { if (backing) backing.color = new Color(Backing.r, Backing.g, Backing.b, a); }

    void KillOverlay()
    {
        if (overlay != null) Destroy(overlay.gameObject);
        overlay = null; backing = null;
        leaves.Clear();
    }

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
    }

    // =====================================================================================
    //  🧩 ตัวช่วย
    // =====================================================================================
    static RectTransform NewImage(string name, Transform parent, Sprite sp, Color col)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image im = go.AddComponent<Image>();
        im.sprite = sp;
        im.color = col;
        im.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    static Transform FindInScene(Scene s, string name)
    {
        if (!s.IsValid()) return null;
        foreach (GameObject root in s.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            Transform t = FindDeep(root.transform, name);
            if (t != null) return t;
        }
        return null;
    }

    static Transform FindDeep(Transform p, string name)
    {
        for (int i = 0; i < p.childCount; i++) if (p.GetChild(i).name == name) return p.GetChild(i);
        for (int i = 0; i < p.childCount; i++) { Transform t = FindDeep(p.GetChild(i), name); if (t != null) return t; }
        return null;
    }

    static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
    static float BackOut(float t, float s) { t = Mathf.Clamp01(t) - 1f; return t * t * ((s + 1f) * t + s) + 1f; }
    static float BounceOut(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 1f / 2.75f) return 7.5625f * t * t;
        if (t < 2f / 2.75f) { t -= 1.5f / 2.75f; return 7.5625f * t * t + 0.75f; }
        if (t < 2.5f / 2.75f) { t -= 2.25f / 2.75f; return 7.5625f * t * t + 0.9375f; }
        t -= 2.625f / 2.75f; return 7.5625f * t * t + 0.984375f;
    }

    // ใบไม้ (สร้างเอง): รูปเลนส์ปลายแหลม + เส้นกลางใบ + เงาครึ่งใบ (สีจริงมาจาก Image.color)
    static Sprite MakeLeafSprite(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;   // -1..1
                // เลนส์: จุดที่อยู่ในวงกลม 2 วง (ศูนย์กลาง ±0.62 แกน x รัศมี 1) -> ใบยาวแนวตั้ง
                float r = 1.0f, off = 0.62f;
                float d1 = Mathf.Sqrt((u - off) * (u - off) + v * v * 0.92f) - r;
                float d2 = Mathf.Sqrt((u + off) * (u + off) + v * v * 0.92f) - r;
                float d = Mathf.Max(d1, d2);                    // < 0 = ในใบ
                float a = Mathf.Clamp01(-d * n * 0.5f);         // ขอบนุ่ม
                // ก้านใบ
                if (v < -0.85f && Mathf.Abs(u) < 0.035f) a = Mathf.Max(a, 1f);
                float shade = u < 0f ? 1f : 0.86f;                                // ครึ่งใบซ้ายสว่าง ขวาเข้ม
                float vein = Mathf.Clamp01(1f - Mathf.Abs(u) / 0.03f) * 0.28f;   // เส้นกลางใบ
                float side = 0f;                                                  // เส้นใบแยก
                for (int k = -2; k <= 2; k++)
                {
                    float vy = k * 0.28f;
                    float lineV = vy + Mathf.Abs(u) * 0.55f;
                    side = Mathf.Max(side, Mathf.Clamp01(1f - Mathf.Abs(v - lineV) / 0.025f) * (Mathf.Abs(u) < 0.32f ? 0.14f : 0f));
                }
                float g = shade * (1f - vein - side) + Mathf.Clamp01(0.25f - (u + 0.3f) * (u + 0.3f) - (v - 0.2f) * (v - 0.2f) * 0.3f) * 0.6f;   // ไฮไลต์
                px[y * n + x] = new Color(Mathf.Clamp01(g), Mathf.Clamp01(g), Mathf.Clamp01(g), a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeDotSprite(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color(1, 1, 1, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // =====================================================================================
    //  🔊 เสียง (สังเคราะห์เอง, ระดับตาม SfxVolume)
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
        clipRustle = Rustle("rustle", 1.0f, 11, 1);
        clipRustleSoft = Rustle("rustle_soft", 0.8f, 7, 2);
        // ป๊อป (พืชโผล่)
        clipPop = Synth("pop", 0.35f, t =>
            Bloop(t, 300f, 900f, 0.1f) * 1.1f + Bell(t, 1760f, 0.05f, 9f) * 0.4f);
        // ตุ๊บ (บัดดี้ลงพื้น)
        clipLand = Synth("land", 0.3f, t =>
            Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 90f, Mathf.Clamp01(t / 0.12f)) * t) * Mathf.Exp(-t * 22f) * Mathf.Min(1f, t * 500f));
    }

    // ใบไม้ไหวกรอบแกรบ: ลม (noise ผ่าน band-pass ที่ขึ้นลง) + เสียงกรอบแกรบสั้นๆ แบบสุ่ม
    static AudioClip Rustle(string name, float dur, int seed, int crackleSeed)
    {
        int n = Mathf.CeilToInt(SR * dur);
        float[] d = new float[n];
        System.Random rnd = new System.Random(seed);
        float lp = 0f, lp2 = 0f, hp = 0f, prev = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, k = t / dur;
            float env = Mathf.Clamp01(k * 6f) * Mathf.Clamp01((1f - k) * 2.2f);   // 0..1 ไม่มีค่าติดลบ
            float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float cut = Mathf.Lerp(0.05f, 0.22f, Mathf.Sin(Mathf.PI * k));
            lp += (nz - lp) * cut;
            lp2 += (lp - lp2) * cut;
            hp = lp2 - prev; prev = lp2;                       // ตัดเบสออก -> เสียงลมใบไม้
            if (rnd.NextDouble() < 0.0016 * env) crackle = 1f;   // กรอบแกรบ
            crackle *= 0.985f;
            float cr = crackle * (float)(rnd.NextDouble() * 2.0 - 1.0);
            d[i] = (hp * 9f + lp2 * 0.6f) * env + cr * 0.35f * env;
        }
        return ToClip(name, d);
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
        return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t)) * env * 0.5f;
    }

    static AudioClip Synth(string name, float dur, Func<float, float> f)
    {
        int n = Mathf.CeilToInt(SR * dur);
        float[] d = new float[n];
        for (int i = 0; i < n; i++) d[i] = f(i / (float)SR);
        return ToClip(name, d);
    }

    static AudioClip ToClip(string name, float[] d)
    {
        int n = d.Length;
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(d[i]) || float.IsInfinity(d[i])) d[i] = 0f;   // กันเสียงพังทั้งระบบ
            float a = Mathf.Abs(d[i]);
            if (a > peak) peak = a;
        }
        float norm = 0.8f / peak;
        int fade = Mathf.Min(n, 400);
        for (int i = 0; i < n; i++)
        {
            d[i] *= norm;
            if (i > n - fade) d[i] *= (n - i) / (float)fade;
        }
        AudioClip c = AudioClip.Create("leaffx_" + name, n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }
}
