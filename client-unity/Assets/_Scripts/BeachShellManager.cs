using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class BeachItemData
{
    public string itemId;   // ID ไอเทมตรงตาม Supabase
    public string itemName; // ชื่อภาษาไทย
    public Sprite itemIcon; // รูปไอเทม
    
    [Header("Drop Rate Settings")]
    [Tooltip("โอกาสดรอป เช่น 0.3 = 30%, 0.25 = 25%")]
    public float dropRate = 0.25f; 
}

// ==================== BeachShellManager Class ====================
public class BeachShellManager : MonoBehaviour
{
    public static BeachShellManager Instance;

    [Header("หอยทั้ง 4 ตำแหน่ง")]
    public Button[] shellButtons; 

    [Header("ระบบ Reward Popup")]
    public RewardController rewardController; 
    public BeachItemData[] beachItems; 

    [Header("ตั้งค่า Cooldown (วินาที)")]
    public float cooldownSeconds = 10f; 
    public TextMeshProUGUI cooldownText; 

    private Button activeShell;
    private Coroutine shakeCoroutine;
    private Quaternion activeShellOriginalRotation;
    private bool isCooldown = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        CheckCooldownState();
    }

    [ContextMenu("Clear Cooldown PlayerPrefs")]
    public void ClearCooldownPrefs()
    {
        PlayerPrefs.DeleteKey("Beach_LastClaimTime");
        PlayerPrefs.Save();
        Debug.Log("[BeachShellManager] 🟢 Cleared Beach_LastClaimTime PlayerPrefs!");
    }

    public void RespawnShells()
    {
        SetupNewRound();
    }

    private void CheckCooldownState()
    {
        string lastClaimTimeString = PlayerPrefs.GetString("Beach_LastClaimTime", "");

        if (!string.IsNullOrEmpty(lastClaimTimeString))
        {
            long lastClaimTicks = Convert.ToInt64(lastClaimTimeString);
            DateTime lastClaimTime = new DateTime(lastClaimTicks);
            TimeSpan timePassed = DateTime.UtcNow - lastClaimTime;

            if (timePassed.TotalSeconds < cooldownSeconds)
            {
                float remainingTime = (float)(cooldownSeconds - timePassed.TotalSeconds);
                Debug.Log($"[BeachShellManager] ⏳ Game is in Cooldown! Remaining: {remainingTime:F1}s");
                StartCoroutine(StartCooldownTimer(remainingTime));
                return;
            }
        }

        SetupNewRound();
    }

    public void SetupNewRound()
    {
        isCooldown = false;
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);

        if (shellButtons == null || shellButtons.Length == 0) return;

        foreach (Button btn in shellButtons)
        {
            if (btn != null)
            {
                btn.gameObject.SetActive(true);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnShellClicked(btn));
            }
        }

        int randomIndex = UnityEngine.Random.Range(0, shellButtons.Length);
        activeShell = shellButtons[randomIndex];
        activeShellOriginalRotation = activeShell.transform.localRotation;

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(PlayShakeAnimation(activeShell.transform));

        Debug.Log($"[BeachShellManager] 🟢 New round ready! Shaking shell index: {randomIndex}");
    }

    private IEnumerator PlayShakeAnimation(Transform targetTransform)
    {
        Quaternion originalRot = targetTransform.localRotation;

        while (true)
        {
            int shakeCount = 4;
            float shakeAngle = 5f;
            float shakeSpeed = 0.04f;

            for (int i = 0; i < shakeCount; i++)
            {
                targetTransform.localRotation = originalRot * Quaternion.Euler(0, 0, shakeAngle);
                yield return new WaitForSeconds(shakeSpeed);

                targetTransform.localRotation = originalRot * Quaternion.Euler(0, 0, -shakeAngle);
                yield return new WaitForSeconds(shakeSpeed);
            }

            targetTransform.localRotation = originalRot;
            yield return new WaitForSeconds(1.5f);
        }
    }

    private void OnShellClicked(Button clickedShell)
    {
        Debug.Log($"[BeachShellManager] 👆 Shell clicked: {clickedShell.name} | isCooldown: {isCooldown}");

        if (isCooldown)
        {
            Debug.LogWarning("[BeachShellManager] ⚠️ Click ignored: Game is currently on cooldown!");
            return;
        }

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        if (activeShell != null) activeShell.transform.localRotation = activeShellOriginalRotation;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.clickSFX);
        }

        if (beachItems != null && beachItems.Length > 0)
        {
            // 🎲 สุ่มไอเทมตามเปอร์เซ็นต์ Drop Rate (Weighted Random)
            BeachItemData currentEarnedItem = GetRandomItemByDropRate();

            if (currentEarnedItem != null)
            {
                Debug.Log($"[BeachShellManager] 🎁 Earned Item: {currentEarnedItem.itemName} ({currentEarnedItem.itemId}) | Drop Rate: {currentEarnedItem.dropRate * 100}%");

                ItemData rewardData = new ItemData
                {
                    itemId = currentEarnedItem.itemId,
                    itemName = currentEarnedItem.itemName,
                    itemIcon = currentEarnedItem.itemIcon
                };

                if (rewardController != null)
                {
                    rewardController.OpenRewardWindow(rewardData);
                }
                else
                {
                    Debug.LogError("[BeachShellManager] 🔴 rewardController is null! Check Inspector.");
                }
            }
        }

        clickedShell.gameObject.SetActive(false);
    }

    // 🎲 ฟังก์ชันสุ่ม Weighted Random
    private BeachItemData GetRandomItemByDropRate()
    {
        float totalWeight = 0f;

        // คำนวณผลรวมน้ำหนักทั้งหมด
        foreach (var item in beachItems)
        {
            totalWeight += item.dropRate;
        }

        if (totalWeight <= 0f) return beachItems[0];

        // สุ่มตัวเลข 0 ถึง totalWeight
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentSum = 0f;

        // สุ่มเทียบช่วงโอกาส
        foreach (var item in beachItems)
        {
            currentSum += item.dropRate;
            if (randomValue <= currentSum)
            {
                return item;
            }
        }

        return beachItems[0];
    }

    public void OnRewardClaimed()
    {
        PlayerPrefs.SetString("Beach_LastClaimTime", DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();

        Debug.Log("[BeachShellManager] 🟢 Reward claimed! Cooldown started.");
        StartCoroutine(StartCooldownTimer(cooldownSeconds));
    }

    private IEnumerator StartCooldownTimer(float duration)
    {
        isCooldown = true;
        float remainingTime = duration;

        if (cooldownText != null) cooldownText.gameObject.SetActive(true);

        while (remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;

            if (cooldownText != null)
            {
                TimeSpan t = TimeSpan.FromSeconds(remainingTime);
                cooldownText.text = string.Format("รอสำรวจใหม่: {0:D2}:{1:D2}", t.Minutes, t.Seconds);
            }

            yield return null;
        }

        SetupNewRound();
    }
}