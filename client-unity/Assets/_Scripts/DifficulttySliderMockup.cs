using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DifficultySliderMockup : MonoBehaviour
{
    [Header("Slider Component")]
    public Slider invisibleSlider;

    [Header("UI Displays")]
    public Image barImage;            // ช่องแสดงรูปหลอด
    public Image startButtonImage;     // ช่องแสดงรูปปุ่มเริ่มเกม
    public TMP_Text titleText;         // ข้อความหัวข้อ (ถ้าพิมพ์แยก)

    [Header("Sprites (ใส่เรียง เขียว=0, เหลือง=1, แดง=2)")]
    public Sprite[] barSprites;        // Size = 3
    public Sprite[] buttonSprites;     // Size = 3

    [Header("Next Screens")]
    public GameObject itemBox;
    public GameObject mixBtn;

    private readonly string[] levelNames = { "ระดับเริ่มต้น", "ระดับปานกลาง", "ระดับยาก" };
    private readonly Color[] textColors = {
        new Color(0.12f, 0.72f, 0.32f), // เขียว
        new Color(0.96f, 0.73f, 0.05f), // เหลือง
        new Color(0.75f, 0.10f, 0.10f)  // แดง
    };

    // 💡 ระดับที่เลือกอยู่ 0 = ง่าย, 1 = กลาง, 2 = ยาก (FertilizerMixerManager อ่านค่านี้)
    public int SelectedDifficulty
    {
        get
        {
            if (invisibleSlider == null) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(invisibleSlider.value), 0, 2);
        }
    }

    private void Start()
    {
        if (invisibleSlider != null)
        {
            invisibleSlider.onValueChanged.AddListener(OnSlide);
            OnSlide(invisibleSlider.value); // อัปเดตสถานะตั้งต้น
        }
    }

    public void OnSlide(float value)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(value), 0, 2);

        // 1. สลับรูปหลอดตามระดับ
        if (barSprites.Length > index && barSprites[index] != null)
        {
            barImage.sprite = barSprites[index];
        }

        // 2. สลับรูปปุ่มเริ่มเกมตามระดับ
        if (buttonSprites.Length > index && buttonSprites[index] != null)
        {
            startButtonImage.sprite = buttonSprites[index];
        }

        // 3. เปลี่ยนข้อความและสีหัวข้อ
        if (titleText != null)
        {
            titleText.text = levelNames[index];
            titleText.color = textColors[index];
        }
    }

    public void OnClickStartGame()
    {
        gameObject.SetActive(false);
        if (itemBox != null) itemBox.SetActive(true);
        if (mixBtn != null) mixBtn.SetActive(true);
    }
}