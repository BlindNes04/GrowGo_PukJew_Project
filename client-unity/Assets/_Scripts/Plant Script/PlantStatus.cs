using System;
using UnityEngine;

[System.Serializable]
public class FertilizerData
{
    public string itemId;          // เช่น "fert_super_01"
    public string itemName;        // ชื่อปุ๋ย เช่น "ปุ๋ยเร่งโตสูตรเข้มข้น"
    public float nutrientAmount;   // แต้มสารอาหารที่ได้ (+30, +50)
    public float moistureAmount;   // แต้มน้ำที่ได้เสริม (+5, +10)
    
    [Header("บัฟพิเศษ")]
    public string buffType;        // เช่น "FastGrowth" (โตไว), "Immunity" (กันป่วย)
    public float buffDurationMinutes; // ระยะเวลาของบัฟ (นาที)
}

public class PlantStatus : MonoBehaviour
{
    [Header("ข้อมูลพืช")]
    public string plantName = "มะเขือเทศสุดเท่";
    public int currentStage = 1; // ร่างปัจจุบัน

    [Header("ค่าสเตตัสหลอด (เป้าหมายเปลี่ยนร่าง)")]
    public float currentMoisture = 0f;  // ค่าความชุ่มชื้นปัจจุบัน
    public float maxMoisture = 100f;    // ค่าความชุ่มชื้นเต็มหลอด

    public float currentNutrients = 0f; // สารอาหารปัจจุบัน
    public float maxNutrients = 100f;   // สารอาหารเต็มหลอด

    [Header("แต้มสะสมพิเศษ")]
    public int relationshipPoints = 0;  // แต้มความสัมพันธ์
    public int flowerCoins = 0;          // ดอกไม้ (ค่าเงินในเกม)
    public int playerEXP = 0;            // EXP ของผู้เล่น

    [Header("⏱️ ตั้งค่า Cooldown รดน้ำ (ปรับใน Inspector)")]
    public float cooldownWaterMinutes = 30f; // 💡 กำหนดเวลารอตรงนี้ (ใส่ 30 คือ 30 นาที)
    public bool testModeInSeconds = false;   // 💡 ติ๊กถูกเพื่อเปลี่ยนหน่วยจาก "นาที" เป็น "วินาที" ไว้ทดสอบ!

    [Header("ตั้งค่าการเพิ่มแต้ม")]
    public float moistureFromWater = 10f;   // ความชุ่มชื้นจากการรดน้ำ (+10)
    public float nutrientsFromWater = 2f;   // สารอาหารจากการรดน้ำ (+2)
    public float nutrientsFromFertilizer = 25f; // สารอาหารจากปุ๋ย (+25)

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

    void Update()
    {
        if (buffRemainingSeconds > 0)
        {
            buffRemainingSeconds -= Time.deltaTime;
            if (buffRemainingSeconds <= 0)
            {
                currentActiveBuff = "None";
                Debug.Log($"[Buff] บัฟของ {plantName} หมดเวลาแล้ว!");
            }
        }
    }

    // 💡 คำนวณเวลา Cooldown รวมเป็นวินาที
    private float GetTotalCooldownInSeconds()
    {
        return testModeInSeconds ? cooldownWaterMinutes : cooldownWaterMinutes * 60f;
    }

    // ==========================================
    // 💾 ระบบ SAVE / LOAD
    // ==========================================
    public void SaveData()
    {
        PlayerPrefs.SetString("Plant_Name", plantName);
        PlayerPrefs.SetInt("Plant_Stage", currentStage);
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
        currentMoisture = PlayerPrefs.GetFloat("Plant_Moisture", 0f);
        currentNutrients = PlayerPrefs.GetFloat("Plant_Nutrients", 0f);
        relationshipPoints = PlayerPrefs.GetInt("Plant_Relationship", 0);
        flowerCoins = PlayerPrefs.GetInt("Plant_FlowerCoins", 0);
        playerEXP = PlayerPrefs.GetInt("Plant_PlayerEXP", 0);
        lastWateredTime = PlayerPrefs.GetString("Plant_LastWateredTime", "");
    }

    [ContextMenu("Reset Plant Data")]
    public void ResetData()
    {
        PlayerPrefs.DeleteKey("Plant_Name");
        PlayerPrefs.DeleteKey("Plant_Stage");
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

        SaveData();
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

    public void WaterPlant()
    {
        lastWateredTime = DateTime.UtcNow.Ticks.ToString();

        currentMoisture = Mathf.Min(maxMoisture, currentMoisture + moistureFromWater);
        currentNutrients = Mathf.Min(maxNutrients, currentNutrients + nutrientsFromWater);

        relationshipPoints += 5;
        flowerCoins += 10;
        playerEXP += 15;

        SaveData();
        CheckEvolution();
    }

    public void NourishPlant()
    {
        currentNutrients = Mathf.Min(maxNutrients, currentNutrients + nutrientsFromFertilizer);

        relationshipPoints += 10;
        flowerCoins += 15;
        playerEXP += 25;

        SaveData();
        CheckEvolution();
    }

    public void CheckEvolution()
    {
        if (currentMoisture >= maxMoisture && currentNutrients >= maxNutrients)
        {
            TriggerEvolution();
        }
    }

    private void TriggerEvolution()
    {
        if (relationshipPoints >= 50)
        {
            Debug.Log($"[Evo Success] {plantName} พัฒนาร่างเป็น: สายร่างเทพ/นางฟ้า!");
        }
        else
        {
            Debug.Log($"[Evo Normal/Bad] {plantName} พัฒนาร่างเป็น: สายปกติ/ร่างซึม!");
        }

        currentMoisture = 0f;
        currentNutrients = 0f;
        currentStage++;

        SaveData();
    }

    public void ApplyFertilizer(FertilizerData fertilizer)
    {
        if (fertilizer == null) return;

        currentNutrients = Mathf.Min(maxNutrients, currentNutrients + fertilizer.nutrientAmount);
        currentMoisture = Mathf.Min(maxMoisture, currentMoisture + fertilizer.moistureAmount);

        relationshipPoints += 10;
        flowerCoins += 15;
        playerEXP += 25;

        if (!string.IsNullOrEmpty(fertilizer.buffType))
        {
            currentActiveBuff = fertilizer.buffType;
            buffRemainingSeconds = fertilizer.buffDurationMinutes * 60f;
        }

        SaveData();
        CheckEvolution();

        MainPlantStatusView statusView = FindObjectOfType<MainPlantStatusView>();
        if (statusView != null)
        {
            statusView.UpdateUI();
        }
    }
}