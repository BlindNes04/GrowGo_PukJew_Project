using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ==================== PlantStoreController (หน้าร้านค้า PlantStore) ====================
// หน้าที่:
//   1) โหลดของในร้านจาก shop_item แล้วสร้างช่อง (ShopItemSlot) ลงใน ScrollView
//   2) กดซื้อ -> เรียก SupabaseManager.BuyShopItem (DB เช็คแต้ม/หักแต้ม/สุ่ม/ใส่กระเป๋าให้)
//   3) ปุ่ม "แนบวุฒิบัตร" -> เลือกรูปจากคลังรูป (NativeGallery) -> ส่งไปตรวจ OCR -> ได้ green_points
//   4) โชว์แต้มดอกไม้/แต้มเขียวของผู้เล่น
//   5) โชว์ผลใน ItemCollectionPopup ทีละกล่อง (GreenPointBox / RewardPoint / SorryBox / SorryBox (1))
public class PlantStoreController : MonoBehaviour
{
    // ---------- รายการของในร้าน ----------
    [Header("🛒 รายการของ (ScrollView)")]
    public Transform contentParent;          // Content ของ ScrollView
    public ShopItemSlot slotPrefab;          // ช่องต้นแบบ (prefab หรือ ShopItemSlot ที่วางไว้ใน Content ก็ได้)

    [Serializable]
    public class IconEntry
    {
        public string id;                    // shop_item_id (เช่น POOL_NORMAL) หรือ item_id (เช่น ITEM_SEED_MELON)
        public Sprite sprite;
    }
    [Tooltip("รูปของแต่ละอย่าง ใส่ได้ทั้ง shop_item_id (รูปในร้าน) และ item_id (รูปของที่สุ่มได้)")]
    public List<IconEntry> icons = new List<IconEntry>();
    public Sprite flowerPointIcon;           // ไอคอนแต้มดอกไม้
    public Sprite greenPointIcon;            // ไอคอนแต้มเขียว

    // ---------- แต้มของผู้เล่น ----------
    [Header("🌸 แต้มผู้เล่น (Point > Florist/Green > Point)")]
    public TextMeshProUGUI flowerPointText;
    public TextMeshProUGUI greenPointText;
    public TextMeshProUGUI relationshipText; // หัวใจของต้นที่เลือกอยู่ (โชว์เฉยๆ เหมือนหน้าหลัก ไม่ได้ใช้ซื้อ)

    // ---------- วุฒิบัตร ----------
    [Header("📜 แนบวุฒิบัตร (OCR)")]
    public Button attachCertificateButton;   // CerBtn
    public TextMeshProUGUI attachButtonText; // ข้อความบน CerBtn (เปลี่ยนเป็น "กำลังตรวจ..." ตอนรอ)
    public GameObject picturePill;           // PicturePill "เลือกจาก รูปภาพ" (โผล่หลังกด CerBtn)
    public Button picturePillButton;         // ปุ่มใน PicturePill
    public int maxImageSize = 1600;          // ย่อรูปก่อนส่ง (px ด้านยาวสุด) ให้ส่งเร็วขึ้น

    // ---------- Popup ผลลัพธ์ (ItemCollectionPopup) ----------
    [Header("💬 Popup ผลลัพธ์ (ItemCollectionPopup)")]
    public GameObject popupRoot;             // ItemCollectionPopup (พื้นหลังมืด)

    [Tooltip("GreenPointBox : แลกวุฒิบัตรสำเร็จ")]
    public GameObject greenPointBox;

    [Tooltip("RewardPoint : ซื้อของสำเร็จ")]
    public GameObject rewardBox;
    public Image rewardIcon;                     // Icon (รูปของที่ได้)
    public TextMeshProUGUI rewardGotText;        // You Got Txt "ชื่อไอเทม x1"

    [Tooltip("SorryBox : วุฒิบัตรไม่ผ่าน")]
    public GameObject certFailBox;

    [Tooltip("CantBuyBox : ซื้อไม่สำเร็จ / แต้มไม่พอ")]
    public GameObject buyFailBox;
    public TextMeshProUGUI buyFailText;          // You Got Txt (เปลี่ยนเป็น "แต้มดอกไม้/Green Point ไม่เพียงพอ")

    [Tooltip("ปุ่มปิดของทุกกล่อง (CloseBtn)")]
    public List<Button> closeButtons = new List<Button>();

    private readonly List<ShopItemSlot> spawnedSlots = new List<ShopItemSlot>();
    private Dictionary<string, List<SupabaseManager.ShopDropData>> dropsByShopItem = new Dictionary<string, List<SupabaseManager.ShopDropData>>();
    private bool isBusy = false;
    private string attachIdleText = "";

    // ==================== เริ่มต้น ====================
    void Start()
    {
        HidePopup();
        if (picturePill != null) picturePill.SetActive(false);
        foreach (var b in closeButtons) if (b != null) b.onClick.AddListener(HidePopup);

        // CerBtn = เปิด/ปิด PicturePill, กด PicturePill = เปิดคลังรูป
        if (attachCertificateButton != null) attachCertificateButton.onClick.AddListener(OnAttachCertificateClicked);
        if (picturePillButton != null) picturePillButton.onClick.AddListener(OnPickPictureClicked);
        if (attachButtonText != null) attachIdleText = attachButtonText.text;

        // แต้มผู้เล่น: ฟัง event จาก SupabaseManager (เปลี่ยนเมื่อซื้อ/ได้แต้ม)
        SupabaseManager.OnPlayerStatsChanged += RefreshPoints;
        if (!SupabaseManager.Instance.playerStatsLoaded) SupabaseManager.Instance.LoadPlayerStats();
        RefreshPoints();
        LoadRelationship();

        // โหลดโอกาสสุ่มก่อน แล้วค่อยโหลดของในร้าน
        StartCoroutine(SupabaseManager.Instance.GetShopDrops(OnDropsLoaded, err => StartCoroutine(SupabaseManager.Instance.GetShopItems(BuildShop))));
    }

    void OnDestroy()
    {
        SupabaseManager.OnPlayerStatsChanged -= RefreshPoints;
    }

    private void RefreshPoints()
    {
        var db = SupabaseManager.Instance;
        if (flowerPointText != null) flowerPointText.text = db.playerFlowerPoints.ToString();
        if (greenPointText != null) greenPointText.text = db.playerGreenPoints.ToString();
    }

    // หัวใจเป็นของต้นไม้แต่ละต้น (player_plant.relationship_score)
    // ใช้ต้นที่เลือกอยู่ล่าสุด (PlantStatus เซฟ id ไว้ใน PlayerPrefs "Plant_PlayerPlantId")
    private void LoadRelationship()
    {
        if (relationshipText == null) return;
        string plantId = PlayerPrefs.GetString("Plant_PlayerPlantId", "");
        if (string.IsNullOrEmpty(plantId)) { relationshipText.text = "0"; return; }

        // 📦 โชว์หัวใจจาก cache ก่อน (ไม่ต้องเห็นค่า default) แล้วค่อยดึง DB ใหม่ด้านล่าง
        SupabasePlayerPlant cachedPlant = SupabaseManager.Instance.GetCachedPlant(plantId);
        if (cachedPlant != null) relationshipText.text = cachedPlant.relationship_score.ToString();

        StartCoroutine(SupabaseManager.Instance.GetRest($"player_plant?select=relationship_score&player_plant_id=eq.{plantId}", json =>
        {
            var rows = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            relationshipText.text = (rows != null && rows.Count > 0 && rows[0]["relationship_score"] != null)
                ? rows[0]["relationship_score"].ToString() : "0";
        }));
    }

    // ==================== สร้างรายการของในร้าน ====================
    private void OnDropsLoaded(List<SupabaseManager.ShopDropData> drops)
    {
        dropsByShopItem.Clear();
        foreach (var d in drops)
        {
            if (!dropsByShopItem.ContainsKey(d.shop_item_id)) dropsByShopItem[d.shop_item_id] = new List<SupabaseManager.ShopDropData>();
            dropsByShopItem[d.shop_item_id].Add(d);
        }
        StartCoroutine(SupabaseManager.Instance.GetShopItems(BuildShop));
    }

    private void BuildShop(List<SupabaseManager.ShopItemData> items)
    {
        // ลบช่องเก่า (กรณีโหลดใหม่)
        foreach (var s in spawnedSlots) if (s != null) Destroy(s.gameObject);
        spawnedSlots.Clear();

        foreach (var item in items)
        {
            ShopItemSlot slot = Instantiate(slotPrefab, contentParent);
            slot.gameObject.SetActive(true);
            Sprite icon = GetIcon(item.shop_item_id) ?? GetIcon(item.target_item_id);
            Sprite currency = item.price_type == "green_points" ? greenPointIcon : flowerPointIcon;
            slot.Setup(item, icon, currency, BuildDescription(item), OnBuyClicked);
            // 🐻 หมี: โชว์ราคาเมล็ดหลังลด
            int bearDiscount = GetBearDiscount(item);
            if (bearDiscount > 0 && slot.priceText != null) slot.priceText.text = $"{item.price_amount - bearDiscount} (-{Mathf.RoundToInt(BuffManager.Instance.activeBuddySkillValue * 100)}%)";
            spawnedSlots.Add(slot);
        }
        // ถ้าใช้ ShopItemSlot ที่วางไว้ใน Content เป็นต้นแบบ (ไม่ได้ทำเป็น prefab) ให้ซ่อนตัวต้นแบบไว้
        if (slotPrefab.transform.parent == contentParent) slotPrefab.gameObject.SetActive(false);
        Debug.Log($"[Store] 🛒 โหลดของในร้าน {items.Count} ชิ้น");
    }

    // คำอธิบายในช่อง: กล่องสุ่มจะต่อท้ายด้วยโอกาสสุ่มจาก shop_item_drop
    private string BuildDescription(SupabaseManager.ShopItemData item)
    {
        StringBuilder sb = new StringBuilder(item.description ?? "");
        if (item.item_type == "GACHA_POOL" && dropsByShopItem.TryGetValue(item.shop_item_id, out var drops))
        {
            sb.Append("\n");
            foreach (var d in drops) sb.Append($"\n{(d.item != null ? d.item.item_name : d.item_id)} {d.chance_display}");
        }
        return sb.ToString();
    }

    private Sprite GetIcon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var e in icons) if (e != null && e.id == id) return e.sprite;
        return null;
    }

    // ==================== ซื้อของ ====================
    private void OnBuyClicked(SupabaseManager.ShopItemData item)
    {
        if (isBusy) return;

        // เช็คแต้มในเครื่องก่อน (DB จะเช็คซ้ำอีกรอบ)
        var db = SupabaseManager.Instance;
        int have = item.price_type == "green_points" ? db.playerGreenPoints : db.playerFlowerPoints;
        if (have < item.price_amount - GetBearDiscount(item))   // 🐻 เทียบกับราคาหลังลด (ตรงกับที่ DB คิด)
        {
            ShowBuyFail(NotEnoughText(item.price_type));
            return;
        }

        SetBusy(true);
        StartCoroutine(db.BuyShopItem(item.shop_item_id, result =>
        {
            SetBusy(false);
            if (result.success) GiveSeedBuddyBonus(item, result);   // 🐿️ กระรอก: ได้เมล็ดแล้วได้แต้มคืน
            if (result.success) ShowReward(result.got_item_name, GetIcon(result.got_item_id));
            else ShowBuyFail(TranslateBuyMessage(result.message));
        },
        err =>
        {
            SetBusy(false);
            ShowBuyFail("เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ลองใหม่อีกครั้ง");
        }));
    }

    // ==================== 🐾 สกิลบัดดี้ในร้านค้า ====================
    // 🐻 หมี SEED_PRICE_DISCOUNT: ใช้แต้มดอกไม้ซื้อเมล็ดถูกลง (ค่าในตาราง buddy = 0.1 = 10%)
    //    ราคาจริงหักใน RPC buy_shop_item ที่ DB (เช็กบัดดี้ของผู้เล่นเองฝั่งเซิร์ฟเวอร์ โกงจากเครื่องไม่ได้)
    //    ฝั่งเกมแค่โชว์ราคาลด + เช็กแต้มก่อนกดซื้อ ให้ตรงกับที่ DB คิด
    //    ลดเฉพาะ: ซื้อเมล็ดตรงๆ (DIRECT_ITEM ที่ได้ ITEM_SEED_) และจ่ายด้วยแต้มดอกไม้ (เชอร์รี่ใช้แต้มเขียว ไม่ลด)
    private int GetBearDiscount(SupabaseManager.ShopItemData item)
    {
        BuffManager buff = BuffManager.Instance;
        bool isFlowerSeed = item.item_type == "DIRECT_ITEM" && item.price_type == "flower_points"
                            && !string.IsNullOrEmpty(item.target_item_id) && item.target_item_id.StartsWith("ITEM_SEED_");
        if (!buff.Has(SkillType.SEED_PRICE_DISCOUNT) || !isFlowerSeed) return 0;
        return item.price_amount - Mathf.RoundToInt(item.price_amount * (1f - buff.activeBuddySkillValue));
    }

    // ซื้อสำเร็จ: 🐿️ กระรอก SEED_CASHBACK ได้เมล็ด -> ได้แต้มดอกไม้คืน (ค่าในตาราง buddy = 5)
    private void GiveSeedBuddyBonus(SupabaseManager.ShopItemData item, SupabaseManager.BuyShopResult result)
    {
        var db = SupabaseManager.Instance;
        BuffManager buff = BuffManager.Instance;

        if (buff.Has(SkillType.SEED_CASHBACK) && !string.IsNullOrEmpty(result.got_item_id) && result.got_item_id.StartsWith("ITEM_SEED_"))
        {
            db.AddPlayerExpAndFlower(0, Mathf.RoundToInt(buff.activeBuddySkillValue));
            Debug.Log($"[Buddy] 🐿️ กระรอกให้แต้มดอกไม้คืน {buff.activeBuddySkillValue}");
        }
    }

    private string TranslateBuyMessage(string message)
    {
        switch (message)
        {
            case "Not enough flower points": return NotEnoughText("flower_points");
            case "Not enough green points": return NotEnoughText("green_points");
            case "User not found": return "ไม่พบข้อมูลผู้เล่น";
            default: return message;
        }
    }

    private string NotEnoughText(string priceType)
    {
        return priceType == "green_points" ? "แต้ม Green Point ไม่เพียงพอ" : "แต้มดอกไม้ไม่เพียงพอ";
    }

    // ==================== แนบวุฒิบัตร (OCR) ====================
    // กด CerBtn -> โชว์/ซ่อน PicturePill "เลือกจาก รูปภาพ"
    private void OnAttachCertificateClicked()
    {
        if (isBusy) return;
        if (picturePill == null) { OnPickPictureClicked(); return; }   // ไม่มี pill ก็เปิดคลังรูปเลย
        picturePill.SetActive(!picturePill.activeSelf);
    }

    // กด PicturePill -> เปิดคลังรูป แล้วส่งไปตรวจ
    private void OnPickPictureClicked()
    {
        if (picturePill != null) picturePill.SetActive(false);
        if (isBusy || NativeGallery.IsMediaPickerBusy()) return;

        // เปิดคลังรูปในมือถือ (ใน Editor จะเป็นหน้าต่างเลือกไฟล์)
        NativeGallery.GetImageFromGallery(path =>
        {
            if (string.IsNullOrEmpty(path)) return;   // ผู้เล่นกดยกเลิก

            // โหลดรูป + ย่อขนาด แล้วแปลงเป็น JPG
            Texture2D tex = NativeGallery.LoadImageAtPath(path, maxImageSize, false);
            if (tex == null)
            {
                ShowCertFail("เปิดรูปไม่ได้ ลองเลือกรูปอื่นอีกครั้ง");
                return;
            }
            byte[] bytes = tex.EncodeToJPG(85);
            Destroy(tex);

            SetBusy(true, "กำลังตรวจ...");
            StartCoroutine(SupabaseManager.Instance.SubmitCertificate(bytes, "certificate.jpg", result =>
            {
                SetBusy(false);
                if (result.success) ShowGreenPoint(result.points_awarded);
                else ShowCertFail(result.reason);   // เหตุผลจากเซิร์ฟเวอร์ เช่น ใบนี้ถูกใช้ไปแล้ว
            },
            err =>
            {
                SetBusy(false);
                ShowCertFail("เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ลองใหม่อีกครั้ง");
            }));
        }, "เลือกรูปวุฒิบัตร", "image/*");
    }

    // ==================== UI ช่วย ====================
    // ระหว่างรอ DB/OCR: ปิดปุ่มทั้งหมด กันกดซ้ำ และเปลี่ยนข้อความบน CerBtn
    private void SetBusy(bool busy, string attachText = null)
    {
        isBusy = busy;
        foreach (var s in spawnedSlots) if (s != null) s.SetInteractable(!busy);
        if (attachCertificateButton != null) attachCertificateButton.interactable = !busy;
        if (attachButtonText != null) attachButtonText.text = (busy && attachText != null) ? attachText : attachIdleText;
    }

    // ---------- Popup: เปิดทีละกล่อง ----------
    private void ShowBox(GameObject box)
    {
        if (popupRoot != null) popupRoot.SetActive(true);
        if (greenPointBox != null) greenPointBox.SetActive(box == greenPointBox);
        if (rewardBox != null) rewardBox.SetActive(box == rewardBox);
        if (certFailBox != null) certFailBox.SetActive(box == certFailBox);
        if (buyFailBox != null) buyFailBox.SetActive(box == buyFailBox);
    }

    private void HidePopup()
    {
        if (greenPointBox != null) greenPointBox.SetActive(false);
        if (rewardBox != null) rewardBox.SetActive(false);
        if (certFailBox != null) certFailBox.SetActive(false);
        if (buyFailBox != null) buyFailBox.SetActive(false);
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    // แลกวุฒิบัตรสำเร็จ -> GreenPointBox (ข้อความตามที่ตั้งไว้ใน scene)
    private void ShowGreenPoint(int points)
    {
        ShowBox(greenPointBox);
    }

    // ซื้อสำเร็จ -> RewardPoint (รูป + ชื่อของที่ได้)
    private void ShowReward(string itemName, Sprite icon)
    {
        if (rewardGotText != null) rewardGotText.text = $"{itemName} x1";
        if (rewardIcon != null && icon != null) rewardIcon.sprite = icon;
        ShowBox(rewardBox);
    }

    // วุฒิบัตรไม่ผ่าน -> SorryBox (ข้อความตามที่ตั้งไว้ใน scene, เหตุผลดูได้ใน Console)
    private void ShowCertFail(string reason)
    {
        Debug.Log($"[Store] 📜 วุฒิบัตรไม่ผ่าน: {reason}");
        ShowBox(certFailBox);
    }

    // ซื้อไม่สำเร็จ -> CantBuyBox
    private void ShowBuyFail(string message)
    {
        Debug.Log($"[Store] 🛒 ซื้อไม่สำเร็จ: {message}");
        // ถ้าลืมลากใน Inspector ให้หา "You Got Txt" ในกล่องเอง
        if (buyFailText == null && buyFailBox != null)
        {
            Transform t = buyFailBox.transform.Find("You Got Txt");
            if (t != null) buyFailText = t.GetComponent<TextMeshProUGUI>();
        }
        // เปลี่ยนข้อความเฉพาะกรณีแต้มไม่พอ (ดอกไม้ / Green Point) กรณีอื่นใช้ข้อความเดิมใน scene
        if (buyFailText != null && message.Contains("ไม่เพียงพอ")) buyFailText.text = message;
        ShowBox(buyFailBox);
    }
}
