using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

// หน้า "สูตรการผสมปุ๋ย" ใน Plant Dex (ติดไว้ที่ FertilizerTab)
// - ปุ๋ยไม่ล็อก: รูป/ชื่อ/คำอธิบายโชว์เสมอ (ชื่อจาก crafting_recipe, คำอธิบายพิมพ์เองได้ใน Inspector)
// - วัตถุดิบ: ดึงจากตาราง item ตาม cat_ ของสูตร (All / ปุ๋ยเน่า = โชว์ทุกตัว)
// - วัตถุดิบจะสว่างเมื่อผู้เล่นเคยใช้มันคราฟปุ๋ยตัวนั้น (ดูจาก crafting_log) ไม่งั้นเป็นเงาดำ
public class FertilizerDexController : MonoBehaviour
{
    [Header("UI ปุ๋ย")]
    public Image fertilizerIcon;            // FertilizerItem
    public TextMeshProUGUI fertilizerName;  // ItemName
    public TextMeshProUGUI fertilizerDesc;  // ItemDesc

    [Header("UI วัตถุดิบ")]
    public Transform ingredientContainer;   // IngredientGroup (ใส่ Grid/Horizontal Layout Group ไว้)
    public GameObject ingredientPrefab;     // prefab ไอเทม 1 ช่อง: ถ้ามีลูกชื่อ "Icon" จะใส่รูปที่ลูก ไม่งั้นใส่ที่ Image ตัวมันเอง

    [Header("ScrollView (ให้ยืดตามจำนวนวัตถุดิบ)")]
    public RectTransform scrollContent;     // FertilizerDexCard > Viewport > Content
    public float bottomPadding = 60f;       // ระยะเผื่อใต้วัตถุดิบแถวสุดท้าย

    [Header("Buttons")]
    public Button btnLeft;
    public Button btnRight;

    [Header("ลำดับหน้า + ข้อความที่พิมพ์เอง (ช่องว่าง = ใช้จาก DB)")]
    public List<FertilizerDexEntry> entries = new List<FertilizerDexEntry>();

    static readonly Color LockedColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    // ข้อมูลจาก Supabase
    readonly Dictionary<string, DbRecipe> recipes = new Dictionary<string, DbRecipe>();
    readonly List<DbItem> ingredients = new List<DbItem>();
    readonly Dictionary<string, HashSet<string>> usedByRecipe = new Dictionary<string, HashSet<string>>(); // recipeId -> item_id ที่เคยใช้

    readonly List<GameObject> spawnedIcons = new List<GameObject>();
    readonly Dictionary<string, Sprite> spriteById = new Dictionary<string, Sprite>();
    bool masterLoaded;
    int currentIndex;

    void Awake()
    {
        if (btnLeft != null) btnLeft.onClick.AddListener(OnPrevClicked);
        if (btnRight != null) btnRight.onClick.AddListener(OnNextClicked);
        if (entries.Count == 0) FillDefaultEntries();
        BuildSpriteLookup();
    }

    // เปิดแท็บเมื่อไหร่ก็โหลด crafting_log ใหม่ (เผื่อเพิ่งคราฟมา)
    void OnEnable()
    {
        UpdateUI();
        if (SupabaseManager.Instance == null)
        {
            Debug.LogWarning("[FertilizerDex] ไม่เจอ SupabaseManager ในฉาก", this);
            return;
        }
        if (masterLoaded) LoadCraftingLog();
        else LoadMasterData();
    }

    // ===== ค่าเริ่มต้น (recipe_id / item_id ตรงกับใน Supabase) =====
    void FillDefaultEntries()
    {
        entries.Add(new FertilizerDexEntry {
            recipeId = "RECIPE_GROWTH", fertilizerItemId = "ITEM_FERT_GROWTH",
            customDescription = "ช่วยเร่งระยะเวลาการเจริญเติบโตของพืช ยิ่งผสมได้ดี พืชยิ่งโตไว สูงสุด 50%"
        });
        entries.Add(new FertilizerDexEntry {
            recipeId = "RECIPE_CURE", fertilizerItemId = "ITEM_FERT_CURE",
            customDescription = "ล้างสถานะป่วยของพืชทันที และเพิ่มเกราะป้องกันโรคได้สูงสุด 3 ครั้ง"
        });
        entries.Add(new FertilizerDexEntry {
            recipeId = "RECIPE_EVO", fertilizerItemId = "ITEM_FERT_EVO",
            customDescription = "เพิ่มโอกาสเก็บเกี่ยวได้ผลผลิตแรร์ สูงสุด 50%"
        });
        entries.Add(new FertilizerDexEntry {
            recipeId = "RECIPE_ALL", fertilizerItemId = "ITEM_FERT_ALL", showAllIngredients = true,
            customDescription = "เพิ่มค่าสถานะทุกอย่างของพืชในคราวเดียว สูงสุด 25%"
        });
        entries.Add(new FertilizerDexEntry {
            recipeId = "RECIPE_RUINED", fertilizerItemId = "ITEM_FERT_RUINED", showAllIngredients = true,
            customDescription = "ได้จากการผสมพลาด ช่วยให้พืชโตขึ้นเล็กน้อย แต่ทำให้พืชป่วย"
        });
    }

    // ===== โหลดข้อมูล =====
    void LoadMasterData()
    {
        var sm = SupabaseManager.Instance;
        StartCoroutine(sm.GetRest("crafting_recipe?select=recipe_id,recipe_name,primary_category,description",
            onSuccess: recipeJson =>
            {
                recipes.Clear();
                foreach (DbRecipe r in Parse<DbRecipe>(recipeJson))
                    if (r != null && !string.IsNullOrEmpty(r.recipe_id)) recipes[r.recipe_id] = r;

                StartCoroutine(sm.GetRest("item?select=item_id,item_name,item_type,metadata&item_type=eq.ingredient&order=item_id",
                    onSuccess: itemJson =>
                    {
                        ingredients.Clear();
                        ingredients.AddRange(Parse<DbItem>(itemJson));
                        masterLoaded = true;
                        LoadCraftingLog();
                    }));
            }));
    }

    void LoadCraftingLog()
    {
        string userId = SupabaseManager.Instance.GetActiveUserId();
        if (string.IsNullOrEmpty(userId)) { UpdateUI(); return; }

        StartCoroutine(SupabaseManager.Instance.GetRest(
            $"crafting_log?select=materials_used,result_item_id,recipe_id&user_id=eq.{userId}",
            onSuccess: json =>
            {
                usedByRecipe.Clear();
                foreach (DbCraftLog log in Parse<DbCraftLog>(json))
                {
                    if (log == null || log.materials_used == null) continue;
                    foreach (FertilizerDexEntry e in entries)
                    {
                        // จับคู่ด้วย recipe_id ก่อน ถ้า null (เช่นปุ๋ยเน่า) ค่อยดูจาก result_item_id
                        bool match = log.recipe_id == e.recipeId ||
                                     (!string.IsNullOrEmpty(log.result_item_id) && log.result_item_id.StartsWith(e.fertilizerItemId));
                        if (!match) continue;

                        if (!usedByRecipe.TryGetValue(e.recipeId, out var set))
                            usedByRecipe[e.recipeId] = set = new HashSet<string>();
                        foreach (string id in log.materials_used) set.Add(id);
                    }
                }
                if (isActiveAndEnabled) UpdateUI();
            }));
    }

    // ===== แสดงผล =====
    void OnPrevClicked() { if (currentIndex > 0) { currentIndex--; UpdateUI(); } }
    void OnNextClicked() { if (currentIndex < entries.Count - 1) { currentIndex++; UpdateUI(); } }

    public void UpdateUI()
    {
        if (entries.Count == 0) return;
        currentIndex = Mathf.Clamp(currentIndex, 0, entries.Count - 1);

        FertilizerDexEntry e = entries[currentIndex];
        recipes.TryGetValue(e.recipeId, out DbRecipe recipe);

        if (btnLeft != null) btnLeft.gameObject.SetActive(currentIndex > 0);
        if (btnRight != null) btnRight.gameObject.SetActive(currentIndex < entries.Count - 1);

        if (fertilizerIcon != null)
        {
            fertilizerIcon.sprite = GetSprite(e.fertilizerItemId);
            fertilizerIcon.color = Color.white;
            fertilizerIcon.preserveAspect = true;
        }
        if (fertilizerName != null) fertilizerName.text = GetName(e, recipe);
        if (fertilizerDesc != null) fertilizerDesc.text = GetDescription(e, recipe);

        ShowIngredients(e, recipe);
    }

    string GetName(FertilizerDexEntry e, DbRecipe recipe)
    {
        if (!string.IsNullOrWhiteSpace(e.customName)) return e.customName;
        if (recipe == null || string.IsNullOrEmpty(recipe.recipe_name)) return "";
        // ใน DB เป็น "สูตรปุ๋ยเร่งโต" -> โชว์ "ปุ๋ยเร่งโต"
        return recipe.recipe_name.StartsWith("สูตร") ? recipe.recipe_name.Substring("สูตร".Length) : recipe.recipe_name;
    }

    string GetDescription(FertilizerDexEntry e, DbRecipe recipe)
    {
        if (!string.IsNullOrWhiteSpace(e.customDescription)) return e.customDescription;
        return recipe != null ? recipe.description : "";
    }

    void ShowIngredients(FertilizerDexEntry e, DbRecipe recipe)
    {
        foreach (GameObject go in spawnedIcons) if (go != null) Destroy(go);
        spawnedIcons.Clear();

        if (ingredientContainer == null || ingredientPrefab == null) return;
        if (!e.showAllIngredients && recipe == null) return; // ยังโหลดสูตรไม่เสร็จ

        usedByRecipe.TryGetValue(e.recipeId, out HashSet<string> used);

        foreach (DbItem item in ingredients)
        {
            if (item == null) continue;
            string cat = item.metadata != null ? item.metadata.category : null;
            if (!e.showAllIngredients && cat != recipe.primary_category) continue;

            GameObject go = Instantiate(ingredientPrefab, ingredientContainer);
            go.name = item.item_id;
            spawnedIcons.Add(go);

            Transform iconT = go.transform.Find("Icon");
            Image img = iconT != null ? iconT.GetComponent<Image>() : go.GetComponent<Image>();
            if (img == null) continue;

            bool unlocked = used != null && used.Contains(item.item_id);
            img.sprite = GetSprite(item.item_id);
            img.color = unlocked ? Color.white : LockedColor;
            img.preserveAspect = true;
        }

        ResizeScrollContent();
    }

    // Content ของ ScrollView ไม่รู้ว่าข้างในยาวแค่ไหน (ลูกๆ จัดตำแหน่งเอง ไม่ได้ใช้ Layout)
    // เลยต้องยืดความสูง Content ให้ลงไปถึงขอบล่างของ IngredientGroup ไม่งั้นเลื่อนแล้วเด้งกลับ
    void ResizeScrollContent()
    {
        if (scrollContent == null || ingredientContainer == null) return;

        var group = ingredientContainer as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        Vector3[] corners = new Vector3[4];
        group.GetWorldCorners(corners);
        float groupBottom = scrollContent.InverseTransformPoint(corners[0]).y;

        float needed = scrollContent.rect.yMax - groupBottom + bottomPadding;
        var viewport = scrollContent.parent as RectTransform;
        if (viewport != null) needed = Mathf.Max(needed, viewport.rect.height);

        scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, needed);
    }

    // ===== รูป: ใช้ตาราง itemSprites ที่ตั้งไว้ใน MainInventoryManager อันเดียวกับกระเป๋า =====
    void BuildSpriteLookup()
    {
        var inv = FindAnyObjectByType<MainInventoryManager>(FindObjectsInactive.Include);
        if (inv == null || inv.itemSprites == null)
        {
            // ฉากผสมปุ๋ยไม่มี MainInventoryManager ให้ใช้รูปจาก FertilizerMixerManager > Items แทน
            var mixer = FindAnyObjectByType<FertilizerMixerManager>(FindObjectsInactive.Include);
            if (mixer != null && mixer.items != null)
            {
                foreach (var d in mixer.items)
                    if (!string.IsNullOrEmpty(d.itemId) && d.itemSprite != null)
                        spriteById[d.itemId] = d.itemSprite;
                return;
            }

            Debug.LogWarning("[FertilizerDex] ไม่เจอ MainInventoryManager เลยไม่มีรูปไอเทม", this);
            return;
        }
        foreach (var m in inv.itemSprites)
            if (m != null && !string.IsNullOrEmpty(m.itemId) && m.itemSprite != null)
                spriteById[m.itemId] = m.itemSprite;
    }

    Sprite GetSprite(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (spriteById.TryGetValue(itemId, out Sprite s)) return s;

        // เผื่อได้ id แบบมีเกรด เช่น ITEM_FERT_EVO_S -> ลองหา ITEM_FERT_EVO
        foreach (var kv in spriteById)
            if (itemId.StartsWith(kv.Key)) return kv.Value;

        Debug.LogWarning($"[FertilizerDex] ไม่มีรูปของ {itemId} ใน MainInventoryManager > Item Sprites", this);
        return null;
    }

    static List<T> Parse<T>(string json)
    {
        try { return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>(); }
        catch (Exception ex)
        {
            Debug.LogError($"[FertilizerDex] อ่าน JSON ไม่ได้: {ex.Message}");
            return new List<T>();
        }
    }

    // ===== รูปแบบข้อมูลจาก Supabase =====
    [Serializable] class DbRecipe { public string recipe_id, recipe_name, primary_category, description; }
    [Serializable] class DbItemMeta { public string category; }
    [Serializable] class DbItem { public string item_id, item_name, item_type; public DbItemMeta metadata; }
    [Serializable] class DbCraftLog { public List<string> materials_used; public string result_item_id, recipe_id; }
}

[Serializable]
public class FertilizerDexEntry
{
    public string recipeId;            // crafting_recipe.recipe_id
    public string fertilizerItemId;    // id ปุ๋ยแบบไม่มีเกรด ใช้หารูป + จับคู่ crafting_log
    public bool showAllIngredients;    // All / ปุ๋ยเน่า = true
    public string customName;          // ว่าง = ใช้ชื่อจาก DB
    [TextArea(2, 4)]
    public string customDescription;   // ว่าง = ใช้คำอธิบายจาก DB
}
