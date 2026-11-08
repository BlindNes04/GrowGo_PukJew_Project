using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SeedManager : MonoBehaviour
{
    [Header("Root Canvas")]
    [SerializeField] private GameObject seedCanvas;

    [Header("Page 1 (Bag)")]
    [SerializeField] private GameObject bagPage;
    [SerializeField] private RectTransform bagTransform;
    [SerializeField] private Button tapToContinueButton;

    [Header("Page 2 (Seed)")]
    [SerializeField] private GameObject seedPage;
    [SerializeField] private RectTransform sunburstTransform;
    [SerializeField] private RectTransform seedTomatoTransform;
    [SerializeField] private CanvasGroup contentCanvasGroup;

    [Header("Finish Button (กดยืนยันแล้วไป MainForest)")]
    [SerializeField] private Button finishButton;
    [SerializeField] private string targetSceneName = "MainForest";

    [Header("Sunburst & Shake")]
    [SerializeField] private float rotateSpeed = 20f;
    [SerializeField] private float shakeAngle = 5f;
    [SerializeField] private float shakeSpeed = 20f;

    [Header("Tomato Pop Settings")]
    [SerializeField] private float popDuration = 1f;
    [SerializeField] private float popOvershoot = 1.2f;

    private Coroutine shakeCoroutine;
    private bool isTransitioning = false;

    private void Awake()
    {
        if (finishButton != null)
        {
            finishButton.onClick.RemoveAllListeners();
            finishButton.onClick.AddListener(OnFinishAndGoToForest);
        }
    }

    private void Update()
    {
        if (seedPage != null && seedPage.activeInHierarchy && sunburstTransform != null)
        {
            sunburstTransform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    public void OpenSeedReward()
    {
        if (seedCanvas != null) seedCanvas.SetActive(true);

        isTransitioning = false;
        bagPage.SetActive(true);
        seedPage.SetActive(false);

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeBagRoutine());

        if (tapToContinueButton != null)
        {
            tapToContinueButton.onClick.RemoveAllListeners();
            tapToContinueButton.onClick.AddListener(OnTapContinue);
        }
    }

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

    public void OnTapContinue()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToSeedPage());
    }

    private IEnumerator TransitionToSeedPage()
    {
        isTransitioning = true;

        bagPage.SetActive(false);
        seedPage.SetActive(true);

        // มอบ ITEM_SEED_TOMATO x1 เข้า DB
        GrantSeedRewardToDB();

        seedTomatoTransform.localScale = Vector3.zero;
        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.alpha = 0f;
        }

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

    // บันทึกเมล็ดมะเขือเทศลง inventory ใน Supabase
    private void GrantSeedRewardToDB()
    {
        if (SupabaseManager.Instance != null)
        {
            StartCoroutine(SupabaseManager.Instance.AddItemToInventory("ITEM_SEED_TOMATO", 1,
                onSuccess: () =>
                {
                    Debug.Log("[SeedManager] 🟢 บันทึก ITEM_SEED_TOMATO x1 ลง DB สำเร็จ");
                },
                onError: (err) =>
                {
                    Debug.LogError($"[SeedManager] 🔴 บันทึกเมล็ดลง DB ล้มเหลว: {err}");
                }
            ));
        }
    }

    // พอกดปุ่มเสร็จสิ้นในหน้า Seed ให้ไป MainForest
    public void OnFinishAndGoToForest()
    {
        Debug.Log("[SeedManager] 🟢 ยืนยันรับเมล็ดพืช -> กำลังไปหน้า MainForest...");
        SceneManager.LoadScene(targetSceneName);
    }
}