using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DraggableShape : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private Vector3 startPosition;

    [HideInInspector]
    public Vector3 smallScale = new Vector3(0.45f, 0.45f, 1f);

    public int[,] Pattern { get; set; }

    public List<Transform> SquarePieces { get; set; } = new List<Transform>();
    public List<Vector2Int> LocalOffsets { get; set; } = new List<Vector2Int>();

    public bool IsInteractable { get; private set; } = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetGreyOut(bool isGrey)
    {
        IsInteractable = !isGrey;

        foreach (Transform piece in SquarePieces)
        {
            Image img = piece.GetComponent<Image>();
            if (img != null)
            {
                img.color = isGrey ? new Color(0.35f, 0.35f, 0.35f, 0.8f) : Color.white;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsInteractable) return; // ถ้าล็อคอยู่ ห้ามลาก

        originalParent = transform.parent;
        startPosition = rectTransform.position;

        transform.SetParent(canvas.transform, true);
        transform.localScale = Vector3.one;

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsInteractable) return;

        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;

        if (GridManager.Instance.TryFindSnapPosition(SquarePieces, LocalOffsets, out int row, out int col))
        {
            GridManager.Instance.ShowPreview(row, col, Pattern);
        }
        else
        {
            GridManager.Instance.ClearPreview();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsInteractable) return;

        canvasGroup.blocksRaycasts = true;

        bool placed = false;
        if (GridManager.Instance.TryFindSnapPosition(SquarePieces, LocalOffsets, out int row, out int col))
        {
            placed = GridManager.Instance.PlaceShape(row, col, Pattern);
        }

        if (placed)
        {
            if (BlockSpawner.Instance != null)
            {
                BlockSpawner.Instance.OnShapeUsed(this);
            }

            Destroy(gameObject);
        }
        else
        {
            GridManager.Instance.ClearPreview();
            transform.SetParent(originalParent, true);
            transform.localScale = smallScale;
            rectTransform.position = startPosition;
        }
    }
}