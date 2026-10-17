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
        public string stageName;
        public Sprite sprite;
        public Vector2 anchoredPosition; // จุดวางบนแท่นหิน (X, Y)
        public Vector2 customSize;       // ขนาด กว้าง x สูง
    }

    [Header("Character Stages (1-5)")]
    public StageData[] stages;

    [Header("Current State")]
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
        SetStageIndex(currentLevel - 1);
    }

    // 💡 สลับรูปภาพ ตำแหน่ง และสเกลตาม Index ของร่าง (0 ถึง 4)
    public void SetStageIndex(int index)
    {
        if (characterImage == null || stages == null || stages.Length == 0) return;

        int clampedIndex = Mathf.Clamp(index, 0, stages.Length - 1);
        StageData data = stages[clampedIndex];

        if (data.sprite != null)
        {
            characterImage.sprite = data.sprite;
            
            // ปรับตำแหน่งตามแต่ละร่าง
            rectTransform.anchoredPosition = data.anchoredPosition;

            // ถ้ากำหนดขนาดไว้ ให้ปรับตาม ถ้าใส่ 0 จะใช้ขนาดจริงของรูป
            if (data.customSize != Vector2.zero)
            {
                rectTransform.sizeDelta = data.customSize;
            }
            else
            {
                characterImage.SetNativeSize();
            }
        }
    }

    // 💡 สั่งเปลี่ยนร่างตาม Level (1-5)
    public void SetStage(int stage)
    {
        currentLevel = stage;
        UpdateCharacterVisual();
    }

    public void EvolveNextStage()
    {
        if (currentLevel < stages.Length)
        {
            currentLevel++;
            UpdateCharacterVisual();
        }
    }
}