using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MagnifyingGlassDrag : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Targets & References")]
    public RectTransform lensArea;         
    public float detectRadius = 70f;       
    public float holdTimeToLock = 0.25f;   

    [Header("UI Panels")]
    public GameObject searchComponent;
    public GameObject rewardComponent;

    private RectTransform handleRectTransform;
    private Canvas canvas;
    private Vector2 handleStartPosition;
    private Vector3 handleStartScale;
    private bool isFound = false;

    private float currentHoldTimer = 0f;
    private bool isHoveringItem = false;

    void Awake()
    {
        handleRectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        if (handleRectTransform != null)
        {
            handleStartScale = handleRectTransform.localScale;
            handleStartPosition = handleRectTransform.anchoredPosition;
        }
    }

    void OnEnable()
    {
        ResetMagnifyingGlass();
    }

    void Update()
    {
        if (isFound) return;

        if (isHoveringItem)
        {
            currentHoldTimer += Time.deltaTime;

            if (currentHoldTimer >= holdTimeToLock)
            {
                isFound = true;
                isHoveringItem = false;

                if (ItemSpawner.Instance != null && ItemSpawner.Instance.activeSpawnPointRect != null)
                {
                    StartCoroutine(TriggerRewardSequence(ItemSpawner.Instance.activeSpawnPointRect));
                }
            }
        }
        else
        {
            currentHoldTimer = 0f;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isFound) return;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isFound || canvas == null) return;

        Vector2 deltaMove = eventData.delta / canvas.scaleFactor;
        handleRectTransform.anchoredPosition += deltaMove;

        CheckIfHovering();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isFound) return;

        isHoveringItem = false;
        currentHoldTimer = 0f;
        handleRectTransform.anchoredPosition = handleStartPosition;
    }

    private void CheckIfHovering()
    {
        if (isFound || ItemSpawner.Instance == null) return;

        RectTransform targetItemRect = ItemSpawner.Instance.activeSpawnPointRect;
        if (targetItemRect == null || !targetItemRect.gameObject.activeInHierarchy)
        {
            isHoveringItem = false;
            return;
        }

        RectTransform checkRect = (lensArea != null) ? lensArea : handleRectTransform;

        Vector2 lensWorldPos = checkRect.position;
        Vector2 itemWorldPos = targetItemRect.position;

        float distance = Vector2.Distance(lensWorldPos, itemWorldPos);
        isHoveringItem = (distance <= detectRadius);
    }

    private IEnumerator TriggerRewardSequence(RectTransform targetItemRect)
    {

        if (canvas != null && targetItemRect != null)
        {
            RectTransform canvasRect = canvas.transform as RectTransform;

            Vector2 itemLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(null, targetItemRect.position),
                null,
                out itemLocalPos
            );

            if (lensArea != null)
            {
                Vector2 lensLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    RectTransformUtility.WorldToScreenPoint(null, lensArea.position),
                    null,
                    out lensLocalPos
                );

                Vector2 offset = handleRectTransform.anchoredPosition - lensLocalPos;
                handleRectTransform.anchoredPosition = itemLocalPos + offset;
            }
            else
            {
                handleRectTransform.anchoredPosition = itemLocalPos;
            }
        }

        if (AudioManager.Instance != null && AudioManager.Instance.itemFoundSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.itemFoundSFX);
        }

        // ZOOM
        float elapsed = 0f;
        float animDuration = 0.15f;
        Vector3 targetScale = handleStartScale * 1.3f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            handleRectTransform.localScale = Vector3.Lerp(handleStartScale, targetScale, elapsed / animDuration);
            yield return null;
        }

        yield return new WaitForSeconds(0.4f);

        if (searchComponent != null) searchComponent.SetActive(false);
        if (rewardComponent != null) rewardComponent.SetActive(true);
    }

    public void ResetMagnifyingGlass()
    {
        isFound = false;
        isHoveringItem = false;
        currentHoldTimer = 0f;

        if (handleRectTransform != null)
        {
            handleRectTransform.anchoredPosition = handleStartPosition;
            handleRectTransform.localScale = handleStartScale;
        }
    }
}