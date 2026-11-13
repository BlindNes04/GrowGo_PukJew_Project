using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ==================== 🍒 Cherry: Popup เลือกสายพันธุ์ (ร่าง 4) ====================
// ใส่สคริปต์นี้ที่ GameObject "CherryFormPopup" (ตัว popup ใน Canvas)
// หน้าที่:
//   1. ปุ่ม CherryForm (ลูกของ TopRightMenuView) โชว์เฉพาะตอนกำลังเลี้ยง Cherry
//   2. รูป seed บนปุ่ม เปลี่ยนตามสายพันธุ์ที่เลือก (เฉพาะตอนถึงร่าง 4)
//   3. ในหน้า popup การ์ด 3 ใบ (LILY / SAKURA / VIOLET) เป็นเงาดำล็อกไว้จนกว่า Cherry จะถึงร่าง 4
//   4. กดการ์ด -> สั่ง PlantStatus.ChangeCherryForm() ให้เปลี่ยนร่าง (เปลี่ยนได้ตลอด ไม่จำกัด)
public class CherryFormPopup : MonoBehaviour
{
    [System.Serializable]
    public class CherryFormCard
    {
        [Tooltip("stage_id ใน DB เช่น STAGE_CH_EVO_LILY")]
        public string stageId;
        public Button cardButton;   // การ์ด (CardLily / CardSakura / CardViolet)
        public Image seedImage;     // ลูก "seed" ในการ์ด (รูปนี้จะถูกใช้กับปุ่ม CherryForm ด้วย)
    }

    [Header("ปุ่มเปิด popup (CherryForm ใน TopRightMenuView)")]
    public Button cherryFormButton;
    public Image cherryFormSeedImage;   // ลูก "seed" ของปุ่ม CherryForm
    [Tooltip("รูป seed ตอนยังไม่ถึงร่าง 4 (ปล่อยว่าง = ใช้รูปที่ตั้งไว้ในซีนตอนแรก)")]
    public Sprite defaultSeedSprite;

    [Header("การ์ดสายพันธุ์ใน popup")]
    public List<CherryFormCard> cards = new List<CherryFormCard>();
    public Button closeButton;

    [Header("สีการ์ด")]
    public Color lockedColor = Color.black;                         // เงาดำตอนล็อก
    public Color unlockedColor = Color.white;                       // สีปกติ
    public Color notSelectedColor = new Color(1f, 1f, 1f, 0.6f);    // ร่างที่ไม่ได้เลือก (จางลงนิดนึง)

    [Header("Plant Status Reference")]
    public PlantStatus plantStatus;

    private bool isSetup = false;

    // ── ผูกปุ่มครั้งเดียว (เรียกได้แม้ตัว popup ยังปิดอยู่) ──
    private void SetupOnce()
    {
        if (isSetup) return;
        isSetup = true;

        if (plantStatus == null) plantStatus = FindAnyObjectByType<PlantStatus>();

        // จำรูป seed ตั้งต้นของปุ่มไว้ ถ้าไม่ได้ใส่ใน Inspector
        if (defaultSeedSprite == null && cherryFormSeedImage != null)
            defaultSeedSprite = cherryFormSeedImage.sprite;

        if (cherryFormButton != null) cherryFormButton.onClick.AddListener(OpenPopup);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePopup);

        foreach (CherryFormCard card in cards)
        {
            if (card.cardButton == null) continue;
            string id = card.stageId; // copy ไว้ใช้ใน lambda
            card.cardButton.onClick.AddListener(() => OnCardClicked(id));
        }
    }

    private void Awake()
    {
        SetupOnce();

        // 👁 ตอนแก้ซีนตั้ง CanvasGroup Alpha = 0 ไว้ซ่อนไม่ให้บัง Simulator ได้ (object ต้อง active)
        //    พอเริ่มเกมคืนค่า Alpha = 1 ให้ตรงนี้ ตอนเปิดหน้าต่างจริงจะได้มองเห็น
        CanvasGroup editorHideGroup = GetComponent<CanvasGroup>();
        if (editorHideGroup != null) editorHideGroup.alpha = 1f;

        // popup ต้องปิดไว้ตอนเริ่มเกม (เปิดเมื่อกดปุ่ม CherryForm)
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        RefreshCards();
    }

    // ==================== เปิด / ปิด popup ====================
    public void OpenPopup()
    {
        gameObject.SetActive(true);
        RefreshCards();
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }

    // 🍒 รูป seed ของสายพันธุ์ร่าง 4 (LILY / SAKURA / VIOLET) ตาม stage_id ไม่เจอ = null
    //    MainPlantStatusView ใช้โชว์รูปเมล็ดในหน้าสเตตัสพืช
    public Sprite GetSeedSpriteForStage(string stageId)
    {
        CherryFormCard card = cards.Find(c => c.stageId == stageId);
        return (card != null && card.seedImage != null) ? card.seedImage.sprite : null;
    }

    // ==================== ปุ่ม CherryForm (มุมขวาบน) ====================
    // PlantStatus.SyncCharacterVisual() เรียกทุกครั้งที่โหลดต้นไม้ / เปลี่ยนร่าง / สลับต้น
    public void RefreshCherryButton(PlantStatus plant)
    {
        SetupOnce();
        if (plant != null) plantStatus = plant;
        if (plantStatus == null) return;

        // 1. โชว์ปุ่มเฉพาะตอนเลี้ยง Cherry อยู่ (ทุกร่าง)
        bool isCherry = plantStatus.speciesId == "SPEC_CHERRY" && !string.IsNullOrEmpty(plantStatus.playerPlantId);
        if (cherryFormButton != null) cherryFormButton.gameObject.SetActive(isCherry);

        // ไม่ใช่ Cherry -> ปิด popup ด้วยถ้าเปิดค้างอยู่
        if (!isCherry && gameObject.activeSelf) ClosePopup();

        // 2. รูป seed บนปุ่ม: ร่าง 4 ใช้รูปของสายพันธุ์ปัจจุบัน / ยังไม่ถึงร่าง 4 ใช้รูปตั้งต้น
        if (cherryFormSeedImage != null)
        {
            Sprite seedSprite = defaultSeedSprite;
            if (plantStatus.IsCherryAtStage4())
            {
                CherryFormCard current = cards.Find(c => c.stageId == plantStatus.currentStageId);
                if (current != null && current.seedImage != null) seedSprite = current.seedImage.sprite;
            }
            cherryFormSeedImage.sprite = seedSprite;
        }

        // ถ้า popup เปิดอยู่ อัปเดตการ์ดตามไปด้วย
        if (gameObject.activeInHierarchy) RefreshCards();
    }

    // ==================== การ์ดใน popup ====================
    // ยังไม่ถึงร่าง 4 = เงาดำ + กดไม่ได้ / ถึงร่าง 4 = กดเลือกได้ ร่างที่ใช้อยู่สีเต็ม ร่างอื่นจางลง
    public void RefreshCards()
    {
        if (plantStatus == null) plantStatus = FindAnyObjectByType<PlantStatus>();
        bool unlocked = plantStatus != null && plantStatus.IsCherryAtStage4();

        foreach (CherryFormCard card in cards)
        {
            bool isCurrent = unlocked && plantStatus.currentStageId == card.stageId;

            if (card.seedImage != null)
            {
                if (!unlocked) card.seedImage.color = lockedColor;
                else card.seedImage.color = isCurrent ? unlockedColor : notSelectedColor;
            }

            if (card.cardButton != null) card.cardButton.interactable = unlocked;
        }
    }

    private void OnCardClicked(string stageId)
    {
        if (plantStatus == null || !plantStatus.IsCherryAtStage4()) return;

        // PlantStatus เป็นคนเปลี่ยนร่าง + เซฟ DB + ปลดล็อก Plant Dex
        // แล้วมันจะเรียก RefreshCherryButton() กลับมาเอง (รูป seed + การ์ดอัปเดต)
        plantStatus.ChangeCherryForm(stageId);
    }
}
