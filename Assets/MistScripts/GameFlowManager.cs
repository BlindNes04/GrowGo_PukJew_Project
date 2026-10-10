using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject startComponent;     
    public GameObject searchComponent;    
    public GameObject rewardComponent;    

    [Header("Error Popup Settings")]
    public Image errorImage;              
    public float showDuration = 1.5f;    
    public float fadeDuration = 0.5f;    

    [Header("Search Quote Settings")]
    public Image quoteImage;              
    public float quoteStartDelay = 0.5f;   
    public float quoteShowDuration = 2f;   
    public float quoteFadeDuration = 0.8f; 

    [Header("Stamina Settings")]
    public int maxStamina = 30;
    public int searchCost = 10;
    public TextMeshProUGUI staminaText;  

    private int currentStamina;
    private Coroutine errorCoroutine;
    private Coroutine quoteCoroutine;

    private const string STAMINA_KEY = "Player_Current_Stamina";
    private const string LAST_DATE_KEY = "Last_Login_Date";

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        CheckMidnightReset();
        UpdateStaminaUI();

        if (startComponent != null) startComponent.SetActive(true);
        if (searchComponent != null) searchComponent.SetActive(false);
        if (rewardComponent != null) rewardComponent.SetActive(false);

        if (errorImage != null) errorImage.gameObject.SetActive(false);
        if (quoteImage != null) quoteImage.gameObject.SetActive(false);

        if (AudioManager.Instance != null && AudioManager.Instance.bgmSource != null)
        {
            if (!AudioManager.Instance.bgmSource.isPlaying)
            {
                AudioManager.Instance.bgmSource.loop = true;
                AudioManager.Instance.bgmSource.Play();
            }
        }
    }

    void CheckMidnightReset()
    {
        string todayStr = DateTime.Now.ToString("yyyyMMdd");
        string lastSavedDate = PlayerPrefs.GetString(LAST_DATE_KEY, "");

        if (string.IsNullOrEmpty(lastSavedDate) || todayStr != lastSavedDate)
        {
            currentStamina = maxStamina;
            PlayerPrefs.SetInt(STAMINA_KEY, currentStamina);
            PlayerPrefs.SetString(LAST_DATE_KEY, todayStr);
            PlayerPrefs.Save();
        }
        else
        {
            currentStamina = PlayerPrefs.GetInt(STAMINA_KEY, maxStamina);
        }
    }

    public void OnClickStartSearch()
    {
        if (currentStamina >= searchCost)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSFX);
            }

            currentStamina -= searchCost;
            PlayerPrefs.SetInt(STAMINA_KEY, currentStamina);
            PlayerPrefs.Save();
            UpdateStaminaUI();

            if (startComponent != null) startComponent.SetActive(false);
            if (rewardComponent != null) rewardComponent.SetActive(false);
            if (searchComponent != null) searchComponent.SetActive(true);

            ShowQuote();
        }
        else
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.errorSFX);
            }
            ShowAutoFadeError();
        }
    }

    public void BackToStart()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSFX);
        }

        if (rewardComponent != null) rewardComponent.SetActive(false);
        if (searchComponent != null) searchComponent.SetActive(false);
        if (startComponent != null) startComponent.SetActive(true);
    }

    void ShowQuote()
    {
        if (quoteImage == null) return;
        if (quoteCoroutine != null) StopCoroutine(quoteCoroutine);
        quoteCoroutine = StartCoroutine(FadeQuoteRoutine());
    }

    IEnumerator FadeQuoteRoutine()
    {
        quoteImage.gameObject.SetActive(false);
        yield return new WaitForSeconds(quoteStartDelay);

        quoteImage.gameObject.SetActive(true);
        Color c = quoteImage.color;
        c.a = 1f;
        quoteImage.color = c;

        yield return new WaitForSeconds(quoteShowDuration);

        float elapsed = 0f;
        while (elapsed < quoteFadeDuration)
        {
            elapsed += Time.deltaTime;
            Color newColor = quoteImage.color;
            newColor.a = Mathf.Lerp(1f, 0f, elapsed / quoteFadeDuration);
            quoteImage.color = newColor;
            yield return null;
        }

        quoteImage.gameObject.SetActive(false);
    }

    void ShowAutoFadeError()
    {
        if (errorImage == null) return;
        if (errorCoroutine != null) StopCoroutine(errorCoroutine);
        errorCoroutine = StartCoroutine(FadeErrorRoutine());
    }

    IEnumerator FadeErrorRoutine()
    {
        errorImage.gameObject.SetActive(true);
        Color c = errorImage.color;
        c.a = 1f;
        errorImage.color = c;

        yield return new WaitForSeconds(showDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            Color newColor = errorImage.color;
            newColor.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            errorImage.color = newColor;
            yield return null;
        }

        errorImage.gameObject.SetActive(false);
    }

    void UpdateStaminaUI()
    {
        if (staminaText != null) staminaText.text = currentStamina.ToString();
    }
}