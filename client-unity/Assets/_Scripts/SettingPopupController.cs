using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class SettingPopupController : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    // ฟังก์ชันสำหรับ "ปุ่มเปิด"
    public void OpenPopup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // เปิดวัตถุขึ้นมาก่อน แล้วเซ็ตความโปร่งใสเป็น 0 ทันทีเพื่อเตรียม Fade In
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;

        FadeTo(1f, true);
    }

    // ฟังก์ชันสำหรับ "ปุ่มปิด"
    public void ClosePopup()
    {
        FadeTo(0f, false);
    }

    private void FadeTo(float targetAlpha, bool isOpening)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, isOpening));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        if (isOpening)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

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
}