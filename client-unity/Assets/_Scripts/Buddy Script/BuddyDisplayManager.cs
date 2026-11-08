using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class BuddyInfoData
{
    public string buddyId;
    public string mbtiType;
    public string buddyName;
    public string skillName;
    public string skillType;
    public string skillValue;
    public string skillDescription;
}

public class BuddyDisplayManager : MonoBehaviour
{
    [Header("CSV File for Mapping (ลากไฟล์ buddy_info_shorten.csv มาใส่)")]
    [SerializeField] private TextAsset resultCsvFile;

    [Header("Display Target")]
    [SerializeField] private SpriteRenderer buddySpriteRenderer;
    [SerializeField] private Image buddyUIImage;
    [SerializeField] private Button buddyClickButton; // ปุ่มกดที่ตัวละคร Buddy

    [Header("Speech Bubble Popup UI")]
    [SerializeField] private GameObject speechBubbleUI;    // ตัว Container กล่องคำพูด
    [SerializeField] private TMP_Text bubbleBuddyNameText;  // Text ชื่อบัดดี้ (เช่น เสือดาวสายฟ้า)
    [SerializeField] private TMP_Text bubbleMbtiText;       // Text MBTI (เช่น ESTP)
    [SerializeField] private TMP_Text bubbleSkillDescText;  // Text คำอธิบายบัฟ

    [Header("Popup Settings")]
    [SerializeField] private float autoHideDelay = 3.5f;   // ระยะเวลาแสดงผลป๊อปอัปก่อนซ่อนตัว (วินาที)

    [Header("UI Info (Optional)")]
    [SerializeField] private TMP_Text buddyNameText;

    // Dictionary เก็บบันทึกข้อมูลเต็มของ Buddy ทุกตัว
    private Dictionary<string, BuddyInfoData> buddyDatabase = new Dictionary<string, BuddyInfoData>(StringComparer.OrdinalIgnoreCase);
    private BuddyInfoData currentBuddyData;
    private Coroutine hideBubbleCoroutine;

    private void Awake()
    {
        LoadMappingCSV();

        // ตั้งค่า Event เมื่อคลิกตัวละคร Buddy
        if (buddyClickButton != null)
        {
            buddyClickButton.onClick.RemoveAllListeners();
            buddyClickButton.onClick.AddListener(OnBuddyClicked);
        }

        // ซ่อน Speech Bubble ไว้ก่อนในตอนเริ่มต้น
        if (speechBubbleUI != null)
        {
            speechBubbleUI.SetActive(false);
        }
    }

    private void Start()
    {
        LoadUserBuddy();
    }

    // โหลด CSV และเก็บบันทึกข้อมูลทั้ง 7 คอลัมน์
    private void LoadMappingCSV()
    {
        if (resultCsvFile == null) return;

        buddyDatabase.Clear();
        string[] lines = resultCsvFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] data = line.Split(',');
            if (data.Length < 7) continue;

            BuddyInfoData info = new BuddyInfoData
            {
                buddyId = data[0].Trim(),
                mbtiType = data[1].Trim().ToUpper(),
                buddyName = data[2].Trim(),
                skillName = data[3].Trim(),
                skillType = data[4].Trim(),
                skillValue = data[5].Trim(),
                skillDescription = data[6].Trim()
            };

            // บันทึกคู่อ้างอิงด้วย buddy_id และ mbti_type
            if (!buddyDatabase.ContainsKey(info.buddyId))
                buddyDatabase[info.buddyId] = info;

            if (!buddyDatabase.ContainsKey(info.mbtiType))
                buddyDatabase[info.mbtiType] = info;
        }
    }

    public void LoadUserBuddy()
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogError("[BuddyDisplayManager] 🔴 ไม่พบ SupabaseManager.Instance!");
            return;
        }

        // ดึง buddy_id ของผู้เล่นจาก DB
        StartCoroutine(SupabaseManager.Instance.GetUserBuddyId(
            onResult: (buddyId) =>
            {
                if (string.IsNullOrEmpty(buddyId))
                {
                    Debug.LogWarning("[BuddyDisplayManager] ⚠️ ผู้เล่นยังไม่มี buddy_id -> ซ่อนตัวละคร Buddy");
                    gameObject.SetActive(false);
                    return;
                }

                Debug.Log($"[BuddyDisplayManager] 🟢 ดึง buddy_id จาก DB สำเร็จ: {buddyId}");
                UpdateBuddyVisual(buddyId);
            },
            onError: (err) =>
            {
                Debug.LogError($"[BuddyDisplayManager] 🔴 ดึง buddy_id ล้มเหลว: {err}");
            }
        ));
    }

    private void UpdateBuddyVisual(string buddyId)
    {
        gameObject.SetActive(true);

        // ค้นหาข้อมูล Buddy Data จาก CSV
        if (buddyDatabase.TryGetValue(buddyId, out BuddyInfoData data))
        {
            currentBuddyData = data;

            // 1. แปลงค่า skillValue จาก string เป็น float แบบปลอดภัย
            float parsedSkillValue = 0f;
            if (!string.IsNullOrEmpty(currentBuddyData.skillValue))
            {
                float.TryParse(
                    currentBuddyData.skillValue,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out parsedSkillValue
                );
            }

            // 2. ยิงส่งค่าพาสซีฟไปติดตั้งที่ PlantStatus ทันที
            PlantStatus plantStatus = FindAnyObjectByType<PlantStatus>();
            if (plantStatus != null)
            {
                plantStatus.EquipBuddyPassive(currentBuddyData.skillType, parsedSkillValue);
            }

            // 3. ส่งต่อให้ BuffManager ดั้งเดิมด้วย (ถ้ายังใช้งานอยู่)
            if (BuffManager.Instance != null)
            {
                BuffManager.Instance.SetActiveBuddySkill(currentBuddyData.skillType, currentBuddyData.skillValue);
            }
        }

        string spriteName = (currentBuddyData != null) ? currentBuddyData.mbtiType.ToLower() : buddyId.Trim().ToLower();

        // โหลดรูปจาก Resources/main_buddy/{spriteName}
        string spritePath = "main_buddy/" + spriteName;
        Sprite buddySprite = Resources.Load<Sprite>(spritePath);

        // Fallback: ถ้ายังหาไม่เจอ ลองดึงด้วยชื่อ buddyId ดั้งเดิมอีกที
        if (buddySprite == null && spriteName != buddyId.Trim().ToLower())
        {
            buddySprite = Resources.Load<Sprite>("main_buddy/" + buddyId.Trim().ToLower());
        }

        if (buddySprite != null)
        {
            if (buddySpriteRenderer != null) buddySpriteRenderer.sprite = buddySprite;
            if (buddyUIImage != null)
            {
                buddyUIImage.sprite = buddySprite;
                buddyUIImage.preserveAspect = true;
            }
            Debug.Log($"[BuddyDisplayManager] 🟢 แสดงผลรูปภาพ Buddy '{spritePath}' เรียบร้อย");
        }
        else
        {
            Debug.LogWarning($"[BuddyDisplayManager] ⚠️ หาไฟล์ Sprite ที่ 'Resources/{spritePath}' ไม่เจอ!");
        }

        if (buddyNameText != null)
        {
            buddyNameText.text = (currentBuddyData != null) ? currentBuddyData.buddyName : buddyId.ToUpper();
        }
    }

    // ฟังก์ชันเมื่อผู้เล่นกดที่ตัวละคร Buddy
    public void OnBuddyClicked()
    {
        if (currentBuddyData == null) return;

        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        ShowSpeechBubble();
    }

    private void ShowSpeechBubble()
    {
        if (speechBubbleUI == null) return;

        // ดึงข้อมูลมาใส่กล่อง Speech Bubble
        if (bubbleBuddyNameText != null) bubbleBuddyNameText.text = currentBuddyData.buddyName;

        // เปลี่ยนสี Text ของ MBTI ตามบ้านความสัมพันธ์
        if (bubbleMbtiText != null)
        {
            bubbleMbtiText.text = currentBuddyData.mbtiType;
            bubbleMbtiText.color = GetMbtiColor(currentBuddyData.mbtiType);
        }

        if (bubbleSkillDescText != null) bubbleSkillDescText.text = currentBuddyData.skillDescription;

        speechBubbleUI.SetActive(true);

        if (hideBubbleCoroutine != null)
        {
            StopCoroutine(hideBubbleCoroutine);
        }

        hideBubbleCoroutine = StartCoroutine(AutoHideBubbleRoutine());
    }

    // ฟังก์ชันเทียบกลุ่มบ้าน MBTI แล้วคืนค่าสีตาม Hex ใน Figma
    private Color GetMbtiColor(string mbti)
    {
        if (string.IsNullOrEmpty(mbti)) return Color.white;
        string code = mbti.Trim().ToUpper();

        // 1. บ้านเขียว (INFP, ENFP, INFJ, ENFJ)
        if (code.Contains("NF"))
        {
            ColorUtility.TryParseHtmlString("#4EBA6F", out Color green);
            return green;
        }

        // 2. บ้านม่วง (INTJ, INTP, ENTJ, ENTP)
        if (code.Contains("NT"))
        {
            ColorUtility.TryParseHtmlString("#8A38F5", out Color purple);
            return purple;
        }

        // 3. บ้านฟ้า/น้ำเงิน (ISTJ, ISFJ, ESTJ, ESFJ)
        if (code.Contains("S") && code.EndsWith("J"))
        {
            ColorUtility.TryParseHtmlString("#3B82F6", out Color blue);
            return blue;
        }

        // 4. บ้านเหลือง/ส้ม (ISTP, ISFP, ESTP, ESFP)
        if (code.Contains("S") && code.EndsWith("P"))
        {
            ColorUtility.TryParseHtmlString("#EAC326", out Color yellow);
            return yellow;
        }

        return Color.white;
    }

    private IEnumerator AutoHideBubbleRoutine()
    {
        yield return new WaitForSeconds(autoHideDelay);
        if (speechBubbleUI != null)
        {
            speechBubbleUI.SetActive(false);
        }
    }
}