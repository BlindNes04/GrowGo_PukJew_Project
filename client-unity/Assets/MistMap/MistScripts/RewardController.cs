using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ==================== RewardController Class ====================
public class RewardController : MonoBehaviour
{
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
        if (backToStartButton != null)
        {
            backToStartButton.onClick.RemoveListener(OnBackToStartClicked);
            backToStartButton.onClick.AddListener(OnBackToStartClicked);
        }
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

    // Opens reward popup and caches item data
    public void OpenRewardWindow(ItemData item)
    {
        if (item == null)
        {
            Debug.LogError("[RewardController] 🔴 Received a null item!", this);
            return;
        }

        currentRewardedItemId = item.itemId;
        Debug.Log($"[RewardController] 🎁 Opened Reward Window with Item: {item.itemName} (ID: {item.itemId})");

        gameObject.SetActive(true);

        if (rewardItemImage != null)
        {
            rewardItemImage.sprite = item.itemIcon;
            rewardItemImage.preserveAspect = true;
            rewardItemImage.enabled = item.itemIcon != null;
            rewardItemImage.gameObject.SetActive(true);
        }

        if (rewardNameText != null)
        {
            rewardNameText.text = item.itemName ?? "";
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
        RectTransform buttonRect = backToStartButton != null
            ? backToStartButton.transform as RectTransform
            : null;

        SetHidden(sunburst);

        if (rewardItemImage != null)
            SetHidden(rewardItemImage.rectTransform);

        if (youGotText != null)
            SetHidden(youGotText.rectTransform);

        if (rewardNameText != null)
            SetHidden(rewardNameText.rectTransform);

        if (buttonRect != null)
            SetHidden(buttonRect);

        if (sunburst != null)
        {
            StartCoroutine(PopAnimation(sunburst, popDuration, 1.1f));
        }

        if (rewardItemImage != null)
        {
            yield return StartCoroutine(PopAnimation(rewardItemImage.rectTransform, popDuration, itemPopOvershoot));
        }

        yield return new WaitForSeconds(textDelay);

        if (youGotText != null)
        {
            yield return StartCoroutine(FadeAnimation(youGotText.rectTransform, fadeDuration));
        }

        yield return new WaitForSeconds(textDelay);

        if (rewardNameText != null)
        {
            yield return StartCoroutine(FadeAnimation(rewardNameText.rectTransform, fadeDuration));
        }

        yield return new WaitForSeconds(0.12f);

        if (buttonRect != null)
        {
            yield return StartCoroutine(PopAnimation(buttonRect, popDuration, buttonPopOvershoot));
        }

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
        if (target == null)
            yield break;

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

        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    // Saves item to Supabase inventory and triggers cooldown on BeachShellManager
    private void OnBackToStartClicked()
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogError("[RewardController] 🔴 SupabaseManager.Instance เป็น NULL!");
        }
        else if (string.IsNullOrEmpty(currentRewardedItemId))
        {
            Debug.LogError("[RewardController] 🔴 currentRewardedItemId เป็นค่าว่าง!");
        }
        else
        {
            Debug.Log($"[RewardController] 🔄 กำลังยิงบันทึกไอเทม '{currentRewardedItemId}' เข้าตาราง inventory ใน Supabase...");

            // 💡 เรียก StartCoroutine บน SupabaseManager.Instance เพื่อไม่ให้ดับตามป๊อปอัปที่โดน SetActive(false)
            SupabaseManager.Instance.StartCoroutine(SupabaseManager.Instance.AddItemToInventory(
                currentRewardedItemId,
                1,
                onSuccess: () =>
                {
                    Debug.Log($"[RewardController] 🟢 บันทึกไอเทม '{currentRewardedItemId}' ลง Supabase DB สำเร็จ!");
                },
                onError: (error) =>
                {
                    Debug.LogError($"[RewardController] 🔴 ยิง Supabase DB ล้มเหลว: {error}");
                }
            ));
        }

        // แจ้ง BeachShellManager ให้เริ่มนับ Cooldown
        if (BeachShellManager.Instance != null)
        {
            BeachShellManager.Instance.OnRewardClaimed();
        }

        // ปิดหน้าต่าง Popup รางวัล
        gameObject.SetActive(false);
    }
}