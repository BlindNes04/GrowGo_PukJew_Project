using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockSpawner : MonoBehaviour
{
    public static BlockSpawner Instance { get; private set; }

    [Header("Setup")]
    public GameObject squarePrefab;    
    public Transform[] spawnSlots;     

    [Header("Scale In Slot")]
    public float slotScale = 0.45f; 

    private readonly List<int[,]> baseShapes = new List<int[,]>()
    {
        new int[,] { { 1 } },                                         // 0:  1x1
        new int[,] { { 1, 1 }, { 1, 1 } },                            // 1:  2x2
        new int[,] { { 1, 1 } },                                      // 2:  เส้นตรง 2 ช่อง
        new int[,] { { 1, 0 }, { 1, 1 } },                            // 3:  มุมฉากเล็ก 2x2
        new int[,] { { 1, 0 }, { 0, 1 } },                            // 4:  ทแยง 2 ช่อง
        new int[,] { { 1, 1, 1 } },                                   // 5:  เส้นตรง 3 ช่อง
        new int[,] { { 1, 0 }, { 1, 0 }, { 1, 1 } },                  // 6:  ตัว L สั้น (3x2)
        new int[,] { { 0, 1 }, { 0, 1 }, { 1, 1 } },                  // 7:  ตัว L สั้นกลับด้าน
        new int[,] { { 1, 1, 1 }, { 0, 1, 0 } },                      // 8:  ตัว T (3x2)
        new int[,] { { 1, 1, 0 }, { 0, 1, 1 } },                      // 9:  ตัว Z (3x2)
        new int[,] { { 0, 1, 1 }, { 1, 1, 0 } },                      // 10: ตัว S (Z กลับด้าน 3x2)
        new int[,] { { 1, 1, 1, 1 } },                                // 11: เส้นตรง 4 ช่อง
        new int[,] { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } },         // 12: 3x3
        new int[,] { { 1, 1, 1, 1, 1 } },                             // 13: เส้นตรง 5 ช่อง
        new int[,] { { 1, 0, 0 }, { 1, 0, 0 }, { 1, 1, 1 } },         // 14: ตัว L ใหญ่ (3x3)
        new int[,] { { 0, 0, 1 }, { 0, 0, 1 }, { 1, 1, 1 } },         // 15: ตัว L ใหญ่กลับด้าน
        new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },         // 16: ทแยง 3 ช่อง
        new int[,] { { 0, 1, 0 }, { 1, 1, 1 }, { 0, 1, 0 } },         // 17: ตัวบวก (+) 3x3
        new int[,] { { 1, 0, 1 }, { 1, 1, 1 } }                       // 18: ตัว C / U-Shape (3x2)
    };

    // ชิ้นเล็ก: 1x1, เส้น 2, มุมฉาก 2x2, ทแยง 2
    private readonly int[] smallPieces = new int[] { 0, 2, 3, 4 };

    // ชิ้นกลาง: 2x2, เส้น 3, L สั้น (ทั้ง 2 ข้าง), T, Z, S, เส้น 4, ตัว C
    private readonly int[] mediumPieces = new int[] { 1, 5, 6, 7, 8, 9, 10, 11, 18 };

    // ชิ้นใหญ่: 3x3, เส้น 5, L ใหญ่ (ทั้ง 2 ข้าง), ทแยง 3, ตัวบวก
    private readonly int[] giantPieces = new int[] { 12, 13, 14, 15, 16, 17 };  

    private List<DraggableShape> currentShapes = new List<DraggableShape>();

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        yield return null;
        SpawnNewSet();
    }

    public void SpawnNewSet()
    {
        currentShapes.Clear();

        List<int[,]> chosenPatterns = new List<int[,]>();
        HashSet<int> usedBaseIndices = new HashSet<int>(); // ป้องกันทรงหลักซ้ำกันใน 1 เซ็ต

        // สล็อต 1: สุ่มชิ้นกลาง (มีโอกาสได้ชิ้นใหญ่ 10%)
        int[] slot1Pool = (Random.value < 0.10f) ? giantPieces : mediumPieces;
        int[,] pat1 = PickShape(slot1Pool, false, usedBaseIndices, out int baseIdx1);
        if (pat1 == null) pat1 = PickShape(mediumPieces, false, usedBaseIndices, out baseIdx1);
        chosenPatterns.Add(pat1);
        usedBaseIndices.Add(baseIdx1);

        // สล็อต 2: สุ่มชิ้นกลางที่วางลงกระดานได้แน่นอน
        int[,] pat2 = PickShape(mediumPieces, true, usedBaseIndices, out int baseIdx2);
        if (pat2 == null) pat2 = PickShape(smallPieces, true, usedBaseIndices, out baseIdx2); // ถ้าวางไม่ได้ ให้สุ่มชิ้นเล็ก
        if (pat2 == null) pat2 = PickShape(smallPieces, true, null, out baseIdx2);
        if (pat2 == null) pat2 = baseShapes[0]; // fallback 1x1
        chosenPatterns.Add(pat2);
        usedBaseIndices.Add(baseIdx2);

        // สล็อต 3: สุ่มชิ้นเล็กที่วางลงกระดานได้แน่นอน
        int[,] pat3 = PickShape(smallPieces, true, usedBaseIndices, out int baseIdx3);
        if (pat3 == null) pat3 = PickShape(mediumPieces, true, usedBaseIndices, out baseIdx3); // ถ้าวางไม่ได้ ให้สุ่มชิ้นกลาง
        if (pat3 == null) pat3 = PickShape(smallPieces, true, null, out baseIdx3);
        if (pat3 == null) pat3 = baseShapes[0]; // fallback 1x1
        chosenPatterns.Add(pat3);
        usedBaseIndices.Add(baseIdx3);

        // สลับตำแหน่งการจัดวางสล็อต
        for (int i = 0; i < chosenPatterns.Count; i++)
        {
            int[,] temp = chosenPatterns[i];
            int randIndex = Random.Range(i, chosenPatterns.Count);
            chosenPatterns[i] = chosenPatterns[randIndex];
            chosenPatterns[randIndex] = temp;
        }

        // นำไปสร้างลงในสล็อต
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            CreateShape(chosenPatterns[i], spawnSlots[i]);
        }

        CheckAllAvailableShapes();
    }

    private int[,] PickShape(int[] pool, bool mustBePlaceable, HashSet<int> excludedIndices, out int chosenBaseIndex)
    {
        chosenBaseIndex = -1;
        List<int> validCandidates = new List<int>();

        foreach (int idx in pool)
        {
            if (excludedIndices != null && excludedIndices.Contains(idx))
            {
                continue;
            }
            validCandidates.Add(idx);
        }

        // Shuffle รายการก่อนสุ่ม
        for (int i = 0; i < validCandidates.Count; i++)
        {
            int temp = validCandidates[i];
            int randIndex = Random.Range(i, validCandidates.Count);
            validCandidates[i] = validCandidates[randIndex];
            validCandidates[randIndex] = temp;
        }

        foreach (int baseIdx in validCandidates)
        {
            // สุ่มหมุน (0, 90, 180, 270 องศา)
            int startRot = Random.Range(0, 4);
            for (int step = 0; step < 4; step++)
            {
                int rotCount = (startRot + step) % 4;
                int[,] rotated = GetRotatedPattern(baseShapes[baseIdx], rotCount);

                if (!mustBePlaceable || (GridManager.Instance != null && GridManager.Instance.HasAnyValidPlacement(rotated)))
                {
                    chosenBaseIndex = baseIdx;
                    return rotated;
                }
            }
        }

        return null;
    }

    private int[,] Rotate90(int[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        int[,] result = new int[cols, rows];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[c, rows - 1 - r] = matrix[r, c];
            }
        }
        return result;
    }

    private int[,] GetRotatedPattern(int[,] original, int rotationCount)
    {
        int[,] current = original;
        for (int i = 0; i < rotationCount; i++)
        {
            current = Rotate90(current);
        }
        return current;
    }

    void CreateShape(int[,] pattern, Transform parentSlot)
    {
        GameObject shapeRoot = new GameObject("DraggableShape", typeof(RectTransform));
        shapeRoot.transform.SetParent(parentSlot, false);

        DraggableShape draggable = shapeRoot.AddComponent<DraggableShape>();
        draggable.Pattern = pattern;
        draggable.smallScale = new Vector3(slotScale, slotScale, 1f);

        float squareSize = GridManager.Instance.ActualCellSize;

        int rows = pattern.GetLength(0);
        int cols = pattern.GetLength(1);

        float startX = -(cols - 1) * squareSize * 0.5f;
        float startY = (rows - 1) * squareSize * 0.5f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (pattern[r, c] == 1)
                {
                    GameObject square = Instantiate(squarePrefab, shapeRoot.transform);
                    RectTransform rt = square.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(squareSize, squareSize);
                    rt.anchoredPosition = new Vector2(startX + (c * squareSize), startY - (r * squareSize));

                    draggable.SquarePieces.Add(square.transform);
                    draggable.LocalOffsets.Add(new Vector2Int(r, c));
                }
            }
        }

        shapeRoot.transform.localScale = new Vector3(slotScale, slotScale, 1f);
        currentShapes.Add(draggable);
    }

    public void OnShapeUsed(DraggableShape usedShape)
    {
        if (currentShapes.Contains(usedShape))
        {
            currentShapes.Remove(usedShape);
        }

        if (currentShapes.Count == 0)
        {
            SpawnNewSet();
        }
        else
        {
            StartCoroutine(CheckShapesNextFrame());
        }
    }

    private IEnumerator CheckShapesNextFrame()
    {
        yield return new WaitForEndOfFrame();
        CheckAllAvailableShapes();
    }

    public void CheckAllAvailableShapes()
    {
        currentShapes.RemoveAll(s => s == null);

        if (currentShapes.Count == 0) return;

        int validShapeCount = 0;

        foreach (var shape in currentShapes)
        {
            if (shape == null) continue;

            bool canBePlaced = GridManager.Instance.HasAnyValidPlacement(shape.Pattern);
            shape.SetGreyOut(!canBePlaced); // ถ้านำไปวางไม่ได้ ให้เปลี่ยนเป็นสีเทา

            if (canBePlaced)
            {
                validShapeCount++;
            }
        }

        if (validShapeCount == 0)
        {
            StartCoroutine(WaitAndTriggerGameOver());
        }
    }

    private IEnumerator WaitAndTriggerGameOver()
    {
        yield return new WaitForSeconds(0.35f);

        int recheckCount = 0;
        foreach (var shape in currentShapes)
        {
            if (shape != null && GridManager.Instance.HasAnyValidPlacement(shape.Pattern))
            {
                recheckCount++;
                shape.SetGreyOut(false);
            }
        }

        if (recheckCount == 0)
        {
            GridManager.Instance.TriggerGameOver();
        }
    }
}