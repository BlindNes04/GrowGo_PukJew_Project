using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // เพิ่มบรรทัดนี้

// โซนที่เข็มหยุด (แดง = ขอบซ้ายสุด/ขวาสุด ได้ปุ๋ยเน่า)
public enum BoilingZone { Red, Yellow, Green }

public class BoilingMinigame : MonoBehaviour
{
    [System.Serializable]
    public class DifficultyBarSetting
    {
        public string label;                          // ไว้ดูใน Inspector เฉยๆ
        [Range(0.02f, 0.8f)] public float greenWidth; // ความกว้างโซนเขียว (สัดส่วนของหลอดทั้งหมด)
        public float barsPerSecond;                   // ความเร็วเข็ม (กี่ความยาวหลอดต่อวินาที)
    }

    [Header("UI References")]
    [SerializeField] private RectTransform indicator;
    [SerializeField] private GameObject resultRewardView; // ลาก RewardView มาใส่
    [SerializeField] private FertilizerMixerManager mixerManager; // ว่างไว้ได้ จะหาเองในฉาก
    [SerializeField] private RectTransform timingBar;            // ว่างไว้ได้ จะใช้ parent ของ Indicator

    [Header("Bar Settings")]
    [SerializeField] private float moveRange = 250f; // ใช้เมื่อหาความกว้างหลอดไม่ได้ (ความเร็วเข็มตั้งตามระดับด้านล่าง)

    [Header("Zones (สร้างหลอดจากแถบสี)")]
    [SerializeField] private bool buildBarFromCode = true;
    [SerializeField] private bool hideOriginalBarImage = true; // ซ่อนรูปหลอดเดิมที่มีเข็มติดมา
    [SerializeField] private Color redColor = new Color(0.69f, 0.17f, 0.13f);
    [SerializeField] private Color yellowColor = new Color(0.97f, 0.81f, 0.27f);
    [SerializeField] private Color greenColor = new Color(0.40f, 0.77f, 0.42f);
    [SerializeField, Range(0.02f, 0.3f)] private float redEdgeWidth = 0.10f; // ขอบแดงแต่ละข้าง (เท่ากันทุกระดับ)
    [SerializeField] private float barInsetX = 0f; // เว้นขอบซ้ายขวาของหลอด (px)
    [SerializeField] private float barInsetY = 0f; // เว้นขอบบนล่างของหลอด (px)

    [Header("รูปทรงหลอด (ใช้รูปหลอดเดิมตัดขอบมน + ขอบดำ)")]
    [SerializeField] private bool useBarShape = true;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private float outlineWidth = 12f; // ความหนาขอบดำ (px)

    [Tooltip("ลำดับ 0 = ง่าย, 1 = กลาง, 2 = ยาก")]
    [SerializeField] private DifficultyBarSetting[] difficultySettings = new DifficultyBarSetting[]
    {
        new DifficultyBarSetting { label = "ง่าย",  greenWidth = 0.40f, barsPerSecond = 1.0f },
        new DifficultyBarSetting { label = "กลาง", greenWidth = 0.25f, barsPerSecond = 1.5f },
        new DifficultyBarSetting { label = "ยาก",  greenWidth = 0.14f, barsPerSecond = 2.1f },
    };

    private bool isPlaying = false;
    private float startTime;
    private float currentFraction; // ตำแหน่งเข็ม 0 = ซ้ายสุด, 1 = ขวาสุด
    private DifficultyBarSetting activeSetting;
    private RectTransform zoneRoot;
    private RectTransform outlineRoot;

    private void OnEnable()
    {
        if (mixerManager == null) mixerManager = FindAnyObjectByType<FertilizerMixerManager>();
        if (timingBar == null && indicator != null) timingBar = indicator.parent as RectTransform;

        int difficulty = mixerManager != null ? mixerManager.CurrentDifficulty : 0;
        activeSetting = GetSetting(difficulty);

        if (buildBarFromCode) BuildZones();

        startTime = Time.time;
        currentFraction = 0f;
        isPlaying = true;
    }

    private DifficultyBarSetting GetSetting(int difficulty)
    {
        if (difficultySettings == null || difficultySettings.Length == 0)
            return new DifficultyBarSetting { greenWidth = 0.3f, barsPerSecond = 1f };
        return difficultySettings[Mathf.Clamp(difficulty, 0, difficultySettings.Length - 1)];
    }

    private float BarWidth
    {
        get
        {
            if (timingBar != null && timingBar.rect.width > 1f) return timingBar.rect.width - barInsetX * 2f - (useBarShape ? outlineWidth * 2f : 0f);
            return moveRange * 2f;
        }
    }

    private void Update()
    {
        if (!isPlaying || indicator == null) return;

        // วิ่งไป-กลับ ตามความเร็วของระดับ
        float t = (Time.time - startTime) * activeSetting.barsPerSecond;
        currentFraction = Mathf.PingPong(t, 1f);

        float x = (currentFraction - 0.5f) * BarWidth;
        indicator.anchoredPosition = new Vector2(x, indicator.anchoredPosition.y);
    }

    // ผูกกับปุ่ม "หยุด"
    public void OnClickStop()
    {
        if (!isPlaying) return;

        isPlaying = false;
        StartCoroutine(TransitionToRewardRoutine(GetZone(currentFraction)));
    }

    public BoilingZone GetZone(float fraction)
    {
        if (fraction < redEdgeWidth || fraction > 1f - redEdgeWidth) return BoilingZone.Red;
        float halfGreen = activeSetting.greenWidth * 0.5f;
        if (Mathf.Abs(fraction - 0.5f) <= halfGreen) return BoilingZone.Green;
        return BoilingZone.Yellow;
    }

    private IEnumerator TransitionToRewardRoutine(BoilingZone zone)
    {
        yield return new WaitForSeconds(0.4f); // เข็มหยุดนิ่งชั่วครู่

        if (mixerManager != null)
        {
            // ให้ Manager คิดผล + บันทึก DB ก่อน แล้วค่อยเปิดหน้ารับปุ๋ย
            mixerManager.FinishMix(zone, ShowRewardView);
        }
        else
        {
            Debug.LogWarning("[BoilingMinigame] ไม่พบ FertilizerMixerManager ในฉาก");
            ShowRewardView();
        }
    }

    private void ShowRewardView()
    {
        gameObject.SetActive(false);            // ซ่อนหน้ามินิเกม
        if (resultRewardView != null)
        {
            resultRewardView.SetActive(true);   // เปิดหน้ารับปุ๋ย
        }
    }

    // 🎨 สร้างแถบสี แดง | เหลือง | เขียว | เหลือง | แดง ตามระดับความยาก
    private void BuildZones()
    {
        if (timingBar == null) return;

        Image original = timingBar.GetComponent<Image>();
        Sprite barSprite = original != null ? original.sprite : null;
        if (hideOriginalBarImage && original != null) original.enabled = false;

        bool shaped = useBarShape && barSprite != null;
        float border = shaped ? outlineWidth : 0f;

        // ชั้นล่าง: เงาดำของรูปหลอด = ขอบดำ
        if (shaped)
        {
            if (outlineRoot == null)
            {
                GameObject outlineObj = new GameObject("Outline", typeof(RectTransform), typeof(Image));
                outlineRoot = outlineObj.GetComponent<RectTransform>();
                outlineRoot.SetParent(timingBar, false);
            }
            outlineRoot.anchorMin = Vector2.zero;
            outlineRoot.anchorMax = Vector2.one;
            outlineRoot.offsetMin = new Vector2(barInsetX, barInsetY);
            outlineRoot.offsetMax = new Vector2(-barInsetX, -barInsetY);

            Image outlineImg = outlineRoot.GetComponent<Image>();
            outlineImg.sprite = barSprite;
            outlineImg.type = original.type;
            outlineImg.color = outlineColor;
            outlineImg.raycastTarget = false;
        }
        else if (outlineRoot != null)
        {
            outlineRoot.gameObject.SetActive(false);
        }

        if (zoneRoot == null)
        {
            GameObject rootObj = new GameObject("Zones", typeof(RectTransform));
            zoneRoot = rootObj.GetComponent<RectTransform>();
            zoneRoot.SetParent(timingBar, false);
        }
        zoneRoot.anchorMin = Vector2.zero;
        zoneRoot.anchorMax = Vector2.one;
        zoneRoot.offsetMin = new Vector2(barInsetX + border, barInsetY + border);
        zoneRoot.offsetMax = new Vector2(-barInsetX - border, -barInsetY - border);

        // ชั้นบน: แถบสี ถูกตัดด้วยรูปทรงหลอด (ขอบมนตามรูปเดิม)
        if (shaped)
        {
            Image maskImg = zoneRoot.GetComponent<Image>();
            if (maskImg == null) maskImg = zoneRoot.gameObject.AddComponent<Image>();
            maskImg.sprite = barSprite;
            maskImg.type = original.type;
            maskImg.raycastTarget = false;

            Mask mask = zoneRoot.GetComponent<Mask>();
            if (mask == null) mask = zoneRoot.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        // เรียงชั้น: ขอบดำ -> แถบสี -> เข็ม
        if (outlineRoot != null) outlineRoot.SetAsFirstSibling();
        zoneRoot.SetSiblingIndex(outlineRoot != null ? 1 : 0);

        for (int i = zoneRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(zoneRoot.GetChild(i).gameObject);
        }

        float red = redEdgeWidth;
        float green = Mathf.Min(activeSetting.greenWidth, 1f - red * 2f);
        float greenStart = 0.5f - green * 0.5f;
        float greenEnd = 0.5f + green * 0.5f;

        AddZone("Red_L", 0f, red, redColor);
        AddZone("Yellow_L", red, greenStart, yellowColor);
        AddZone("Green", greenStart, greenEnd, greenColor);
        AddZone("Yellow_R", greenEnd, 1f - red, yellowColor);
        AddZone("Red_R", 1f - red, 1f, redColor);
    }

    private void AddZone(string zoneName, float from, float to, Color color)
    {
        if (to <= from) return;

        GameObject obj = new GameObject(zoneName, typeof(RectTransform), typeof(Image));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(zoneRoot, false);
        rt.anchorMin = new Vector2(from, 0f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = obj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // ฟังก์ชันผูกกับปุ่ม "กลับสู่หน้าหลัก"
    public void OnClickBackToHome()
    {
        // โหลดฉากปัจจุบันใหม่ทั้งหมดเพื่อรีเซ็ตของทุกอย่างพร้อมเริ่มรอบใหม่
        SceneManager.LoadScene("MainForest");
    }
}
