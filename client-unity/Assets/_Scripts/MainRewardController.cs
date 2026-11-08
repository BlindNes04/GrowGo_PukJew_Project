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
}