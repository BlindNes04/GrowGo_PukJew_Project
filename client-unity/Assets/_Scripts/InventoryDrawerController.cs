using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class InventoryDrawerController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Close Button")]
    [SerializeField] private Button closeButton;

    private CanvasGroup canvasGroup;
    private Coroutine activeFadeRoutine;
    private bool isOpen = false;

    private void Awake()
    {
        SetupCanvasGroup();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseDrawer);
        }
    }

    private void SetupCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    public void ToggleDrawer()
    {
        if (isOpen)
        {
            CloseDrawer();
        }
        else
        {
            OpenDrawer();
        }
    }

    public void OpenDrawer()
    {
        // ปลุก GameObject ให้ตื่นก่อนเป็นอันดับแรก (ป้องกัน Coroutine พัง 100%)
        gameObject.SetActive(true);
        SetupCanvasGroup();

        isOpen = true;
        StartFade(1f, true);
    }

    public void CloseDrawer()
    {
        isOpen = false;
        StartFade(0f, false);
    }

    private void StartFade(float targetAlpha, bool interactable)
    {
        SetupCanvasGroup();

        // ป้องกัน Error กรณีสั่งปิดตอนที่ตัวมันปิดไปแล้ว
        if (!gameObject.activeInHierarchy) return;

        if (activeFadeRoutine != null) StopCoroutine(activeFadeRoutine);
        activeFadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, interactable));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool interactable)
    {
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // ถ้าจางหายจนจบ ให้ปิดตัวเองไปเลย
        if (!interactable)
        {
            gameObject.SetActive(false);
        }
    }
}