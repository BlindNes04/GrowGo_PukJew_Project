using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SeedManager : MonoBehaviour
{
    // =========================================================
    // Canvas Reference
    // =========================================================
    [Header("Root Canvas")]
    [SerializeField] private GameObject seedCanvas;

    // =========================================================
    // UI References
    // =========================================================
    [Header("Page 1 (Bag)")]
    [SerializeField] private GameObject bagPage;
    [SerializeField] private RectTransform bagTransform;
    [SerializeField] private Button tapToContinueButton;

    [Header("Page 2 (Seed)")]
    [SerializeField] private GameObject seedPage;
    [SerializeField] private RectTransform sunburstTransform;
    [SerializeField] private RectTransform seedTomatoTransform;
    [SerializeField] private CanvasGroup contentCanvasGroup; 

    [Header("Sunburst & Shake")]
    [SerializeField] private float rotateSpeed = 20f;       
    [SerializeField] private float shakeAngle = 5f;         
    [SerializeField] private float shakeSpeed = 20f;        

    [Header("Tomato Pop Settings")]
    [SerializeField] private float popDuration = 1f;      
    [SerializeField] private float popOvershoot = 1.2f;    

    private Coroutine shakeCoroutine;
    private bool isTransitioning = false;

    private void Update()
    {
        // หมุนแสงเรื่อยๆ เมื่อเปิดหน้า SeedPage
        if (seedPage != null && seedPage.activeInHierarchy && sunburstTransform != null)
        {
            sunburstTransform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    // =========================================================
    // Start Page 
    // =========================================================
    public void OpenSeedReward()
    {
        if (seedCanvas != null) seedCanvas.SetActive(true);

        isTransitioning = false;
        bagPage.SetActive(true);
        seedPage.SetActive(false);

        // เริ่มเขย่าถุงวนลูปต่อเนื่อง
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeBagRoutine());

        if (tapToContinueButton != null)
        {
            tapToContinueButton.onClick.RemoveAllListeners();
            tapToContinueButton.onClick.AddListener(OnTapContinue);
        }
    }

    // Shake Animation (Bag)
   private IEnumerator ShakeBagRoutine()

    {
        if (bagTransform == null) yield break;
        Quaternion initialRotation = bagTransform.localRotation;
        while (true)
        {
            float timer = 0f;
            while (timer < 1.2f)
            {
                timer += Time.deltaTime;

                float zAngle = Mathf.Sin(timer * shakeSpeed) * shakeAngle;

                bagTransform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, zAngle);

                yield return null;

            }
            bagTransform.localRotation = initialRotation;
            yield return new WaitForSeconds(0.8f);
        }
    }

    // =========================================================
    // UI Transition
    // =========================================================
    public void OnTapContinue()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToSeedPage());
    }

    private IEnumerator TransitionToSeedPage()
    {
        isTransitioning = true;

        // ซ่อนหน้าถุง เปิดหน้ามะเขือเทศ
        bagPage.SetActive(false);
        seedPage.SetActive(true);

        seedTomatoTransform.localScale = Vector3.zero;
        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.alpha = 0f;
        }

        // Pop Animation (Tomato)
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);

            float c1 = popOvershoot;
            float c3 = c1 + 1f;
            float currentScale = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            seedTomatoTransform.localScale = Vector3.one * currentScale;
            yield return null;
        }

        seedTomatoTransform.localScale = Vector3.one;

        // =========================================================
        // Fade-in Sequence (Texts & Buttons)
        // =========================================================
        if (contentCanvasGroup != null)
        {
            float fadeDuration = 0.4f;
            elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                contentCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            contentCanvasGroup.alpha = 1f;
        }
    }
}