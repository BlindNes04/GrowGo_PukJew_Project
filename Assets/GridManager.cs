using UnityEngine;

public class GridManager : MonoBehaviour
{
    private const int ROWS = 8;
    private const int COLS = 8;

    [Header("UI References")]
    public GameObject cellPrefab; 
    public Transform boardGrid;  

    void Start()
    {
        CreateBoard();
    }

    void CreateBoard()
    {
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                GameObject cell = Instantiate(cellPrefab, boardGrid);
                cell.name = $"Cell_{r}_{c}";
            }
        }
    }
}