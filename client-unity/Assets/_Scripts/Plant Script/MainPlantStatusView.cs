using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainPlantStatusView : MonoBehaviour
{
    public static MainPlantStatusView Instance;

    [Header("อ้างอิงข้อมูลพืช")]
    public PlantStatus plantStatus;

    [Header("UI Text & Images")]
    public TextMeshProUGUI plantNameText;
    public Image moistureFillBar;          // หลอดสีฟ้า (Moisture_Fill)
    public Image nutrientsFillBar;         // หลอดสีแดง (Nutrient_Fill)

    [Header("UI Buttons")]
    public Button waterButton;             // ปุ่ม Water_Button
    public GameObject cooldownWaterButtonObj; // ปุ่ม Cooldown_Water_Button
    public TextMeshProUGUI cooldownText;   // Text Cooldown
    public Button nourishButton;           // ปุ่ม Nourish_Button

    [Header("UI Background / Overlay ปิดหน้าต่าง")]
    public Button closeOverlayButton;      // ปุ่ม CloseOverlay (ล่องหน)

    [Header("UI Popups")]
    public GameObject inventoryBoxUI;

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

        if (closeOverlayButton != null)
            closeOverlayButton.onClick.AddListener(CloseView);

        if (plantStatus == null)
        {
            plantStatus = FindObjectOfType<PlantStatus>();
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

    // 💡 ฟังก์ชันเปิดหน้ารดน้ำ (กดทีเดียวติด ไม่ต้องใส่ parameter)
    public void OpenView()
    {
        if (plantStatus == null)
        {
            plantStatus = FindObjectOfType<PlantStatus>();
        }

        if (plantStatus != null)
        {
            plantStatus.LoadData(); // โหลดข้อมูลล่าสุดจาก PlayerPrefs
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

    public void UpdateUI()
    {
        if (plantStatus == null) return;

        if (plantNameText != null)
            plantNameText.text = plantStatus.plantName;

        // คำนวณหลอดสีฟ้า (ความชุ่มชื้น)
        if (moistureFillBar != null)
        {
            float fillRatio = plantStatus.currentMoisture / plantStatus.maxMoisture;
            moistureFillBar.fillAmount = Mathf.Clamp01(fillRatio);
        }

        // คำนวณหลอดสีแดง (สารอาหาร)
        if (nutrientsFillBar != null)
        {
            float fillRatio = plantStatus.currentNutrients / plantStatus.maxNutrients;
            nutrientsFillBar.fillAmount = Mathf.Clamp01(fillRatio);
        }
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

    private void OnNourishButtonClicked()
    {
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        if (inventoryBoxUI != null)
        {
            inventoryBoxUI.SetActive(true);
        }
    }
}