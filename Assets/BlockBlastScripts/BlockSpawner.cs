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

    private readonly List<int[,]> shapePatterns = new List<int[,]>()
    {
        new int[,] { { 1 } },
        new int[,] { { 1, 1 }, { 1, 1 } },
        new int[,] { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } },
        new int[,] { { 1, 1 } },
        new int[,] { { 1 }, { 1 } },
        new int[,] { { 1, 1, 1 } },
        new int[,] { { 1 }, { 1 } },
        new int[,] { { 1, 1, 1, 1 } },
        new int[,] { { 1 }, { 1 }, { 1 }, { 1 } },
        new int[,] { { 1, 1, 1, 1, 1 } },
        new int[,] { { 1 }, { 1 }, { 1 }, { 1 }, { 1 } },
        new int[,] { { 1, 0 }, { 1, 1 } },
        new int[,] { { 0, 1 }, { 1, 1 } },
        new int[,] { { 1, 1 }, { 1, 0 } },
        new int[,] { { 1, 1 }, { 0, 1 } },
        new int[,] { { 1, 0, 0 }, { 1, 0, 0 }, { 1, 1, 1 } },
        new int[,] { { 0, 0, 1 }, { 0, 0, 1 }, { 1, 1, 1 } },
        new int[,] { { 1, 1, 1 }, { 1, 0, 0 }, { 1, 0, 0 } },
        new int[,] { { 1, 1, 1 }, { 0, 0, 1 }, { 0, 0, 1 } },
        new int[,] { { 1, 0 }, { 1, 0 }, { 1, 1 } },
        new int[,] { { 0, 1 }, { 0, 1 }, { 1, 1 } },
        new int[,] { { 1, 1, 1 }, { 1, 0, 0 } },
        new int[,] { { 1, 1, 1 }, { 0, 0, 1 } },
        new int[,] { { 1, 1, 1 }, { 0, 1, 0 } },
        new int[,] { { 0, 1, 0 }, { 1, 1, 1 } },
        new int[,] { { 1, 0 }, { 1, 1 }, { 1, 0 } },
        new int[,] { { 0, 1 }, { 1, 1 }, { 0, 1 } },
        new int[,] { { 1, 1, 0 }, { 0, 1, 1 } },
        new int[,] { { 0, 1, 1 }, { 1, 1, 0 } },
        new int[,] { { 0, 1 }, { 1, 1 }, { 1, 0 } },
        new int[,] { { 1, 0 }, { 1, 1 }, { 0, 1 } }
    };

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

        List<int> availableIndices = new List<int>();
        for (int i = 0; i < shapePatterns.Count; i++)
        {
            availableIndices.Add(i);
        }

        for (int i = 0; i < spawnSlots.Length; i++)
        {
            int randPos = Random.Range(0, availableIndices.Count);
            int patternIndex = availableIndices[randPos];
            availableIndices.RemoveAt(randPos);

            CreateShape(shapePatterns[patternIndex], spawnSlots[i]);
        }

        CheckAllAvailableShapes();
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
            shape.SetGreyOut(!canBePlaced);

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