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

        if (locationText != null)
        {
            int locId = GetLocationIdForData(currentItem);
            locationText.text = GetLocationName(locId);
        }

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

    // 🟢 4. เมื่อกดปุ่ม "เลือก/ใช้" ไอเทม
    public void OnClickSelectButton()
    {
        if (currentSelectedItem == -1 || currentSelectedItem >= loadedInventory.Count) return;

        SupabaseInventoryItem selectedInvItem = loadedInventory[currentSelectedItem];
        string itemId = selectedInvItem.item.item_id;

        // 💡 ถ้าเป็นเมล็ดพันธุ์ ให้เข้าสู่ฟังก์ชันปลูกพืช
        if (!string.IsNullOrEmpty(itemId) && itemId.StartsWith("ITEM_SEED_"))
        {
            UseSeedToPlant(itemId);
        }
        else
        {
            ShowFloatingTextPopup(currentSelectedItem);
            CloseDetail();
        }
    }

    // 🟢 5. กระบวนการใช้เมล็ด: หักเมล็ดใน DB -> สร้าง player_plant -> รีเฟรชกระเป๋าทันที
    private void UseSeedToPlant(string seedItemId)
    {
        if (SupabaseManager.Instance == null) return;

        string initialStageId = MapSeedToInitialStage(seedItemId);
        string defaultNickname = MapSeedToDefaultNickname(seedItemId);

        Debug.Log($"[MainInventoryManager] 🔄 กำลังใช้เมล็ด {seedItemId} (Stage: {initialStageId})...");

        // 1. หักเมล็ดออกจาก DB (-1) (ถ้าจำนวนเหลือ 0 จะลบแถวใน DB ให้อัตโนมัติ)
        StartCoroutine(SupabaseManager.Instance.AddItemToInventory(
            seedItemId,
            -1,
            onSuccess: () =>
            {
                Debug.Log($"[MainInventoryManager] 🟢 หักเมล็ด {seedItemId} สำเร็จ!");

                // 2. สร้างพืชใหม่ลงตาราง player_plant
                StartCoroutine(SupabaseManager.Instance.CreatePlayerPlant(
                    initialStageId,
                    defaultNickname,
                    onSuccess: () =>
                    {
                        Debug.Log($"[MainInventoryManager] 🟢 สร้างพืชใหม่ {initialStageId} ลง player_plant สำเร็จ!");

                        // 3. รีเฟรช UI กระเป๋าทันทีเพื่อให้จำนวนไอเทมลดลง/หายไปจากหน้าจอ
                        FetchInventoryAndRender();
                        CloseDetail();
                    },
                    onError: (err) => Debug.LogError($"[MainInventoryManager] 🔴 สร้าง player_plant ล้มเหลว: {err}")
                ));
            },
            onError: (err) => Debug.LogError($"[MainInventoryManager] 🔴 หักเมล็ดล้มเหลว: {err}")
        ));
    }

    private string MapSeedToInitialStage(string seedItemId)
    {
        string upperSeed = seedItemId.ToUpper();

        if (upperSeed.Contains("COCONUT") || upperSeed.Contains("CC")) return "STAGE_CC_01";
        if (upperSeed.Contains("TOMATO") || upperSeed.Contains("TM")) return "STAGE_TM_01";
        if (upperSeed.Contains("MELON") || upperSeed.Contains("ML")) return "STAGE_ML_01";
        if (upperSeed.Contains("CHERRY") || upperSeed.Contains("CH")) return "STAGE_CH_01";

        return "STAGE_TM_01";
    }

    private string MapSeedToDefaultNickname(string seedItemId)
    {
        string upperSeed = seedItemId.ToUpper();

        if (upperSeed.Contains("COCONUT") || upperSeed.Contains("CC")) return "มะพร้าวของฉัน";
        if (upperSeed.Contains("TOMATO") || upperSeed.Contains("TM")) return "มะเขือเทศของฉัน";
        if (upperSeed.Contains("MELON") || upperSeed.Contains("ML")) return "เมล่อนของฉัน";
        if (upperSeed.Contains("CHERRY") || upperSeed.Contains("CH")) return "เชอร์รี่ของฉัน";

        return "ต้นไม้ของฉัน";
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
    // 🧠 SMART MATCHING FUNCTIONS
    // ==========================================

    private Sprite GetItemSpriteSmart(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        var exactMatch = itemSprites.Find(x => x.itemId == itemId);
        if (exactMatch != null && exactMatch.itemSprite != null)
            return exactMatch.itemSprite;

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

    public string GetLocationName(int locationId)
    {
        var found = locationCatalog.Find(x => x.locationId == locationId);
        return found != null ? found.locationName : "พบได้จากกิจกรรมทั่วไป";
    }

    public Sprite GetLocationIcon(int locationId)
    {
        var found = locationCatalog.Find(x => x.locationId == locationId);
        return found != null ? found.locationIcon : null;
    }

    private int GetLocationIdForData(SupabaseInventoryItem invItem)
    {
        if (invItem == null || invItem.item == null) return 0;

        var match = itemSprites.Find(x => x.itemId == invItem.item.item_id);
        if (match != null) return match.defaultLocationId;

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