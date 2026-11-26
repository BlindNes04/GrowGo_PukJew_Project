using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
//  GrowGoToast : ป้ายข้อความลอยกลางจอ (แจ้งผลสั้นๆ แล้วจางหายเอง)
//  วิธีใช้ : GrowGoToast.Show("เปลี่ยนชื่อแล้ว");
//  - สร้าง Canvas ของตัวเองบนสุด ไม่ต้องลากอะไรใส่ฉาก
//  - ใช้ฟอนต์จากข้อความในฉากที่อ่านภาษาไทยได้ (ฟอนต์ default ของ TMP ไม่มีภาษาไทย)
// =====================================================================
public class GrowGoToast : MonoBehaviour
{
    private static GrowGoToast instance;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private Coroutine routine;

    // ==================== เรียกใช้ ====================
    public static void Show(string message, float seconds = 2.2f)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (instance == null) Build();
        instance.PickThaiFont();
        instance.label.text = message;
        if (instance.routine != null) instance.StopCoroutine(instance.routine);
        instance.routine = instance.StartCoroutine(instance.Play(seconds));
    }

    // ==================== สร้างป้าย (ครั้งแรกครั้งเดียว) ====================
    private static void Build()
    {
        GameObject root = new GameObject("GrowGoToast", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(root);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;   // อยู่บนสุดเสมอ
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1170f, 2532f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        box.transform.SetParent(root.transform, false);
        RectTransform rt = (RectTransform)box.transform;
        rt.anchorMin = new Vector2(0.1f, 0.5f);
        rt.anchorMax = new Vector2(0.9f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 150f);
        rt.anchoredPosition = new Vector2(0f, -250f);
        Image bg = box.GetComponent<Image>();
        bg.color = new Color(0.36f, 0.24f, 0.15f, 0.95f);   // น้ำตาลเข้ม โทนเดียวกับปุ่มในเกม
        bg.raycastTarget = false;

        GameObject txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(box.transform, false);
        RectTransform trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(30f, 10f);
        trt.offsetMax = new Vector2(-30f, -10f);
        TextMeshProUGUI tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 28f;
        tmp.fontSizeMax = 54f;
        tmp.raycastTarget = false;

        instance = root.AddComponent<GrowGoToast>();
        instance.group = box.GetComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;
        instance.label = tmp;
    }

    // ==================== หาฟอนต์ที่มีภาษาไทยจากข้อความในฉาก ====================
    private void PickThaiFont()
    {
        if (label.font != null && label.font.HasCharacter('ก', true, true)) return;
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (t == label || t.font == null) continue;
            if (t.font.HasCharacter('ก', true, true)) { label.font = t.font; return; }
        }
    }

    // ==================== โผล่ -> ค้าง -> จางหาย ====================
    private IEnumerator Play(float seconds)
    {
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; group.alpha = t / 0.15f; yield return null; }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; group.alpha = 1f - t / 0.3f; yield return null; }
        group.alpha = 0f;
        routine = null;
    }
}
