using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ItemData
{
    public string itemId;         
    public string itemName;        
    public Sprite itemIcon;         
}

public class ItemSpawner : MonoBehaviour
{
    public static ItemSpawner Instance { get; private set; }

    [Header("Spawn Points")]
    public List<Image> spawnPointImages; 

    [Header("Rarity Chance Settings")]
    public float silverChance = 70f;  
    public float goldChance = 25f;    
    public float rainbowChance = 5f;   

    [Header("Item Pool")]
    public List<ItemData> silverItems = new List<ItemData>();
    public List<ItemData> goldItems = new List<ItemData>();
    public List<ItemData> rainbowItems = new List<ItemData>();

    // ตัวแปรเก็บ ItemData ปัจจุบันที่สุ่มได้จริง
    public ItemData currentActiveItem;
    public RectTransform activeSpawnPointRect;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        RandomizeItem();
    }

    public void RandomizeItem()
    {
        if (spawnPointImages == null || spawnPointImages.Count == 0) return;

        foreach (var spawnImg in spawnPointImages)
        {
            if (spawnImg != null) spawnImg.gameObject.SetActive(false);
        }

        // สุ่มไอเทมจาก Pool
        currentActiveItem = PickRandomItemByRarity();

        if (currentActiveItem == null)
        {
            currentActiveItem = GetFallbackItem();
        }

        // สุ่มจุดสปาวน์
        int randomPointIndex = Random.Range(0, spawnPointImages.Count);
        Image selectedSpawn = spawnPointImages[randomPointIndex];

        if (selectedSpawn != null && currentActiveItem != null && currentActiveItem.itemIcon != null)
        {
            selectedSpawn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            selectedSpawn.sprite = currentActiveItem.itemIcon;
            selectedSpawn.gameObject.SetActive(true);
            activeSpawnPointRect = selectedSpawn.rectTransform;
        }
    }

    private ItemData PickRandomItemByRarity()
    {
        float totalChance = silverChance + goldChance + rainbowChance;
        float roll = Random.Range(0f, totalChance);

        if (roll < silverChance) return GetRandomFromList(silverItems);
        if (roll < silverChance + goldChance) return GetRandomFromList(goldItems);
        return GetRandomFromList(rainbowItems);
    }

    private ItemData GetRandomFromList(List<ItemData> itemList)
    {
        if (itemList == null || itemList.Count == 0) return null;
        return itemList[Random.Range(0, itemList.Count)];
    }

    private ItemData GetFallbackItem()
    {
        if (silverItems.Count > 0) return silverItems[0];
        if (goldItems.Count > 0) return goldItems[0];
        if (rainbowItems.Count > 0) return rainbowItems[0];
        return null;
    }
}