using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Gameplay UI")]
    public TextMeshProUGUI scoreText;

    [Header("Game Over UI")]
    public GameObject gameOverPopup;
    public TextMeshProUGUI finalScoreText;
    public Image rewardImage;
    public TextMeshProUGUI rewardNameText;

    [Header("Sunburst Settings")]
    public RectTransform sunburst;
    public float rotateSpeed = 20f;

    [Header("Reward Sprites")]
    public Sprite soilSprite;
    public Sprite chickenManureSprite;
    public Sprite eggshellSprite;
    public Sprite herbSprite;
    public Sprite tomatoSeedSprite;
    public Sprite coconutSprite;
    public Sprite melonSeedSprite;

    [Header("Reward Item IDs (ต้องตรงกับ item_id ในตาราง item ของ Supabase)")]
    public string soilItemId = "ITEM_SOIL";
    public string chickenManureItemId = "ITEM_FERTILIZER_CHICKEN";
    public string eggshellItemId = "ITEM_EGGSHELL";
    public string herbItemId = "ITEM_HERB";
    public string tomatoSeedItemId = "ITEM_SEED_TOMATO";
    public string coconutSeedItemId = "ITEM_SEED_COCONUT";
    public string melonSeedItemId = "ITEM_SEED_MELON";

    private int currentScore = 0;
    private bool isGameOver = false;
    public bool hasMergedMelon = false;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        UpdateScoreText();
        if (gameOverPopup != null) gameOverPopup.SetActive(false);
    }

    void Update()
    {
        if (isGameOver && sunburst != null)
        {
            sunburst.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    public void AddScore(int points)
    {
        if (isGameOver) return;
        currentScore += points;
        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        if (scoreText != null) scoreText.text = currentScore.ToString();
    }

    public void UnlockMelon()
    {
        hasMergedMelon = true;
    }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        FruitSpawner spawner = FindAnyObjectByType<FruitSpawner>();
        if (spawner != null) spawner.enabled = false;

        if (finalScoreText != null) finalScoreText.text = currentScore.ToString();

        if (hasMergedMelon && currentScore >= 3000)
        {
            GrantReward(melonSeedItemId, 1, melonSeedSprite, "เมล็ดเมล่อน x1");
        }
        else if (hasMergedMelon && currentScore < 3000)
        {
            int dropChance = Random.Range(0, 100);

            if (dropChance < 70) 
                GrantReward(tomatoSeedItemId, 1, tomatoSeedSprite, "เมล็ดมะเขือเทศ x1");
            else 
                GrantReward(coconutSeedItemId, 1, coconutSprite, "เมล็ดมะพร้าว x1");
        }
        else
        {
            int randomGeneral = Random.Range(0, 4);

            if (randomGeneral == 0) 
                GrantReward(soilItemId, 3, soilSprite, "ดินโบราณ x3");
            else if (randomGeneral == 1) 
                GrantReward(chickenManureItemId, 3, chickenManureSprite, "มูลไก่หมัก x3");
            else if (randomGeneral == 2) 
                GrantReward(eggshellItemId, 3, eggshellSprite, "เปลือกไข่บด x3");
            else if (randomGeneral == 3) 
                GrantReward(herbItemId, 3, herbSprite, "สมุนไพร x3");
        }

        if (gameOverPopup != null)
        {
            StartCoroutine(AnimateGameOverPopup());
        }
    }

    // ฟังก์ชันแจกรางวัล: โชว์ UI + ยิงเข้า DB
    private void GrantReward(string itemId, int count, Sprite sprite, string itemName)
    {
        SetRewardUI(sprite, itemName);

        if (SupabaseManager.Instance != null)
        {
            StartCoroutine(SupabaseManager.Instance.AddItemToInventory(itemId, count, 
                onSuccess: () => 
                {
                    Debug.Log($"[MergeFruit] 🟢 ได้รับรางวัล {itemName} (ID: {itemId} x{count}) บันทึกลง DB เรียบร้อย");
                }, 
                onError: (error) => 
                {
                    Debug.LogError($"[MergeFruit] 🔴 บันทึกรางวัลลง DB ล้มเหลว: {error}");
                }
            ));
        }
        else
        {
            Debug.LogError("[MergeFruit] 🔴 ไม่พบ SupabaseManager.Instance ไม่สามารถมอบรางวัลลง DB ได้!");
        }
    }

    private void SetRewardUI(Sprite sprite, string itemName)
    {
        if (rewardImage != null) rewardImage.sprite = sprite;
        if (rewardNameText != null) rewardNameText.text = itemName;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator AnimateGameOverPopup()
    {
        gameOverPopup.SetActive(true);

        float baseStartScale = 0.85f;
        float animDuration = 0.55f;
        float softBounce = 1.035f;

        SetElementScale(gameOverPopup.transform, 0.82f);
        if (finalScoreText != null) SetElementScale(finalScoreText.transform, baseStartScale);
        if (rewardImage != null) SetElementScale(rewardImage.transform, baseStartScale);
        if (rewardNameText != null) SetElementScale(rewardNameText.transform, baseStartScale);
        if (sunburst != null) SetElementScale(sunburst.transform, 0.75f);

        StartCoroutine(SmoothPop(gameOverPopup.transform, animDuration, softBounce, 0.82f));

        if (finalScoreText != null) StartCoroutine(SmoothPop(finalScoreText.transform, animDuration, softBounce, baseStartScale));
        if (rewardImage != null) StartCoroutine(SmoothPop(rewardImage.transform, animDuration, 1.05f, baseStartScale));
        if (rewardNameText != null) StartCoroutine(SmoothPop(rewardNameText.transform, animDuration, softBounce, baseStartScale));
        if (sunburst != null) StartCoroutine(SmoothPop(sunburst.transform, animDuration + 0.05f, 1.05f, 0.75f));

        yield return null;
    }

    private IEnumerator SmoothPop(Transform target, float duration, float overshoot, float startScale = 0.85f)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        target.localScale = Vector3.one * startScale;
        float s = (overshoot - 1f) * 1.70158f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

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
}