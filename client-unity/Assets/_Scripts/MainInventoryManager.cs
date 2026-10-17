using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainInventoryManager : MonoBehaviour
{
    [System.Serializable]
    public class ItemSpriteMapping
    {
        public string itemId;          // เช่น ITEM_ING_SANDPILE หรือใส่ Base ID เช่น ITEM_FERT_ALL
        public Sprite itemSprite;      // รูปไอคอนไอเทม
        public int defaultLocationId;  // (Option) รหัสสถานที่เริ่มต้นถ้าใน DB ไม่ระบุ
    }

    [System.Serializable]
    public class FrameSpriteMapping
    {
        public string rarityTier;      // เช่น grade_a, grade_b, silver, gold
        public Sprite frameSprite;     // รูปกรอบ Frame
    }

    [System.Serializable]
    public class LocationMapping
    {
        public int locationId;         // รหัสสถานที่ เช่น 0, 1, 2
        public string locationName;    // ข้อความแสดง เช่น "พบได้ที่สวนหลัก", "พบที่ชายหาดระยิบระยับ"
        public Sprite locationIcon;    // (Option) รูปไอคอนสถานที่/Hint
    }

    [Header("🎨 Sprite Catalog Database")]
    public List<ItemSpriteMapping> itemSprites;
    public List<FrameSpriteMapping> frameSprites;
    public Sprite defaultFrame;

    [Header("📍 Location / Hint Catalog (รหัสสถานที่หาไอเทม)")]
    public List<LocationMapping> locationCatalog;

    [Header("UI Grid & Slot Prefab")]
    public Transform itemGrid;         // วัตถุ ItemGrid
    public GameObject itemSlotPrefab;  // Prefab ItemSlot

    [Header("Item Detail UI")]
    public GameObject itemDetailBox;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text locationText;      // Text แสดงสถานที่พบ
    public Image detailIconImage;
    public Image hintIconImage;

    [Header("Popup Floating Text")]
    public TMP_Text floatingText;
    public float textOffsetY = -55f;
    public float popupDuration = 1.8f;

    private List<SupabaseInventoryItem> loadedInventory = new List<SupabaseInventoryItem>();
    private List<ItemSlotUI> instantiatedSlots = new List<ItemSlotUI>();
    private int currentSelectedItem = -1;
    private Coroutine hideTextCoroutine;

    private void OnEnable()
    {
        FetchInventoryAndRender();
    }

    // 🌐 1. ดึงข้อมูลจาก Supabase
    public void FetchInventoryAndRender()
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogWarning("[MainInventoryManager] ไม่พบ SupabaseManager ใน Scene!");
            return;
        }

        StartCoroutine(SupabaseManager.Instance.GetUserInventory(
            onSuccess: (json) =>
            {
                ParseAndBuildInventoryUI(json);
            },
            onError: (err) =>
            {
                Debug.LogError($"[MainInventoryManager] โหลดกระเป๋าไม่สำเร็จ: {err}");
            }
        ));
    }

    // 🎨 2. แปลง JSON แล้วสร้างช่อง Slot บน UI
    private void ParseAndBuildInventoryUI(string json)
    {
        if (itemGrid == null || itemSlotPrefab == null) return;

        foreach (Transform child in itemGrid)
        {
            Destroy(child.gameObject);
        }

        instantiatedSlots.Clear();
        loadedInventory.Clear();
        CloseDetail();

        SupabaseInventoryItem[] items = JsonHelper.FromJson<SupabaseInventoryItem>(json);
        if (items == null || items.Length == 0) return;

        int index = 0;
        foreach (var invItem in items)
        {
            if (invItem.quantity <= 0 || invItem.item == null) continue;

            loadedInventory.Add(invItem);

            // 💡 เรียกใช้ Smart Matcher ค้นหารูปแม้จะเป็นไอเทมหลายเกรด
            Sprite icon = GetItemSpriteSmart(invItem.item.item_id);
            Sprite frame = GetFrameSprite(invItem.item.rarity_tier);

            GameObject newSlotObj = Instantiate(itemSlotPrefab, itemGrid);
            ItemSlotUI slotUI = newSlotObj.GetComponent<ItemSlotUI>();

            if (slotUI != null)
            {
                slotUI.SetupSlot(index, icon, frame, invItem.quantity, this);
                instantiatedSlots.Add(slotUI);
            }

            index++;
        }
    }

    // 🖱️ 3. เมื่อคลิกเลือกไอเทม
    public void OnClickItemInGrid(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= loadedInventory.Count) return;

        if (currentSelectedItem == itemIndex)
        {
            CloseDetail();
            return;
        }

        currentSelectedItem = itemIndex;
        UpdateBBSelectionVisuals(itemIndex);

        SupabaseInventoryItem currentItem = loadedInventory[itemIndex];

        if (nameText != null) nameText.text = currentItem.item.item_name;
        if (descText != null) descText.text = currentItem.item.description;

        // 📍 แสดงสถานที่พบจาก Location Catalog
        if (locationText != null)
        {
            int locId = GetLocationIdForData(currentItem);
            locationText.text = GetLocationName(locId);
        }

        // 🖼️ รูปไอคอนไอเทม
        Sprite itemIcon = GetItemSpriteSmart(currentItem.item.item_id);
        if (detailIconImage != null)
        {
            if (itemIcon != null)
            {
                detailIconImage.sprite = itemIcon;
                detailIconImage.gameObject.SetActive(true);
            }
            else
            {
                detailIconImage.gameObject.SetActive(false);
            }
        }

        // 🖼️ รูปไอคอนสถานที่/Hint (ถ้ามี)
        if (hintIconImage != null)
        {
            int locId = GetLocationIdForData(currentItem);
            Sprite hintIcon = GetLocationIcon(locId);

            if (hintIcon != null)
            {
                hintIconImage.sprite = hintIcon;
                hintIconImage.gameObject.SetActive(true);
            }
            else
            {
                hintIconImage.gameObject.SetActive(false);
            }
        }

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(true);
        }
    }

    private void UpdateBBSelectionVisuals(int selectedIndex)
    {
        for (int i = 0; i < instantiatedSlots.Count; i++)
        {
            if (instantiatedSlots[i] != null)
            {
                instantiatedSlots[i].SetSelected(i == selectedIndex);
            }
        }
    }

    public void OnClickSelectButton()
    {
        if (currentSelectedItem == -1) return;

        ShowFloatingTextPopup(currentSelectedItem);
        CloseDetail();
    }

    private void ShowFloatingTextPopup(int itemIndex)
    {
        if (floatingText == null || itemGrid == null) return;
        if (itemIndex >= itemGrid.childCount) return;

        Transform targetSlot = itemGrid.GetChild(itemIndex);

        floatingText.transform.position = targetSlot.position;
        floatingText.transform.localPosition += new Vector3(0, textOffsetY, 0);
        floatingText.transform.SetAsLastSibling();

        SupabaseInventoryItem selectedItem = loadedInventory[itemIndex];

        string textToShow = !string.IsNullOrEmpty(selectedItem.item.description)
            ? selectedItem.item.description
            : selectedItem.item.item_name;

        floatingText.text = textToShow;
        floatingText.gameObject.SetActive(true);

        if (hideTextCoroutine != null)
        {
            StopCoroutine(hideTextCoroutine);
        }
        hideTextCoroutine = StartCoroutine(HideTextRoutine());
    }

    private IEnumerator HideTextRoutine()
    {
        yield return new WaitForSeconds(popupDuration);
        if (floatingText != null)
        {
            floatingText.gameObject.SetActive(false);
        }
    }

    public void CloseDetail()
    {
        currentSelectedItem = -1;
        UpdateBBSelectionVisuals(-1);

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(false);
        }
    }

    private void OnDisable()
    {
        CloseDetail();
        if (floatingText != null)
        {
            floatingText.gameObject.SetActive(false);
        }
    }

    // ==========================================
    // 🧠 SMART MATCHING FUNCTIONS (ฟังก์ชันค้นหาอัจฉริยะ)
    // ==========================================

    // 💡 1. ค้นหารูปไอคอน: ลองค้นตาม ID เต็มก่อน ถ้าไม่เจอจะตัด _A, _B ออกแล้วหาใหม่
    private Sprite GetItemSpriteSmart(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        // ค้นหาแบบตรงตัวก่อน (เช่น ITEM_ING_SANDPILE)
        var exactMatch = itemSprites.Find(x => x.itemId == itemId);
        if (exactMatch != null && exactMatch.itemSprite != null)
            return exactMatch.itemSprite;

        // ถ้าหาไม่เจอ และเป็นไอดีที่มี _ ต่อท้าย (เช่น ITEM_FERT_ALL_A -> ITEM_FERT_ALL)
        if (itemId.Contains("_"))
        {
            int lastUnderscore = itemId.LastIndexOf('_');
            string baseId = itemId.Substring(0, lastUnderscore);

            var baseMatch = itemSprites.Find(x => x.itemId == baseId);
            if (baseMatch != null && baseMatch.itemSprite != null)
                return baseMatch.itemSprite;
        }

        return null;
    }

    // 💡 2. ดึงชื่อสถานที่จาก Location Catalog ตาม Location ID
    public string GetLocationName(int locationId)
    {
        var found = locationCatalog.Find(x => x.locationId == locationId);
        return found != null ? found.locationName : "พบได้จากกิจกรรมทั่วไป";
    }

    // 💡 3. ดึงรูป Hint จาก Location Catalog
    public Sprite GetLocationIcon(int locationId)
    {
        var found = locationCatalog.Find(x => x.locationId == locationId);
        return found != null ? found.locationIcon : null;
    }

    // 💡 4. คำนวณหา Location ID ของไอเทม
    private int GetLocationIdForData(SupabaseInventoryItem invItem)
    {
        if (invItem == null || invItem.item == null) return 0;

        // ลองค้นหาใน Catalog ก่อน
        var match = itemSprites.Find(x => x.itemId == invItem.item.item_id);
        if (match != null) return match.defaultLocationId;

        // ถ้าเป็นปุ๋ย ให้เป็นสถานที่ 0 (สวนหลัก)
        if (invItem.item.item_type == "fertilizer") return 0;

        return 0;
    }

    private Sprite GetFrameSprite(string rarityTier)
    {
        if (string.IsNullOrEmpty(rarityTier)) return defaultFrame;
        var found = frameSprites.Find(x => x.rarityTier.ToLower() == rarityTier.ToLower());
        return found != null ? found.frameSprite : defaultFrame;
    }
}