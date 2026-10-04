using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // ใช้สำหรับรีสตาร์ทเกม

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("การตั้งค่า UI")]
    public TextMeshProUGUI scoreText;
    public GameObject gameOverPanel; // หน้าต่าง UI ที่จะโชว์ตอนแพ้

    private int currentScore = 0;
    private bool isGameOver = false;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        UpdateScoreText();
        if (gameOverPanel != null) gameOverPanel.SetActive(false); // ซ่อนหน้า Game Over ไว้ก่อน
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

    // ฟังก์ชันนี้จะถูกเรียกเมื่อผลไม้ล้ำเส้นเกินเวลา
    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (gameOverPanel != null) gameOverPanel.SetActive(true); // โชว์หน้า Game Over

        // ปิดสคริปต์เสกผลไม้ เพื่อไม่ให้เล่นต่อได้
        FruitSpawner spawner = FindFirstObjectByType<FruitSpawner>();
        if (spawner != null) spawner.enabled = false;
    }

    // เอาไว้ผูกกับปุ่ม Restart
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}