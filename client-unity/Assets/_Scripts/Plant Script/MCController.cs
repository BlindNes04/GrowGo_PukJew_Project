using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MCController : MonoBehaviour
{
    [Header("UI Reference")]
    public Image characterImage;
    public RectTransform rectTransform; // ใช้คุมตำแหน่ง/ขนาด

    [System.Serializable]
    public class StageData
    {
        public string stageId;           // ตรงกับ stage_id ใน DB (เช่น STAGE_TM_01, STAGE_CC_EVO_LORD)
        public string stageName;         // ชื่อร่างสำหรับแสดงผลบน Pill UI (เช่น หน่อกะทิน้อย, มะเขือเทศนางฟ้า)
        public Sprite sprite;            // ภาพรูปพืช
        public Vector2 anchoredPosition; // จุดวางบนแท่นหิน (X, Y)
        public Vector2 customSize;       // ขนาด กว้าง x สูง (ถ้าใส่ 0 จะใช้ขนาดจริงของรูป)
    }

    [Header("Character Stages (รายการร่างพืชทุกชนิด)")]
    public List<StageData> stages = new List<StageData>();

    [Header("Current State")]
    public string currentStageId = "STAGE_TM_01";
    public int currentLevel = 1;

    void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        UpdateCharacterVisual();
    }

    public void UpdateCharacterVisual()
    {
        SetStageById(currentStageId);
    }

    // 💡 1. สลับภาพ ตำแหน่ง และสเกลตาม stage_id จาก DB โดยตรง
    public void SetStageById(string stageId)
    {
        if (characterImage == null || stages == null || stages.Count == 0) return;

        currentStageId = stageId;

        // ค้นหา StageData ที่ตรงกับ stage_id ใน DB
        StageData data = stages.Find(s => s.stageId == stageId);

        if (data != null && data.sprite != null)
        {
            ApplyStageVisual(data);
        }
        else
        {
            Debug.LogWarning($"[MCController] ⚠️ ไม่พบข้อมูลภาพสำหรับ stageId: '{stageId}' โปรดตรวจสอบ stageId ใน Inspector");
        }
    }

    // 💡 2. ดึงชื่อร่าง (Stage Name) ตาม stageId ไปแสดงผลบน Pill UI
    public string GetStageNameById(string stageId)
    {
        if (stages == null || stages.Count == 0) return "";

        StageData data = stages.Find(s => s.stageId == stageId);
        return data != null ? data.stageName : "";
    }

    // 💡 3. สลับภาพตาม Index (สำหรับกรณีรองรับระบบเก่า)
    public void SetStageIndex(int index)
    {
        if (characterImage == null || stages == null || stages.Count == 0) return;

        int clampedIndex = Mathf.Clamp(index, 0, stages.Count - 1);
        StageData data = stages[clampedIndex];

        if (data != null && data.sprite != null)
        {
            currentStageId = data.stageId;
            ApplyStageVisual(data);
        }
    }

    // ⚙️ แสดงผลภาพและจัดตำแหน่งบนแท่นหิน
    private void ApplyStageVisual(StageData data)
    {
        characterImage.sprite = data.sprite;
        
        // ปรับตำแหน่งบนแท่นหิน
        rectTransform.anchoredPosition = data.anchoredPosition;

        // ปรับขนาด
        if (data.customSize != Vector2.zero)
        {
            rectTransform.sizeDelta = data.customSize;
        }
        else
        {
            characterImage.SetNativeSize();
        }
    }

    // 💡 สั่งเปลี่ยนร่างตาม Level (1-5)
    public void SetStage(int stage)
    {
        currentLevel = stage;
        SetStageIndex(currentLevel - 1);
    }
}