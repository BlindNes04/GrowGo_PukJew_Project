using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NourishPopupView : MonoBehaviour
{
    [Header("UI Grid & Slot Prefab")]
    public Transform itemGrid;         // ลาก ItemGrid ของ Nourish_Popup มาใส่
    public GameObject itemSlotPrefab;  // ลาก Prefab ItemSlot มาใส่
    public Button closeButton;         // ลาก CloseBtn มาใส่

    [Header("Item Detail UI")]
    public GameObject itemDetailBox;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text locationText;
    public Image detailIconImage;
    public Image hintIconImage;
    public Button selectButton;        // ปุ่มกด "ใช้" ปุ๋ย

    [Header("Reference Main Inventory Catalog")]
    public MainInventoryManager mainInventoryManager; // ลาก MainInventoryManager มาวางเพื่อดึง Sprite Catalog

    private List<SupabaseInventoryItem> filteredInventory = new List<SupabaseInventoryItem>();
    private List<ItemSlotUI> instantiatedSlots = new List<ItemSlotUI>();
    private int currentSelectedItem = -1;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnUseItemClicked);
        }
    }

    private void OnEnable()
    {
        FetchNourishItemsAndRender();
    }

    // 🌐 1. ดึงข้อมูลกระเป๋าจาก Supabase และกรองเฉพาะของบำรุง
    public void FetchNourishItemsAndRender()
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogWarning("[NourishPopupView] ไม่พบ SupabaseManager ใน Scene!");
            return;
        }

        StartCoroutine(SupabaseManager.Instance.GetUserInventory(
            onSuccess: (json) =>
            {
                ParseAndRenderNourishSlots(json);
            },
            onError: (err) =>
            {
                Debug.LogError($"[NourishPopupView] โหลดข้อมูลไม่สำเร็จ: {err}");
            }
        ));
    }

    // 🎨 2. กรองข้อมูลเฉพาะ fertilizer / ingredient แล้วสร้างช่องบน UI
    private void ParseAndRenderNourishSlots(string json)
    {
        if (itemGrid == null || itemSlotPrefab == null) return;

        foreach (Transform child in itemGrid)
        {
            Destroy(child.gameObject);
        }

        instantiatedSlots.Clear();
        filteredInventory.Clear();
        CloseDetail();

        SupabaseInventoryItem[] allItems = JsonHelper.FromJson<SupabaseInventoryItem>(json);
        if (allItems == null || allItems.Length == 0) return;

        int index = 0;
        foreach (var invItem in allItems)
        {
            if (invItem.quantity <= 0 || invItem.item == null) continue;

            string type = invItem.item.item_type;

            if (type == "fertilizer" || type == "ingredient")
            {
                filteredInventory.Add(invItem);

                Sprite icon = GetItemSprite(invItem.item.item_id);
                Sprite frame = GetFrameSprite(invItem.item.rarity_tier);

                GameObject newSlotObj = Instantiate(itemSlotPrefab, itemGrid);
                ItemSlotUI slotUI = newSlotObj.GetComponent<ItemSlotUI>();

                if (slotUI != null)
                {
                    slotUI.SetupSlot(index, icon, frame, invItem.quantity, null);
                    
                    Button btn = slotUI.GetComponent<Button>();
                    int slotIdx = index;
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OnClickItemInNourishGrid(slotIdx));
                    }

                    instantiatedSlots.Add(slotUI);
                }

                index++;
            }
        }
    }

    // 🖱️ 3. เมื่อคลิกเลือกไอเทมปุ๋ยใน Grid
    public void OnClickItemInNourishGrid(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= filteredInventory.Count) return;

        if (currentSelectedItem == itemIndex)
        {
            CloseDetail();
            return;
        }

        currentSelectedItem = itemIndex;
        UpdateBBSelectionVisuals(itemIndex);

        SupabaseInventoryItem currentItem = filteredInventory[itemIndex];

        if (nameText != null) nameText.text = currentItem.item.item_name;
        if (descText != null) descText.text = currentItem.item.description;

        if (locationText != null)
        {
            locationText.text = $"ประเภท: {currentItem.item.item_type}";
        }

        Sprite itemIcon = GetItemSprite(currentItem.item.item_id);
        if (detailIconImage != null)
        {
            detailIconImage.sprite = itemIcon;
            detailIconImage.gameObject.SetActive(itemIcon != null);
        }

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(true);
        }
    }

    // 🔘 4. เมื่อกดปุ่ม "ใช้" ปุ๋ย
    private void OnUseItemClicked()
    {
        if (currentSelectedItem == -1 || currentSelectedItem >= filteredInventory.Count) return;

        SupabaseInventoryItem selectedItem = filteredInventory[currentSelectedItem];

        // ดึง item_id แบบรัดกุม
        string targetItemId = "";
        if (selectedItem.item != null && !string.IsNullOrEmpty(selectedItem.item.item_id))
        {
            targetItemId = selectedItem.item.item_id;
        }
        else if (!string.IsNullOrEmpty(selectedItem.item_id))
        {
            targetItemId = selectedItem.item_id;
        }

        if (string.IsNullOrEmpty(targetItemId))
        {
            Debug.LogError("[Nourish] ไม่พบ item_id ของไอเทมนี้!");
            return;
        }

        // 🔊 เล่นเสียง SFX ปุ่ม
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        // 🌿 1. สั่งบำรุงพืช (เพิ่มค่าสารอาหาร +25, EXP +25, Coins +15, Relationship +10)
        PlantStatus plant = FindFirstObjectByType<PlantStatus>();
        if (plant != null)
        {
            plant.ApplyFertilizerFromSupabase(selectedItem);
        }

        // 🔄 2. คำนวณจำนวนใหม่ (ลบออก 1)
        int oldQuantity = selectedItem.quantity;
        int newQuantity = Mathf.Max(0, oldQuantity - 1);

        Debug.Log($"[Nourish] ไอเทม: {targetItemId} | จำนวนเดิม: {oldQuantity} -> จำนวนใหม่: {newQuantity}");

        // อัปเดตค่าในเครื่องทันที
        selectedItem.quantity = newQuantity;

        // 🌐 3. ยิงอัปเดตลง Supabase (PATCH)
        if (SupabaseManager.Instance != null)
        {
            StartCoroutine(SupabaseManager.Instance.UpdateItemQuantity(targetItemId, newQuantity, 
                onSuccess: () =>
                {
                    Debug.Log($"[Nourish] ตัดจำนวน {targetItemId} ใน DB เหลือ {newQuantity} สำเร็จ");
                    FetchNourishItemsAndRender();
                },
                onError: (err) =>
                {
                    Debug.LogError($"[Nourish] ตัดจำนวนใน DB ไม่สำเร็จ: {err}");
                }
            ));
        }

        // ❌ 4. ปิดหน้าต่าง Popup บำรุง (เปิดกลับมาใหม่ได้ตลอด)
        ClosePopup();
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

    public void CloseDetail()
    {
        currentSelectedItem = -1;
        UpdateBBSelectionVisuals(-1);

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(false);
        }
    }

    public void ClosePopup()
    {
        CloseDetail();
        gameObject.SetActive(false);
    }

    private Sprite GetItemSprite(string itemId)
    {
        if (mainInventoryManager == null) return null;
        var found = mainInventoryManager.itemSprites.Find(x => x.itemId == itemId);
        if (found != null && found.itemSprite != null) return found.itemSprite;

        if (itemId.Contains("_"))
        {
            string baseId = itemId.Substring(0, itemId.LastIndexOf('_'));
            var baseMatch = mainInventoryManager.itemSprites.Find(x => x.itemId == baseId);
            if (baseMatch != null) return baseMatch.itemSprite;
        }

        return null;
    }

    private Sprite GetFrameSprite(string rarityTier)
    {
        if (mainInventoryManager == null || string.IsNullOrEmpty(rarityTier)) return null;
        var found = mainInventoryManager.frameSprites.Find(x => x.rarityTier.ToLower() == rarityTier.ToLower());
        return found != null ? found.frameSprite : mainInventoryManager.defaultFrame;
    }
}