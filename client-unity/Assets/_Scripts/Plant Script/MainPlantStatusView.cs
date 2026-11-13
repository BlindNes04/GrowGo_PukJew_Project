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

    [Header("🌱 รูปเมล็ดด้านซ้าย เปลี่ยนตามสายพันธุ์")]
    [Tooltip("ว่างไว้ได้ จะหาลูกชื่อ PlantIcon เอง")]
    public Image plantSeedIcon;
    [Tooltip("ว่างไว้ได้ จะหาเองในฉาก (ใช้รูปเมล็ด ITEM_SEED_* จาก itemSprites ของกระเป๋า)")]
    public MainInventoryManager inventoryManager;

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
            CloseWhenTapOutside();
        }
    }

    // ==================== 👆 แตะนอกหน้าสเตตัส = ปิด ====================
    // แตะตรงไหนก็ได้นอกกรอบหน้านี้ (รวมปุ่มอื่น เช่น กระเป๋า ร้านค้า เมนู) -> ปิดหน้านี้ก่อน แล้วปุ่มนั้นทำงานต่อตามปกติ
    // หน้าต่างเลยไม่ซ้อนกัน (แตะที่ว่างผ่าน CloseOverlay ก็ยังปิดได้เหมือนเดิม)
    // ยกเว้นตอนป๊อปอัปที่เปิดจากหน้านี้ยังเปิดอยู่ (บำรุง / เปลี่ยนชื่อ / แจ้งพืชป่วย) ให้ป๊อปอัปนั้นจัดการเอง
    private void CloseWhenTapOutside()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        bool childPopupOpen =
            (nourishPopupUI != null && nourishPopupUI.activeInHierarchy) ||
            (renamePlantPopup != null && renamePlantPopup.gameObject.activeInHierarchy) ||
            (plantSickPopup != null && plantSickPopup.activeInHierarchy);
        if (childPopupOpen) return;

        // แตะโดนอะไรก็ได้ที่อยู่ในหน้านี้ (ตัวกรอบ หรือลูกทุกตัว เช่น ปุ่มรดน้ำ/บำรุง ที่วางยื่นออกนอกกรอบ) = ไม่ปิด
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        foreach (RectTransform part in GetComponentsInChildren<RectTransform>())
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(part, Input.mousePosition, cam)) return;
        }

        CloseView();
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

    // ==================== 🌱 รูปเมล็ดตามสายพันธุ์ ====================
    // แปลง species_id เป็น item_id ของเมล็ด (SPEC_MELON -> ITEM_SEED_MELON)
    // แล้วใช้รูปจาก itemSprites ของกระเป๋าหลัก (ชุดเดียวกับที่โชว์ในกระเป๋า ไม่ต้องลากรูปซ้ำ)
    // ยกเว้นเชอร์รี่ร่าง 4 ใช้เมล็ดของสายพันธุ์ที่เลือกจาก CherryFormPopup
    private void UpdateSeedIcon()
    {
        if (plantSeedIcon == null)
        {
            Transform icon = transform.Find("PlantIcon");
            if (icon != null) plantSeedIcon = icon.GetComponent<Image>();
        }
        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<MainInventoryManager>(FindObjectsInactive.Include);
        if (plantSeedIcon == null || inventoryManager == null || string.IsNullOrEmpty(plantStatus.speciesId)) return;

        string seedItemId = plantStatus.speciesId.Replace("SPEC_", "ITEM_SEED_");
        Sprite seedSprite = inventoryManager.GetItemSpriteSmart(seedItemId);

        // 🍒 เชอร์รี่ร่าง 4: ใช้เมล็ดของสายพันธุ์ที่เลือก (LILY / SAKURA / VIOLET) ชุดเดียวกับปุ่ม CherryForm
        if (plantStatus.IsCherryAtStage4())
        {
            CherryFormPopup cherryPopup = plantStatus.cherryFormPopup != null
                ? plantStatus.cherryFormPopup
                : FindAnyObjectByType<CherryFormPopup>(FindObjectsInactive.Include);
            Sprite formSeed = cherryPopup != null ? cherryPopup.GetSeedSpriteForStage(plantStatus.currentStageId) : null;
            if (formSeed != null) seedSprite = formSeed;
        }

        if (seedSprite != null) plantSeedIcon.sprite = seedSprite;   // หาไม่เจอ = ใช้รูปเดิมในฉาก
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

        // 🌱 รูปเมล็ดด้านซ้ายตามสายพันธุ์ของต้นที่อยู่บนแท่น
        UpdateSeedIcon();

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