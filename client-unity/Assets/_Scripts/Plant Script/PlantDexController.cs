using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlantDexController : MonoBehaviour
{
    public static PlantDexController Instance;

    [Header("UI Component")]
    public Transform gridContainer;       // Content Transform ใน ScrollView
    public GameObject plantCardPrefab;    // Prefab ของการ์ดพืช
    public Button closeButton;            // ปุ่มกากบาทปิด Pop-up
    public GameObject plantTab;           // หน้า PlantTab (ว่างไว้ได้ จะหาลูกชื่อ "PlantTab" ให้เอง)

    private bool plantTabWasOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    // เปิด Popup อย่างเดียว การโหลดข้อมูลจะเกิดใน Update ตอนที่หน้า PlantTab ถูกเปิด
    public void OpenPlantDex()
    {
        gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        // ปิด Popup แล้ว เปิดใหม่ครั้งหน้าจะได้โหลดใหม่
        plantTabWasOpen = false;
    }

    // เช็คทุกเฟรมว่า PlantTab เพิ่งถูกเปิดหรือเปล่า (เปิดจากปุ่ม Plant Dex หรือกดสลับแท็บมา) ถ้าใช่ให้ดึงข้อมูลใหม่
    private void Update()
    {
        if (plantTab == null)
        {
            Transform t = transform.Find("PlantTab");
            if (t == null) return;
            plantTab = t.gameObject;
        }

        bool isOpen = plantTab.activeInHierarchy;
        if (isOpen && !plantTabWasOpen) LoadPlantDexData();
        plantTabWasOpen = isOpen;
    }

    public void RefreshPlantDex()
    {
        LoadPlantDexData();
    }

    private int loadVersion = 0; // กันโหลดซ้อนกัน 2 รอบแล้วการ์ดเบิ้ล

    /// <summary>
    /// ฟังก์ชันสั่งปิด Pop-up
    /// </summary>
    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }

    private void LoadPlantDexData()
    {
        if (SupabaseManager.Instance == null || gridContainer == null || plantCardPrefab == null) return;

        int myVersion = ++loadVersion;

        // 2. ดึง Master Data และ User Collection
        StartCoroutine(SupabaseManager.Instance.GetEvolutionStageMasterData(
            onSuccess: (masterStages) =>
            {
                // 🟢 จัดลำดับสายพันธุ์บังคับ: TM -> CC -> ML -> CH
                if (masterStages != null)
                {
                    masterStages.Sort((a, b) =>
                    {
                        int priorityA = GetSpeciesPriority(a.stage_id);
                        int priorityB = GetSpeciesPriority(b.stage_id);

                        // ถ้าสายพันธุ์ต่างกัน ให้เรียงตามลำดับความสำคัญ (TM -> CC -> ML -> CH)
                        if (priorityA != priorityB)
                        {
                            return priorityA.CompareTo(priorityB);
                        }

                        // ถ้าเป็นสายพันธุ์เดียวกัน ให้เรียงตาม stage_number (หรือ stage_id)
                        if (a.stage_number != b.stage_number)
                        {
                            return a.stage_number.CompareTo(b.stage_number);
                        }

                        return string.Compare(a.stage_id, b.stage_id);
                    });
                }

                string activeUserId = SupabaseManager.Instance.GetActiveUserId();
                StartCoroutine(SupabaseManager.Instance.GetUserCollection(activeUserId, (unlockedList) =>
                {
                    // ถ้ามีการโหลดรอบใหม่เริ่มไปแล้ว ทิ้งผลรอบนี้
                    if (myVersion != loadVersion) return;

                    // 1. เคลียร์ Card เก่าออกก่อนสร้างใหม่ (ทำตอนข้อมูลมาแล้ว จอจะได้ไม่ว่างระหว่างโหลด)
                    foreach (Transform child in gridContainer)
                    {
                        Destroy(child.gameObject);
                    }

                    foreach (var stage in masterStages)
                    {
                        GameObject cardObj = Instantiate(plantCardPrefab, gridContainer);
                        PlantDexCard card = cardObj.GetComponent<PlantDexCard>();

                        if (card != null)
                        {
                            bool isUnlocked = unlockedList.Contains(stage.stage_id);

                            string spritePath = $"Plants/{stage.stage_id.Trim()}";
                            Sprite stageSprite = Resources.Load<Sprite>(spritePath);

                            card.SetupCard(stage.stage_name, stageSprite, isUnlocked);
                        }
                    }
                }));
            }
        ));
    }

    /// <summary>
    /// 🟢 ฟังก์ชันกำหนดความสำคัญของสายพันธุ์ (1: TM, 2: CC, 3: ML, 4: CH)
    /// </summary>
    private int GetSpeciesPriority(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return 999;

        string idUpper = stageId.ToUpper();

        if (idUpper.Contains("_TM_") || idUpper.EndsWith("_TM")) return 1;
        if (idUpper.Contains("_CC_") || idUpper.EndsWith("_CC")) return 2;
        if (idUpper.Contains("_ML_") || idUpper.EndsWith("_ML")) return 3;
        if (idUpper.Contains("_CH_") || idUpper.EndsWith("_CH")) return 4;

        return 99; // สำหรับสายพันธุ์อื่นๆ เพิ่มเติมในอนาคต
    }
}