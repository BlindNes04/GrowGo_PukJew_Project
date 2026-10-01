using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class QuestionData
{
    public string id;
    public string section;
    public string question;

    public string choiceA;
    public string choiceB;

    public string choiceATarget;
    public string choiceBTarget;
}

public class QuizManager : MonoBehaviour
{
    [Header("CSV File")]
    [SerializeField] private TextAsset csvFile;

    [Header("Question UI")]
    [SerializeField] private TMP_Text sectionText;
    [SerializeField] private TMP_Text questionNumberText;
    [SerializeField] private TMP_Text questionText;

    [Header("Choice UI")]
    [SerializeField] private Button buttonA;
    [SerializeField] private Button buttonB;

    [SerializeField] private TMP_Text textA;
    [SerializeField] private TMP_Text textB;

    [Header("Navigation UI")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button prevButton;

    [Header("Answered Sprite")]
    [SerializeField] private Sprite answeredChoice;

    [Header("Progress Bar")]
    [SerializeField] private Image[] progressImages;
    [SerializeField] private Sprite emptyProgressSprite;
    [SerializeField] private Sprite answeredProgressSprite;

    [Header("Next Canvas Reference")]
    [SerializeField] private GameObject quizCanvas;
    [SerializeField] private GameObject buddyCanvas;
    [SerializeField] private BuddyManager buddyManager;

    [Header("Question Settings")]
    [SerializeField] private int questionsPerRound = 5;
    [SerializeField] private float autoAdvanceDelay = 0.25f;

    private List<QuestionData> questions = new List<QuestionData>();
    private int currentQuestion = 0;
    
    private List<int> selectedChoiceIndices = new List<int>();
    private List<string> answers = new List<string>();

    private Image buttonAImage;
    private Image buttonBImage;
    private Sprite originalSpriteA;
    private Sprite originalSpriteB;
    private CanvasGroup nextButtonCanvasGroup;
    private CanvasGroup prevButtonCanvasGroup;

    private bool isTransitioning = false; 

    private void Awake()
    {
        if (buttonA != null)
        {
            buttonAImage = buttonA.GetComponent<Image>();
            if (buttonAImage != null) originalSpriteA = buttonAImage.sprite;
            buttonA.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        if (buttonB != null)
        {
            buttonBImage = buttonB.GetComponent<Image>();
            if (buttonBImage != null) originalSpriteB = buttonBImage.sprite;
            buttonB.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        if (nextButton != null)
        {
            nextButton.navigation = new Navigation { mode = Navigation.Mode.None };
            nextButtonCanvasGroup = nextButton.GetComponent<CanvasGroup>();
            if (nextButtonCanvasGroup == null)
            {
                nextButtonCanvasGroup = nextButton.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (prevButton != null)
        {
            prevButton.navigation = new Navigation { mode = Navigation.Mode.None };
            prevButtonCanvasGroup = prevButton.GetComponent<CanvasGroup>();
            if (prevButtonCanvasGroup == null)
            {
                prevButtonCanvasGroup = prevButton.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public void StartQuiz()
    {
        LoadCSV();

        currentQuestion = 0;
        selectedChoiceIndices.Clear();
        answers.Clear();
        isTransitioning = false;

        if (buttonA != null)
        {
            buttonA.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => SelectAnswer(1));
        }

        if (buttonB != null)
        {
            buttonB.onClick.RemoveAllListeners();
            buttonB.onClick.AddListener(() => SelectAnswer(2));
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(NextQuestion);
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(PreviousQuestion);
        }

        ShowQuestion();
    }

    private void LoadCSV()
    {
        if (csvFile == null)
        {
            Debug.LogError("ยังไม่ได้ลากไฟล์ CSV ใส่ในช่อง CSV File ของ QuizManager!");
            return;
        }

        questions.Clear();

        string[] lines = csvFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] data = line.Split(',');
            if (data.Length < 7) continue;

            questions.Add(new QuestionData
            {
                id = data[0].Trim().Trim('\"'), 
                section = data[1].Trim(),
                question = data[2].Trim(),
                choiceA = data[3].Trim(),
                choiceB = data[4].Trim(),
                choiceATarget = data[5].Trim(),
                choiceBTarget = data[6].Trim()
            });
        }
    }

    private void ShowQuestion()
    {
        StopAllCoroutines();

        if (questions == null || questions.Count == 0)
        {
            Debug.LogError("ไม่มีข้อมูลคำถามใน List!");
            return;
        }

        if (currentQuestion >= questions.Count)
        {
            FinishQuiz();
            return;
        }

        QuestionData q = questions[currentQuestion];

        if (sectionText != null) sectionText.text = q.section;
        if (questionNumberText != null) questionNumberText.text = q.id;
        if (questionText != null) questionText.text = q.question;

        if (textA != null) textA.text = q.choiceA;
        if (textB != null) textB.text = q.choiceB;

        bool hasAnswered = (currentQuestion < selectedChoiceIndices.Count && selectedChoiceIndices[currentQuestion] != 0);

        if (hasAnswered)
        {
            int savedChoice = selectedChoiceIndices[currentQuestion];
            if (buttonAImage != null) SetAnsweredButton(buttonAImage, savedChoice == 1);
            if (buttonBImage != null) SetAnsweredButton(buttonBImage, savedChoice == 2);
        }
        else
        {
            ResetChoiceButton();
        }

        UpdateNextButtonState(hasAnswered);
        UpdatePrevButtonState(); 
        UpdateProgressBar();
        isTransitioning = false;
    }

    private void SelectAnswer(int answer)
    {
        if (isTransitioning) return;

        QuestionData q = questions[currentQuestion];
        string target = (answer == 1) ? q.choiceATarget : q.choiceBTarget;

        if (buttonAImage != null) SetAnsweredButton(buttonAImage, answer == 1);
        if (buttonBImage != null) SetAnsweredButton(buttonBImage, answer == 2);

        bool isReviewingOldQuestion = (currentQuestion < selectedChoiceIndices.Count);

        if (isReviewingOldQuestion)
        {
            selectedChoiceIndices[currentQuestion] = answer;
            answers[currentQuestion] = target;
        }
        else
        {
            selectedChoiceIndices.Add(answer);
            answers.Add(target);
        }

        UpdateProgressBar();
        UpdateNextButtonState(true);

        bool isLastQuestion = (currentQuestion == questions.Count - 1);
        if (isLastQuestion) return; 

        isTransitioning = true;
        StopAllCoroutines();
        StartCoroutine(AutoAdvanceRoutine());
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);
        currentQuestion++;
        ShowQuestion();
    }

    private void SetAnsweredButton(Image image, bool answered)
    {
        if (image == buttonAImage)
        {
            image.sprite = answered ? answeredChoice : originalSpriteA;
        }
        else if (image == buttonBImage)
        {
            image.sprite = answered ? answeredChoice : originalSpriteB;
        }

        image.color = Color.white;
    }

    private void ResetChoiceButton()
    {
        if (buttonAImage != null)
        {
            buttonAImage.sprite = originalSpriteA;
            buttonAImage.color = Color.white;
        }

        if (buttonBImage != null)
        {
            buttonBImage.sprite = originalSpriteB;
            buttonBImage.color = Color.white;
        }
    }

    private void UpdateNextButtonState(bool canClick)
    {
        if (nextButton == null) return;

        nextButton.gameObject.SetActive(true);
        nextButton.interactable = true;

        if (nextButtonCanvasGroup != null)
        {
            nextButtonCanvasGroup.alpha = canClick ? 1.0f : 0.4f;
            nextButtonCanvasGroup.blocksRaycasts = canClick;
        }
    }

    private void UpdatePrevButtonState()
    {
        if (prevButton == null) return;

        bool canGoBack = (currentQuestion > 0);

        if (prevButtonCanvasGroup != null)
        {
            prevButtonCanvasGroup.alpha = canGoBack ? 1.0f : 0f;
            prevButtonCanvasGroup.blocksRaycasts = canGoBack;
        }
    }

    public void NextQuestion()
    {
        if (isTransitioning) return;

        bool hasAnswered = (currentQuestion < selectedChoiceIndices.Count && selectedChoiceIndices[currentQuestion] != 0);
        if (!hasAnswered) return;

        StopAllCoroutines();

        if (currentQuestion >= questions.Count - 1)
        {
            FinishQuiz();
            return;
        }

        currentQuestion++;
        ShowQuestion();
    }

    public void PreviousQuestion()
    {
        if (isTransitioning || currentQuestion <= 0) return;

        StopAllCoroutines();
        currentQuestion--;
        ShowQuestion();
    }

    private void UpdateProgressBar()
    {
        if (progressImages == null || progressImages.Length == 0) return;

        int indexInRound = currentQuestion % questionsPerRound;
        bool currentQuestionAnswered = (currentQuestion < selectedChoiceIndices.Count && selectedChoiceIndices[currentQuestion] != 0);
        int filledCount = indexInRound + (currentQuestionAnswered ? 1 : 0);

        for (int i = 0; i < progressImages.Length; i++)
        {
            if (progressImages[i] != null)
            {
                progressImages[i].sprite = (i < filledCount) ? answeredProgressSprite : emptyProgressSprite;
            }
        }
    }

    // =========================================================
    // switch to BuddyCanvas
    // =========================================================
    private void FinishQuiz()
    {
        if (quizCanvas != null) quizCanvas.SetActive(false);
        if (buddyCanvas != null) buddyCanvas.SetActive(true);

        if (buddyManager != null)
        {
            buddyManager.gameObject.SetActive(true);
            buddyManager.ShowResult(answers);
        }
    }
}