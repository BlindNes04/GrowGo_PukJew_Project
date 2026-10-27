using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ==================== BeachGameFlowManager Class ====================
public class BeachGameFlowManager : MonoBehaviour
{
    public static BeachGameFlowManager Instance { get; private set; }

    [Header("UI Components")]
    public GameObject startComponent;     
    public GameObject searchComponent;    
    public GameObject rewardComponent;    
    public GameObject cooldownComponent;  

    [Header("Shell Cooldown Settings")]
    public float respawnCooldownSeconds = 300f;
    public TextMeshProUGUI cooldownTimerText;  

    [Header("Error Popup Settings")]
    public Image errorImage;              
    public float showDuration = 1.5f;    
    public float fadeDuration = 0.5f;    

    private GameObject currentClickedShell;
    private Coroutine cooldownCoroutine;
    private Coroutine errorCoroutine;

    private const string COOLDOWN_KEY = "BeachIsland_Last_Collect_Timestamp";

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        CheckCooldownState();
    }

    // Checks persistent cooldown state on scene start
    void CheckCooldownState()
    {
        string lastSavedTimeStr = PlayerPrefs.GetString(COOLDOWN_KEY, "");

        if (!string.IsNullOrEmpty(lastSavedTimeStr))
        {
            DateTime lastSavedTime = DateTime.Parse(lastSavedTimeStr);
            double elapsedSeconds = (DateTime.UtcNow - lastSavedTime).TotalSeconds;

            if (elapsedSeconds < respawnCooldownSeconds)
            {
                float remainingSeconds = (float)(respawnCooldownSeconds - elapsedSeconds);
                StartCooldown(remainingSeconds);
                return;
            }
        }

        ShowReadyToExploreState();
    }

    // Triggered when user interacts with a shell on the beach
    public void OnShellCollected(GameObject shellObj, ItemData rewardItem)
    {
        currentClickedShell = shellObj;

        if (startComponent != null) startComponent.SetActive(false);
        if (searchComponent != null) searchComponent.SetActive(false);

        if (rewardComponent != null)
        {
            RewardController rewardCtrl = rewardComponent.GetComponent<RewardController>();
            if (rewardCtrl != null && rewardItem != null)
            {
                rewardCtrl.OpenRewardWindow(rewardItem);
            }
            rewardComponent.SetActive(true);
        }
    }

    // Callback when closing reward UI, removing collected shell, and starting cooldown
    public void BackToStart()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSFX);
        }

        // Remove collected shell from scene
        if (currentClickedShell != null)
        {
            Destroy(currentClickedShell);
            currentClickedShell = null;
        }

        if (rewardComponent != null) rewardComponent.SetActive(false);

        // Record timestamp and initiate cooldown timer
        PlayerPrefs.SetString(COOLDOWN_KEY, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();

        StartCooldown(respawnCooldownSeconds);
    }

    // Starts cooldown sequence and timer UI updates
    private void StartCooldown(float remainingTime)
    {
        if (startComponent != null) startComponent.SetActive(false);
        if (cooldownComponent != null) cooldownComponent.SetActive(true);

        if (cooldownCoroutine != null) StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(CooldownTimerRoutine(remainingTime));
    }

    private IEnumerator CooldownTimerRoutine(float duration)
    {
        float timer = duration;

        while (timer > 0f)
        {
            timer -= Time.deltaTime;

            if (cooldownTimerText != null)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(timer);
                cooldownTimerText.text = string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
            }

            yield return null;
        }

        ShowReadyToExploreState();
    }

    // Resets UI and triggers shell respawn when cooldown completes
    private void ShowReadyToExploreState()
    {
        if (cooldownComponent != null) cooldownComponent.SetActive(false);
        if (rewardComponent != null) rewardComponent.SetActive(false);
        if (startComponent != null) startComponent.SetActive(true);

        // Respawn shells on beach if manager is assigned
        if (BeachShellManager.Instance != null)
        {
            BeachShellManager.Instance.RespawnShells();
        }
    }

    public void ShowAutoFadeError()
    {
        if (errorImage == null) return;
        if (errorCoroutine != null) StopCoroutine(errorCoroutine);
        errorCoroutine = StartCoroutine(FadeErrorRoutine());
    }

    private IEnumerator FadeErrorRoutine()
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
}