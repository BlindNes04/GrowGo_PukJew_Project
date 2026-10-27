using System;
using Newtonsoft.Json;

// ==================== Inventory Item Data Model ====================
[Serializable]
public class InventoryItemData
{
    [JsonProperty("user_id")]
    public string userId;

    [JsonProperty("item_id")]
    public string itemId;

    [JsonProperty("quantity")]
    public int quantity;
}