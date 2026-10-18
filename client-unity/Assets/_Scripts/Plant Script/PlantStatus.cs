using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 🧪 โครงสร้างข้อมูลปุ๋ย
[System.Serializable]
public class FertilizerData
{
    public string itemId;
    public string itemName;
    public float nutrientAmount = 25f;
    public float moistureAmount = 0f;

    [Header("แต้มรางวัลเฉพาะปุ๋ยนี้ (ถ้าใส่ 0 จะใช้ค่าพื้นฐาน)")]
    public int customEXP = 0;
    public int customCoins = 0;
    public int customRelationship = 0;

    [Header("บัฟพิเศษจากปุ๋ย")]
    public string buffType = "None";
    public float buffDurationMinutes = 0f;
}

public class PlantStatus : MonoBehaviour
{
    [Header("🔑 ข้อมูลผู้เล่นและสล็อต (player_plant)")]
    public string playerPlantId = "PLANT_SLOT_01";
    public string userId = "USER_001";
    public string plantNickname = "น้องมะเขือติดปีก";

    public string plantName 
    { 
        get => plantNickname; 
        set => plantNickname = value; 
    }

    [Header("🌱 ข้อมูลสายพันธุ์และร่างปัจจุบัน (Dynamic From DB)")]
    public string speciesId = "SPEC_TOMATO"; 
    public string speciesName = "มะเขือเทศ";
    public string currentStageId = "STAGE_TM_01";
    public int currentStageLevel = 1;
    public string evoForm = "None";
    public bool isFullyGrown = false;

    [Header("⏱️ สเปกสายพันธุ์ (plant_species)")]
    public float waterIntervalHours = 2f;
    public bool isWaterSensitive = false;
    public bool isHeatSensitive = true;

    [Header("💧 ค่าสเตตัสหลอด")]
    public float currentMoisture = 0f;
    public float maxMoisture = 100f;
    public float currentNutrients = 0f;
    public float maxNutrients = 100f;

    [Header("🛡️ สถานะและเกราะป้องกัน (player_plant)")]
    public bool isSick = false;
    public string sickUntil = "";
    public int immunityShields = 0;
    public float hoursDryBeforeSick = 5f;

    [Header("❤️ แต้มสะสมพิเศษ")]
    public int relationshipScore = 90;
    public int flowerCoins = 0;
    public int playerEXP = 0;

    [Header("⏱️ เวลาการบันทึก (Timestamp)")]
    public string lastWateredAt = "";
    public string plantedAt = "";

    [Header("⏱️ Cooldown & Buff")]
    public bool testModeInSeconds = false;
    public string currentActiveBuff = "None";
    public float buffRemainingSeconds = 0f;

    [Header("🎉 ตัวคูณอีเวนต์ / บัฟพิเศษ")]
    public float expMultiplier = 1.0f;
    public float coinMultiplier = 1.0f;
    public float relationshipMultiplier = 1.0f;

    [Header("💧 ค่าปกติการรดน้ำ / ใส่ปุ๋ย Base Values")]
    public float baseMoistureFromWater = 100f;
    public float baseNutrientsFromWater = 2f;
    public int baseExpFromWater = 15;
    public int baseCoinsFromWater = 10;
    public int baseRelationshipFromWater = 5;

    public float baseNutrientsFromFertilizer = 25f;
    public int baseExpFromFertilizer = 25;
    public int baseCoinsFromFertilizer = 15;
    public int baseRelationshipFromFertilizer = 10;

    [HideInInspector] public List<SupabasePlantSpecies> masterSpeciesList = new List<SupabasePlantSpecies>();
    [HideInInspector] public List<SupabasePlantStage> masterStagesList = new List<SupabasePlantStage>();
    [HideInInspector] public List<SupabaseEvolutionRule> masterRulesList = new List<SupabaseEvolutionRule>();

    void Awake()
    {
        LoadData();
    }

    void Start()
    {
        UpdateMoistureStatus();
        SyncCharacterVisual();

        if (SupabaseManager.Instance != null)
        {
            StartCoroutine(LoadAllDataFromSupabaseRoutine());
        }
    }

    void Update()
    {
        UpdateMoistureStatus();

        if (buffRemainingSeconds > 0)
        {
            buffRemainingSeconds -= Time.deltaTime;
            if (buffRemainingSeconds <= 0)
            {
                OnBuffExpired();
            }
        }
    }

    private bool TryParseDateTime(string timeStr, out DateTime result)
    {
        result = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(timeStr)) return false;

        string cleanTime = timeStr.Replace("T", " ").Replace("Z", "");
        if (cleanTime.Contains("+")) cleanTime = cleanTime.Split('+')[0];
        if (cleanTime.Contains(".")) cleanTime = cleanTime.Split('.')[0];

        return DateTime.TryParse(
            cleanTime.Trim(), 
            System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, 
            out result
        );
    }

    private IEnumerator LoadAllDataFromSupabaseRoutine()
    {
        Debug.Log("[PlantStatus] ⏳ เริ่มยิงดึงข้อมูลพืชจาก Supabase...");

        SupabasePlayerPlant playerPlantData = null;
        List<SupabasePlantSpecies> speciesList = new List<SupabasePlantSpecies>();
        List<SupabasePlantStage> stagesList = new List<SupabasePlantStage>();
        List<SupabaseEvolutionRule> rulesList = new List<SupabaseEvolutionRule>();

        yield return StartCoroutine(SupabaseManager.Instance.GetPlayerPlantData(
            onSuccess: (json) => {
                string wrappedJson = "{\"items\":" + json + "}";
                SupabasePlayerPlantList list = JsonUtility.FromJson<SupabasePlayerPlantList>(wrappedJson);
                if (list != null && list.items != null && list.items.Count > 0)
                {
                    playerPlantData = list.items.Find(p => 
                        !string.IsNullOrEmpty(p.player_plant_id) && 
                        p.player_plant_id.Trim().Equals(playerPlantId.Trim(), StringComparison.OrdinalIgnoreCase)
                    );

                    if (playerPlantData == null)
                    {
                        Debug.LogWarning($"[PlantStatus] ⚠️ ไม่พบ player_plant_id: '{playerPlantId}' ใน DB จะใช้ข้อมูลสล็อตแรก ({list.items[0].player_plant_id}) แทน");
                        playerPlantData = list.items[0];
                    }
                }
            },
            onError: (err) => Debug.LogError($"[PlantStatus] 🔴 ดึง player_plant ล้มเหลว: {err}")
        ));

        yield return StartCoroutine(SupabaseManager.Instance.GetPlantMasterData(
            onSpeciesSuccess: (json) => {
                string wrappedJson = "{\"items\":" + json + "}";
                SupabasePlantSpeciesList list = JsonUtility.FromJson<SupabasePlantSpeciesList>(wrappedJson);
                if (list != null) speciesList = list.items;
            },
            onStagesSuccess: (json) => {
                string wrappedJson = "{\"items\":" + json + "}";
                SupabasePlantStageList list = JsonUtility.FromJson<SupabasePlantStageList>(wrappedJson);
                if (list != null) stagesList = list.items;
            },
            onRulesSuccess: (json) => {
                string wrappedJson = "{\"items\":" + json + "}";
                SupabaseEvolutionRuleList list = JsonUtility.FromJson<SupabaseEvolutionRuleList>(wrappedJson);
                if (list != null) rulesList = list.items;
            },
            onError: (err) => Debug.LogError($"[PlantStatus] 🔴 ดึง Master Data ล้มเหลว: {err}")
        ));

        if (playerPlantData != null)
        {
            SetupFromSupabase(playerPlantData, speciesList, stagesList, rulesList);
            Debug.Log($"[PlantStatus] 🟢 โหลดข้อมูลพืชสำเร็จ! stage_id ปัจจุบัน: {currentStageId}");

            if (MainPlantStatusView.Instance != null)
            {
                MainPlantStatusView.Instance.UpdateUI();
            }
        }
        else
        {
            Debug.LogWarning("[PlantStatus] ⚠️ ไม่พบข้อมูลพืชใน DB จะใช้ข้อมูล Local แทน");
        }
    }

    public void SetPlantName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        plantNickname = newName.Trim();
        SaveData();
    }

    public string GetCurrentStageName()
    {
        MCController mc = GetComponent<MCController>();
        if (mc != null)
        {
            string nameFromMC = mc.GetStageNameById(currentStageId);
            if (!string.IsNullOrEmpty(nameFromMC)) return nameFromMC;
        }

        SupabasePlantStage stageData = masterStagesList.Find(s => s.stage_id == currentStageId);
        if (stageData != null && !string.IsNullOrEmpty(stageData.stage_name))
        {
            return stageData.stage_name;
        }

        return "ร่างเริ่มต้น";
    }

    public float GetRemainingCooldownSeconds()
    {
        if (string.IsNullOrWhiteSpace(lastWateredAt)) return 0f;

        DateTime lastTime;
        if (!TryParseDateTime(lastWateredAt, out lastTime)) return 0f;

        TimeSpan timePassed = DateTime.UtcNow - lastTime;
        float cooldownSeconds = testModeInSeconds ? waterIntervalHours : waterIntervalHours * 3600f;

        float remaining = cooldownSeconds - (float)timePassed.TotalSeconds;
        return Mathf.Max(0f, remaining);
    }

    public void SetupFromSupabase(
        SupabasePlayerPlant playerPlant, 
        List<SupabasePlantSpecies> speciesList, 
        List<SupabasePlantStage> stagesList, 
        List<SupabaseEvolutionRule> rulesList)
    {
        if (playerPlant == null) return;

        masterSpeciesList = speciesList ?? new List<SupabasePlantSpecies>();
        masterStagesList = stagesList ?? new List<SupabasePlantStage>();
        masterRulesList = rulesList ?? new List<SupabaseEvolutionRule>();

        playerPlantId = playerPlant.player_plant_id;
        userId = playerPlant.user_id;
        currentStageId = playerPlant.stage_id;
        plantNickname = playerPlant.plant_nickname;
        currentNutrients = playerPlant.current_exp;
        relationshipScore = playerPlant.relationship_score;
        lastWateredAt = playerPlant.last_watered_at;
        isFullyGrown = playerPlant.is_fully_grown;
        isSick = playerPlant.is_sick;
        sickUntil = playerPlant.sick_until;
        immunityShields = playerPlant.immunity_shields;
        plantedAt = playerPlant.planted_at;

        RefreshPlantDataFromMaster();

        // 💡 ตั้งเวลาให้ย้อนหลังจนคูลดาวน์หมดพอดี (-waterIntervalHours * 1.1f)
        // ผลคือ: ไม่ป่วย + คูลดาวน์เหลือ 0 วินาที พร้อมให้กดรดน้ำทันที ไม่ติดนับถอยหลัง 59:59 แล้ว!
        if (!isSick && !string.IsNullOrWhiteSpace(lastWateredAt))
        {
            DateTime lastTime;
            if (TryParseDateTime(lastWateredAt, out lastTime))
            {
                TimeSpan timePassed = DateTime.UtcNow - lastTime;
                float maxAllowedDryHours = (waterIntervalHours * 5f) + hoursDryBeforeSick;
                if (timePassed.TotalHours >= maxAllowedDryHours)
                {
                    lastWateredAt = DateTime.UtcNow.AddHours(-waterIntervalHours * 1.1f).ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
        }

        UpdateMoistureStatus();
        SaveDataToLocalOnly();
        SyncCharacterVisual();
    }

    public void RefreshPlantDataFromMaster()
    {
        SupabasePlantStage currentStageData = masterStagesList.Find(s => s.stage_id == currentStageId);
        if (currentStageData != null)
        {
            speciesId = currentStageData.species_id;
            currentStageLevel = currentStageData.stage_level;
        }

        SupabasePlantSpecies speciesData = masterSpeciesList.Find(sp => sp.species_id == speciesId);
        if (speciesData != null)
        {
            speciesName = speciesData.species_name;
            waterIntervalHours = speciesData.water_interval_hours;
            isWaterSensitive = speciesData.is_water_sensitive;
            isHeatSensitive = speciesData.is_heat_sensitive;
        }

        UpdateMaxNutrientsFromDB();
    }

    public void UpdateMaxNutrientsFromDB()
    {
        int targetLevel = currentStageLevel < 3 ? currentStageLevel + 1 : 4;
        SupabasePlantStage targetStageData = masterStagesList.Find(s => s.species_id == speciesId && s.stage_level == targetLevel);

        if (targetStageData != null)
        {
            maxNutrients = targetStageData.required_exp;
        }
        else
        {
            if (speciesId == "SPEC_TOMATO") maxNutrients = currentStageLevel == 1 ? 100f : (currentStageLevel == 2 ? 250f : 500f);
            else if (speciesId == "SPEC_COCONUT") maxNutrients = currentStageLevel == 1 ? 150f : (currentStageLevel == 2 ? 350f : 700f);
            else maxNutrients = 100f;
        }
    }

    public void SyncCharacterVisual()
    {
        MCController mc = GetComponent<MCController>();
        if (mc != null)
        {
            mc.SetStageById(currentStageId);
        }
    }

    public void CheckEvolution()
    {
        if (currentNutrients >= maxNutrients && !isFullyGrown)
        {
            TriggerEvolution();
        }
    }

    private void TriggerEvolution()
    {
        currentNutrients = 0f;

        if (currentStageLevel < 3)
        {
            currentStageLevel++;

            SupabasePlantStage nextStage = masterStagesList.Find(s => s.species_id == speciesId && s.stage_level == currentStageLevel);
            if (nextStage != null)
            {
                currentStageId = nextStage.stage_id;
            }
        }
        else if (currentStageLevel == 3)
        {
            currentStageLevel = 4;
            isFullyGrown = true;
            EvaluateStage4EvolutionRules();
        }
        else
        {
            isFullyGrown = true;
            SaveData();
            return;
        }

        UpdateMaxNutrientsFromDB();
        SaveData();
        SyncCharacterVisual();
    }

    private void EvaluateStage4EvolutionRules()
    {
        List<SupabaseEvolutionRule> speciesRules = masterRulesList.FindAll(r => r.stage_id.StartsWith($"STAGE_{speciesId.Replace("SPEC_", "")}"));

        bool matchedRule = false;

        foreach (SupabaseEvolutionRule rule in speciesRules)
        {
            if (rule.target_metric == "relationship_score")
            {
                if (rule.comparison_operator == ">=" && relationshipScore >= rule.threshold_value)
                {
                    currentStageId = rule.stage_id;
                    evoForm = rule.stage_id.Contains("ANGEL") || rule.stage_id.Contains("LORD") ? "Angel" : "Good";
                    matchedRule = true;
                    break;
                }
                else if (rule.comparison_operator == "<" && relationshipScore < rule.threshold_value)
                {
                    currentStageId = rule.stage_id;
                    evoForm = rule.stage_id.Contains("DEVIL") || rule.stage_id.Contains("PIRATE") ? "Devil" : "Bad";
                    matchedRule = true;
                    break;
                }
            }
        }

        if (!matchedRule)
        {
            if (speciesId == "SPEC_TOMATO")
            {
                currentStageId = relationshipScore >= 80 ? "STAGE_TM_EVO_ANGEL" : "STAGE_TM_EVO_DEVIL";
                evoForm = relationshipScore >= 80 ? "Angel" : "Devil";
            }
            else if (speciesId == "SPEC_COCONUT")
            {
                currentStageId = relationshipScore >= 85 ? "STAGE_CC_EVO_LORD" : "STAGE_CC_EVO_PIRATE";
                evoForm = relationshipScore >= 85 ? "Lord" : "Pirate";
            }
        }
    }

    public void WaterPlant()
    {
        if (!CanWater()) return;

        lastWateredAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        currentMoisture = maxMoisture;

        int finalExp = Mathf.RoundToInt(baseExpFromWater * expMultiplier);
        int finalCoins = Mathf.RoundToInt(baseCoinsFromWater * coinMultiplier);
        int finalRelationship = Mathf.RoundToInt(baseRelationshipFromWater * relationshipMultiplier);

        playerEXP += finalExp;
        flowerCoins += finalCoins;
        relationshipScore += finalRelationship;

        SaveData();

        if (AlertPopupManager.Instance != null)
        {
            AlertPopupManager.Instance.ShowAlerts(
                moisture: maxMoisture,
                exp: finalExp,
                flower: finalCoins,
                relationship: finalRelationship
            );
        }

        if (MainPlantStatusView.Instance != null)
        {
            MainPlantStatusView.Instance.UpdateUI();
        }
    }

    public void UpdateMoistureStatus()
    {
        if (string.IsNullOrWhiteSpace(lastWateredAt))
        {
            currentMoisture = 0f;
            return;
        }

        DateTime lastTime;
        if (!TryParseDateTime(lastWateredAt, out lastTime)) return;

        TimeSpan timePassed = DateTime.UtcNow - lastTime;
        float hoursPassed = (float)timePassed.TotalHours;
        if (hoursPassed < 0) hoursPassed = 0;

        float decayRatePerHour = maxMoisture / Mathf.Max(0.1f, waterIntervalHours * 5f);
        currentMoisture = Mathf.Max(0f, maxMoisture - (hoursPassed * decayRatePerHour));

        if (currentMoisture <= 0f)
        {
            float hoursDry = hoursPassed - (waterIntervalHours * 5f);
            if (hoursDry >= hoursDryBeforeSick && !isSick)
            {
                if (immunityShields > 0)
                {
                    immunityShields--;
                }
                else
                {
                    isSick = true;
                    sickUntil = DateTime.UtcNow.AddHours(24).ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
        }
    }

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

    public void ApplyFertilizerFromSupabase(SupabaseInventoryItem invItem)
    {
        if (invItem == null || invItem.item == null)
        {
            ApplyFertilizer();
            return;
        }

        ItemMetadata meta = invItem.item.metadata;
        if (meta == null)
        {
            ApplyFertilizer();
            return;
        }

        float value = meta.GetEffectValue();

        float gainNutrients = baseNutrientsFromFertilizer;
        float gainMoisture = 0f;
        int gainExp = baseExpFromFertilizer;
        int gainCoins = baseCoinsFromFertilizer;
        int gainRelationship = baseRelationshipFromFertilizer;

        switch (meta.effect_type)
        {
            case "all_rounder":
                gainNutrients += baseNutrientsFromFertilizer * value;
                gainMoisture += maxMoisture * value;
                gainExp = Mathf.RoundToInt(gainExp * (1f + value));
                gainCoins = Mathf.RoundToInt(gainCoins * (1f + value));
                gainRelationship = Mathf.RoundToInt(gainRelationship * (1f + value));
                break;

            case "cure_protect":
                isSick = false;
                sickUntil = "";
                immunityShields += 1;
                currentMoisture = maxMoisture;
                lastWateredAt = DateTime.UtcNow.AddHours(-waterIntervalHours * 1.1f).ToString("yyyy-MM-dd HH:mm:ss");
                break;

            case "boost_evo":
                gainRelationship += Mathf.RoundToInt(baseRelationshipFromFertilizer * (1f + value));
                break;

            case "speed_growth":
                gainNutrients += baseNutrientsFromFertilizer * (1f + value);
                break;
        }

        ApplyFertilizerInternal(gainNutrients, gainMoisture, gainExp, gainCoins, gainRelationship, "None", 0f);
    }

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
        relationshipScore += finalRelationship;

        if (!string.IsNullOrEmpty(buffType) && buffType != "None" && buffDuration > 0)
        {
            ActivateBuff(buffType, buffDuration);
        }

        SaveData();

        if (AlertPopupManager.Instance != null)
        {
            AlertPopupManager.Instance.ShowAlerts(
                moisture: gainMoisture,
                exp: finalExp,
                flower: finalCoins,
                relationship: finalRelationship
            );
        }

        CheckEvolution();

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
    }

    private void OnBuffExpired()
    {
        if (currentActiveBuff == "DoubleEXP")
        {
            expMultiplier = 1.0f;
        }

        currentActiveBuff = "None";
    }

    public bool CanWater()
    {
        if (isSick) return false;
        if (string.IsNullOrWhiteSpace(lastWateredAt)) return true;

        DateTime lastTime;
        if (!TryParseDateTime(lastWateredAt, out lastTime)) return true;

        TimeSpan timePassed = DateTime.UtcNow - lastTime;
        float cooldownSeconds = testModeInSeconds ? waterIntervalHours : waterIntervalHours * 3600f;

        return timePassed.TotalSeconds >= cooldownSeconds;
    }

    public void SaveDataToLocalOnly()
    {
        PlayerPrefs.SetString("Plant_PlayerPlantId", playerPlantId);
        PlayerPrefs.SetString("Plant_UserId", userId);
        PlayerPrefs.SetString("Plant_SpeciesId", speciesId);
        PlayerPrefs.SetString("Plant_StageId", currentStageId);
        PlayerPrefs.SetString("Plant_Nickname", plantNickname);
        PlayerPrefs.SetInt("Plant_StageLevel", currentStageLevel);
        PlayerPrefs.SetString("Plant_EvoForm", evoForm);
        PlayerPrefs.SetInt("Plant_IsFullyGrown", isFullyGrown ? 1 : 0);
        PlayerPrefs.SetFloat("Plant_Moisture", currentMoisture);
        PlayerPrefs.SetFloat("Plant_Nutrients", currentNutrients);
        PlayerPrefs.SetInt("Plant_RelationshipScore", relationshipScore);
        PlayerPrefs.SetInt("Plant_FlowerCoins", flowerCoins);
        PlayerPrefs.SetInt("Plant_PlayerEXP", playerEXP);
        PlayerPrefs.SetInt("Plant_IsSick", isSick ? 1 : 0);
        PlayerPrefs.SetString("Plant_SickUntil", sickUntil);
        PlayerPrefs.SetInt("Plant_ImmunityShields", immunityShields);
        PlayerPrefs.SetString("Plant_LastWateredAt", lastWateredAt);
        PlayerPrefs.SetString("Plant_PlantedAt", plantedAt);
        PlayerPrefs.Save();
    }

    public void SaveData()
    {
        SaveDataToLocalOnly();

        if (SupabaseManager.Instance != null)
        {
            StartCoroutine(SupabaseManager.Instance.UpdatePlayerPlantStatus(this));
        }
    }

    public void LoadData()
    {
        playerPlantId = PlayerPrefs.GetString("Plant_PlayerPlantId", "PLANT_SLOT_01");
        userId = PlayerPrefs.GetString("Plant_UserId", "USER_001");
        speciesId = PlayerPrefs.GetString("Plant_SpeciesId", "SPEC_TOMATO");
        currentStageId = PlayerPrefs.GetString("Plant_StageId", "STAGE_TM_01");
        plantNickname = PlayerPrefs.GetString("Plant_Nickname", "น้องมะเขือติดปีก");
        currentStageLevel = PlayerPrefs.GetInt("Plant_StageLevel", 1);
        evoForm = PlayerPrefs.GetString("Plant_EvoForm", "None");
        isFullyGrown = PlayerPrefs.GetInt("Plant_IsFullyGrown", 0) == 1;
        currentMoisture = PlayerPrefs.GetFloat("Plant_Moisture", 0f);
        currentNutrients = PlayerPrefs.GetFloat("Plant_Nutrients", 0f);
        relationshipScore = PlayerPrefs.GetInt("Plant_RelationshipScore", 90);
        flowerCoins = PlayerPrefs.GetInt("Plant_FlowerCoins", 0);
        playerEXP = PlayerPrefs.GetInt("Plant_PlayerEXP", 0);
        isSick = PlayerPrefs.GetInt("Plant_IsSick", 0) == 1;
        sickUntil = PlayerPrefs.GetString("Plant_SickUntil", "");
        immunityShields = PlayerPrefs.GetInt("Plant_ImmunityShields", 0);
        lastWateredAt = PlayerPrefs.GetString("Plant_LastWateredAt", "");
        plantedAt = PlayerPrefs.GetString("Plant_PlantedAt", "");

        RefreshPlantDataFromMaster();
        SyncCharacterVisual();
    }
}