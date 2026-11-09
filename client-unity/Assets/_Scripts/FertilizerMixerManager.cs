using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FertilizerMixerManager : MonoBehaviour
{
    [System.Serializable]
    public struct ItemData
    {
        public string itemId;        // เช่น ITEM_ING_EGG หรือ ITEM_FERT_GROWTH (ไม่ต้องใส่เกรด)
        public string itemName;      // ว่างไว้ = ใช้ชื่อจาก DB
        public string description;   // ว่างไว้ = ใช้คำอธิบายจาก DB
        public string location;
        public Sprite frameSprite;   // ว่างไว้ = ใช้กรอบตาม rarity ด้านล่าง
        public Sprite itemSprite;
    }

    [System.Serializable]
    public class FrameByRarity
    {
        public string rarityTier;    // silver, gold, rainbow
        public Sprite frameSprite;
    }

    [System.Serializable]
    public class RecipeMapping
    {
        public string category;        // cat_growth, cat_heal, cat_evo
        public string recipeId;        // RECIPE_GROWTH
        public string fertilizerBaseId; // ITEM_FERT_GROWTH (เกรดจะต่อท้ายให้ เช่น _S)
    }

    [Header("Storage & Detail UI")]
    public GameObject storageView;
    public GameObject itemDetailBox;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text locationText;

    [Header("Item Database (ใส่ id + รูป ของวัตถุดิบและปุ๋ย)")]
    public ItemData[] items;

    [Header("Inventory (ดึงจากกระเป๋าจริง)")]
    public Transform itemGrid;               // ว่างไว้ได้ จะหา ItemGrid ใน storageView ให้
    public GameObject itemSlotPrefab;        // Prefab ช่องไอเทม (Prefabs/Slot_Crab)
    public string ingredientPrefix = "ITEM_ING_";
    public List<FrameByRarity> frameSprites = new List<FrameByRarity>();
    public Sprite defaultFrame;
    [Range(0f, 1f)] public float usedItemAlpha = 0.35f; // ช่องที่ถูกหยิบไปแล้วจะจางลง

    [Header("Item Selection Highlights (ของเดิม ใช้กับช่องที่วางไว้ตายตัว)")]
    public GameObject[] itemSelectHighlights;

    [Header("Bottom Slots (3 ช่องล่าง)")]
    public Image[] slotFrames;
    public Image[] slotIcons;
    public Sprite defaultSlotSprite; // ลากรูปสี่เหลี่ยมมนสีครีม (Rectangle 6) มาใส่

    [Header("Warning Toast")]
    public GameObject warningPopup;
    public Animator popupAnimator;
    public float showDuration = 2.1f;

    [Header("Boiling Minigame Transition")]
    public GameObject boilingMinigameView;
    public GameObject[] objectsToHide;

    [Header("Difficulty")]
    public DifficultySliderMockup difficultySlider; // ว่างไว้ได้ จะหาเองในฉาก

    [Header("Recipes (หมวด → สูตร → ปุ๋ย)")]
    public List<RecipeMapping> recipes = new List<RecipeMapping>
    {
        new RecipeMapping { category = "cat_growth", recipeId = "RECIPE_GROWTH", fertilizerBaseId = "ITEM_FERT_GROWTH" },
        new RecipeMapping { category = "cat_heal",   recipeId = "RECIPE_CURE",   fertilizerBaseId = "ITEM_FERT_CURE" },
        new RecipeMapping { category = "cat_evo",    recipeId = "RECIPE_EVO",    fertilizerBaseId = "ITEM_FERT_EVO" },
    };
    public string allRecipeId = "RECIPE_ALL";
    public string allFertilizerBaseId = "ITEM_FERT_ALL";
    public string ruinedItemId = "ITEM_FERT_RUINED";
    public string ruinedRecipeId = "RECIPE_RUINED"; // ใช้บันทึก user_recipe_discovery ตอนได้ปุ๋ยเน่า

    [Header("Scoring (คะแนน = score วัตถุดิบ + ความยาก + จังหวะ)")]
    [Tooltip("ลำดับ 0 = ง่าย, 1 = กลาง, 2 = ยาก")]
    public int[] difficultyBonus = { 0, 2, 4 };
    public int greenBonus = 3;
    public int yellowBonus = 0;
    public int gradeBMin = 6;
    public int gradeAMin = 9;
    public int gradeSMin = 12;

    [Header("Reward UI (ใส่หรือไม่ใส่ก็ได้)")]
    public Image rewardIcon;
    public TMP_Text rewardNameText;
    public TMP_Text rewardGradeText;
    public TMP_Text rewardStatusText; // แสดง "กำลังบันทึก..." / แจ้งเตือนถ้าบันทึกไม่สำเร็จ
    public TMP_Text rewardDescText;   // ItemDesc: คำอธิบายปุ๋ยจาก DB
    public TMP_Text rewardSuccessText; // SuccessText: ใช้แค่ทำอนิเมชันเฟดเข้า
    public RectTransform rewardButton; // ปุ่มกลับสู่หน้าหลัก ใช้ทำอนิเมชันเด้ง + กดแล้วกลับฉากหลัก
    public string homeSceneName = "MainForest";

    [Header("Reward Sunburst & Animation (แบบเดียวกับหน้ารางวัลเกาะ)")]
    public RectTransform sunburst;
    public float rotateSpeed = 20f;
    public float popDuration = 0.35f;
    public float fadeDuration = 0.4f;
    public float itemPopOvershoot = 1.2f;
    public float buttonPopOvershoot = 1.08f;
    public float textDelay = 0.08f;

    public int CurrentDifficulty
    {
        get
        {
            if (difficultySlider == null) difficultySlider = FindAnyObjectByType<DifficultySliderMockup>(FindObjectsInactive.Include);
            return difficultySlider != null ? difficultySlider.SelectedDifficulty : 0;
        }
    }

    private static readonly string[] DifficultyKeys = { "easy", "medium", "hard" };

    private int currentSelectedItem = -1;
    private bool[] isSlotFilled = new bool[3];
    private int[] slotItemIndex = new int[3] { -1, -1, -1 }; // จำว่าช่องไหนใส่วัตถุดิบลำดับไหน (ใน ingredients)
    private Coroutine hideCoroutine;

    private readonly List<SupabaseInventoryItem> ingredients = new List<SupabaseInventoryItem>();
    private readonly List<ItemSlotUI> gridSlots = new List<ItemSlotUI>();
    private bool inventoryLoaded = false;
    private bool isLoadingInventory = false;
    private bool isFinishing = false;
    private Coroutine rewardSequence;

    private void Awake()
    {
        // ถ้าไม่ได้ใส่ defaultSlotSprite ใน Inspector ให้จำรูปปัจจุบันของช่องแรกไว้
        if (defaultSlotSprite == null && slotFrames.Length > 0 && slotFrames[0] != null)
        {
            defaultSlotSprite = slotFrames[0].sprite;
        }

        if (itemGrid == null && storageView != null)
        {
            itemGrid = FindChildRecursive(storageView.transform, "ItemGrid");
        }

        // ปุ่มในหน้ารับปุ๋ย กดแล้วกลับ MainForest (ผูกผ่านโค้ด ไม่ต้องตั้ง OnClick ใน Inspector)
        if (rewardButton != null)
        {
            Button homeButton = rewardButton.GetComponent<Button>();
            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(GoToMainScene);
                homeButton.onClick.AddListener(GoToMainScene);
            }
        }
    }

    public void GoToMainScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(homeSceneName);
    }

    private void Start()
    {
        LoadInventory();
    }

    // ==========================================
    // 🌐 ดึงกระเป๋าจาก Supabase (เฉพาะ ITEM_ING_)
    // ==========================================
    public void LoadInventory()
    {
        if (isLoadingInventory) return;
        if (SupabaseManager.Instance == null)
        {
            Debug.LogWarning("[FertilizerMixerManager] ไม่พบ SupabaseManager ในฉาก!");
            return;
        }

        isLoadingInventory = true;
        StartCoroutine(SupabaseManager.Instance.GetUserInventory(
            onSuccess: (json) =>
            {
                isLoadingInventory = false;
                ParseInventory(json);
            },
            onError: (err) =>
            {
                isLoadingInventory = false;
                Debug.LogError($"[FertilizerMixerManager] โหลดกระเป๋าไม่สำเร็จ: {err}");
            }
        ));
    }

    private void ParseInventory(string json)
    {
        ingredients.Clear();

        SupabaseInventoryItem[] all = JsonHelper.FromJson<SupabaseInventoryItem>(json);
        if (all != null)
        {
            foreach (var inv in all)
            {
                if (inv == null || inv.item == null || inv.quantity <= 0) continue;
                if (string.IsNullOrEmpty(inv.item.item_id) || !inv.item.item_id.StartsWith(ingredientPrefix)) continue;
                ingredients.Add(inv);
            }
            ingredients.Sort((a, b) => string.Compare(a.item.item_id, b.item.item_id, StringComparison.Ordinal));
        }

        inventoryLoaded = true;

        // ถ้าคลังเปิดอยู่ตอนโหลดเสร็จ ให้วาดใหม่
        if (storageView != null && storageView.activeInHierarchy) BuildGrid();
    }

    private void BuildGrid()
    {
        if (itemGrid == null)
        {
            Debug.LogWarning("[FertilizerMixerManager] ไม่พบ ItemGrid");
            return;
        }
        if (itemSlotPrefab == null)
        {
            Debug.LogWarning("[FertilizerMixerManager] ยังไม่ได้ลาก Item Slot Prefab ใส่ใน Inspector");
            return;
        }

        // ล้างช่องเดิม (รวมช่องที่วางไว้ในฉาก)
        for (int i = itemGrid.childCount - 1; i >= 0; i--)
        {
            GameObject child = itemGrid.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        gridSlots.Clear();

        for (int i = 0; i < ingredients.Count; i++)
        {
            SupabaseInventoryItem inv = ingredients[i];
            ItemData? data = FindItemData(inv.item.item_id);

            Sprite icon = GetItemSprite(inv.item.item_id);
            Sprite frame = (data.HasValue && data.Value.frameSprite != null) ? data.Value.frameSprite : GetFrameByRarity(inv.item.rarity_tier);

            GameObject slotObj = Instantiate(itemSlotPrefab, itemGrid);
            ItemSlotUI slotUI = slotObj.GetComponent<ItemSlotUI>();
            if (slotUI != null)
            {
                slotUI.SetupSlotWithCallback(i, icon, frame, inv.quantity, OnClickItemInGrid);
            }
            gridSlots.Add(slotUI);
        }

        RefreshGridVisuals();
    }

    private void RefreshGridVisuals()
    {
        for (int i = 0; i < gridSlots.Count; i++)
        {
            ItemSlotUI slot = gridSlots[i];
            if (slot == null) continue;

            slot.SetSelected(i == currentSelectedItem);

            CanvasGroup group = slot.GetComponent<CanvasGroup>();
            if (group == null) group = slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = IsIngredientUsed(i) ? usedItemAlpha : 1f;
        }
    }

    private bool IsIngredientUsed(int ingredientIndex)
    {
        for (int s = 0; s < slotItemIndex.Length; s++)
        {
            if (isSlotFilled[s] && slotItemIndex[s] == ingredientIndex) return true;
        }
        return false;
    }

    // ==========================================
    // 🧺 คลังวัตถุดิบ + 3 ช่องล่าง
    // ==========================================
    public void OpenStorage()
    {
        currentSelectedItem = -1;
        ClearAllHighlights();
        if (itemDetailBox != null) itemDetailBox.SetActive(false);
        storageView.SetActive(true);

        if (inventoryLoaded) BuildGrid();
        else LoadInventory();
    }

    public void CloseStorage()
    {
        ClearAllHighlights();
        currentSelectedItem = -1;
        storageView.SetActive(false);
    }

    // ฟังก์ชันใหม่: ผูกกับช่องสี่เหลี่ยมด้านล่างทั้ง 3 ช่อง
    public void OnClickBottomSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= isSlotFilled.Length) return;

        // กรณีที่ 1: ช่องนี้มีไอเทมอยู่แล้ว -> สั่งลบออก
        if (isSlotFilled[slotIndex])
        {
            // ซ่อนรูปไอคอน และเปลี่ยนกรอบกลับเป็นช่องสีครีม
            slotIcons[slotIndex].gameObject.SetActive(false);
            if (defaultSlotSprite != null)
            {
                slotFrames[slotIndex].sprite = defaultSlotSprite;
            }

            // ล้างสถานะช่องว่าง (คืนสิทธิ์ให้กลับไปเลือกชิ้นนี้ในคลังได้ใหม่)
            isSlotFilled[slotIndex] = false;
            slotItemIndex[slotIndex] = -1;
        }
        // กรณีที่ 2: ช่องว่างอยู่ -> สั่งเปิดคลังเก็บของ
        else
        {
            OpenStorage();
        }
    }

    public void OnClickItemInGrid(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= ingredients.Count) return;
        if (IsIngredientUsed(itemIndex)) return; // ห้ามเลือกของซ้ำ

        // กดไอเทมเดิมซ้ำ = ปิด popup
        if (currentSelectedItem == itemIndex && itemDetailBox != null && itemDetailBox.activeSelf)
        {
            CloseDetail();
            return;
        }

        currentSelectedItem = itemIndex;
        SupabaseInventoryItem inv = ingredients[itemIndex];
        ItemData? data = FindItemData(inv.item.item_id);

        string itemName = (data.HasValue && !string.IsNullOrEmpty(data.Value.itemName)) ? data.Value.itemName : inv.item.item_name;
        string desc = (data.HasValue && !string.IsNullOrEmpty(data.Value.description)) ? data.Value.description : inv.item.description;
        string location = data.HasValue ? data.Value.location : "";

        if (nameText != null) nameText.text = itemName;
        if (descText != null) descText.text = desc;
        if (locationText != null) locationText.text = location;
        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(true);

            // 📍 ให้ popup เด้งใต้ช่องที่เลือก
            if (itemIndex < gridSlots.Count && gridSlots[itemIndex] != null)
                ItemSlotUI.PlacePopupNear(itemDetailBox.transform as RectTransform, gridSlots[itemIndex].transform as RectTransform);
        }

        RefreshGridVisuals();
    }

    public void OnConfirmSelect()
    {
        int targetSlot = -1;
        for (int i = 0; i < isSlotFilled.Length; i++)
        {
            if (!isSlotFilled[i])
            {
                targetSlot = i;
                break;
            }
        }

        if (targetSlot != -1 && currentSelectedItem >= 0 && currentSelectedItem < ingredients.Count && !IsIngredientUsed(currentSelectedItem))
        {
            SupabaseInventoryItem inv = ingredients[currentSelectedItem];
            ItemData? data = FindItemData(inv.item.item_id);

            Sprite frame = (data.HasValue && data.Value.frameSprite != null) ? data.Value.frameSprite : GetFrameByRarity(inv.item.rarity_tier);
            if (frame != null) slotFrames[targetSlot].sprite = frame;

            slotIcons[targetSlot].sprite = GetItemSprite(inv.item.item_id);
            slotIcons[targetSlot].gameObject.SetActive(true);

            isSlotFilled[targetSlot] = true;
            slotItemIndex[targetSlot] = currentSelectedItem; // จำว่าช่องนี้ใส่วัตถุดิบชิ้นไหน
        }

        ClearAllHighlights();
        if (itemDetailBox != null) itemDetailBox.SetActive(false);
        CloseStorage();
    }

    private void ClearAllHighlights()
    {
        if (itemSelectHighlights != null)
        {
            for (int i = 0; i < itemSelectHighlights.Length; i++)
            {
                if (itemSelectHighlights[i] != null)
                    itemSelectHighlights[i].SetActive(false);
            }
        }

        foreach (var slot in gridSlots)
        {
            if (slot != null) slot.SetSelected(false);
        }
    }

    public void OnClickMix()
    {
        bool isComplete = isSlotFilled[0] && isSlotFilled[1] && isSlotFilled[2];

        if (!isComplete)
        {
            ShowWarning();
        }
        else
        {
            // 1. สั่งซ่อน UI ทุกอย่างที่เลือกไว้ทันที
            if (objectsToHide != null)
            {
                foreach (GameObject obj in objectsToHide)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }

            // 2. เปิดหน้าต่างมินิเกมต้มปุ๋ย
            if (boilingMinigameView != null)
            {
                boilingMinigameView.SetActive(true);
            }
        }
    }

    // ==========================================
    // 🧪 คิดผลการผสม (BoilingMinigame เรียกตอนกดหยุด)
    // ==========================================
    public void FinishMix(BoilingZone zone, Action onShowReward)
    {
        if (isFinishing) return;
        isFinishing = true;

        List<SupabaseInventoryItem> used = new List<SupabaseInventoryItem>();
        for (int s = 0; s < slotItemIndex.Length; s++)
        {
            int idx = slotItemIndex[s];
            if (isSlotFilled[s] && idx >= 0 && idx < ingredients.Count) used.Add(ingredients[idx]);
        }

        int difficulty = CurrentDifficulty;
        string difficultyKey = DifficultyKeys[Mathf.Clamp(difficulty, 0, DifficultyKeys.Length - 1)];

        string resultItemId;
        string resultGrade;
        string recipeId;
        string timingResult;
        int totalScore;

        if (zone == BoilingZone.Red)
        {
            // เข็มหยุดตรงขอบแดง = ปุ๋ยเน่า ไม่ว่าจะใช้วัตถุดิบหรือความยากไหน
            resultItemId = ruinedItemId;
            resultGrade = "ruined";
            recipeId = null;
            timingResult = "bad";
            totalScore = 0;
        }
        else
        {
            timingResult = zone == BoilingZone.Green ? "perfect" : "good";

            int ingredientScore = 0;
            foreach (var inv in used)
            {
                if (inv.item.metadata != null) ingredientScore += inv.item.metadata.GetScore();
            }
            int diffBonus = (difficultyBonus != null && difficultyBonus.Length > 0)
                ? difficultyBonus[Mathf.Clamp(difficulty, 0, difficultyBonus.Length - 1)] : 0;
            int timingBonus = zone == BoilingZone.Green ? greenBonus : yellowBonus;
            totalScore = ingredientScore + diffBonus + timingBonus;

            string gradeLetter = GetGradeLetter(totalScore);
            resultGrade = "grade_" + gradeLetter.ToLower();

            RecipeMapping recipe = FindRecipe(used);
            string baseId = recipe != null ? recipe.fertilizerBaseId : allFertilizerBaseId;
            recipeId = recipe != null ? recipe.recipeId : allRecipeId;
            resultItemId = $"{baseId}_{gradeLetter}";
        }

        Debug.Log($"[FertilizerMixerManager] ผสมได้ {resultItemId} | คะแนน {totalScore} | {difficultyKey} | {timingResult}");

        // แสดงผลบนหน้ารับปุ๋ย
        if (rewardIcon != null)
        {
            Sprite icon = GetItemSprite(resultItemId);
            rewardIcon.sprite = icon;
            rewardIcon.gameObject.SetActive(icon != null);
        }
        if (rewardGradeText != null)
        {
            rewardGradeText.text = resultGrade == "ruined" ? "ปุ๋ยเน่า" : $"เกรด {resultGrade.Substring(6).ToUpper()}";
        }
        if (rewardNameText != null) rewardNameText.text = $"{resultItemId} x1"; // ชื่อจริงจะมาแทนหลังดึงจาก DB
        if (rewardDescText != null) rewardDescText.text = "";
        if (rewardStatusText != null) rewardStatusText.text = "กำลังบันทึก...";

        StartCoroutine(SaveResultRoutine(used, difficultyKey, timingResult, totalScore, resultGrade, resultItemId, recipeId, onShowReward));
    }

    private IEnumerator SaveResultRoutine(List<SupabaseInventoryItem> used, string difficultyKey, string timingResult, int totalScore,
        string resultGrade, string resultItemId, string recipeId, Action onShowReward)
    {
        bool hasError = false;
        SupabaseManager db = SupabaseManager.Instance;

        if (db == null)
        {
            Debug.LogError("[FertilizerMixerManager] ไม่พบ SupabaseManager บันทึกผลไม่ได้");
            hasError = true;
        }
        else
        {
            // 1. หักวัตถุดิบ 3 ชิ้นออกจากกระเป๋า (เหลือ 0 จะลบแถวออกให้เอง)
            List<string> materials = new List<string>();
            foreach (var inv in used)
            {
                materials.Add(inv.item.item_id);
                yield return StartCoroutine(db.AddItemToInventory(inv.item.item_id, -1, null, (err) => hasError = true));
            }

            // 2. เพิ่มปุ๋ย 1 ชิ้นเข้ากระเป๋า
            yield return StartCoroutine(db.AddItemToInventory(resultItemId, 1, null, (err) => hasError = true));

            // 3. บันทึก crafting_log
            yield return StartCoroutine(db.InsertCraftingLog(materials, difficultyKey, timingResult, totalScore,
                resultGrade, resultItemId, recipeId, null, (err) => hasError = true));

            // 📝 Behavior Log: ผสมปุ๋ย 1 ครั้ง
            db.LogBehavior("craft_fertilizer", "result_item_id", resultItemId, "grade", resultGrade, "difficulty", difficultyKey);

            // 4. บันทึกว่าค้นพบสูตรนี้แล้ว (ปุ๋ยเน่าใช้ ruinedRecipeId)
            string discoveredRecipe = string.IsNullOrEmpty(recipeId) ? ruinedRecipeId : recipeId;
            yield return StartCoroutine(db.UnlockRecipeDiscovery(discoveredRecipe, null, (err) => hasError = true));

            // 5. ดึงชื่อ + คำอธิบายปุ๋ยจาก DB มาแสดง (ชื่อใน DB มี "(เกรด X)" อยู่แล้ว)
            yield return StartCoroutine(db.GetRest($"item?select=item_name,description&item_id=eq.{resultItemId}",
                (json) =>
                {
                    NameRow row = ExtractFirstRow(json);
                    if (row == null) return;
                    if (rewardNameText != null && !string.IsNullOrEmpty(row.item_name)) rewardNameText.text = $"{row.item_name} x1";
                    if (rewardDescText != null && !string.IsNullOrEmpty(row.description)) rewardDescText.text = row.description.Trim();
                }));
        }

        if (rewardStatusText != null)
        {
            rewardStatusText.text = hasError ? "บันทึกไม่สำเร็จ ลองใหม่อีกครั้ง" : "";
        }

        // ให้รอบหน้าโหลดกระเป๋าใหม่
        inventoryLoaded = false;
        onShowReward?.Invoke();

        if (rewardSequence != null) StopCoroutine(rewardSequence);
        rewardSequence = StartCoroutine(PlayRewardSequence());
    }

    // ==========================================
    // ✨ อนิเมชันหน้ารับปุ๋ย (ยกมาจาก BeachRewardController)
    // ==========================================
    private void Update()
    {
        if (sunburst != null && sunburst.gameObject.activeInHierarchy)
        {
            sunburst.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }

        // 👆 แตะนอก popup รายละเอียด (และไม่ได้แตะช่องไอเทม) = ปิด popup
        if (itemDetailBox != null && itemDetailBox.activeInHierarchy && Input.GetMouseButtonDown(0))
        {
            Vector2 pointer = Input.mousePosition;
            bool onPopup = IsPointerInside(itemDetailBox.transform as RectTransform, pointer);
            bool onSlot = false;
            foreach (var slot in gridSlots)
            {
                if (slot != null && IsPointerInside(slot.transform as RectTransform, pointer)) { onSlot = true; break; }
            }
            if (!onPopup && !onSlot) CloseDetail();
        }
    }

    public void CloseDetail()
    {
        currentSelectedItem = -1;
        if (itemDetailBox != null) itemDetailBox.SetActive(false);
        RefreshGridVisuals();
    }

    private bool IsPointerInside(RectTransform rect, Vector2 screenPoint)
    {
        if (rect == null) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
    }

    private IEnumerator PlayRewardSequence()
    {
        RectTransform iconRect = rewardIcon != null ? rewardIcon.rectTransform : null;
        RectTransform successRect = rewardSuccessText != null ? rewardSuccessText.rectTransform : null;
        RectTransform nameRect = rewardNameText != null ? rewardNameText.rectTransform : null;
        RectTransform descRect = rewardDescText != null ? rewardDescText.rectTransform : null;

        SetHidden(sunburst);
        SetHidden(iconRect);
        SetHidden(successRect);
        SetHidden(nameRect);
        SetHidden(descRect);
        SetHidden(rewardButton);

        if (sunburst != null) StartCoroutine(PopAnimation(sunburst, popDuration, 1.1f));

        if (iconRect != null && rewardIcon.gameObject.activeSelf)
            yield return StartCoroutine(PopAnimation(iconRect, popDuration, itemPopOvershoot));

        yield return new WaitForSeconds(textDelay);
        if (successRect != null) yield return StartCoroutine(FadeAnimation(successRect, fadeDuration));

        yield return new WaitForSeconds(textDelay);
        if (nameRect != null) yield return StartCoroutine(FadeAnimation(nameRect, fadeDuration));

        yield return new WaitForSeconds(textDelay);
        if (descRect != null) yield return StartCoroutine(FadeAnimation(descRect, fadeDuration));

        yield return new WaitForSeconds(0.12f);
        if (rewardButton != null) yield return StartCoroutine(PopAnimation(rewardButton, popDuration, buttonPopOvershoot));

        rewardSequence = null;
    }

    private void SetHidden(RectTransform target)
    {
        if (target == null) return;
        target.localScale = Vector3.zero;
        GetOrAddCanvasGroup(target.gameObject).alpha = 0f;
    }

    private IEnumerator PopAnimation(RectTransform target, float duration, float overshoot)
    {
        if (target == null) yield break;
        CanvasGroup group = GetOrAddCanvasGroup(target.gameObject);
        target.localScale = Vector3.zero;
        group.alpha = 0f;

        if (duration <= 0f)
        {
            target.localScale = Vector3.one;
            group.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleT = Mathf.SmoothStep(0f, 1f, t);
            float bounce = 1f + (overshoot - 1f) * Mathf.Sin(t * Mathf.PI);
            target.localScale = Vector3.one * (scaleT * bounce);
            group.alpha = t;
            yield return null;
        }

        target.localScale = Vector3.one;
        group.alpha = 1f;
    }

    private IEnumerator FadeAnimation(RectTransform target, float duration)
    {
        if (target == null) yield break;
        CanvasGroup group = GetOrAddCanvasGroup(target.gameObject);
        target.localScale = Vector3.one;
        group.alpha = 0f;

        if (duration <= 0f)
        {
            group.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        group.alpha = 1f;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();
        return group;
    }

    private string GetGradeLetter(int score)
    {
        if (score >= gradeSMin) return "S";
        if (score >= gradeAMin) return "A";
        if (score >= gradeBMin) return "B";
        return "C";
    }

    // 2-3 ชิ้นหมวดเดียวกัน = ปุ๋ยหมวดนั้น, 3 หมวดไม่ซ้ำ = ปุ๋ยรวม (คืน null)
    private RecipeMapping FindRecipe(List<SupabaseInventoryItem> used)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (var inv in used)
        {
            string cat = inv.item.metadata != null ? inv.item.metadata.category : null;
            if (string.IsNullOrEmpty(cat)) continue;
            counts[cat] = counts.TryGetValue(cat, out int c) ? c + 1 : 1;
        }

        foreach (var pair in counts)
        {
            if (pair.Value >= 2)
            {
                RecipeMapping found = recipes.Find(r => r.category == pair.Key);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static NameRow ExtractFirstRow(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        NameRow[] rows = JsonHelper.FromJson<NameRow>(json);
        return (rows != null && rows.Length > 0) ? rows[0] : null;
    }

    [Serializable]
    private class NameRow
    {
        public string item_name;
        public string description;
    }

    // ==========================================
    // 🧠 หา id → รูป / กรอบ
    // ==========================================
    private ItemData? FindItemData(string itemId)
    {
        if (items == null || string.IsNullOrEmpty(itemId)) return null;
        foreach (var d in items)
        {
            if (d.itemId == itemId) return d;
        }
        return null;
    }

    private Sprite GetItemSprite(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        ItemData? exact = FindItemData(itemId);
        if (exact.HasValue && exact.Value.itemSprite != null) return exact.Value.itemSprite;

        // ปุ๋ยมีเกรดต่อท้าย เช่น ITEM_FERT_GROWTH_S → ลองหา ITEM_FERT_GROWTH
        int lastUnderscore = itemId.LastIndexOf('_');
        if (lastUnderscore > 0)
        {
            ItemData? baseMatch = FindItemData(itemId.Substring(0, lastUnderscore));
            if (baseMatch.HasValue && baseMatch.Value.itemSprite != null) return baseMatch.Value.itemSprite;
        }
        return null;
    }

    private Sprite GetFrameByRarity(string rarityTier)
    {
        if (string.IsNullOrEmpty(rarityTier) || frameSprites == null) return defaultFrame;
        var found = frameSprites.Find(x => x != null && !string.IsNullOrEmpty(x.rarityTier) && x.rarityTier.ToLower() == rarityTier.ToLower());
        return found != null && found.frameSprite != null ? found.frameSprite : defaultFrame;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    // ==========================================
    // ⚠️ ป๊อปอัปเตือน (ใส่วัตถุดิบไม่ครบ)
    // ==========================================
    public void ShowWarning()
    {
        if (warningPopup == null) return;
        warningPopup.SetActive(true);
        if (popupAnimator != null)
        {
            popupAnimator.Rebind();
            popupAnimator.Update(0f);
        }
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HidePopupRoutine());
    }

    private IEnumerator HidePopupRoutine()
    {
        yield return new WaitForSeconds(showDuration);
        warningPopup.SetActive(false);
    }
}
