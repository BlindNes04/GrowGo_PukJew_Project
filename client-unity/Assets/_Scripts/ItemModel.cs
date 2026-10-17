using System;

[System.Serializable]
public class SupabaseInventoryItem
{
    public string user_id;
    public string item_id;
    public int quantity;
    public SupabaseItemMaster item;
}

[System.Serializable]
public class SupabaseItemMaster
{
    public string item_id;
    public string item_name;
    public string item_type;
    public string rarity_tier;
    public string description;
}

// 💡 Helper สำหรับแปลง JSON Array จาก UnityWebRequest
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string newJson = "{\"Items\":" + json + "}";
        Wrapper<T> wrapper = UnityEngine.JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.Items;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }
}