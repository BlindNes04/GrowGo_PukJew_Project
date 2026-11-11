using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class GardenDropItem
{
    public string itemId;
    public string itemName;
    public Sprite itemIcon;
    [Range(0f, 1f)] public float dropRate = 0.15f;
}

public class MainRewardController : MonoBehaviour
{
    public static MainRewardController Instance;

    [Header("🎁 รายชื่อไอเทมดรอปโซน ISL_GARDEN (ตาม DB)")]
    public List<GardenDropItem> gardenDropPool = new List<GardenDropItem>();

    [Header("Reward UI Display")]
    public Image rewardItemImage;
    public TextMeshProUGUI rewardNameText;
    public TextMeshProUGUI youGotText;

    [Header("Sunburst Settings")]
    public RectTransform sunburst;
    public float rotateSpeed = 20f;

    [Header("Animation Settings")]
    public float popDuration = 0.35f;
    public float fadeDuration = 0.4f;
    public float itemPopOvershoot = 1.2f;
    public float buttonPopOvershoot = 1.08f;
    public float textDelay = 0.08f;

    [Header("Buttons")]
    public Button backToStartButton;

    // ==================== 📅 Daily Reward (รางวัลประจำวัน) ====================
    [Header("📅 Daily Reward (ลาก UI จาก DailyRewardPopup)")]
    public bool checkDailyRewardOnStart = true;   // เข้า MainForest แล้วเช็คว่าวันนี้รับรางวัลหรือยัง
    public GameObject dailyPopupRoot;             // DailyRewardPopup (ตัวนอกสุด)
    public RectTransform dailyPopupPanel;         // Popup (กล่องสีขาว) ใช้เด้งตอนเปิด
    public RectTransform dailySunburst;           // sunburst (หมุนตลอดตอนเปิด)
    public Image dailyItemFrame;                  // ItemContainer (กรอบตาม tier)
    public Image dailyItemImage;                  // Item (รูปไอเทม)
    public TextMeshProUGUI dailyItemNameText;     // ItemName -> "ชื่อไอเทม x1"
    public Button dailyClaimButton;               // ปุ่ม "รับ"
    public MainInventoryManager spriteSource;     // ที่เก็บรูปไอเทม/กรอบ (ว่างไว้ได้ เดี๋ยวหาให้เอง)

    private Coroutine sequenceCoroutine;
    private string currentRewardedItemId;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (backToStartButton != null)
        {
            backToStartButton.onClick.RemoveListener(OnBackToStartClicked);
            backToStartButton.onClick.AddListener(OnBackToStartClicked);
        }

        // 📅 ซ่อน popup รางวัลรายวันไว้ก่อน + ผูกปุ่ม "รับ"
        if (dailyPopupRoot != null) dailyPopupRoot.SetActive(false);
        if (dailyClaimButton != null)
        {
            dailyClaimButton.onClick.RemoveListener(OnDailyClaimClicked);
            dailyClaimButton.onClick.AddListener(OnDailyClaimClicked);
        }

        // 📅 เช็ครางวัลประจำวัน (ต้องเรียกก่อน SetActive(false) เพราะ Start จะไม่ทำงานจนกว่าจะเปิดหน้าต่าง)
        if (checkDailyRewardOnStart && Instance == this)
            CheckDailyReward();

        // 👁 ตอนแก้ซีนตั้ง CanvasGroup Alpha = 0 ไว้ซ่อนไม่ให้บัง Simulator ได้ (object ต้อง active)
        //    พอเริ่มเกมคืนค่า Alpha = 1 ให้ตรงนี้ ตอนเปิดหน้าต่างจริงจะได้มองเห็น
        CanvasGroup editorHideGroup = GetComponent<CanvasGroup>();
        if (editorHideGroup != null) editorHideGroup.alpha = 1f;

        gameObject.SetActive(false);
    }

    private void Start()
    {
        FetchDropRatesFromDatabase();
    }

    private void OnDestroy()
    {
        if (backToStartButton != null)
            backToStartButton.onClick.RemoveListener(OnBackToStartClicked);
    }

    private void Update()
    {
        if (sunburst != null && sunburst.gameObject.activeInHierarchy)
        {
            sunburst.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// ดึงค่า drop_rate จากตาราง island_item บน Supabase มาเขียนทับ gardenDropPool ใน memory
    /// </summary>
    public void FetchDropRatesFromDatabase()
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogWarning("[MainRewardController] ⚠️ ไม่พบ SupabaseManager! จะใช้ค่า dropRate เดิมใน Inspector");
            return;
        }

        StartCoroutine(SupabaseManager.Instance.GetIslandItems("ISL_GARDEN", (dbItems) =>
        {
            if (dbItems == null || dbItems.Count == 0) return;

            foreach (var dbItem in dbItems)
            {
                GardenDropItem targetItem = gardenDropPool.Find(x => x.itemId == dbItem.item_id);
                if (targetItem != null)
                {
                    targetItem.dropRate = dbItem.drop_rate;
                    Debug.Log($"[MainRewardController] 🔄 ซิงก์ dropRate '{dbItem.item_id}' = {dbItem.drop_rate} จาก DB สำเร็จ");
                }
            }
        }));
    }

    /// <summary>
    /// สุ่มไอเทมจาก gardenDropPool ตาม dropRate ใน DB
    /// </summary>
    public void TriggerRandomCareReward()
    {
        if (gardenDropPool == null || gardenDropPool.Count == 0)
        {
            Debug.LogWarning("[MainRewardController] ⚠️ ไม่พบรายการไอเทมใน gardenDropPool!");
            return;
        }

        float totalWeight = 0f;
        foreach (var item in gardenDropPool)
        {
            totalWeight += item.dropRate;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentSum = 0f;
        GardenDropItem selectedItem = gardenDropPool[0];

        foreach (var item in gardenDropPool)
        {
            currentSum += item.dropRate;
            if (randomValue <= currentSum)
            {
                selectedItem = item;
                break;
            }
        }

        OpenRewardWindow(selectedItem.itemId, selectedItem.itemName, selectedItem.itemIcon);
    }

    public void OpenRewardWindow(string itemId, string itemName, Sprite itemIcon)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        currentRewardedItemId = itemId;
        gameObject.SetActive(true);

        if (rewardItemImage != null)
        {
            rewardItemImage.sprite = itemIcon;
            rewardItemImage.preserveAspect = true;
            rewardItemImage.enabled = itemIcon != null;
            rewardItemImage.gameObject.SetActive(true);
        }

        if (rewardNameText != null)
        {
            rewardNameText.text = itemName ?? "";
            rewardNameText.gameObject.SetActive(true);
        }

        if (youGotText != null)
            youGotText.gameObject.SetActive(true);

        if (sunburst != null)
            sunburst.gameObject.SetActive(true);

        if (backToStartButton != null)
            backToStartButton.gameObject.SetActive(true);

        if (sequenceCoroutine != null)
            StopCoroutine(sequenceCoroutine);

        sequenceCoroutine = StartCoroutine(PlaySequenceAnimation());
    }

    private IEnumerator PlaySequenceAnimation()
    {
        RectTransform buttonRect = backToStartButton != null ? backToStartButton.transform as RectTransform : null;

        SetHidden(sunburst);
        if (rewardItemImage != null) SetHidden(rewardItemImage.rectTransform);
        if (youGotText != null) SetHidden(youGotText.rectTransform);
        if (rewardNameText != null) SetHidden(rewardNameText.rectTransform);
        if (buttonRect != null) SetHidden(buttonRect);

        if (sunburst != null) StartCoroutine(PopAnimation(sunburst, popDuration, 1.1f));

        if (rewardItemImage != null)
            yield return StartCoroutine(PopAnimation(rewardItemImage.rectTransform, popDuration, itemPopOvershoot));

        yield return new WaitForSeconds(textDelay);

        if (youGotText != null)
            yield return StartCoroutine(FadeAnimation(youGotText.rectTransform, fadeDuration));

        yield return new WaitForSeconds(textDelay);

        if (rewardNameText != null)
            yield return StartCoroutine(FadeAnimation(rewardNameText.rectTransform, fadeDuration));

        yield return new WaitForSeconds(0.12f);

        if (buttonRect != null)
            yield return StartCoroutine(PopAnimation(buttonRect, popDuration, buttonPopOvershoot));

        sequenceCoroutine = null;
    }

    private void SetHidden(RectTransform target)
    {
        if (target == null) return;
        target.localScale = Vector3.zero;
        CanvasGroup group = GetOrAddCanvasGroup(target.gameObject);
        group.alpha = 0f;
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

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();
        return group;
    }

    private void OnBackToStartClicked()
    {
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(currentRewardedItemId))
        {
            SupabaseManager.Instance.StartCoroutine(SupabaseManager.Instance.AddItemToInventory(
                currentRewardedItemId,
                1,
                onSuccess: () =>
                {
                    Debug.Log($"[MainRewardController] 🟢 บันทึกไอเทม '{currentRewardedItemId}' ลง Supabase DB สำเร็จ!");
                },
                onError: (error) =>
                {
                    Debug.LogError($"[MainRewardController] 🔴 ยิง Supabase DB ล้มเหลว: {error}");
                }
            ));
        }

        gameObject.SetActive(false);
    }

    // ==================== 📅 Daily Reward (รางวัลประจำวัน) ====================
    // ขั้นตอน: เข้า MainForest -> ยิง RPC claim_daily_reward
    //   - วันนี้ (เวลาไทย) ยังไม่รับ -> DB สุ่ม silver 85% / gold 15% + ใส่ของลงกระเป๋าให้เลย -> เด้ง DailyRewardPopup
    //   - วันนี้รับไปแล้ว -> ไม่ทำอะไร
    // รีเซ็ตทุกเที่ยงคืนเวลาไทย เหมือน stamina
    // หมายเหตุ: GameObject ของสคริปต์นี้ถูกปิดใน Awake (Coroutine/Update ใช้ไม่ได้)
    //          จึงให้ SupabaseManager (ไม่ถูกปิด) เป็นคนรัน Coroutine แทน
    public void CheckDailyReward()
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogWarning("[MainRewardController] ⚠️ ไม่พบ SupabaseManager! ข้ามการเช็ครางวัลประจำวัน");
            return;
        }

        Debug.Log($"[DailyReward] 🔍 เริ่มเช็ครางวัลประจำวันของ {SupabaseManager.Instance.GetActiveUserId()}");

        SupabaseManager.Instance.StartCoroutine(SupabaseManager.Instance.ClaimDailyReward(
            onResult: (result) =>
            {
                if (result == null) return;
                if (!result.claimed)   // รับไปแล้ววันนี้ / ไม่เจอ user / ไม่มีของใน tier
                {
                    Debug.Log($"[DailyReward] 🟡 ไม่ได้รับรางวัล: {result.message}");
                    return;
                }
                Debug.Log($"[DailyReward] 🟢 ได้ {result.reward_item_name} ({result.reward_item_id}) tier {result.reward_tier} x{result.reward_quantity} -> ใส่ inventory ใน DB แล้ว");
                if (this == null) return;                        // เปลี่ยนซีนไปแล้ว
                OpenDailyRewardPopup(result.reward_item_id, result.reward_item_name, result.reward_tier, result.reward_quantity);
            },
            onError: (error) =>
            {
                Debug.LogError($"[MainRewardController] 🔴 เช็ครางวัลประจำวันล้มเหลว: {error}");
            }
        ));
    }

    // ใส่ข้อมูลไอเทมลง DailyRewardPopup แล้วเล่นอนิเมชัน (ใช้ PopAnimation/FadeAnimation ตัวเดียวกับหน้าต่างเดิม)
    private void OpenDailyRewardPopup(string itemId, string itemName, string tier, int quantity)
    {
        if (dailyPopupRoot == null)
        {
            Debug.LogWarning("[MainRewardController] ⚠️ ยังไม่ได้ลาก DailyRewardPopup ใส่ช่อง dailyPopupRoot");
            return;
        }

        if (spriteSource == null)
            spriteSource = FindAnyObjectByType<MainInventoryManager>(FindObjectsInactive.Include);

        // 1. กรอบตาม tier (silver / gold)
        //    (หาจาก Frame Sprites ของ MainInventoryManager ถ้าไม่เจอใช้ Default Frame)
        if (dailyItemFrame != null && spriteSource != null && !string.IsNullOrEmpty(tier))
        {
            var frame = spriteSource.frameSprites?.Find(x => x.rarityTier.ToLower() == tier.ToLower());
            dailyItemFrame.sprite = frame != null ? frame.frameSprite : spriteSource.defaultFrame;
        }

        // 2. รูปไอเทมที่สุ่มได้
        if (dailyItemImage != null)
        {
            Sprite icon = FindItemSprite(itemId);
            dailyItemImage.sprite = icon;
            dailyItemImage.preserveAspect = true;
            dailyItemImage.enabled = icon != null;
        }

        // 3. ชื่อไอเทม + จำนวน เช่น "ไข่ x1"
        if (dailyItemNameText != null)
            dailyItemNameText.text = $"{itemName} x{(quantity > 0 ? quantity : 1)}";   // กันกรณี DB ยังไม่ส่งจำนวนมา

        dailyPopupRoot.SetActive(true);
        Debug.Log($"[DailyReward] 🎁 เปิด popup: {itemName} x{quantity} (กรอบ {tier})");
        SupabaseManager.Instance.StartCoroutine(PlayDailyPopupAnimation());
        SupabaseManager.Instance.StartCoroutine(RotateDailySunburst());
    }

    // ลำดับอนิเมชัน: กล่องเด้ง -> แสง+ไอเทมเด้ง -> ชื่อค่อยๆ โผล่ -> ปุ่มเด้ง
    private IEnumerator PlayDailyPopupAnimation()
    {
        RectTransform frameRect = dailyItemFrame != null ? dailyItemFrame.rectTransform : null;
        RectTransform nameRect = dailyItemNameText != null ? dailyItemNameText.rectTransform : null;
        RectTransform buttonRect = dailyClaimButton != null ? dailyClaimButton.transform as RectTransform : null;

        SetHidden(dailySunburst);
        SetHidden(frameRect);
        SetHidden(nameRect);
        SetHidden(buttonRect);

        if (dailyPopupPanel != null)
            yield return PopAnimation(dailyPopupPanel, popDuration, 1.05f);

        if (dailySunburst != null) SupabaseManager.Instance.StartCoroutine(PopAnimation(dailySunburst, popDuration, 1.1f));
        if (frameRect != null) yield return PopAnimation(frameRect, popDuration, itemPopOvershoot);

        yield return new WaitForSeconds(textDelay);
        if (nameRect != null) yield return FadeAnimation(nameRect, fadeDuration);

        yield return new WaitForSeconds(0.12f);
        if (buttonRect != null) yield return PopAnimation(buttonRect, popDuration, buttonPopOvershoot);
    }

    // หมุน sunburst ตลอดเวลาที่ popup เปิดอยู่ (แทน Update เพราะสคริปต์นี้ถูกปิด)
    private IEnumerator RotateDailySunburst()
    {
        while (dailySunburst != null && dailyPopupRoot != null && dailyPopupRoot.activeInHierarchy)
        {
            dailySunburst.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
            yield return null;
        }
    }

    // ปุ่ม "รับ": ของอยู่ในกระเป๋าแล้ว (RPC ใส่ให้) แค่ปิด popup
    private void OnDailyClaimClicked()
    {
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        if (dailyPopupRoot != null)
            dailyPopupRoot.SetActive(false);
        Debug.Log("[DailyReward] ✅ กดรับแล้ว ปิด popup");
    }

    // หารูปไอเทม: ดูใน gardenDropPool ก่อน ถ้าไม่มีไปดู Item Sprites ของ MainInventoryManager
    private Sprite FindItemSprite(string itemId)
    {
        GardenDropItem poolItem = gardenDropPool.Find(x => x.itemId == itemId);
        if (poolItem != null && poolItem.itemIcon != null) return poolItem.itemIcon;

        if (spriteSource != null && spriteSource.itemSprites != null)
        {
            var mapping = spriteSource.itemSprites.Find(x => x.itemId == itemId);
            if (mapping != null) return mapping.itemSprite;
        }

        Debug.LogWarning($"[MainRewardController] ⚠️ ไม่พบรูปของ '{itemId}' (เพิ่มใน Item Sprites ของ MainInventoryManager)");
        return null;
    }
}
