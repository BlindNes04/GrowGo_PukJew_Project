using UnityEngine;
using UnityEngine.EventSystems;

public class MagnifyingGlassDrag : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Lens Area")]
    public RectTransform lensArea; 

    private RectTransform handleRectTransform; 
    private Canvas canvas;
    
    private Vector2 handleStartPosition;
    private Vector2 lensStartPosition;

    void Awake()
    {
        handleRectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        handleStartPosition = handleRectTransform.anchoredPosition;
        if (lensArea != null)
        {
            lensStartPosition = lensArea.anchoredPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;

        Vector2 deltaMove = eventData.delta / canvas.scaleFactor;

        handleRectTransform.anchoredPosition += deltaMove;

        if (lensArea != null)
        {
            lensArea.anchoredPosition += deltaMove;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        handleRectTransform.anchoredPosition = handleStartPosition;
        if (lensArea != null)
        {
            lensArea.anchoredPosition = lensStartPosition;
        }
    }
}