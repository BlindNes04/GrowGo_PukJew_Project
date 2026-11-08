using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainPlantStatusView : MonoBehaviour
{
    public static MainPlantStatusView Instance;

    [Header("อ้างอิงข้อมูลพืช")]
    public PlantStatus plantStatus;

    [Header("UI Display Container (ซ่อนพืชถ้าไม่มีใน DB)")]
    [Tooltip("GameObject รวมของตัวพืช/กระถางบนแท่นหิน (ถ้าไม่มีพืช จะสั่ง SetActive(false))")]
    public GameObject plantDisplayContainer;

    [Header("UI Text & Images (สเตตัสพืช)")]
    public TextMeshProUGUI plantNameText;
    public string namePrefix = "ชื่อ: ";
    public TextMeshProUGUI stageNamePillText; // ลาก Text ใน Pill สีเทา/ส้ม ใต้ชื่อพืชมาใส่ช่องนี้
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
    public GameObject nourishPopupUI;      // หน้า Pop-up บำรุงพืช (Nourish_Popup)
    public RenamePlantPopup renamePlantPopup; // หน้า Pop-up เปลี่ยนชื่อ
    public GameObject plantSickPopup;

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
            plantStatus = FindAnyObjectByType<PlantStatus>();
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

    // ฟังก์ชันเปิดหน้ารดน้ำ
    public void OpenView()
    {
        if (plantStatus == null)
        {
            plantStatus = FindAnyObjectByType<PlantStatus>();
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

    // ฟังก์ชันปิดหน้ารดน้ำ
    public void CloseView()
    {
        gameObject.SetActive(false);

        if (closeOverlayButton != null)
            closeOverlayButton.gameObject.SetActive(false);
    }

    // อัปเดต UI แสดงผลหลอดความชุ่มชื้น สารอาหาร และชื่อร่างใน Pill
    public void UpdateUI()
    {
        if (plantStatus == null)
        {
            SetPlantContainerVisible(false);
            return;
        }

        // เช็กว่ามีพืชอยู่ในระบบจริงไหม
        bool hasActivePlant = !string.IsNullOrEmpty(plantStatus.playerPlantId);

        if (!hasActivePlant)
        {
            Debug.Log("[MainPlantStatusView] 🟢 ไม่พบข้อมูลพืชในระบบ -> ซ่อนการแสดงผลตัวพืช (พื้นที่ว่าง)");
            SetPlantContainerVisible(false);
            return;
        }

        // ถ้ามีพืช -> เปิดแสดงผลตัวพืชและอัปเดต UI
        SetPlantContainerVisible(true);

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

        // 6. Plant Sick Popup
        if (plantSickPopup != null)
        {
            plantSickPopup.SetActive(plantStatus.isSick);
        }
    }

    private void SetPlantContainerVisible(bool isVisible)
    {
        if (plantDisplayContainer != null)
        {
            plantDisplayContainer.SetActive(isVisible);
        }
    }

    private void UpdateCooldownUI()
    {
        if (plantStatus == null || string.IsNullOrEmpty(plantStatus.playerPlantId)) return;

        // อัปเดตหลอดความชุ่มชื้นให้ขยับลดลงเรื่อยๆ แบบเรียลไทม์ขณะเปิดหน้าต่างทิ้งไว้
        if (moistureFillBar != null && plantStatus.maxMoisture > 0)
        {
            moistureFillBar.fillAmount = Mathf.Clamp01(plantStatus.currentMoisture / plantStatus.maxMoisture);
        }
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

                // ใช้ (int)t.TotalHours เพื่อรองรับเผื่อกรณี Cooldown เกิน 24 ชั่วโมง
                if (cooldownText != null)
                    cooldownText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
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

    // เมื่อกดปุ่ม "บำรุง"
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