using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainPlantStatusView : MonoBehaviour
{
    public static MainPlantStatusView Instance;

    [Header("อ้างอิงข้อมูลพืช")]
    public PlantStatus plantStatus;

    [Header("UI Text & Images (สเตตัสพืช)")]
    public TextMeshProUGUI plantNameText;
    public string namePrefix = "ชื่อ: ";
    public TextMeshProUGUI stageNamePillText; // 👈 ลาก Text ใน Pill สีเทา/ส้ม ใต้ชื่อพืชมาใส่ช่องนี้
    public Image moistureFillBar;          // หลอดสีฟ้า (Moisture_Fill)
    public Image nutrientsFillBar;         // หลอดสีแดง (Nutrient_Fill)

    [Header("UI Buttons")]
    public Button waterButton;             // ปุ่ม Water_Button
    public GameObject cooldownWaterButtonObj; // ปุ่ม Cooldown_Water_Button
    public TextMeshProUGUI cooldownText;   // Text Cooldown
    public Button nourishButton;           // ปุ่ม Nourish_Button
    public Button editNameButton;          // ปุ่ม NameEdit (ดินสอ)

    [Header("UI Background / Overlay ปิดหน้าต่าง")]
    public Button closeOverlayButton;      // ปุ่ม CloseOverlay (ล่องหน)

    [Header("UI Popups")]
    public GameObject nourishPopupUI;      // 🌿 หน้า Pop-up บำรุงพืช (Nourish_Popup)
    public RenamePlantPopup renamePlantPopup; // หน้า Pop-up เปลี่ยนชื่อ

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (waterButton != null)
            waterButton.onClick.AddListener(OnWaterButtonClicked);

        if (nourishButton != null)
            nourishButton.onClick.AddListener(OnNourishButtonClicked);

        if (editNameButton != null)
            editNameButton.onClick.AddListener(OnEditNameButtonClicked);

        if (closeOverlayButton != null)
            closeOverlayButton.onClick.AddListener(CloseView);

        if (plantStatus == null)
        {
            plantStatus = FindFirstObjectByType<PlantStatus>();
        }

        CloseView();
    }

    void OnEnable()
    {
        UpdateUI();
    }

    void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            UpdateCooldownUI();
        }
    }

    // 💡 ฟังก์ชันเปิดหน้ารดน้ำ
    public void OpenView()
    {
        if (plantStatus == null)
        {
            plantStatus = FindFirstObjectByType<PlantStatus>();
        }

        if (plantStatus != null)
        {
            plantStatus.LoadData();
        }

        gameObject.SetActive(true);

        if (closeOverlayButton != null)
            closeOverlayButton.gameObject.SetActive(true);

        UpdateUI();
    }

    // 💡 ฟังก์ชันปิดหน้ารดน้ำ
    public void CloseView()
    {
        gameObject.SetActive(false);

        if (closeOverlayButton != null)
            closeOverlayButton.gameObject.SetActive(false);
    }

    // 💡 อัปเดต UI แสดงผลหลอดความชุ่มชื้น สารอาหาร และชื่อร่างใน Pill
    public void UpdateUI()
    {
        if (plantStatus == null) return;

        // 1. ชื่อตั้งของพืช
        if (plantNameText != null)
            plantNameText.text = $"{namePrefix}{plantStatus.plantName}";

        // 2. ชื่อร่างใน Pill ใต้ชื่อพืช (เช่น มะเขือเทศนางฟ้า, หน่อกะทิน้อย)
        if (stageNamePillText != null)
            stageNamePillText.text = plantStatus.GetCurrentStageName();

        // 3. หลอดความชุ่มชื้น (สีฟ้า)
        if (moistureFillBar != null)
        {
            float fillRatio = plantStatus.currentMoisture / plantStatus.maxMoisture;
            moistureFillBar.fillAmount = Mathf.Clamp01(fillRatio);
        }

        // 4. หลอดสารอาหาร (สีแดง)
        if (nutrientsFillBar != null)
        {
            float fillRatio = plantStatus.currentNutrients / plantStatus.maxNutrients;
            nutrientsFillBar.fillAmount = Mathf.Clamp01(fillRatio);
        }

        // 5. สั่งซิงค์ภาพพืชบนแท่นหินให้ตรงกับร่างปัจจุบัน
        plantStatus.SyncCharacterVisual();
    }

    private void UpdateCooldownUI()
    {
        if (plantStatus == null) return;

        if (plantStatus.CanWater())
        {
            if (waterButton != null) waterButton.gameObject.SetActive(true);
            if (cooldownWaterButtonObj != null) cooldownWaterButtonObj.SetActive(false);
        }
        else
        {
            if (waterButton != null) waterButton.gameObject.SetActive(false);
            if (cooldownWaterButtonObj != null) cooldownWaterButtonObj.SetActive(true);

            if (plantStatus.isSick)
            {
                if (cooldownText != null) cooldownText.text = "พืชป่วย!";
            }
            else
            {
                float remainingSecs = plantStatus.GetRemainingCooldownSeconds();
                TimeSpan t = TimeSpan.FromSeconds(remainingSecs);

                if (cooldownText != null)
                    cooldownText.text = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
            }
        }
    }

    private void OnWaterButtonClicked()
    {
        if (plantStatus == null || !plantStatus.CanWater()) return;

        plantStatus.WaterPlant();

        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        UpdateUI();
    }

    // 🌿 เมื่อกดปุ่ม "บำรุง"
    private void OnNourishButtonClicked()
    {
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        if (nourishPopupUI != null)
        {
            nourishPopupUI.SetActive(true);
        }
    }

    private void OnEditNameButtonClicked()
    {
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        if (renamePlantPopup != null && plantStatus != null)
        {
            renamePlantPopup.OpenPopup(plantStatus);
        }
    }
}