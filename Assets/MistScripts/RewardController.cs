using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Buttons")]
    public Button backToStartButton;        

    private void Awake()
    {
        if (backToStartButton != null)
        {
            backToStartButton.onClick.RemoveAllListeners();
            backToStartButton.onClick.AddListener(OnBackToStartClicked);
        }
    }

    private void Update()
    {
        if (sunburst != null)
        {
            sunburst.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    public void OpenRewardWindow(ItemData item)
    {
        gameObject.SetActive(true);

        if (item != null)
        {
            if (rewardItemImage != null)
            {
                rewardItemImage.sprite = item.itemIcon;
                rewardItemImage.enabled = true;
                rewardItemImage.gameObject.SetActive(true);

                Color c = rewardItemImage.color;
                c.a = 1f;
                rewardItemImage.color = c;
            }

            if (rewardNameText != null)
            {
                rewardNameText.text = item.itemName;
                rewardNameText.enabled = true;
                rewardNameText.gameObject.SetActive(true);
            }
        }

        StopAllCoroutines();
        StartCoroutine(PlaySequenceAnimation());
    }

    private IEnumerator PlaySequenceAnimation()
    {
        SetScaleZero(sunburst);
        if (rewardItemImage != null) SetScaleZero(rewardItemImage.rectTransform);
        if (youGotText != null) SetScaleZero(youGotText.rectTransform);
        if (rewardNameText != null) SetScaleZero(rewardNameText.rectTransform);
        if (backToStartButton != null) SetScaleZero(backToStartButton.image.rectTransform);

        if (sunburst != null) StartCoroutine(PopAnimation(sunburst, popDuration, 1.1f));
        if (rewardItemImage != null) yield return StartCoroutine(PopAnimation(rewardItemImage.rectTransform, popDuration, 1.25f));

        yield return new WaitForSeconds(0.08f);

        if (youGotText != null) yield return StartCoroutine(PopAnimation(youGotText.rectTransform, popDuration, 1.15f));

        yield return new WaitForSeconds(0.08f);

        if (rewardNameText != null) yield return StartCoroutine(PopAnimation(rewardNameText.rectTransform, popDuration, 1.15f));

        yield return new WaitForSeconds(0.12f);

        if (backToStartButton != null) yield return StartCoroutine(PopAnimation(backToStartButton.image.rectTransform, popDuration, 1.1f));
    }

    private void SetScaleZero(RectTransform target)
    {
        if (target != null) target.localScale = Vector3.zero;
    }

    private IEnumerator PopAnimation(RectTransform target, float duration, float overshoot)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        target.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float backT = 1f + (overshoot - 1f) * Mathf.Sin(t * Mathf.PI);

            target.localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, t) * backT);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    private void OnBackToStartClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.BackToStart();
        }
    }
}