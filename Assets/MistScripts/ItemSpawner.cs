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

    public ItemData currentActiveItem;
    public RectTransform activeSpawnPointRect;

    private ItemData previousItem;
    private Sprite previousItemIcon;
    private int previousSpawnPointIndex = -1;

    void Awake()
    {
        Instance = this;
    }

    public void RandomizeItem()
    {
        if (spawnPointImages == null || spawnPointImages.Count == 0)
        {
            Debug.LogWarning("ItemSpawner: No spawn points assigned.");
            return;
        }

        // ซ่อนตำแหน่งทั้งหมด
        foreach (Image spawnImg in spawnPointImages)
        {
            if (spawnImg != null)
                spawnImg.gameObject.SetActive(false);
        }

        // สุ่มเลือกไอเทม
        ItemData newItem = PickRandomItemByRarity();

        if (newItem == null)
        {
            Debug.LogWarning("ItemSpawner: No valid items available.");
            activeSpawnPointRect = null;
            return;
        }

        // สุ่มเลือกตำแหน่ง
        List<int> availablePoints = new List<int>();

        for (int i = 0; i < spawnPointImages.Count; i++)
        {
            if (spawnPointImages[i] != null)
                availablePoints.Add(i);
        }

        if (availablePoints.Count == 0)
        {
            activeSpawnPointRect = null;
            return;
        }

        if (availablePoints.Count > 1 &&
            availablePoints.Contains(previousSpawnPointIndex))
        {
            availablePoints.Remove(previousSpawnPointIndex);
        }

        int randomPointIndex = availablePoints[
            UnityEngine.Random.Range(0, availablePoints.Count)
        ];

        Image selectedSpawn = spawnPointImages[randomPointIndex];

        previousItem = newItem;
        previousItemIcon = newItem.itemIcon;
        currentActiveItem = newItem;
        previousSpawnPointIndex = randomPointIndex;

        selectedSpawn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        selectedSpawn.sprite = currentActiveItem.itemIcon;
        selectedSpawn.gameObject.SetActive(true);

        activeSpawnPointRect = selectedSpawn.rectTransform;

        Debug.Log(
            "New item: " + currentActiveItem.itemName +
            " | Spawn point: " + randomPointIndex
        );
    }

    private ItemData PickRandomItemByRarity()
    {
        List<ItemData> allItems = new List<ItemData>();
        AddValidItems(silverItems, allItems);
        AddValidItems(goldItems, allItems);
        AddValidItems(rainbowItems, allItems);

        if (allItems.Count == 0)
            return null;

        List<ItemData> differentItems = allItems.FindAll(item =>
            !IsSameItem(item, previousItem) &&
            (previousItemIcon == null || item.itemIcon != previousItemIcon));

        List<ItemData> eligibleItems = differentItems.Count > 0 
            ? differentItems 
            : allItems;

        float totalChance = Mathf.Max(0f, silverChance) +
                            Mathf.Max(0f, goldChance) +
                            Mathf.Max(0f, rainbowChance);

        if (totalChance > 0f)
        {
            float roll = Random.Range(0f, totalChance);
            List<ItemData> preferredPool;

            if (roll < Mathf.Max(0f, silverChance))
                preferredPool = silverItems;
            else if (roll < Mathf.Max(0f, silverChance) + Mathf.Max(0f, goldChance))
                preferredPool = goldItems;
            else
                preferredPool = rainbowItems;

            List<ItemData> preferredEligible = new List<ItemData>();
            if (preferredPool != null)
            {
                foreach (ItemData item in preferredPool)
                {
                    if (item == null || item.itemIcon == null)
                        continue;

                    if (eligibleItems.Contains(item))
                        preferredEligible.Add(item);
                }
            }

            if (preferredEligible.Count > 0)
                return preferredEligible[Random.Range(0, preferredEligible.Count)];
        }

        return eligibleItems[Random.Range(0, eligibleItems.Count)];
    }

    private void AddValidItems(List<ItemData> source, List<ItemData> destination)
    {
        if (source == null)
            return;

        foreach (ItemData item in source)
        {
            if (item != null && item.itemIcon != null && !destination.Contains(item))
                destination.Add(item);
        }
    }

    private bool IsSameItem(ItemData a, ItemData b)
    {
        if (a == null || b == null)
            return false;

        if (!string.IsNullOrEmpty(a.itemId) &&
            !string.IsNullOrEmpty(b.itemId))
        {
            return a.itemId == b.itemId;
        }

        return a == b;
    }
}
