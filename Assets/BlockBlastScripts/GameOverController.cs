using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class RewardItem
{
    public string itemName;
    public Sprite itemIcon;
}

[System.Serializable]
public class ScoreRewardTier
{
    public string tierName;
    public int minScore;
    public List<RewardItem> possibleRewards;
}

public class GameOverController : MonoBehaviour
{
    [Header("Game Over GameObject")]
    [SerializeField] private GameObject gameOverPanel;

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

    [Header("Reward Tiers")]
    [SerializeField] private List<ScoreRewardTier> rewardTiers = new List<ScoreRewardTier>();

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

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (finalScore != null) finalScore.text = finalScoreVal.ToString();

            PickRewardByScore(finalScoreVal);
            StartCoroutine(AnimateGameOverPopup());
        }
    }

    private void PickRewardByScore(int scoreVal)
    {
        ScoreRewardTier matchedTier = null;

        for (int i = rewardTiers.Count - 1; i >= 0; i--)
        {
            if (scoreVal >= rewardTiers[i].minScore)
            {
                matchedTier = rewardTiers[i];
                break;
            }
        }

        if (matchedTier == null && rewardTiers.Count > 0)
        {
            matchedTier = rewardTiers[0];
        }

        if (matchedTier != null && matchedTier.possibleRewards != null && matchedTier.possibleRewards.Count > 0)
        {
            int randIndex = Random.Range(0, matchedTier.possibleRewards.Count);
            RewardItem selectedReward = matchedTier.possibleRewards[randIndex];

            if (itemImg != null && selectedReward.itemIcon != null)
            {
                itemImg.sprite = selectedReward.itemIcon;
            }

            if (itemName != null)
            {
                itemName.text = selectedReward.itemName;
            }
        }
    }

    private IEnumerator AnimateGameOverPopup()
    {
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

            // Ease-Out Back
            t -= 1f;
            float easeOut = (t * t * ((s + 1f) * t + s) + 1f);

            float currentScale = Mathf.LerpUnclamped(startScale, 1f, easeOut);
            target.localScale = Vector3.one * currentScale;

            yield return null;
        }

        target.localScale = Vector3.one;
    }
    private void SetElementScale(Transform t, float scale)
    {
        if (t != null) t.localScale = Vector3.one * scale;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToHome()
    {
        if (!string.IsNullOrEmpty(homeSceneName))
        {
            SceneManager.LoadScene(homeSceneName);
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }
}