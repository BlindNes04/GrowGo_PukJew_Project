using System.Collections.Generic;
using UnityEngine;

public class BlockSpawner : MonoBehaviour
{
    [Header("Setup")]
    public GameObject squarePrefab;    
    public Transform[] spawnSlots;     
    public float squareSize = 40f;     

    private readonly List<int[,]> shapePatterns = new List<int[,]>()
    {
        // 1x1
        new int[,] {
            { 1 }
        },
        // 2x2
        new int[,] {
            { 1, 1 },
            { 1, 1 }
        },
        // แนวนอนยาว 3 ช่อง
        new int[,] {
            { 1, 1, 1 }
        },
        // แนวตั้งยาว 3 ช่อง
        new int[,] {
            { 1 },
            { 1 },
            { 1 }
        },
        // ตัว L
        new int[,] {
            { 1, 0 },
            { 1, 0 },
            { 1, 1 }
        },
        // ตัว T
        new int[,] {
            { 1, 1, 1 },
            { 0, 1, 0 }
        }
    };

    void Start()
    {
        SpawnNewSet();
    }

    public void SpawnNewSet()
    {
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            int randomIndex = Random.Range(0, shapePatterns.Count);
            CreateShape(shapePatterns[randomIndex], spawnSlots[i]);
        }
    }

    void CreateShape(int[,] pattern, Transform parentSlot)
    {
        GameObject shapeRoot = new GameObject("DraggableShape", typeof(RectTransform));
        shapeRoot.transform.SetParent(parentSlot, false);

        shapeRoot.AddComponent<DraggableShape>();

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
                }
            }
        }
    }
}