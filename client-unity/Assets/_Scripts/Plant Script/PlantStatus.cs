using System;
using UnityEngine;

// 🧪 โครงสร้างข้อมูลปุ๋ย
[System.Serializable]
public class FertilizerData
{
    public string itemId;          // ID ปุ๋ย
    public string itemName;        // ชื่อปุ๋ย
    public float nutrientAmount = 25f; // แต้มสารอาหาร (+25)
    public float moistureAmount = 0f;  // แต้มน้ำเสริม (ถ้ามี)

    [Header("แต้มรางวัลเฉพาะปุ๋ยนี้ (ถ้าใส่ 0 จะใช้ค่าพื้นฐาน)")]
    public int customEXP = 0;
    public int customCoins = 0;
    public int customRelationship = 0;

    [Header("บัฟพิเศษจากปุ๋ย")]
    public string buffType = "None";   // เช่น "DoubleEXP", "FastGrowth"
    public float buffDurationMinutes = 0f;
}

public class PlantStatus : MonoBehaviour
{
    [Header("ข้อมูลพืช")]
    public string plantName = "มะเขือเทศสุดเท่";

    [Header("ข้อมูลการพัฒนาร่าง (1: Baby, 2: Teenage, 3: Adult, 4: Final Evolution)")]
    public int currentStage = 1;             // ร่างปัจจุบัน
    public string evoForm = "None";          // "None", "Angel", "Devil"
    public int evoRelationshipThreshold = 50; // แต้มหัวใจสะสมขั้นต่ำที่ต้องใช้เปลี่ยนร่างเป็น Angel

    [Header("ค่าสเตตัสหลอด (เป้าหมายเปลี่ยนร่าง)")]
    public float currentMoisture = 0f;  // ค่าความชุ่มชื้นปัจจุบัน
    public float maxMoisture = 100f;    // ค่าความชุ่มชื้นเต็มหลอด

    public float currentNutrients = 0f; // สารอาหารปัจจุบัน
    public float maxNutrients = 100f;   // สารอาหารเต็มหลอด

    [Header("แต้มสะสมพิเศษ")]
    public int relationshipPoints = 0;  // แต้มความสัมพันธ์ (หัวใจ)
    public int flowerCoins = 0;          // ดอกไม้ (เหรียญ)
    public int playerEXP = 0;            // EXP ของผู้เล่น

    [Header("⏱️ ตั้งค่า Cooldown รดน้ำ")]
    public float cooldownWaterMinutes = 30f;
    public bool testModeInSeconds = false;

    [Header("💧 ค่าปกติการรดน้ำ (Base Values)")]
    public float baseMoistureFromWater = 10f;
    public float baseNutrientsFromWater = 2f;
    public int baseExpFromWater = 15;
    public int baseCoinsFromWater = 10;
    public int baseRelationshipFromWater = 5;

    [Header("🧪 ค่าปกติการใส่ปุ๋ย (Base Values)")]
    public float baseNutrientsFromFertilizer = 25f;
    public int baseExpFromFertilizer = 25;
    public int baseCoinsFromFertilizer = 15;
    public int baseRelationshipFromFertilizer = 10;

    [Header("🎉 ตัวคูณอีเวนต์ / บัฟพิเศษ (Multipliers)")]
    public float expMultiplier = 1.0f;
    public float coinMultiplier = 1.0f;
    public float relationshipMultiplier = 1.0f;

    [Header("สถานะพืช")]
    public bool isSick = false;

    [HideInInspector]
    public string lastWateredTime = "";

    [Header("บัฟที่กำลังทำงานอยู่")]
    public string currentActiveBuff = "None";
    public float buffRemainingSeconds = 0f;

    void Awake()
    {
        LoadData();
    }

    void Start()
    {
        SyncCharacterVisual(); // โหลดภาพร่างปัจจุบันขึ้นมาแสดงบนแท่นหิน
    }

    void Update()
    {
        if (buffRemainingSeconds > 0)
        {
            buffRemainingSeconds -= Time.deltaTime;
            if (buffRemainingSeconds <= 0)
            {
                OnBuffExpired();
            }
        }
    }

    private void OnBuffExpired()
    {
        if (currentActiveBuff == "DoubleEXP")
        {
            expMultiplier = 1.0f;
        }

        currentActiveBuff = "None";
        Debug.Log($"[Buff] บัฟของ {plantName} หมดเวลาแล้ว!");
    }

    private float GetTotalCooldownInSeconds()
    {
        return testModeInSeconds ? cooldownWaterMinutes : cooldownWaterMinutes * 60f;
    }

    // ==========================================
    // 🎨 สั่งสลับรูปภาพและขนาดพืชบนจอ (Sync กับ MCController)
    // ==========================================
    public void SyncCharacterVisual()
    {
        MCController mc = GetComponent<MCController>();
        if (mc == null) return;

        int targetIndex = 0;

        if (currentStage == 1)
        {
            targetIndex = 0; // Index 0: baby
        }
        else if (currentStage == 2)
        {
            targetIndex = 1; // Index 1: teenage
        }
        else if (currentStage == 3)
        {
            targetIndex = 2; // Index 2: adult
        }
        else if (currentStage >= 4)
        {
            if (evoForm == "Angel")
            {
                targetIndex = 3; // Index 3: evo angel (สายเทพ)
            }
            else
            {
                targetIndex = 4; // Index 4: evo devil (สายปีศาจ)
            }
        }

        mc.SetStageIndex(targetIndex);
    }

    // ==========================================
    // 🌱 ระบบเปลี่ยนร่างเมื่อหลอดเต็ม
    // ==========================================
    public void CheckEvolution()
    {
        if (currentMoisture >= maxMoisture && currentNutrients >= maxNutrients)
        {
            TriggerEvolution();
        }
    }

    private void TriggerEvolution()
    {
        currentMoisture = 0f;
        currentNutrients = 0f;

        if (currentStage < 3)
        {
            currentStage++;
            Debug.Log($"[Evolution] {plantName} เติบโตขึ้นเป็น ร่าง {currentStage}!");
        }
        else if (currentStage == 3)
        {
            currentStage = 4;

            if (relationshipPoints >= evoRelationshipThreshold)
            {
                evoForm = "Angel";
                Debug.Log($"[Evo Success] {plantName} ได้รับการดูแลอย่างดี! พัฒนาร่างเป็น: Angel (ร่างเทพ)");
            }
            else
            {
                evoForm = "Devil";
                Debug.Log($"[Evo Bad] {plantName} ไม่ได้รับการดูแลเพียงพอ! พัฒนาร่างเป็น: Devil (ร่างปีศาจ)");
            }
        }
        else
        {
            Debug.Log($"{plantName} อยู่ในร่างขั้นสุดแล้ว!");
            SaveData();
            return;
        }

        SaveData();            // 💾 บันทึก Stage และ evoForm ลง PlayerPrefs
        SyncCharacterVisual(); // 🎨 สลับรูปภาพบนแท่นหินทันที
    }

    // ==========================================
    // 💧 ฟังก์ชันกด "รดน้ำ"
    // ==========================================
    public void WaterPlant()
    {
        if (!CanWater()) return;

        lastWateredTime = DateTime.UtcNow.Ticks.ToString();

        float finalMoisture = baseMoistureFromWater;
        float finalNutrients = baseNutrientsFromWater;
        int finalExp = Mathf.RoundToInt(baseExpFromWater * expMultiplier);
        int finalCoins = Mathf.RoundToInt(baseCoinsFromWater * coinMultiplier);
        int finalRelationship = Mathf.RoundToInt(baseRelationshipFromWater * relationshipMultiplier);

        currentMoisture = Mathf.Min(maxMoisture, currentMoisture + finalMoisture);
        currentNutrients = Mathf.Min(maxNutrients, currentNutrients + finalNutrients);
        playerEXP += finalExp;
        flowerCoins += finalCoins;
        relationshipPoints += finalRelationship;

        SaveData();

        if (AlertPopupManager.Instance != null)
        {
            AlertPopupManager.Instance.ShowAlerts(
                moisture: finalMoisture,
                exp: finalExp,
                flower: finalCoins,
                relationship: finalRelationship
            );
        }

        CheckEvolution();
    }

    // ==========================================
    // 🧪 ฟังก์ชันกด "ใส่ปุ๋ย" (Overload 3 แบบ)
    // ==========================================

    // 💡 1. เรียกใช้ปุ๋ยแบบใช้ค่าปกติ Base Values (สั่งงานจาก NourishPopupView)
    public void ApplyFertilizer()
    {
        ApplyFertilizerInternal(
            baseNutrientsFromFertilizer,
            0f,
            baseExpFromFertilizer,
            baseCoinsFromFertilizer,
            baseRelationshipFromFertilizer,
            "None",
            0f
        );
    }

    // 💡 2. เรียกใช้ปุ๋ยแบบส่งข้อมูล FertilizerData (สำหรับ Mockup / ScriptableObject)
    public void ApplyFertilizer(FertilizerData fertilizer)
    {
        if (fertilizer == null)
        {
            ApplyFertilizer();
            return;
        }

        float gainNutrients = fertilizer.nutrientAmount > 0 ? fertilizer.nutrientAmount : baseNutrientsFromFertilizer;
        float gainMoisture = fertilizer.moistureAmount;

        int gainExp = fertilizer.customEXP > 0 ? fertilizer.customEXP : baseExpFromFertilizer;
        int gainCoins = fertilizer.customCoins > 0 ? fertilizer.customCoins : baseCoinsFromFertilizer;
        int gainRelationship = fertilizer.customRelationship > 0 ? fertilizer.customRelationship : baseRelationshipFromFertilizer;

        ApplyFertilizerInternal(gainNutrients, gainMoisture, gainExp, gainCoins, gainRelationship, fertilizer.buffType, fertilizer.buffDurationMinutes);
    }

    // 💡 3. เรียกใช้ปุ๋ยตรงจากข้อมูล Supabase (SupabaseInventoryItem)
    public void ApplyFertilizerFromSupabase(SupabaseInventoryItem item)
    {
        if (item == null || item.item == null)
        {
            ApplyFertilizer();
            return;
        }

        // สามารถขยายการดึงค่าเอฟเฟกต์จาก metadata เพิ่มเติมตรงนี้ได้ในอนาคต
        ApplyFertilizerInternal(
            baseNutrientsFromFertilizer,
            0f,
            baseExpFromFertilizer,
            baseCoinsFromFertilizer,
            baseRelationshipFromFertilizer,
            "None",
            0f
        );
    }

    // ⚙️ ฟังก์ชันคำนวณหลักในการใส่ปุ๋ย
    private void ApplyFertilizerInternal(
        float gainNutrients, 
        float gainMoisture, 
        int gainExp, 
        int gainCoins, 
        int gainRelationship, 
        string buffType, 
        float buffDuration)
    {
        int finalExp = Mathf.RoundToInt(gainExp * expMultiplier);
        int finalCoins = Mathf.RoundToInt(gainCoins * coinMultiplier);
        int finalRelationship = Mathf.RoundToInt(gainRelationship * relationshipMultiplier);

        currentNutrients = Mathf.Min(maxNutrients, currentNutrients + gainNutrients);
        if (gainMoisture > 0)
        {
            currentMoisture = Mathf.Min(maxMoisture, currentMoisture + gainMoisture);
        }

        playerEXP += finalExp;
        flowerCoins += finalCoins;
        relationshipPoints += finalRelationship;

        if (!string.IsNullOrEmpty(buffType) && buffType != "None" && buffDuration > 0)
        {
            ActivateBuff(buffType, buffDuration);
        }

        SaveData();

        // 🔔 เด้ง Alert Popup แจ้งเตือนผู้เล่น
        if (AlertPopupManager.Instance != null)
        {
            AlertPopupManager.Instance.ShowAlerts(
                moisture: gainMoisture,
                exp: finalExp,
                flower: finalCoins,
                relationship: finalRelationship
            );
        }

        // 🌱 เช็กเปลี่ยนร่างถ้าหลอดเต็ม
        CheckEvolution();

        // 🔄 อัปเดต UI บนหน้าพืชทันที
        if (MainPlantStatusView.Instance != null)
        {
            MainPlantStatusView.Instance.UpdateUI();
        }
    }

    public void ActivateBuff(string buffType, float durationMinutes)
    {
        currentActiveBuff = buffType;
        buffRemainingSeconds = durationMinutes * 60f;

        if (buffType == "DoubleEXP")
        {
            expMultiplier = 2.0f;
        }

        Debug.Log($"[Buff Activated] {buffType} เป็นเวลา {durationMinutes} นาที!");
    }

    // ==========================================
    // ✏️ ฟังก์ชันเปลี่ยนชื่อพืช
    // ==========================================
    public void SetPlantName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        plantName = newName.Trim();
        SaveData();
        Debug.Log($"[PlantStatus] เปลี่ยนชื่อพืชเป็น: {plantName} เรียบร้อย!");
    }

    // ==========================================
    // 💾 ระบบ SAVE / LOAD
    // ==========================================
    public void SaveData()
    {
        PlayerPrefs.SetString("Plant_Name", plantName);
        PlayerPrefs.SetInt("Plant_Stage", currentStage);
        PlayerPrefs.SetString("Plant_EvoForm", evoForm);
        PlayerPrefs.SetFloat("Plant_Moisture", currentMoisture);
        PlayerPrefs.SetFloat("Plant_Nutrients", currentNutrients);
        PlayerPrefs.SetInt("Plant_Relationship", relationshipPoints);
        PlayerPrefs.SetInt("Plant_FlowerCoins", flowerCoins);
        PlayerPrefs.SetInt("Plant_PlayerEXP", playerEXP);
        PlayerPrefs.SetString("Plant_LastWateredTime", lastWateredTime);
        PlayerPrefs.Save();
    }

    public void LoadData()
    {
        plantName = PlayerPrefs.GetString("Plant_Name", "มะเขือเทศสุดเท่");
        currentStage = PlayerPrefs.GetInt("Plant_Stage", 1);
        evoForm = PlayerPrefs.GetString("Plant_EvoForm", "None");
        currentMoisture = PlayerPrefs.GetFloat("Plant_Moisture", 0f);
        currentNutrients = PlayerPrefs.GetFloat("Plant_Nutrients", 0f);
        relationshipPoints = PlayerPrefs.GetInt("Plant_Relationship", 0);
        flowerCoins = PlayerPrefs.GetInt("Plant_FlowerCoins", 0);
        playerEXP = PlayerPrefs.GetInt("Plant_PlayerEXP", 0);
        lastWateredTime = PlayerPrefs.GetString("Plant_LastWateredTime", "");

        SyncCharacterVisual();
    }

    [ContextMenu("Reset Plant Data")]
    public void ResetData()
    {
        PlayerPrefs.DeleteKey("Plant_Name");
        PlayerPrefs.DeleteKey("Plant_Stage");
        PlayerPrefs.DeleteKey("Plant_EvoForm");
        PlayerPrefs.DeleteKey("Plant_Moisture");
        PlayerPrefs.DeleteKey("Plant_Nutrients");
        PlayerPrefs.DeleteKey("Plant_Relationship");
        PlayerPrefs.DeleteKey("Plant_FlowerCoins");
        PlayerPrefs.DeleteKey("Plant_PlayerEXP");
        PlayerPrefs.DeleteKey("Plant_LastWateredTime");

        currentMoisture = 0f;
        currentNutrients = 0f;
        relationshipPoints = 0;
        flowerCoins = 0;
        playerEXP = 0;
        lastWateredTime = "";
        currentStage = 1;
        evoForm = "None";

        SaveData();
        SyncCharacterVisual();
        Debug.Log("[PlantStatus] รีเซ็ตข้อมูลพืชเรียบร้อยแล้ว!");
    }

    public bool CanWater()
    {
        if (isSick) return false;
        if (string.IsNullOrEmpty(lastWateredTime)) return true;

        long lastTicks = Convert.ToInt64(lastWateredTime);
        DateTime lastTime = new DateTime(lastTicks);
        TimeSpan timePassed = DateTime.UtcNow - lastTime;

        return timePassed.TotalSeconds >= GetTotalCooldownInSeconds();
    }

    public float GetRemainingCooldownSeconds()
    {
        if (string.IsNullOrEmpty(lastWateredTime)) return 0f;

        long lastTicks = Convert.ToInt64(lastWateredTime);
        DateTime lastTime = new DateTime(lastTicks);
        TimeSpan timePassed = DateTime.UtcNow - lastTime;

        float remaining = GetTotalCooldownInSeconds() - (float)timePassed.TotalSeconds;
        return Mathf.Max(0f, remaining);
    }

    // ==========================================
    // 🧪 ฟังก์ชันสำหรับทดสอบใน Unity Editor (ContextMenu)
    // ==========================================

    [ContextMenu("🧪 Test: เติมหลอดเต็ม + สั่งเปลี่ยนร่าง")]
    public void TestFillBarsAndEvolve()
    {
        currentMoisture = maxMoisture;
        currentNutrients = maxNutrients;
        CheckEvolution();
        if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
    }

    [ContextMenu("🧪 Test: ตั้งค่าหัวใจสูง (สาย Angel)")]
    public void TestSetHighRelationship()
    {
        relationshipPoints = 100;
        Debug.Log("[Test] ตั้งค่า Relationship = 100 (สาย Angel)");
        if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
    }

    [ContextMenu("🧪 Test: ตั้งค่าหัวใจต่ำ (สาย Devil)")]
    public void TestSetLowRelationship()
    {
        relationshipPoints = 0;
        Debug.Log("[Test] ตั้งค่า Relationship = 0 (สาย Devil)");
        if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
    }
}