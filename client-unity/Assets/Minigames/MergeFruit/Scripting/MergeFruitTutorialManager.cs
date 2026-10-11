using UnityEngine;

public class MergeFruitTutorialManager : MonoBehaviour
{
    [Header("Tutorial Popup Container")]
    public GameObject popupTutorialPanel;

    [Header("Tutorial Pages (tut01 - tut05)")]
    public GameObject[] pages;

    private int currentIndex = 0;

    private void Start()
    {
        if (popupTutorialPanel != null)
            popupTutorialPanel.SetActive(false);
    }

    public void OpenTutorial()
    {
        popupTutorialPanel.SetActive(true);
        currentIndex = 0;
        ShowPage(currentIndex);
    }

    public void CloseTutorial()
    {
        popupTutorialPanel.SetActive(false);
    }

    public void NextPage()
    {
        if (currentIndex < pages.Length - 1)
        {
            currentIndex++;
            ShowPage(currentIndex);
        }
    }

    public void PrevPage()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            ShowPage(currentIndex);
        }
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
            {
                pages[i].SetActive(i == index);
            }
        }
    }
}