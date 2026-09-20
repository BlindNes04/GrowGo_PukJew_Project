using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UniversalPopup : MonoBehaviour
{
    [Header("Fade Animation")]
    [Tooltip("เปิด-ปิดการใช้แอนิเมชัน Fade (ถ้าติ๊กออกจะเป็นการเปิดปิดทันที)")]
    public bool useFade = true;
    public float fadeDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        InitCanvasGroup();
    }

    private void InitCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // ฟังก์ชันสำหรับ "ปุ่มเปิด"
    public void Open()
    {
        InitCanvasGroup();
        gameObject.SetActive(true);

        if (useFade)
        {
            canvasGroup.alpha = 0f;
            StartFade(1f, true);
        }
        else
        {
            SetInteractable(true);
            canvasGroup.alpha = 1f;
        }
    }

    // ฟังก์ชันสำหรับ "ปุ่มปิด"
    public void Close()
    {
        InitCanvasGroup();

        if (!gameObject.activeInHierarchy) return;

        if (useFade)
        {
            StartFade(0f, false);
        }
        else
        {
            SetInteractable(false);
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }

    // ฟังก์ชันสำหรับปุ่มแบบสลับสถานะ (กดครั้งแรกเปิด กดซ้ำปิด)
    public void Toggle()
    {
        if (gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.05f)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void StartFade(float targetAlpha, bool isOpening)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, isOpening));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        SetInteractable(isOpening);

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (!isOpening)
        {
            gameObject.SetActive(false);
        }
    }

    private void SetInteractable(bool state)
    {
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }
}