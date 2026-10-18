using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SwitchPlantPopup : MonoBehaviour
{
    [System.Serializable]
    public class SpeciesIconData
    {
        [Tooltip("ใส่คีย์ เช่น TM, CC, SPEC_TOMATO, SPEC_COCONUT")]
        public string speciesKey; 
        public Sprite iconSprite; 
    }

    [Header("UI References")]
    public Transform gridContainer;       
    public GameObject plantCardPrefab;    
    public Button closeButton;            

    [Header("🎨 รายการรูปภาพไอคอนพืช")]
    public List<SpeciesIconData> speciesIcons = new List<SpeciesIconData>();

    [Header("Plant Status Reference")]
    public PlantStatus plantStatus;       

    private List<GameObject> spawnedCards = new List<GameObject>();

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePopup);

        if (plantStatus == null)
            plantStatus = FindFirstObjectByType<PlantStatus>();
    }

    // 💡 เมื่อเปิด Pop-up (Active) ให้ทำงานยิงดึงข้อมูลแค่จุดนี้จุดเดียว
    private void OnEnable()
    {
        RefreshPlantGrid();
    }

    // 🚀 สั่งเปิดหน้าต่างสลับพืช (ให้ OnEnable ทำงานดึงข้อมูลเอง ไม่ต้องสั่งซ้ำ)
    public void OpenPopup()
    {
        gameObject.SetActive(true);
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }

    public void RefreshPlantGrid()
    {
        // 💡 หยุด Coroutine เก่าทั้งหมดป้องกันการยิง API ซ้ำซ้อน
        StopAllCoroutines();

        // 1. เคลียร์การ์ดเก่าออกให้หมดก่อน
        foreach (var card in spawnedCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedCards.Clear();

        // 2. ดึงข้อมูลพืชจาก Supabase
        if (SupabaseManager.Instance != null)
        {
            StartCoroutine(SupabaseManager.Instance.GetPlayerPlantData(
                onSuccess: (json) =>
                {
                    string wrappedJson = "{\"items\":" + json + "}";
                    SupabasePlayerPlantList list = JsonUtility.FromJson<SupabasePlayerPlantList>(wrappedJson);

                    if (list != null && list.items != null)
                    {
                        foreach (var plantData in list.items)
                        {
                            CreatePlantCard(plantData);
                        }

                        // บังคับให้ Grid Layout Group จัดตำแหน่งให้สวยงาม
                        StartCoroutine(RebuildLayoutRoutine());
                    }
                },
                onError: (err) => Debug.LogError($"[SwitchPlant] 🔴 ดึงรายการพืชไม่สำเร็จ: {err}")
            ));
        }
    }

    private void CreatePlantCard(SupabasePlayerPlant plantData)
    {
        if (plantCardPrefab == null || gridContainer == null) return;

        GameObject newCard = Instantiate(plantCardPrefab, gridContainer, false);
        newCard.transform.localScale = Vector3.one;
        spawnedCards.Add(newCard);

        // เปลี่ยนรูปไอคอนในช่อง Seed
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

        // ตั้งค่าปุ่มกดเลือกพืช
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

            if (upperStage.Contains(key) || 
               (key.Contains("TOMATO") && upperStage.Contains("TM")) ||
               (key.Contains("COCONUT") && upperStage.Contains("CC")))
            {
                return data.iconSprite;
            }
        }

        return null;
    }

    private void SelectPlant(string selectedPlayerPlantId)
    {
        if (plantStatus != null)
        {
            Debug.Log($"[SwitchPlant] 🔄 สลับไปเล่นพืช ID: {selectedPlayerPlantId}");
            plantStatus.playerPlantId = selectedPlayerPlantId.Trim();
            plantStatus.StartCoroutine("LoadAllDataFromSupabaseRoutine");
        }

        ClosePopup();
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