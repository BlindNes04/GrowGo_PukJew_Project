using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MagnifyingGlassDrag : MonoBehaviour,
    IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Targets & References")]
    public RectTransform lensArea;
    public RectTransform searchFrame;
    public float detectRadius = 70f;
    public float holdTimeToLock = 0.25f;

    [Header("Lens Boundary")]
    [Range(0f, 0.10f)]
    public float allowedLensOutsideFraction = 0.08f;

    [Header("Found Animation")]
    public float zoomDuration = 0.15f;
    public float zoomScale = 1.3f;
    public float rewardDelay = 0.4f;

    [Header("UI Panels")]
    public GameObject searchComponent;
    public GameObject rewardComponent;

    private RectTransform handleRectTransform;
    private RectTransform dragParentRect;
    private Canvas canvas;
    private Camera uiCamera;

    private Vector2 handleStartAnchoredPosition;
    private Vector3 handleStartScale;
    private Vector2 pointerOffsetLocal;

    private bool isFound;
    private bool isHoveringItem;
    private float currentHoldTimer;
    private Coroutine rewardCoroutine;
    private RectTransform temporarilyMovedItem;
    private Vector3 itemOriginalWorldPosition;
    private Vector2 itemOriginalAnchoredPosition;

    private void Awake()
    {
        handleRectTransform = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();

        if (handleRectTransform != null && handleRectTransform.parent != null)
            dragParentRect = handleRectTransform.parent as RectTransform;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        if (handleRectTransform != null)
        {
            handleStartAnchoredPosition = handleRectTransform.anchoredPosition;
            handleStartScale = handleRectTransform.localScale;
        }

    }

    private void OnEnable()
    {
        ResetMagnifyingGlass();
    }

    private void Update()
    {
        if (isFound)
            return;

        if (!isHoveringItem)
        {
            currentHoldTimer = 0f;
            return;
        }

        currentHoldTimer += Time.deltaTime;
        if (currentHoldTimer < holdTimeToLock)
            return;

        ItemSpawner spawner = ItemSpawner.Instance;
        if (spawner == null || spawner.activeSpawnPointRect == null ||
            spawner.currentActiveItem == null)
        {
            isHoveringItem = false;
            currentHoldTimer = 0f;
            return;
        }

        isFound = true;
        isHoveringItem = false;

        if (rewardCoroutine != null)
            StopCoroutine(rewardCoroutine);

        rewardCoroutine = StartCoroutine(
            TriggerRewardSequence(spawner.activeSpawnPointRect, spawner.currentActiveItem));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isFound || dragParentRect == null)
            return;

        // เก็บตำแหน่งที่ผู้เล่นกดจับแว่นขยายไว้ 
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParentRect, eventData.position, uiCamera, out Vector2 pointerLocal))
        {
            pointerOffsetLocal = (Vector2)handleRectTransform.localPosition - pointerLocal;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isFound || dragParentRect == null || handleRectTransform == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParentRect, eventData.position, uiCamera, out Vector2 pointerLocal))
        {
            handleRectTransform.localPosition = pointerLocal + pointerOffsetLocal;
            KeepLensWithinSmallAllowance();
        }

        CheckIfHovering();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isFound)
            return;

        isHoveringItem = false;
        currentHoldTimer = 0f;

        if (handleRectTransform != null)
            handleRectTransform.anchoredPosition = handleStartAnchoredPosition;
    }

    private void KeepLensWithinSmallAllowance()
    {
        if (searchFrame == null || handleRectTransform == null)
            return;

        RectTransform lens = lensArea != null ? lensArea : handleRectTransform;
        Rect frame = searchFrame.rect;
        Vector3[] corners = new Vector3[4];
        lens.GetWorldCorners(corners);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Vector3 corner in corners)
        {
            Vector3 local = searchFrame.InverseTransformPoint(corner);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
        }

        float allowanceX = (maxX - minX) * Mathf.Clamp(allowedLensOutsideFraction, 0f, 0.10f);
        float allowanceY = (maxY - minY) * Mathf.Clamp(allowedLensOutsideFraction, 0f, 0.10f);
        float allowedMinX = frame.xMin - allowanceX;
        float allowedMaxX = frame.xMax + allowanceX;
        float allowedMinY = frame.yMin - allowanceY;
        float allowedMaxY = frame.yMax + allowanceY;

        float dx = 0f;
        float dy = 0f;
        if (minX < allowedMinX) dx = allowedMinX - minX;
        if (maxX + dx > allowedMaxX) dx += allowedMaxX - (maxX + dx);
        if (minY < allowedMinY) dy = allowedMinY - minY;
        if (maxY + dy > allowedMaxY) dy += allowedMaxY - (maxY + dy);

        if (Mathf.Abs(dx) > 0.001f || Mathf.Abs(dy) > 0.001f)
        {
            Vector3 worldDelta = searchFrame.TransformPoint(new Vector3(dx, dy, 0f))
                               - searchFrame.TransformPoint(Vector3.zero);
            handleRectTransform.position += worldDelta;
        }
    }

    // ขยับรูปไอเทมจากจุดเกิดให้มาอยู่ตรงกลางเลนส์
    private void CenterItemImageInLens(RectTransform itemRect)
    {
        if (itemRect == null || lensArea == null)
            return;

        Vector3[] corners = new Vector3[4];
        lensArea.GetWorldCorners(corners);
        Vector3 lensCenter = (corners[0] + corners[2]) * 0.5f;
        Vector3 itemCenter = itemRect.TransformPoint(itemRect.rect.center);
        itemRect.position += lensCenter - itemCenter;
    }

    private void RestoreItemImagePosition()
    {
        if (temporarilyMovedItem == null)
            return;

        if (temporarilyMovedItem.parent != null)
            temporarilyMovedItem.anchoredPosition = itemOriginalAnchoredPosition;
        else
            temporarilyMovedItem.position = itemOriginalWorldPosition;

        temporarilyMovedItem = null;
    }

    private void CheckIfHovering()
    {
        ItemSpawner spawner = ItemSpawner.Instance;
        if (isFound || spawner == null)
        {
            isHoveringItem = false;
            return;
        }

        RectTransform target = spawner.activeSpawnPointRect;
        RectTransform lens = lensArea != null ? lensArea : handleRectTransform;

        if (target == null || lens == null || !target.gameObject.activeInHierarchy)
        {
            isHoveringItem = false;
            return;
        }

        Vector2 lensScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, lens.position);
        Vector2 itemScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);

        isHoveringItem = Vector2.Distance(lensScreen, itemScreen) <= detectRadius;
    }

    private IEnumerator TriggerRewardSequence(RectTransform targetItemRect, ItemData foundItem)
    {
        if (AudioManager.Instance != null && AudioManager.Instance.itemFoundSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.itemFoundSFX);

        if (targetItemRect != null)
        {
            temporarilyMovedItem = targetItemRect;
            itemOriginalWorldPosition = targetItemRect.position;
            itemOriginalAnchoredPosition = targetItemRect.anchoredPosition;
        }

        Vector3 startScale = handleRectTransform.localScale;
        Vector3 endScale = handleStartScale * zoomScale;
        float elapsed = 0f;

        // ตรวจและปรับตำแหน่งระหว่างการซูม เพื่อไม่ให้เลนส์หลุดออกนอกกรอบ
        KeepLensWithinSmallAllowance();
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = zoomDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / zoomDuration);
            handleRectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            KeepLensWithinSmallAllowance();
            CenterItemImageInLens(targetItemRect);
            yield return null;
        }

        handleRectTransform.localScale = endScale;
        KeepLensWithinSmallAllowance();
        CenterItemImageInLens(targetItemRect);

        yield return new WaitForSeconds(rewardDelay);
        RestoreItemImagePosition();

        if (searchComponent != null)
            searchComponent.SetActive(false);

        if (rewardComponent == null)
        {
            rewardCoroutine = null;
            yield break;
        }

        RewardController controller = rewardComponent.GetComponent<RewardController>();
        if (controller == null)
            controller = rewardComponent.GetComponentInChildren<RewardController>(true);

        if (controller == null || foundItem == null)
        {
            rewardCoroutine = null;
            yield break;
        }

        rewardComponent.SetActive(true);
        controller.OpenRewardWindow(foundItem);
        rewardCoroutine = null;
    }

    public void ResetMagnifyingGlass()
    {
        isFound = false;
        isHoveringItem = false;
        currentHoldTimer = 0f;

        if (rewardCoroutine != null)
        {
            StopCoroutine(rewardCoroutine);
            rewardCoroutine = null;
        }

        RestoreItemImagePosition();

        if (handleRectTransform != null)
        {
            handleRectTransform.anchoredPosition = handleStartAnchoredPosition;
            handleRectTransform.localScale = handleStartScale;
        }
    }
}