using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public class SettingPopupController : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDuration = 0.2f;

    [Header("ลบบัญชี")]
    public GameObject confirmDeletePopup;          // ป๊อปอัปถามยืนยัน (ไม่ใส่ก็ได้ จะลบทันทีเมื่อกดปุ่ม)
    public string sceneAfterDelete = "MainMenu";   // ลบเสร็จแล้วกลับไปฉากนี้

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

    // ==================== ลบบัญชี ====================
    // ปุ่ม "ลบบัญชี" ในหน้าตั้งค่า -> เปิดป๊อปอัปยืนยันก่อน (กันกดพลาด)
    public void OnDeleteAccountButton()
    {
        if (confirmDeletePopup != null) confirmDeletePopup.SetActive(true);
        else ConfirmDeleteAccount();
    }

    // ปุ่ม "ยกเลิก" ในป๊อปอัปยืนยัน
    public void CancelDeleteAccount()
    {
        if (confirmDeletePopup != null) confirmDeletePopup.SetActive(false);
    }

    // ปุ่ม "ยืนยันลบ" -> ลบข้อมูลใน Supabase ทุกตาราง แล้วกลับไปหน้าเริ่มเกม
    public void ConfirmDeleteAccount()
    {
        // ใช้ coroutine ของ SupabaseManager เพราะอยู่ข้ามฉาก (ป๊อปอัปนี้อาจถูกปิดระหว่างรอ)
        SupabaseManager.Instance.StartCoroutine(SupabaseManager.Instance.DeleteAccount(
            onSuccess: () => SceneManager.LoadScene(sceneAfterDelete),
            onError: (err) => Debug.LogError($"[Setting] 🔴 ลบบัญชีไม่สำเร็จ: {err}")
        ));
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