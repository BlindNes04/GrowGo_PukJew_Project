using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial GameObject")]
    [SerializeField] private GameObject popupPanel; 

    [Header("Tutorial Button")]
    [SerializeField] private Button openTutorialButton;

    [Header("Tutorial Image")]
    [SerializeField] private Image displayImage;

    [Header("Tutorial Sprites")]
    [SerializeField] private Sprite[] tutorialSprites; 

    [Header("Navigation Buttons")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;

    private int currentIndex = 0;

    private void Awake()
    {
        if (openTutorialButton != null) 
            openTutorialButton.onClick.AddListener(OpenTutorial);

        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (backButton != null) backButton.onClick.AddListener(BackPage);
        if (closeButton != null) closeButton.onClick.AddListener(CloseTutorial);
    }

    private void Start()
    {
        CloseTutorial();
    }

    public void OpenTutorial()
    {
        if (popupPanel != null) popupPanel.SetActive(true);
        currentIndex = 0;
        UpdateUI();
    }

    public void NextPage()
    {
        if (currentIndex < tutorialSprites.Length - 1)
        {
            currentIndex++;
            UpdateUI();
        }
    }

    public void BackPage()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateUI();
        }
    }

    public void CloseTutorial()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    private void UpdateUI()
    {
        if (displayImage != null && tutorialSprites != null && tutorialSprites.Length > 0)
        {
            displayImage.sprite = tutorialSprites[currentIndex];
        }

        if (backButton != null)
        {
            backButton.gameObject.SetActive(currentIndex > 0);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(currentIndex < tutorialSprites.Length - 1);
        }
    }
}