using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public const int ROWS = 8;
    public const int COLS = 8;

    [Header("UI References")]
    public GameObject cellPrefab; 
    public RectTransform boardGrid; 
    public GameObject squarePrefab; 

    [Header("Combo Text Prefab")]
    [SerializeField] private GameObject comboTextPrefab; 

    [Header("Score UI")]
    public TextMeshProUGUI ScoreText;        
    public TextMeshProUGUI BestScoreText;    

    [Header("Game Over Controller")]
    [SerializeField] private GameOverController gameOverController;

    private bool[,] occupied = new bool[ROWS, COLS];
    private RectTransform[,] cellTransforms = new RectTransform[ROWS, COLS];
    private GameObject[,] placedBlocks = new GameObject[ROWS, COLS];
    private List<GameObject> activePreviews = new List<GameObject>();

    public float ActualCellSize { get; private set; } = 40f;
    private int currentScore = 0;
    private int bestScore = 0;
    private int currentCombo = 0;
    private bool isGameOver = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CreateBoard();
        LoadAndDisplayScores();
    }

    // สร้างช่องตาราง 8x8
    void CreateBoard()
    {
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                GameObject cell = Instantiate(cellPrefab, boardGrid);
                cell.name = $"Cell_{r}_{c}";
                cellTransforms[r, c] = cell.GetComponent<RectTransform>();
            }
        }

        Canvas.ForceUpdateCanvases();
        if (cellTransforms[0, 0] != null)
        {
            ActualCellSize = cellTransforms[0, 0].rect.width;
            if (ActualCellSize <= 0) ActualCellSize = 120f;
        }
    }

    private void LoadAndDisplayScores()
    {
        currentScore = 0;
        bestScore = PlayerPrefs.GetInt("BestScore", 0);

        if (ScoreText != null) ScoreText.text = "0";
        if (BestScoreText != null) BestScoreText.text = bestScore.ToString();
    }

    public bool HasAnyValidPlacement(int[,] pattern)
    {
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (CanPlace(r, c, pattern)) return true;
            }
        }
        return false;
    }

    public bool TryFindSnapPosition(List<Transform> squarePieces, List<Vector2Int> localOffsets, out int bestStartRow, out int bestStartCol)
    {
        bestStartRow = -1;
        bestStartCol = -1;
        if (squarePieces == null || squarePieces.Count == 0) return false;

        float snapRadius = ActualCellSize * 0.85f;
        Transform samplePiece = squarePieces[0];
        Vector2Int sampleOffset = localOffsets[0];

        int nearRow = -1;
        int nearCol = -1;
        float minDistance = float.MaxValue;

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (cellTransforms[r, c] == null) continue;
                float dist = Vector3.Distance(samplePiece.position, cellTransforms[r, c].position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearRow = r;
                    nearCol = c;
                }
            }
        }

        if (minDistance > snapRadius) return false;

        bestStartRow = nearRow - sampleOffset.x;
        bestStartCol = nearCol - sampleOffset.y;
        return true;
    }

    public bool CanPlace(int startRow, int startCol, int[,] pattern)
    {
        int pRows = pattern.GetLength(0);
        int pCols = pattern.GetLength(1);

        for (int r = 0; r < pRows; r++)
        {
            for (int c = 0; c < pCols; c++)
            {
                if (pattern[r, c] == 1)
                {
                    int targetR = startRow + r;
                    int targetC = startCol + c;

                    if (targetR < 0 || targetR >= ROWS || targetC < 0 || targetC >= COLS)
                        return false;

                    if (occupied[targetR, targetC])
                        return false;
                }
            }
        }
        return true;
    }

    // แสดงเงาโปร่งใส (Alpha 0.25) บนกระดานตามตำแหน่งที่บล็อกจะลง
    public void ShowPreview(int startRow, int startCol, int[,] pattern)
    {
        ClearPreview();
        if (!CanPlace(startRow, startCol, pattern)) return;

        int pRows = pattern.GetLength(0);
        int pCols = pattern.GetLength(1);

        for (int r = 0; r < pRows; r++)
        {
            for (int c = 0; c < pCols; c++)
            {
                if (pattern[r, c] == 1)
                {
                    int tr = startRow + r;
                    int tc = startCol + c;

                    GameObject previewObj = Instantiate(squarePrefab, cellTransforms[tr, tc]);
                    RectTransform rt = previewObj.GetComponent<RectTransform>();
                    rt.sizeDelta = cellTransforms[tr, tc].sizeDelta;
                    rt.anchoredPosition = Vector2.zero;

                    Image img = previewObj.GetComponent<Image>();
                    if (img != null)
                    {
                        Color cColor = img.color;
                        img.color = new Color(cColor.r, cColor.g, cColor.b, 0.25f);
                    }
                    activePreviews.Add(previewObj);
                }
            }
        }
    }

    public void ClearPreview()
    {
        foreach (var obj in activePreviews)
        {
            if (obj != null) Destroy(obj);
        }
        activePreviews.Clear();
    }

    // นำบล็อกลงกระดาน อัปเดตสถานะ เพิ่มคะแนน และสั่งตรวจแถวเต็ม
    public bool PlaceShape(int startRow, int startCol, int[,] pattern)
    {
        if (!CanPlace(startRow, startCol, pattern)) return false;

        ClearPreview();
        int pRows = pattern.GetLength(0);
        int pCols = pattern.GetLength(1);
        int placedCount = 0;

        for (int r = 0; r < pRows; r++)
        {
            for (int c = 0; c < pCols; c++)
            {
                if (pattern[r, c] == 1)
                {
                    int tr = startRow + r;
                    int tc = startCol + c;

                    occupied[tr, tc] = true; // นำบล็อกลงกระดาน
                    GameObject placedObj = Instantiate(squarePrefab, cellTransforms[tr, tc]);
                    RectTransform rt = placedObj.GetComponent<RectTransform>();
                    rt.sizeDelta = cellTransforms[tr, tc].sizeDelta;
                    rt.anchoredPosition = Vector2.zero;

                    placedBlocks[tr, tc] = placedObj;
                    placedCount++;
                }
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlaceSound();
        }

        AddScore(placedCount * 10); // ได้แต้มตามจำนวนชิ้นที่วางลงไป
        CheckAndClearLines(); // เช็กว่ามีแถวเต็มหรือไม่

        if (BlockSpawner.Instance != null)
        {
            BlockSpawner.Instance.CheckAllAvailableShapes(); // สั่ง Spawner เช็กบล็อกที่เหลือ
        }

        return true;
    }

    private void CheckAndClearLines()
    {
        // หาแถวแนวนอนและแนวตั้งที่บล็อกเต็มทุกช่อง
        List<int> fullRows = new List<int>();
        List<int> fullCols = new List<int>();

        for (int r = 0; r < ROWS; r++)
        {
            bool isFull = true;
            for (int c = 0; c < COLS; c++)
            {
                if (!occupied[r, c]) { isFull = false; break; }
            }
            if (isFull) fullRows.Add(r);
        }

        for (int c = 0; c < COLS; c++)
        {
            bool isFull = true;
            for (int r = 0; r < ROWS; r++)
            {
                if (!occupied[r, c]) { isFull = false; break; }
            }
            if (isFull) fullCols.Add(c);
        }

        int linesCleared = fullRows.Count + fullCols.Count;

        if (linesCleared == 0)
        {
            currentCombo = 0; // ไม่มีแถวเคลียร์ คอมโบ = 0
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBreakSound();
        }

        currentCombo++; // แถวเคลียร์ ได้คอมโบเพิ่ม
        HashSet<Vector2Int> cellsToClear = new HashSet<Vector2Int>();

        // สร้างเลเซอร์แนวนอนและแนวตั้งผ่านแถวที่เคลียร์
        foreach (int r in fullRows)
        {
            for (int c = 0; c < COLS; c++) cellsToClear.Add(new Vector2Int(r, c));
            StartCoroutine(SpawnLaserLine(r, true));
        }

        foreach (int c in fullCols)
        {
            for (int r = 0; r < ROWS; r++) cellsToClear.Add(new Vector2Int(r, c));
            StartCoroutine(SpawnLaserLine(c, false));
        }

        List<GameObject> blocksToAnimate = new List<GameObject>();
        Vector3 averageWorldPos = Vector3.zero;

        foreach (var pos in cellsToClear)
        {
            occupied[pos.x, pos.y] = false;
            if (placedBlocks[pos.x, pos.y] != null)
            {
                blocksToAnimate.Add(placedBlocks[pos.x, pos.y]);
                averageWorldPos += placedBlocks[pos.x, pos.y].transform.position;
                placedBlocks[pos.x, pos.y] = null;
            }
        }

        if (blocksToAnimate.Count > 0) averageWorldPos /= blocksToAnimate.Count;

        // โบนัสคอมโบต่อเนื่อง
        int baseLineScore = (linesCleared * (linesCleared + 1) / 2) * 100; 
        int comboBonus = (currentCombo > 1) ? (currentCombo * 100) : 0;
        int totalEarned = baseLineScore + comboBonus;

        bool isBoardEmpty = true;
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (occupied[r, c]) { isBoardEmpty = false; break; }
            }
            if (!isBoardEmpty) break;
        }

        StartCoroutine(ClearSequence(blocksToAnimate, totalEarned, currentCombo, isBoardEmpty, averageWorldPos));
    }

    private IEnumerator ClearSequence(List<GameObject> blocks, int earnedScore, int comboCount, bool isAllClear, Vector3 centerPos)
    {
        yield return StartCoroutine(PlayClearAnimation(blocks));
        yield return StartCoroutine(SpawnComboOrScoreText(earnedScore, comboCount, isAllClear, centerPos));

        AddScore(earnedScore);

        if (isAllClear)
        {
            int allClearBonus = 1000;
            AddScore(allClearBonus);
            yield return StartCoroutine(SpawnAllClearBanner(allClearBonus));
        }
    }

    private IEnumerator SpawnComboOrScoreText(int score, int combo, bool isAllClear, Vector3 worldPos)
    {
        if (comboTextPrefab == null) yield break;

        GameObject textObj = Instantiate(comboTextPrefab, boardGrid.root);
        textObj.transform.position = worldPos;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            if (combo > 1)
            {
                tmp.text = $"<color=#FFFFFF>คอมโบ</color> <color=#FFCC00>X{combo}</color>";
            }
            else
            {
                string[] normalPraises = { "เยี่ยม!", "ดีมาก!", "สวยงาม!", "แจ๋วเลย!", "เจ๋งมาก!" };
                string[] highPraises = { "สุดยอด!", "เพอร์เฟกต์!", "ว้าว!", "ยอดเยี่ยม!", "เก่งมาก!" };

                string praise;
                if (score >= 400)
                {
                    praise = highPraises[Random.Range(0, highPraises.Length)];
                }
                else
                {
                    praise = normalPraises[Random.Range(0, normalPraises.Length)];
                }

                tmp.text = $"<color=#FFB300>+{score}</color>\n<color=#4EFA72>{praise}</color>";
            }
        }

        RectTransform rt = textObj.GetComponent<RectTransform>();
        float duration = 0.85f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 targetScale = Vector3.one * 1.15f;
        Vector3 startPos = rt.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (t < 0.25f)
            {
                rt.localScale = Vector3.Lerp(startScale, targetScale, t / 0.25f);
            }
            else
            {
                rt.localScale = Vector3.Lerp(targetScale, Vector3.one, (t - 0.25f) / 0.75f);
                rt.position = startPos + new Vector3(0, (t - 0.25f) * 60f, 0);

                if (t > 0.6f && tmp != null)
                {
                    float fadeT = (t - 0.6f) / 0.4f;
                    tmp.alpha = Mathf.Lerp(1f, 0f, fadeT);
                }
            }

            yield return null;
        }

        Destroy(textObj);
    }

    private IEnumerator SpawnAllClearBanner(int bonus)
    {
        GameObject banner = new GameObject("AllClearBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
        banner.transform.SetParent(boardGrid.root, false);
        banner.transform.position = boardGrid.position;

        TextMeshProUGUI tmp = banner.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 92;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        tmp.text = $"<b><color=#FFE600>ล้างกระดาน!</color></b>\n<b><color=#FFFFFF>+{bonus}</color></b>";
        tmp.outlineWidth = 0.38f;
        tmp.outlineColor = new Color32(20, 20, 30, 255);
        tmp.raycastTarget = false;

        RectTransform rt = banner.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 400);

        float duration = 1.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.25f, t);
            if (t > 0.7f) tmp.alpha = Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);

            yield return null;
        }

        Destroy(banner);
    }

    private IEnumerator SpawnLaserLine(int index, bool isRow)
    {
        GameObject laser = new GameObject("LaserLine", typeof(RectTransform), typeof(Image));
        laser.transform.SetParent(boardGrid.root, false);
        RectTransform rt = laser.GetComponent<RectTransform>();
        Image img = laser.GetComponent<Image>();

        Color laserColor = new Color(0.2f, 0.85f, 1f, 0.95f);
        img.color = laserColor;
        img.raycastTarget = false;

        float fullLength = boardGrid.rect.width * boardGrid.lossyScale.x;
        float thickness = ActualCellSize * boardGrid.lossyScale.y * 0.45f;

        if (isRow)
        {
            rt.sizeDelta = new Vector2(fullLength, thickness);
            Vector3 centerRowPos = (cellTransforms[index, 0].position + cellTransforms[index, COLS - 1].position) * 0.5f;
            rt.position = centerRowPos;
        }
        else
        {
            rt.sizeDelta = new Vector2(thickness, fullLength);
            Vector3 centerColPos = (cellTransforms[0, index].position + cellTransforms[ROWS - 1, index].position) * 0.5f;
            rt.position = centerColPos;
        }

        float duration = 0.45f;
        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (isRow) rt.localScale = new Vector3(baseScale.x, Mathf.Lerp(1.6f, 0f, t), 1f);
            else rt.localScale = new Vector3(Mathf.Lerp(1.6f, 0f, t), baseScale.y, 1f);

            img.color = new Color(laserColor.r, laserColor.g, laserColor.b, Mathf.Lerp(0.95f, 0f, t));
            yield return null;
        }

        Destroy(laser);
    }

    private IEnumerator PlayClearAnimation(List<GameObject> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            GameObject block = blocks[i];
            if (block != null) StartCoroutine(PopAndDestroy(block));
            yield return new WaitForSeconds(0.02f);
        }

        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator PopAndDestroy(GameObject block)
    {
        if (block == null) yield break;

        Transform t = block.transform;
        Image img = block.GetComponent<Image>();
        Color startCol = img != null ? img.color : Color.white;
        Vector3 origScale = t.localScale;

        float elapsed = 0f;
        float popTime = 0.12f;
        while (elapsed < popTime)
        {
            if (block == null) yield break;
            elapsed += Time.deltaTime;
            float progress = elapsed / popTime;

            t.localScale = Vector3.Lerp(origScale, origScale * 1.3f, progress);
            if (img != null) img.color = Color.Lerp(startCol, Color.white, progress);
            yield return null;
        }

        elapsed = 0f;
        float shrinkTime = 0.18f;
        Vector3 popScale = t.localScale;
        while (elapsed < shrinkTime)
        {
            if (block == null) yield break;
            elapsed += Time.deltaTime;
            float progress = elapsed / shrinkTime;

            t.localScale = Vector3.Lerp(popScale, Vector3.zero, progress);
            if (img != null)
            {
                Color c = img.color;
                img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, progress));
            }
            yield return null;
        }

        Destroy(block);
    }

    private void AddScore(int amount)
    {
        currentScore += amount;

        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            PlayerPrefs.SetInt("BestScore", bestScore); // อัปเดตสถิติสูงสุด
            PlayerPrefs.Save();
            if (BestScoreText != null) BestScoreText.text = bestScore.ToString();
        }

        if (ScoreText != null)
        {
            ScoreText.text = currentScore.ToString();
        }
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGameOverSound();
        }

        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            PlayerPrefs.SetInt("BestScore", bestScore);
            PlayerPrefs.Save();
        }

        if (gameOverController != null)
        {
            gameOverController.ShowGameOver(currentScore);
        }
    }
}