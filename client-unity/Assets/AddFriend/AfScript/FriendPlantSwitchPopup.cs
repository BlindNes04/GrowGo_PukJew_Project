using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ==================== FriendPlantSwitchPopup ====================
// โครงเดียวกับ SwitchPlantPopup (หน้าหลัก) แต่ใช้กับหน้าเยี่ยมสวนเพื่อน
// ต่างจาก SwitchPlantPopup 3 จุด (ชื่อตัวแปรใน Inspector เหมือนกัน ยกเว้น plantStatus -> friendVisit):
//   1) รายการต้นไม้มาจาก FriendVisitController.friendPlants (ต้นของเพื่อน) ไม่ได้ดึงต้นของเรา
//   2) กดเลือกการ์ด = แค่เปลี่ยนต้นที่ "ดู" (FriendVisitController.ShowPlant) ไม่บันทึกอะไรลง DB
//   3) ตัวกรอง "ยังไม่ได้รดน้ำ" คิดจากเวลาที่ใหม่กว่าระหว่างเจ้าของรด กับเพื่อนรด
public class FriendPlantSwitchPopup : MonoBehaviour
{
    [System.Serializable]
    public class SpeciesIconData
    {
        [Tooltip("ใส่คีย์ ให้ตรงกับ stageId เช่น STAGE_CC_01, STAGE_TM_01, STAGE_ML_01, STAGE_CH_01")]
        public string speciesKey; 
        public Sprite iconSprite; 
    }

    [Header("UI References")]
    public Transform gridContainer;       
    public GameObject plantCardPrefab;    
    public Button closeButton;            

    [Header("🎨 รายการรูปภาพไอคอนพืช")]
    public List<SpeciesIconData> speciesIcons = new List<SpeciesIconData>();

    [Header("Friend Visit Reference (ถ้าไม่ลากใส่ จะหาเองตอนเริ่ม)")]
    public FriendVisitController friendVisit;

    private List<GameObject> spawnedCards = new List<GameObject>();

    // ==================== 🔍 ค้นหา / กรองพืช ====================
    [System.Serializable]
    public class SpeciesFilterToggle
    {
        [Tooltip("species_id ใน DB เช่น SPEC_TOMATO, SPEC_COCONUT, SPEC_MELON, SPEC_CHERRY")]
        public string speciesId;
        public Toggle toggle;
    }

    [Header("🔍 ค้นหา")]
    public TMP_InputField searchInput;    // ช่อง Search
    public Button searchButton;           // ปุ่มแว่นขยาย (Edit)

    [Header("🔽 กรอง (Toggle = ช่องติ๊ก)")]
    public Button filterButton;           // ปุ่มกรวย เปิด/ปิด FilterPopup
    public GameObject filterPanel;        // FilterPopup (แผงที่มี Toggle ทั้งหมด)
    public Button filterConfirmButton;    // ปุ่ม "ยืนยัน" = ใช้ตัวกรองที่ติ๊ก แล้วปิดแผง
    public Button filterClearButton;      // ปุ่ม "ล้างค่า" = เอาติ๊กออกหมด โชว์พืชทั้งหมด
    public Button filterCloseButton;      // ปุ่ม X = ปิดแผงเฉยๆ ไม่เปลี่ยนตัวกรอง
    public List<SpeciesFilterToggle> speciesToggles = new List<SpeciesFilterToggle>();
    public Toggle fullyGrownToggle;       // โตเต็มที่
    public Toggle notFullyGrownToggle;    // ยังไม่โตเต็มที่
    public Toggle sickToggle;             // ป่วย
    public Toggle notSickToggle;          // ไม่ป่วย
    public Toggle needsWaterToggle;       // ยังไม่ได้รดน้ำ (ถึงเวลารดน้ำแล้ว หรือยังไม่เคยรด)

    // เก็บข้อมูลพืชคู่กับการ์ด ไว้ซ่อน/โชว์ตอนค้นหา โดยไม่ต้องโหลด DB ใหม่
    private class PlantCardEntry
    {
        public SupabasePlayerPlant data;
        public GameObject card;
        public string speciesId;
        public string speciesName;
    }
    private List<PlantCardEntry> cardEntries = new List<PlantCardEntry>();

    // ตัวกรองที่ "กดยืนยันแล้ว" (ติ๊กเฉยๆ ยังไม่กรอง ต้องกดยืนยันก่อน)
    private List<string> appliedSpecies = new List<string>();
    private bool appliedGrown, appliedNotGrown, appliedSick, appliedNotSick, appliedNeedsWater;

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePopup);

        if (friendVisit == null)
            friendVisit = FindAnyObjectByType<FriendVisitController>();

        // 🔍 ผูกช่องค้นหา: พิมพ์ทีละตัวก็กรองทันที
        if (searchInput != null) searchInput.onValueChanged.AddListener(_ => ApplySearchAndFilter());
        if (searchButton != null) searchButton.onClick.AddListener(ApplySearchAndFilter);

        // 🔽 ผูกปุ่มของแผงกรอง (ติ๊กแล้วยังไม่กรอง จะกรองตอนกด "ยืนยัน")
        if (filterButton != null) filterButton.onClick.AddListener(ToggleFilterPanel);
        if (filterConfirmButton != null) filterConfirmButton.onClick.AddListener(ConfirmFilter);
        if (filterClearButton != null) filterClearButton.onClick.AddListener(ClearFilter);
        if (filterCloseButton != null) filterCloseButton.onClick.AddListener(CloseFilterPanel);
        if (filterPanel != null) filterPanel.SetActive(false);
    }

    private void OnEnable()
    {
        // ==================== คืนค่าให้กดได้ทุกครั้งที่เปิด ====================
        // ปุ่ม X ผูกทั้ง UniversalPopup.Close (ปิดการกดแล้วค่อยๆ เฟด) และ ClosePopup (SetActive(false) ทันที)
        // เฟดเลยหยุดกลางทาง CanvasGroup ค้างสถานะ "กดไม่ได้"
        // จึงตั้ง CanvasGroup กลับเป็นโชว์ + กดได้ ทุกครั้งที่ป๊อปอัปถูกเปิด
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (friendVisit == null)
            friendVisit = FindAnyObjectByType<FriendVisitController>();
        RefreshPlantGrid();
    }

    public void OpenPopup()
    {
        gameObject.SetActive(true);
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
        CloseFilterPanel(); // 🔽 FilterPopup อยู่นอก popup นี้ ต้องสั่งปิดตามด้วย
    }

    // 🟢 สร้างการ์ดจากต้นไม้ของเพื่อน (ทุกครั้งที่เปิดป๊อปอัป)
    public void RefreshPlantGrid()
    {
        StopAllCoroutines();

        foreach (var card in spawnedCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedCards.Clear();
        cardEntries.Clear();

        // 🤝 ใช้ต้นไม้ของเพื่อนที่ FriendVisitController โหลดไว้แล้ว (ไม่ต้องยิง DB ซ้ำ)
        if (friendVisit != null)
        {
            foreach (var plantData in friendVisit.friendPlants)
            {
                CreatePlantCard(plantData);
            }

            // 🔍 สร้างการ์ดครบแล้ว กรองตามคำค้น/ตัวกรองที่ตั้งไว้ (ในนี้สั่ง rebuild layout ให้แล้ว)
            ApplySearchAndFilter();
        }
    }

    private void CreatePlantCard(SupabasePlayerPlant plantData)
    {
        if (plantCardPrefab == null || gridContainer == null) return;

        GameObject newCard = Instantiate(plantCardPrefab, gridContainer, false);
        newCard.transform.localScale = Vector3.one;
        spawnedCards.Add(newCard);

        Image iconImage = GetPlantIconImage(newCard);
        if (iconImage != null)
        {
            Sprite matchedSprite = GetIconForPlant(plantData.stage_id);
            if (matchedSprite != null)
            {
                iconImage.sprite = matchedSprite;
                iconImage.gameObject.SetActive(true);
            }
        }

        // 🏷️ ใส่ชื่อบนการ์ด (ลูกชื่อ "Name" ใน prefab PlantSeedSlot)
        //    ใช้ชื่อเล่นต้นก่อน ถ้าไม่มีชื่อเล่นใช้ชื่อสายพันธุ์แทน
        string speciesId = GetSpeciesIdOfStage(plantData.stage_id);
        string speciesName = GetSpeciesName(speciesId);

        Transform nameTransform = newCard.transform.Find("Name");
        TMP_Text nameText = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
        if (nameText != null)
            nameText.text = !string.IsNullOrEmpty(plantData.plant_nickname) ? plantData.plant_nickname : speciesName;

        // 🔍 เก็บไว้ใช้ตอนค้นหา/กรอง
        cardEntries.Add(new PlantCardEntry
        {
            data = plantData,
            card = newCard,
            speciesId = speciesId,
            speciesName = speciesName
        });

        Button cardButton = newCard.GetComponent<Button>();
        if (cardButton == null) cardButton = newCard.GetComponentInChildren<Button>();

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() =>
            {
                SelectPlant(plantData.player_plant_id);
            });
        }
    }

    private Image GetPlantIconImage(GameObject cardObj)
    {
        Transform seedTransform = cardObj.transform.Find("Seed");
        if (seedTransform != null)
        {
            Image seedImg = seedTransform.GetComponent<Image>();
            if (seedImg != null) return seedImg;
        }

        Image[] images = cardObj.GetComponentsInChildren<Image>();
        foreach (var img in images)
        {
            if (img.gameObject != cardObj && img.gameObject.name != "BG")
            {
                return img;
            }
        }

        return null;
    }

    private Sprite GetIconForPlant(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return null;

        string upperStage = stageId.ToUpper();

        foreach (var data in speciesIcons)
        {
            if (string.IsNullOrEmpty(data.speciesKey)) continue;

            string key = data.speciesKey.ToUpper();

            if (upperStage == key || upperStage.Contains(key)) return data.iconSprite;
            if ((key.Contains("TOMATO") || key.Contains("TM")) && upperStage.Contains("TM")) return data.iconSprite;
            if ((key.Contains("COCONUT") || key.Contains("CC")) && upperStage.Contains("CC")) return data.iconSprite;
            if ((key.Contains("MELON") || key.Contains("ML")) && upperStage.Contains("ML")) return data.iconSprite;
            if ((key.Contains("CHERRY") || key.Contains("CH")) && upperStage.Contains("CH")) return data.iconSprite;
        }

        return null;
    }

    // 🤝 เลือกการ์ด = เปลี่ยนต้นที่ดูอยู่ในหน้าเยี่ยมเพื่อน (ดูอย่างเดียว ไม่บันทึกลง DB)
    private void SelectPlant(string selectedPlayerPlantId)
    {
        if (friendVisit != null)
        {
            Debug.Log($"[FriendPlantSwitch] 👀 ดูต้นไม้ของเพื่อน ID: {selectedPlayerPlantId}");
            friendVisit.ShowPlant(selectedPlayerPlantId.Trim());
        }

        ClosePopup();
    }

    // ==================== 🔍 ค้นหา / กรองพืช ====================

    // หา species_id / ชื่อสายพันธุ์ จากตารางที่ FriendVisitController โหลดไว้
    private string GetSpeciesIdOfStage(string stageId)
    {
        return friendVisit != null ? friendVisit.GetSpeciesIdOfStage(stageId) : "";
    }

    private string GetSpeciesName(string speciesId)
    {
        return friendVisit != null ? friendVisit.GetSpeciesName(speciesId) : "";
    }

    // 💧 ต้นนี้หิวน้ำไหม (ใช้สูตรเดียวกับปุ่มรดน้ำในหน้าเยี่ยมเพื่อน)
    private bool NeedsWater(PlantCardEntry entry)
    {
        return friendVisit != null && friendVisit.GetSecondsUntilThirsty(entry.data) <= 0;
    }

    public void ToggleFilterPanel()
    {
        if (filterPanel != null) filterPanel.SetActive(!filterPanel.activeSelf);
    }

    public void CloseFilterPanel()
    {
        if (filterPanel != null) filterPanel.SetActive(false);
    }

    // ปุ่ม "ยืนยัน": จำค่าที่ติ๊กไว้ -> กรองการ์ด -> ปิดแผง
    public void ConfirmFilter()
    {
        appliedSpecies.Clear();
        foreach (var sf in speciesToggles)
            if (sf.toggle != null && sf.toggle.isOn) appliedSpecies.Add(sf.speciesId);

        appliedGrown = fullyGrownToggle != null && fullyGrownToggle.isOn;
        appliedNotGrown = notFullyGrownToggle != null && notFullyGrownToggle.isOn;
        appliedSick = sickToggle != null && sickToggle.isOn;
        appliedNotSick = notSickToggle != null && notSickToggle.isOn;
        appliedNeedsWater = needsWaterToggle != null && needsWaterToggle.isOn;

        ApplySearchAndFilter();
        CloseFilterPanel();
    }

    // ปุ่ม "ล้างค่า": เอาติ๊กออกทุกช่อง แล้วโชว์พืชทั้งหมดทันที (แผงยังเปิดอยู่)
    public void ClearFilter()
    {
        foreach (var sf in speciesToggles)
            if (sf.toggle != null) sf.toggle.isOn = false;
        foreach (Toggle t in new[] { fullyGrownToggle, notFullyGrownToggle, sickToggle, notSickToggle, needsWaterToggle })
            if (t != null) t.isOn = false;

        appliedSpecies.Clear();
        appliedGrown = appliedNotGrown = appliedSick = appliedNotSick = appliedNeedsWater = false;
        ApplySearchAndFilter();
    }

    // ซ่อน/โชว์การ์ดตามคำค้น + ตัวกรอง
    // กติกา: ในกลุ่มเดียวกันติ๊กหลายอัน = เอาอันไหนก็ได้ (OR), ข้ามกลุ่ม = ต้องผ่านทุกกลุ่ม (AND)
    //        กลุ่มไหนไม่ติ๊กเลย = ไม่กรองกลุ่มนั้น
    public void ApplySearchAndFilter()
    {
        // คำค้น: ตัดช่องว่าง + ไม่สนตัวพิมพ์เล็ก/ใหญ่ พิมพ์ไม่ครบก็เจอ (ใช้ Contains)
        string query = searchInput != null ? searchInput.text.Trim().ToLower() : "";

        // ใช้ตัวกรองที่กดยืนยันแล้ว (จาก ConfirmFilter)
        List<string> selectedSpecies = appliedSpecies;
        bool wantGrown = appliedGrown;
        bool wantNotGrown = appliedNotGrown;
        bool wantSick = appliedSick;
        bool wantNotSick = appliedNotSick;

        foreach (PlantCardEntry entry in cardEntries)
        {
            if (entry.card == null) continue;
            bool show = true;

            // 🔧 ถ้าตอนสร้างการ์ด PlantStatus ยังโหลดตาราง evolution_stage ไม่เสร็จ ชนิดพืชจะว่าง
            //    เลยหาชนิด/ชื่อสายพันธุ์ใหม่อีกรอบตอนกรอง (ตอนนี้ข้อมูลโหลดเสร็จแล้ว)
            if (string.IsNullOrEmpty(entry.speciesId))
            {
                entry.speciesId = GetSpeciesIdOfStage(entry.data.stage_id);
                entry.speciesName = GetSpeciesName(entry.speciesId);
            }

            // 1. ค้นหา: ชื่อเล่น หรือ ชื่อสายพันธุ์
            if (query.Length > 0)
            {
                string nickname = (entry.data.plant_nickname ?? "").ToLower();
                string speciesName = (entry.speciesName ?? "").ToLower();
                show = nickname.Contains(query) || speciesName.Contains(query);
            }

            // 2. กรองชนิดพืช
            if (show && selectedSpecies.Count > 0)
                show = selectedSpecies.Contains(entry.speciesId);

            // 3. กรองโตเต็มที่ / ยังไม่โต
            if (show && (wantGrown || wantNotGrown))
                show = (wantGrown && entry.data.is_fully_grown) || (wantNotGrown && !entry.data.is_fully_grown);

            // 4. กรองป่วย / ไม่ป่วย
            if (show && (wantSick || wantNotSick))
                show = (wantSick && entry.data.is_sick) || (wantNotSick && !entry.data.is_sick);

            // 5. กรองยังไม่ได้รดน้ำ
            if (show && appliedNeedsWater)
                show = NeedsWater(entry);

            entry.card.SetActive(show);
        }

        if (gameObject.activeInHierarchy) StartCoroutine(RebuildLayoutRoutine());
    }

    private IEnumerator RebuildLayoutRoutine()
    {
        yield return new WaitForEndOfFrame();
        if (gridContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridContainer.GetComponent<RectTransform>());
        }
    }
}