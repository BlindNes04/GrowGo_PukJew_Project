using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class RewardItem
{
    public string itemId;     
    public string itemName;   
    public Sprite itemIcon;  
}

public class GameOverController : MonoBehaviour
{
    [Header("Game Over GameObject")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Game Over Banner")]
    [SerializeField] private TextMeshProUGUI gameOverBannerText;
    [SerializeField] private float bannerFloatDistance = 80f;
    [SerializeField] private float bannerDuration = 0.85f;

    [Header("Elements to Pop/Fade")]
    [SerializeField] private Transform frame;
    [SerializeField] private Transform gameOverText;
    [SerializeField] private Transform scoreHeader;
    [SerializeField] private TextMeshProUGUI finalScore;
    [SerializeField] private RectTransform sunburst;
    [SerializeField] private Image itemImg;
    [SerializeField] private Transform youGotText;
    [SerializeField] private TextMeshProUGUI itemName;

    [Header("Buttons & Scenes")]
    [SerializeField] private Button homebutton;
    [SerializeField] private Button restartbutton;
    [SerializeField] private string homeSceneName = "MainMenu";

    [Header("Reward (Silver)")]
    [SerializeField] private List<RewardItem> silverItems = new List<RewardItem>();
    [Header("Reward (Gold)")]
    [SerializeField] private List<RewardItem> goldItems = new List<RewardItem>();

    [Header("Reward (Special)")]
    [SerializeField] private RewardItem wateringCard30;
    [SerializeField] private RewardItem wateringCard50;
    [SerializeField] private RewardItem revivePotion;

    [Header("Sunburst Settings")]
    [SerializeField] private float rotateSpeed = 20f;

    private bool isShown = false;

    private void Awake()
    {
        if (restartbutton != null) restartbutton.onClick.AddListener(RestartGame);
        if (homebutton != null) homebutton.onClick.AddListener(GoToHome);
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverBannerText != null) gameOverBannerText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isShown && sunburst != null)
        {
            sunburst.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    public void ShowGameOver(int finalScoreVal)
    {
        if (isShown) return;
        isShown = true;

        if (finalScore != null) finalScore.text = finalScoreVal.ToString();

        PickRewardByScore(finalScoreVal);
        StartCoroutine(AnimateGameOverPopup());
    }

    private void PickRewardByScore(int scoreVal)
    {
        RewardItem chosenReward = null;
        float roll = Random.Range(0f, 100f);

        // คะแนนต่ำกว่า 5,000 
        if (scoreVal < 5000)
        {
            if (roll < 20f)
            {
                chosenReward = wateringCard30;
            }
            else
            {
                chosenReward = GetRandomFromList(silverItems);
            }
        }
        // คะแนน 5,000 - 9,999
        else if (scoreVal < 10000)
        {
            if (roll < 25f)
            {
                chosenReward = wateringCard50; 
            }
            else if (roll < 60f)
            {
                chosenReward = wateringCard30; 
            }
            else
            {
                chosenReward = GetRandomFromList(silverItems); 
            }
        }
        // คะแนน 10,000 ขึ้นไป
        else
        {
            if (roll < 15f)
            {
                chosenReward = revivePotion;   
            }
            else if (roll < 55f)
            {
                chosenReward = wateringCard50; 
            }
            else if (roll < 85f)
            {
                chosenReward = wateringCard30; 
            }
            else
            {
                chosenReward = GetRandomFromList(goldItems);
            }
        }

        if (chosenReward != null)
        {
            if (itemImg != null && chosenReward.itemIcon != null)
            {
                itemImg.sprite = chosenReward.itemIcon;
            }

            if (itemName != null)
            {
                itemName.text = chosenReward.itemName;
            }

            SaveRewardToInventory(chosenReward.itemId);
        }
    }

    private RewardItem GetRandomFromList(List<RewardItem> list)
    {
        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    private void SaveRewardToInventory(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        Debug.Log($"[Reward] มอบไอเทม: {itemId}");

        int currentQty = PlayerPrefs.GetInt("INV_" + itemId, 0);
        PlayerPrefs.SetInt("INV_" + itemId, currentQty + 1);
        PlayerPrefs.Save();
    }

    private IEnumerator AnimateGameOverPopup()
    {
        if (gameOverBannerText != null)
        {
            yield return StartCoroutine(PlayBannerAnimation());
        }
        else
        {
            yield return new WaitForSeconds(0.85f);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        float baseStartScale = 0.85f;
        SetElementScale(frame, 0.82f);
        SetElementScale(gameOverText, baseStartScale);
        SetElementScale(scoreHeader, baseStartScale);
        if (finalScore != null) SetElementScale(finalScore.transform, baseStartScale);
        if (sunburst != null) SetElementScale(sunburst.transform, 0.75f);
        if (itemImg != null) SetElementScale(itemImg.transform, baseStartScale);
        SetElementScale(youGotText, baseStartScale);
        if (itemName != null) SetElementScale(itemName.transform, baseStartScale);
        if (homebutton != null) SetElementScale(homebutton.transform, baseStartScale);
        if (restartbutton != null) SetElementScale(restartbutton.transform, baseStartScale);

        float animDuration = 0.55f;
        float softBounce = 1.035f;

        if (frame != null) StartCoroutine(SmoothPop(frame, animDuration, softBounce, 0.82f));
        if (gameOverText != null) StartCoroutine(SmoothPop(gameOverText, animDuration, softBounce, baseStartScale));
        if (scoreHeader != null) StartCoroutine(SmoothPop(scoreHeader, animDuration, softBounce, baseStartScale));
        if (finalScore != null) StartCoroutine(SmoothPop(finalScore.transform, animDuration, softBounce, baseStartScale));
        if (sunburst != null) StartCoroutine(SmoothPop(sunburst.transform, animDuration + 0.05f, 1.05f, 0.75f));
        if (itemImg != null) StartCoroutine(SmoothPop(itemImg.transform, animDuration, 1.05f, baseStartScale));
        if (youGotText != null) StartCoroutine(SmoothPop(youGotText, animDuration, softBounce, baseStartScale));
        if (itemName != null) StartCoroutine(SmoothPop(itemName.transform, animDuration, softBounce, baseStartScale));
        if (homebutton != null) StartCoroutine(SmoothPop(homebutton.transform, animDuration, softBounce, baseStartScale));

        if (restartbutton != null)
        {
            yield return StartCoroutine(SmoothPop(restartbutton.transform, animDuration, softBounce, baseStartScale));
        }
    }

    private IEnumerator PlayBannerAnimation()
    {
        RectTransform bannerRect = gameOverBannerText.rectTransform;
        Vector2 startPos = bannerRect.anchoredPosition;
        Vector2 targetPos = startPos + new Vector2(0f, bannerFloatDistance);

        Color originalColor = gameOverBannerText.color;
        gameOverBannerText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        gameOverBannerText.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < bannerDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bannerDuration;

            bannerRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);

            float alpha = 1f;
            if (t < 0.25f)
            {
                alpha = t / 0.25f;
            }
            else if (t > 0.7f)
            {
                alpha = Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
            }

            gameOverBannerText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        gameOverBannerText.gameObject.SetActive(false);
        bannerRect.anchoredPosition = startPos;
        gameOverBannerText.color = originalColor;
    }

    private IEnumerator SmoothPop(Transform target, float duration, float overshoot, float startScale = 0.85f)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        target.localScale = Vector3.one * startScale;
        float s = (overshoot - 1f) * 1.70158f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease Out Back
            t -= 1f;
            float easeOut = (t * t * ((s + 1f) * t + s) + 1f);

            float currentScale = Mathf.LerpUnclamped(startScale, 1f, easeOut);
            target.localScale = Vector3.one * currentScale;

            yield return null;
        }

        target.localScale = Vector3.one;
    }

    private void SetElementScale(Transform target, float scale)
    {
        if (target != null) target.localScale = Vector3.one * scale;
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }
}