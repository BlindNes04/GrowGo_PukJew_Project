using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntroManager : MonoBehaviour
{
    [Header("All Canvases")]
    [SerializeField] private GameObject introCanvas;
    [SerializeField] private GameObject quizCanvas;
    [SerializeField] private GameObject buddyCanvas;
    [SerializeField] private GameObject seedCanvas;

    [Header("Quiz Manager Reference")]
    [SerializeField] private QuizManager quizManager;

    [Header("Elements to Fade In")]
    [SerializeField] private Image logoImage;
    [SerializeField] private TMP_Text welcomeText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button startButton;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float delayBetween = 0.25f;

    private Image startButtonImage;
    private TMP_Text startButtonText;

    private void Awake()
    {
        if (introCanvas != null) introCanvas.SetActive(true);
        if (quizCanvas != null) quizCanvas.SetActive(false);
        if (buddyCanvas != null) buddyCanvas.SetActive(false);
        if (seedCanvas != null) seedCanvas.SetActive(false);

        if (startButton != null)
        {
            startButtonImage = startButton.GetComponent<Image>();
            startButtonText = startButton.GetComponentInChildren<TMP_Text>();
        }

        // ซ่อน Elements เพื่อเตรียม Fade In
        SetAlpha(logoImage, 0f);
        SetAlpha(welcomeText, 0f);
        SetAlpha(dialogueText, 0f);
        SetButtonAlpha(0f);

        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartQuizClicked);
        }
    }

    private void Start()
    {
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        yield return StartCoroutine(FadeGraphic(logoImage));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(welcomeText));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(dialogueText));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeButtonRoutine());

        if (startButton != null)
        {
            startButton.interactable = true;
        }
    }

    private IEnumerator FadeGraphic(Graphic graphic)
    {
        if (graphic == null) yield break;

        float timer = 0f;
        Color c = graphic.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            graphic.color = c;
            yield return null;
        }

        c.a = 1f;
        graphic.color = c;
    }

    private IEnumerator FadeButtonRoutine()
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            SetButtonAlpha(currentAlpha);
            yield return null;
        }

        SetButtonAlpha(1f);
    }

    private void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    private void SetButtonAlpha(float alpha)
    {
        SetAlpha(startButtonImage, alpha);
        SetAlpha(startButtonText, alpha);
    }

    // =========================================================
    // Switch to QuizCanvas
    // =========================================================
    public void OnStartQuizClicked()
    {
        if (introCanvas != null) introCanvas.SetActive(false);
        if (quizCanvas != null) quizCanvas.SetActive(true);

        if (quizManager != null)
        {
            quizManager.gameObject.SetActive(true);
            quizManager.StartQuiz(); 
        }
    }
}