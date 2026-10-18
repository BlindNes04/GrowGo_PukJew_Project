using System;

// 📦 1. คลาสสำหรับรับค่า metadata JSONB จาก Supabase
[Serializable]
public class ItemMetadata
{
    public string effect;          // เช่น "reduce_watering_time"
    public string multiplier;      // เช่น "0.3", "0.5"
    public string category;        // เช่น "cat_all", "cat_heal", "cat_evo", "cat_growth"
    public string effect_type;     // เช่น "all_rounder", "cure_protect", "boost_evo", "speed_growth"
    public string effect_value;    // ใช้ string เพื่อรองรับทั้ง "2" และ 0.15 ใน DB

    // 💡 ฟังก์ชันแปลงค่าเป็น float อัตโนมัติ
    public float GetEffectValue()
    {
        if (float.TryParse(effect_value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            return val;
        }
        return 0f;
    }
}

// 📦 2. คลาสข้อมูลไอเทมหลัก (ตาราง items)
[Serializable]
public class SupabaseItemMaster
{
    public string item_id;
    public string item_name;
    public string item_type;
    public string rarity_tier;
    public string description;
    public ItemMetadata metadata;  // 👈 เปลี่ยนเป็น ItemMetadata เพื่อให้ Unity อ่าน JSON Object ได้ทันที
}

// 📦 3. คลาสข้อมูลไอเทมในกระเป๋าผู้เล่น (ตาราง inventory)
[Serializable]
public class SupabaseInventoryItem
{
    public string user_id;
    public string item_id;
    public int quantity;
    public SupabaseItemMaster item;
}

// 📦 4. Helper สำหรับแปลง JSON Array
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string newJson = "{\"Items\":" + json + "}";
        Wrapper<T> wrapper = UnityEngine.JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.Items;
    }

    [Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }
}