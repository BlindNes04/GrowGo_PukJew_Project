using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ResultData
{
    public string buddyId;
    public string mbtiType;
    public string buddyName;
    public string skillName;
    public string skillDescription;
}

public class BuddyManager : MonoBehaviour
{
    [Header("CSV File")]
    [SerializeField] private TextAsset resultCsvFile;

    [Header("Buddy Result UI")]
    [SerializeField] private TMP_Text titleText;               
    [SerializeField] private Image resultCharacterImage;      
    [SerializeField] private TMP_Text resultBuddyNameText;     
    [SerializeField] private TMP_Text resultSkillNameText;    
    [SerializeField] private TMP_Text resultDescriptionText;  

    [Header("Canvas & Next Manager")]
    [SerializeField] private GameObject buddyCanvas;
    [SerializeField] private SeedManager seedManager;

    [Header("Next Button")]
    [SerializeField] private Button nextButton;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float delayBetween = 0.2f;

    private Dictionary<string, ResultData> resultDatabase = new Dictionary<string, ResultData>(StringComparer.OrdinalIgnoreCase);
    private CanvasGroup nextButtonCanvasGroup;

    private void Awake()
    {
        LoadResultCSV();

        if (nextButton != null)
        {
            nextButton.navigation = new Navigation { mode = Navigation.Mode.None };
            nextButtonCanvasGroup = nextButton.GetComponent<CanvasGroup>();
            if (nextButtonCanvasGroup == null)
            {
                nextButtonCanvasGroup = nextButton.gameObject.AddComponent<CanvasGroup>();
            }

            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(GoToSeedCanvas);
        }
    }

    private void LoadResultCSV()
    {
        if (resultCsvFile == null) return;

        resultDatabase.Clear();
        string[] lines = resultCsvFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] data = line.Split(',');
            if (data.Length < 7) continue;

            string mbtiKey = data[1].Trim().ToUpper();
            resultDatabase[mbtiKey] = new ResultData
            {
                buddyId = data[0].Trim(),
                mbtiType = mbtiKey,
                buddyName = data[2].Trim(),
                skillName = data[3].Trim(),
                skillDescription = data[6].Trim()
            };
        }
    }

    public void ShowResult(List<string> answeredTargets)
    {
        if (buddyCanvas != null)
        {
            buddyCanvas.SetActive(true);
        }

        int scoreE = 0, scoreI = 0;
        int scoreS = 0, scoreN = 0;
        int scoreT = 0, scoreF = 0;
        int scoreJ = 0, scoreP = 0;

        foreach (string target in answeredTargets)
        {
            string t = target.Trim().ToUpper();
            if (t == "E") scoreE++;
            else if (t == "I") scoreI++;
            else if (t == "S") scoreS++;
            else if (t == "N") scoreN++;
            else if (t == "T") scoreT++;
            else if (t == "F") scoreF++;
            else if (t == "J") scoreJ++;
            else if (t == "P") scoreP++;
        }

        string finalMBTI = "";
        finalMBTI += (scoreE >= scoreI) ? "E" : "I";
        finalMBTI += (scoreS >= scoreN) ? "S" : "N";
        finalMBTI += (scoreT >= scoreF) ? "T" : "F";
        finalMBTI += (scoreJ >= scoreP) ? "J" : "P";

        PrepareResultUI(finalMBTI);
        StartCoroutine(PlayResultFadeSequence());
    }

    private void PrepareResultUI(string mbtiKey)
    {
        SetAlpha(titleText, 0f);
        SetAlpha(resultCharacterImage, 0f);
        SetAlpha(resultBuddyNameText, 0f);
        SetAlpha(resultSkillNameText, 0f);
        SetAlpha(resultDescriptionText, 0f);

        if (nextButtonCanvasGroup != null)
        {
            nextButtonCanvasGroup.alpha = 0f;
            nextButtonCanvasGroup.blocksRaycasts = false;
        }

        if (resultDatabase.TryGetValue(mbtiKey, out ResultData data))
        {
            if (resultBuddyNameText != null) resultBuddyNameText.text = data.buddyName;
            if (resultSkillNameText != null) resultSkillNameText.text = data.skillName;
            if (resultDescriptionText != null) resultDescriptionText.text = data.skillDescription;

            LoadCharacterSprite(data.mbtiType);
        }
        else
        {
            if (resultBuddyNameText != null) resultBuddyNameText.text = $"คู่หูของคุณ: {mbtiKey}";
            if (resultSkillNameText != null) resultSkillNameText.text = "";
            if (resultDescriptionText != null) resultDescriptionText.text = "ยินดีด้วย คุณทำแบบทดสอบสำเร็จแล้ว!";

            LoadCharacterSprite(mbtiKey);
        }
    }

    private void LoadCharacterSprite(string imageName)
    {
        if (resultCharacterImage == null) return;

        Sprite loadedSprite = Resources.Load<Sprite>("buddy/" + imageName.Trim().ToLower());
        resultCharacterImage.sprite = loadedSprite;
        resultCharacterImage.preserveAspect = true;
    }

    // =========================================================
    // Fade-in Sequence
    // =========================================================
    private IEnumerator PlayResultFadeSequence()
    {
        yield return StartCoroutine(FadeGraphic(titleText));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(resultCharacterImage));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(resultBuddyNameText));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(resultSkillNameText));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(resultDescriptionText));
        yield return new WaitForSeconds(delayBetween);

        yield return StartCoroutine(FadeGraphic(nextButtonCanvasGroup));
        if (nextButtonCanvasGroup != null) nextButtonCanvasGroup.blocksRaycasts = true;
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

    private IEnumerator FadeGraphic(CanvasGroup cg)
    {
        if (cg == null) yield break;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    // =========================================================
    // Switch to SeedCanvas
    // =========================================================
    private void GoToSeedCanvas()
    {
        if (buddyCanvas != null) buddyCanvas.SetActive(false);

        if (seedManager != null)
        {
            seedManager.OpenSeedReward();
        }
    }
}