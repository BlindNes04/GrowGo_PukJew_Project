using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class BeachItemData
{
    public string itemId;   // ID ไอเทมไว้ส่งเข้า DB (เช่น "beach_sand_01", "shell_01")
    public string itemName; // ชื่อภาษาไทย
    public Sprite itemIcon; // รูปไอเทม
}

public class BeachShellManager : MonoBehaviour
{
    public static BeachShellManager Instance;

    [Header("หอยทั้ง 4 ตำแหน่ง")]
    public Button[] shellButtons; // ลาก Shell 01 - 04 มาใส่

    [Header("ระบบ Reward Popup")]
    public RewardController rewardController; // ลาก reward component มาใส่
    public BeachItemData[] beachItems; // รายการไอเทมหาดทราย

    [Header("ตั้งค่า Cooldown (วินาที)")]
    public float cooldownSeconds = 10f; // ตั้งเวลารอรีไอเทม (ทดสอบใส่ 10 วินาทีก่อนได้)
    public TextMeshProUGUI cooldownText; // UI แสดงเวลาถอยหลัง (ถ้ามี หรือปล่อย None ไว้ได้)

    private Button activeShell;
    private Coroutine shakeCoroutine;
    private Quaternion activeShellOriginalRotation;
    private bool isCooldown = false;
    private BeachItemData currentEarnedItem;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CheckCooldownState();
    }

    // 1. ตรวจสอบสถานะเวลา Cooldown
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
                // ยังอยู่ในช่วง Cooldown
                float remainingTime = (float)(cooldownSeconds - timePassed.TotalSeconds);
                StartCoroutine(StartCooldownTimer(remainingTime));
                return;
            }
        }

        // ถ้าไม่มี Cooldown ค้างอยู่ ให้เริ่มสุ่มหอยรอบใหม่ทันที
        SetupNewRound();
    }

    // 2. เริ่มรอบใหม่: โชว์หอยทั้งหมด + สุ่มหอย 1 ตัวให้สั่น
    public void SetupNewRound()
    {
        isCooldown = false;
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);

        // แสดงหอยทุกตัวกลับมาที่เดิม
        foreach (Button btn in shellButtons)
        {
            if (btn != null)
            {
                btn.gameObject.SetActive(true);
                btn.onClick.RemoveAllListeners();
            }
        }

        if (shellButtons.Length == 0) return;

        // สุ่มเลือกหอย 1 ตัว
        int randomIndex = UnityEngine.Random.Range(0, shellButtons.Length);
        activeShell = shellButtons[randomIndex];
        activeShellOriginalRotation = activeShell.transform.localRotation;

        activeShell.onClick.AddListener(() => OnShellClicked(activeShell));

        // เล่น Animation เขย่า
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(PlayShakeAnimation(activeShell.transform));
    }

    // 3. Animation เขย่าหอย
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

    // 4. เมื่อผู้เล่นกดที่หอย
    private void OnShellClicked(Button clickedShell)
    {
        if (isCooldown) return;

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        clickedShell.transform.localRotation = activeShellOriginalRotation;

        if (MAudioManager.instance != null)
        {
            MAudioManager.instance.PlayClickSFX();
        }

        // สุ่มไอเทมรางวัล
        if (beachItems.Length > 0 && rewardController != null)
        {
            int randomItemIndex = UnityEngine.Random.Range(0, beachItems.Length);
            currentEarnedItem = beachItems[randomItemIndex];

            ItemData rewardData = new ItemData
            {
                itemName = currentEarnedItem.itemName,
                itemIcon = currentEarnedItem.itemIcon
            };

            // เปิด Popup รางวัล
            rewardController.OpenRewardWindow(rewardData);
        }

        // ซ่อนหอยตัวที่ถูกกดไป
        clickedShell.gameObject.SetActive(false);
    }

    // 5. ฟังก์ชันนี้จะถูกเรียกเมื่อผู้เล่นกดปิด Popup รางวัล
    public void OnRewardClaimed()
    {
        // 5.1 เพิ่มไอเทมเข้า Inventory (ตอนนี้ทำเป็น Mock ไว้ก่อน)
        if (currentEarnedItem != null)
        {
            AddItemToInventoryMock(currentEarnedItem.itemId, 1);
        }

        // 5.2 บันทึกเวลาที่เริ่ม Cooldown
        PlayerPrefs.SetString("Beach_LastClaimTime", DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();

        // 5.3 เริ่มนับเวลาถอยหลัง
        StartCoroutine(StartCooldownTimer(cooldownSeconds));
    }

    // 6. นับเวลา Cooldown ถอยหลัง
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

        // พอเวลาหมด ให้เปิดรอบสุ่มใหม่ หอยจะกลับมาโชว์ครบเหมือนเดิม
        SetupNewRound();
    }

    // 💡 ฟังก์ชันจำลองการยิงเข้า Inventory (เตรียมไว้เชื่อม Supabase พรุ่งนี้)
    private void AddItemToInventoryMock(string itemId, int amount)
    {
        Debug.Log($"[Mock Inventory] เพิ่มไอเทมสำเร็จ: ID={itemId}, จำนวน={amount}");
        
        // TODO (พรุ่งนี้ตอนต่อ Supabase):
        // SupabaseClient.From<UserInventory>().Insert(new UserInventory { ItemId = itemId, Amount = amount });
    }
}