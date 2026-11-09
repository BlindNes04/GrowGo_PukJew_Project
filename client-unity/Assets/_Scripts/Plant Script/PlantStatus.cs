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

    [Header("🐾 ข้อมูล Buddy Passive Buff")]
    public string activeBuddySkillType = "NONE"; // เช่น CURE_SICK_CHANCE, DRY_GRACE_TIME
    public float activeBuddySkillValue = 0f;

    public float baseNutrientsFromFertilizer = 25f;
    public int baseExpFromFertilizer = 25;
    public int baseCoinsFromFertilizer = 15;
    public int baseRelationshipFromFertilizer = 10;

    [Header("⭐ EXP ผู้เล่นเมื่อต้นไม้ขึ้นสเตจ")]
    public int expFromStageUp = 50;

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

        if (DateTimeOffset.TryParse(timeStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTimeOffset dto))
        {
            result = dto.UtcDateTime;
            if (result.Year > 2400)
            {
                result = result.AddYears(-543);
            }
            return true;
        }
        return false;
    }

    private IEnumerator LoadAllDataFromSupabaseRoutine()
    {
        Debug.Log("[PlantStatus] ⏳ เริ่มยิงดึงข้อมูลพืชจาก Supabase...");

        SupabasePlayerPlant playerPlantData = null;
        List<SupabasePlantSpecies> speciesList = new List<SupabasePlantSpecies>();
        List<SupabasePlantStage> stagesList = new List<SupabasePlantStage>();
        List<SupabaseEvolutionRule> rulesList = new List<SupabaseEvolutionRule>();

        yield return StartCoroutine(SupabaseManager.Instance.GetPlayerPlantData(
            onSuccess: (json) =>
            {
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
            onSpeciesSuccess: (json) =>
            {
                string wrappedJson = "{\"items\":" + json + "}";
                SupabasePlantSpeciesList list = JsonUtility.FromJson<SupabasePlantSpeciesList>(wrappedJson);
                if (list != null) speciesList = list.items;
            },
            onStagesSuccess: (json) =>
            {
                string wrappedJson = "{\"items\":" + json + "}";
                SupabasePlantStageList list = JsonUtility.FromJson<SupabasePlantStageList>(wrappedJson);
                if (list != null) stagesList = list.items;
            },
            onRulesSuccess: (json) =>
            {
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
            Debug.LogWarning("[PlantStatus] ⚠️ ไม่พบข้อมูลพืชใน DB ซ่อนตัวละครพืช");

            playerPlantId = "";
            plantNickname = "";

            SyncCharacterVisual();

            if (MainPlantStatusView.Instance != null)
            {
                MainPlantStatusView.Instance.UpdateUI();
            }
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

        // ปลดล็อกร่างปัจจุบัน + ร่างก่อนหน้าทั้งหมดลง DB ทันทีที่โหลดเกมสำเร็จ
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(currentStageId))
        {
            StartCoroutine(SupabaseManager.Instance.UnlockPlantAndPredecessors(currentStageId));
        }
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
            // 🔧 แก้บั๊กผู้เล่นใหม่: เดิมสั่ง SetActive(false) ทั้ง GameObject
            //    แต่ PlantStatus อยู่บน GameObject เดียวกัน พอปิดแล้วโหลดพืชใหม่ไม่ได้ (coroutine ไม่ทำงาน)
            //    และสคริปต์อื่นที่ใช้ FindAnyObjectByType<PlantStatus>() ก็หาไม่เจอ
            //    เลยเปลี่ยนเป็นซ่อนแค่รูปตัวละคร + ปุ่มกดแทน
            bool hasPlant = !string.IsNullOrEmpty(playerPlantId);
            mc.gameObject.SetActive(true);

            UnityEngine.UI.Image characterImg = mc.GetComponent<UnityEngine.UI.Image>();
            if (characterImg != null) characterImg.enabled = hasPlant;
            UnityEngine.UI.Button characterBtn = mc.GetComponent<UnityEngine.UI.Button>();
            if (characterBtn != null) characterBtn.enabled = hasPlant;

            if (hasPlant)
            {
                mc.SetStageById(currentStageId);
            }
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

        // ⭐ ต้นไม้ขึ้นสเตจ -> ผู้เล่นได้ EXP โบนัส
        if (SupabaseManager.Instance != null) SupabaseManager.Instance.AddPlayerExpAndFlower(expFromStageUp, 0);

        // ปลดล็อกร่างใหม่ + ร่างก่อนหน้าทั้งหมดลง DB เมื่อเปลี่ยนร่างสำเร็จ
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(currentStageId))
        {
            StartCoroutine(SupabaseManager.Instance.UnlockPlantAndPredecessors(currentStageId));
        }
    }

    private void EvaluateStage4EvolutionRules()
    {
        // ── หา rule ของพืชชนิดนี้ ──
        // ดูจากตาราง evolution_stage ว่า stage_id ของ rule เป็นของ species นี้ไหม
        // (เดิมเดาจากชื่อ STAGE_TOMATO แต่ id จริงเป็นตัวย่อ STAGE_TM_ เลยหาไม่เจอ)
        List<SupabaseEvolutionRule> speciesRules = masterRulesList.FindAll(r => masterStagesList.Exists(s => s.stage_id == r.stage_id && s.species_id == speciesId));

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

        // ── ไม่เจอ rule ที่ตรง ──
        // เกณฑ์ร่างขั้น 4 ยึดตามตาราง evolution_rule ใน DB อย่างเดียว (ลบค่าที่ hardcode ไว้ออกแล้ว)
        if (!matchedRule)
        {
            Debug.LogWarning($"[PlantStatus] ⚠️ ไม่พบ evolution_rule ที่ตรงกับ {speciesId} (relationship {relationshipScore})");
        }
    }

    // สั่งบันทึกเข้า PlantDex ทุกครั้งที่พืชเปลี่ยนร่าง/เริ่มปลูก
    public void CheckAndRecordPlantDex(string stageId)
    {
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(stageId))
        {
            StartCoroutine(SupabaseManager.Instance.UnlockPlantAndPredecessors(stageId));
        }
    }

    // 1. ฟังก์ชันรดน้ำ (Watering)
    public void WaterPlant()
    {
        // เช็กโอกาสพืชป่วยจากการรดน้ำช่วงฝนตก/พายุ (water_damage_chance)
        if (WeatherManager.Instance != null && WeatherManager.Instance.currentWeather != null)
        {
            var w = WeatherManager.Instance.currentWeather;
            if (w.water_damage_chance > 0f && UnityEngine.Random.value <= w.water_damage_chance)
            {
                if (activeBuddySkillType != "PREVENT_RAIN_SICK")
                {
                    isSick = true;
                    sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    Debug.LogWarning($"[PlantStatus] 🌧️ พืชป่วยจากฝน/น้ำเกิน! โอกาสติด: {w.water_damage_chance * 100}%");
                }
            }
        }

        // ถ้ามีสกิลบัดดี้ช่วยแก้ป่วยตอนรดน้ำ
        if (isSick && activeBuddySkillType == "CURE_SICK_CHANCE")
        {
            float roll = UnityEngine.Random.value; // สุ่ม 0.0 ถึง 1.0
            if (roll <= activeBuddySkillValue)
            {
                isSick = false;
                sickUntil = "";
                Debug.Log("[PlantStatus] 🐾 บัดดี้ช่วยรักษาอาการป่วยให้ตอนรดน้ำสำเร็จ!");
            }
        }

        if (!CanWater()) return;

        lastWateredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
        currentMoisture = maxMoisture;

        // คำนวณ EXP พื้นฐาน + บัฟบัดดี้ + ตัวคูณสภาพอากาศ (exp_multiplier)
        float buddyExpBonus = (activeBuddySkillType == "WATER_PLANT_EXP") ? activeBuddySkillValue : 0f;
        int baseExp = Mathf.RoundToInt(baseExpFromWater + buddyExpBonus);

        float weatherExpMult = (WeatherManager.Instance != null && WeatherManager.Instance.currentWeather != null)
            ? WeatherManager.Instance.currentWeather.exp_multiplier
            : 1.0f;

        int finalExp = (BuffManager.Instance != null)
            ? Mathf.RoundToInt(BuffManager.Instance.CalculateFinalExp(baseExpFromWater, isWateringAction: true) * weatherExpMult)
            : Mathf.RoundToInt(baseExp * weatherExpMult);

        int finalCoins = Mathf.RoundToInt(baseCoinsFromWater * coinMultiplier);
        int finalRelationship = Mathf.RoundToInt(baseRelationshipFromWater * relationshipMultiplier);

        playerEXP += finalExp;
        flowerCoins += finalCoins;

        // ⭐ บันทึก EXP ผู้เล่น + แต้มดอกไม้ ลงตาราง user (คิดเลเวลใน SupabaseManager)
        if (SupabaseManager.Instance != null) SupabaseManager.Instance.AddPlayerExpAndFlower(finalExp, finalCoins);
        relationshipScore += finalRelationship;

        SaveData();

        // สุ่มไอเทม
        CheckCareDrop("water");

        // บันทึกประวัติการรดน้ำลงตาราง care_log
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(playerPlantId))
        {
            StartCoroutine(SupabaseManager.Instance.LogCareAction(
                playerPlantId,
                null,
                "water",
                finalExp,
                finalRelationship
            ));
        }

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
    // 🟢 ใส่ตัวแปรนี้ไว้ข้างนอกฟังก์ชัน เพื่อเก็บเวลาคูลดาวน์สุ่มป่วย
    private float nextHeatCheckTime = 0f;

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

        if (WeatherManager.Instance != null && WeatherManager.Instance.currentWeather != null)
        {
            var w = WeatherManager.Instance.currentWeather;

            if (w.auto_water)
            {
                currentMoisture = maxMoisture;
            }
            else
            {
                float dryTimeMultiplier = (w.weather_id == "WTH_HOT") ? 1.1f : 2.0f;
                float decayRatePerHour = maxMoisture / Mathf.Max(0.1f, waterIntervalHours * dryTimeMultiplier);
                currentMoisture = Mathf.Max(0f, maxMoisture - (hoursPassed * decayRatePerHour));
            }

            // ☀️ แก้ไขบัคสุ่มทะลุเฟรมเรต! (สุ่มแดดเผาแค่ทุกๆ 10 นาทีพอ)
            if (w.heat_damage_chance > 0f && currentMoisture < 20f && !isSick)
            {
                if (Time.time >= nextHeatCheckTime)
                {
                    nextHeatCheckTime = Time.time + 600f; // 🟢 ตั้งเวลารอไปอีก 10 นาที (600 วินาที) ค่อยสุ่มใหม่

                    if (UnityEngine.Random.value <= w.heat_damage_chance)
                    {
                        if (activeBuddySkillType != "PREVENT_HEAT_DAMAGE")
                        {
                            isSick = true;
                            sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                            relationshipScore = Mathf.Max(0, relationshipScore - 5);
                            Debug.LogWarning($"[PlantStatus] ☀️ พืชป่วยจากแดดจัดและขาดน้ำ! โอกาสติด: {w.heat_damage_chance * 100}%");

                            SaveData();
                            if (MainPlantStatusView.Instance != null)
                            {
                                MainPlantStatusView.Instance.UpdateUI();
                            }
                        }
                    }
                }
            }
        }
        else
        {
            float dryTimeMultiplier = 2f;
            float decayRatePerHour = maxMoisture / Mathf.Max(0.1f, waterIntervalHours * dryTimeMultiplier);
            currentMoisture = Mathf.Max(0f, maxMoisture - (hoursPassed * decayRatePerHour));
        }

        if (isSick && !string.IsNullOrEmpty(sickUntil))
        {
            if (TryParseDateTime(sickUntil, out DateTime recoveryTime))
            {
                if (DateTime.UtcNow >= recoveryTime)
                {
                    isSick = false;
                    sickUntil = "";
                    lastWateredAt = DateTime.UtcNow.AddHours(-waterIntervalHours * 1.1f).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    Debug.Log("[PlantStatus] 🟢 หายป่วยเองตามธรรมชาติแล้ว!");
                    SaveData();
                    return;
                }
            }
        }

        if (currentMoisture <= 0f && !isSick)
        {
            float baseDryThreshold = waterIntervalHours * 5f;
            float bonusDryHours = (activeBuddySkillType == "DRY_GRACE_TIME") ? activeBuddySkillValue : 0f;
            float totalAllowedDryTime = baseDryThreshold + bonusDryHours;

            float hoursDry = hoursPassed - totalAllowedDryTime;

            if (hoursDry >= 24f)
            {
                if (immunityShields > 0)
                {
                    immunityShields--;
                    lastWateredAt = DateTime.UtcNow.AddHours(-waterIntervalHours * 1.1f).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    Debug.Log($"[PlantStatus] 🛡️ โล่ป้องกันทำงาน! เหลือโล่ {immunityShields} ชั้น");
                    SaveData();
                }
                else
                {
                    isSick = true;
                    sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    relationshipScore = Mathf.Max(0, relationshipScore - 5);
                    Debug.Log($"[PlantStatus] 🥀 น้ำแห้งข้ามวัน! พืชป่วย หัก -5 แต้ม");
                    SaveData();
                    if (MainPlantStatusView.Instance != null)
                    {
                        MainPlantStatusView.Instance.UpdateUI();
                    }
                }
            }
        }
    }

    public void ApplyFertilizer()
    {
        ApplyFertilizerInternal(
            null,
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

        string usedItemId = invItem.item.item_id;
        ItemMetadata meta = invItem.item.metadata;
        if (meta == null)
        {
            ApplyFertilizerInternal(usedItemId, baseNutrientsFromFertilizer, 0f, baseExpFromFertilizer, baseCoinsFromFertilizer, baseRelationshipFromFertilizer, "None", 0f);
            return;
        }

        // 1. ปรับให้ดึงคีย์ได้ทั้ง 2 แบบ (รองรับ effect_type ของปุ๋ย และ effect ของไอเทมพิเศษ)
        string currentEffect = !string.IsNullOrEmpty(meta.effect_type) ? meta.effect_type : meta.effect;

        // 2. ดึงค่าตัวเลขออกมารองรับทั้ง effect_value และ multiplier
        float value = meta.GetEffectValue();
        if (value == 0f && !string.IsNullOrEmpty(meta.multiplier))
        {
            float.TryParse(meta.multiplier, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        float gainNutrients = baseNutrientsFromFertilizer;
        float gainMoisture = 0f;
        int gainExp = baseExpFromFertilizer;
        int gainCoins = baseCoinsFromFertilizer;
        int gainRelationship = baseRelationshipFromFertilizer;
        float rareHarvestChance = 0f;

        // 3. เอา currentEffect มาเข้า Switch
        switch (currentEffect)
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

            // 1. ปุ๋ยกลายพันธุ์ (ITEM_FERT_EVO_A/B/C/S)
            // เป็นปุ๋ย -> ได้สารอาหารพื้นฐาน 25f + แต้มความสัมพันธ์
            case "boost_evo":
            case "rare_harvest":
                gainNutrients = baseNutrientsFromFertilizer;
                gainRelationship += Mathf.RoundToInt(baseRelationshipFromFertilizer * (1f + value));
                rareHarvestChance = value; // ฝากค่า % ดรอปแรร์ (เช่น 0.10, 0.20, 0.35, 0.50)
                break;

            // 2. โพชั่นสาหร่ายเรืองแสง (ITEM_ING_SEAWEED_POTION)
            // เป็นโพชั่น/ส่วนผสม -> ไม่เพิ่มสารอาหารปกติ (gainNutrients = 0) แต่บวกแต้ม EXP เร่งร่างวิวัฒนาการ 5% ทันที
            case "seaweed_potion":
            case "mutation_boost":
                gainNutrients = 0f; // พืชไม่อิ่มสารอาหารปกติ
                int evoBonus = Mathf.RoundToInt(maxNutrients * value); // 5% ของ Max Nutrients
                currentNutrients = Mathf.Min(maxNutrients, currentNutrients + evoBonus);
                gainExp = Mathf.RoundToInt(gainExp * (1f + value));
                Debug.Log($"[PlantStatus] 🧪 ใช้โพชั่นสาหร่าย! เพิ่มแต้มวิวัฒนาการพิเศษ +{evoBonus} (ไม่เพิ่มหลอดสารอาหารปกติ)");
                break;

            case "speed_growth":
                gainNutrients += baseNutrientsFromFertilizer * (1f + value);
                break;

            // 4. ลดเวลารดน้ำ
            case "reduce_watering_time":
                bool success = TryReduceWateringCooldown(value);
                if (!success)
                {
                    return;
                }
                return;

            // 5. ปุ๋ย Rotten Stink (ป่วยทันที)
            case "rotten_stink":
                gainNutrients += baseNutrientsFromFertilizer * value;
                gainExp = Mathf.RoundToInt(gainExp * (1f + value));

                isSick = true;
                sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                relationshipScore = Mathf.Max(0, relationshipScore - 5);

                Debug.Log("[PlantStatus] 🦨 ใช้ปุ๋ย Rotten Stink! พืชโตขึ้นนิดหน่อย แต่ป่วยทันทีและโดนหัก -5 แต้ม");
                break;

            // 6. ไอเทมย้อนร่าง
            case "transform_back":
                if (currentStageLevel >= 4 && isFullyGrown)
                {
                    currentStageLevel = 3;
                    isFullyGrown = false;
                    evoForm = "None";

                    SupabasePlantStage stage3Data = masterStagesList.Find(s => s.species_id == speciesId && s.stage_level == 3);
                    if (stage3Data != null)
                    {
                        currentStageId = stage3Data.stage_id;
                    }

                    currentNutrients = 0f;
                    UpdateMaxNutrientsFromDB();

                    SaveData();
                    SyncCharacterVisual();

                    Debug.Log($"[PlantStatus] 🔄 ย้อนร่างสำเร็จ! กลับมาที่ร่าง 3 (Stage ID: {currentStageId})");
                }
                else
                {
                    Debug.LogWarning("[PlantStatus] ⚠️ ไม่สามารถใช้ไอเทมย้อนร่างได้! พืชยังไม่ได้อยู่ร่างสุดท้าย (ขั้น 4)");
                    return;
                }
                return;
        }

        ApplyFertilizerInternal(usedItemId, gainNutrients, gainMoisture, gainExp, gainCoins, gainRelationship, "None", 0f);
    }

    // 🧪 2. ฟังก์ชันใส่ปุ๋ย (Fertilizing)
    private void ApplyFertilizerInternal(
        string itemId,
        float gainNutrients,
        float gainMoisture,
        int gainExp,
        int gainCoins,
        int gainRelationship,
        string buffType,
        float buffDuration,
        float customBonusChance = 0f) // โอกาสดรอปไอเทมพิเศษจากปุ๋ย
    {
        // ถ้ามีบัดดี้สายเร่งความเร็วเติบโต
        float buddyGrowthBonus = (activeBuddySkillType == "GROWTH_SPEED_BOOST") ? activeBuddySkillValue : 0f;
        float adjustedNutrients = gainNutrients * (1f + buddyGrowthBonus);

        // เอา exp_multiplier จาก WeatherManager ไปคูณเพิ่ม
        float weatherExpMult = (WeatherManager.Instance != null && WeatherManager.Instance.currentWeather != null)
            ? WeatherManager.Instance.currentWeather.exp_multiplier
            : 1.0f;

        int finalExp = Mathf.RoundToInt(gainExp * expMultiplier * weatherExpMult);
        int finalCoins = Mathf.RoundToInt(gainCoins * coinMultiplier);
        int finalRelationship = Mathf.RoundToInt(gainRelationship * relationshipMultiplier);

        currentNutrients = Mathf.Min(maxNutrients, currentNutrients + adjustedNutrients);
        if (gainMoisture > 0)
        {
            currentMoisture = Mathf.Min(maxMoisture, currentMoisture + gainMoisture);
        }

        playerEXP += finalExp;
        flowerCoins += finalCoins;

        // ⭐ บันทึก EXP ผู้เล่น + แต้มดอกไม้ ลงตาราง user (คิดเลเวลใน SupabaseManager)
        if (SupabaseManager.Instance != null) SupabaseManager.Instance.AddPlayerExpAndFlower(finalExp, finalCoins);
        relationshipScore += finalRelationship;

        if (!string.IsNullOrEmpty(buffType) && buffType != "None" && buffDuration > 0)
        {
            ActivateBuff(buffType, buffDuration);
        }

        SaveData();

        // สุ่มไอเทมจากการใส่ปุ๋ย
        CheckCareDrop("fertilize", customBonusChance);

        // บันทึกประวัติการใส่ปุ๋ยลงตาราง care_log
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(playerPlantId))
        {
            StartCoroutine(SupabaseManager.Instance.LogCareAction(
                playerPlantId,
                itemId,
                "fertilize",
                finalExp,
                finalRelationship
            ));
        }

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

    public void EquipBuddyPassive(string skillType, float skillValue)
    {
        activeBuddySkillType = skillType;
        activeBuddySkillValue = skillValue;
        Debug.Log($"[PlantStatus] 🐾 เปิดใช้งานพาสซีฟบัดดี้: {skillType} (ค่าพลัง: {skillValue})");
    }

    // ฟังก์ชันสุ่มดรอปไอเทมจากการดูแลพืช (รดน้ำ / ใส่ปุ๋ย)
    private void CheckCareDrop(string actionType, float customBonusChance = 0f)
    {
        float baseDropChance = 0.15f; // โอกาสพื้นฐาน 15%

        // รวมโบนัสจาก Buddy Passive (ถ้ามี)
        float buddyBonus = (activeBuddySkillType == "DOUBLE_DROP_CHANCE" || activeBuddySkillType == "ISLAND_RARE_CHANCE")
            ? activeBuddySkillValue
            : 0f;

        // โอกาสรวมทั้งหมด = โอกาสพื้นฐาน + บัดดี้ + % จากเกรดปุ๋ย (ถ้ามี)
        float finalDropChance = baseDropChance + buddyBonus + customBonusChance;

        if (UnityEngine.Random.value <= finalDropChance)
        {
            Debug.Log($"[PlantStatus] 🎁 สุ่มได้ไอเทมดรอปสำเร็จ! Action: {actionType} (โอกาสรวม: {finalDropChance * 100f:F1}%)");

            // เรียก MainRewardController สุ่มไอเทมตาม dropRate ใน DB โดยไม่ต้องส่งพารามิเตอร์
            if (MainRewardController.Instance != null)
            {
                MainRewardController.Instance.TriggerRandomCareReward();
            }
            else
            {
                Debug.LogWarning("[PlantStatus] ⚠️ ไม่พบ MainRewardController! ไม่สามารถดรอปไอเทมได้");
            }
        }
    }

    public bool CanWater()
    {
        // ถ้าป่วย ห้ามรดน้ำ
        if (isSick) return false;

        if (string.IsNullOrWhiteSpace(lastWateredAt)) return true;

        DateTime lastTime;
        if (!TryParseDateTime(lastWateredAt, out lastTime)) return true;

        TimeSpan timePassed = DateTime.UtcNow - lastTime;
        float cooldownSeconds = testModeInSeconds ? waterIntervalHours : waterIntervalHours * 3600f;

        return timePassed.TotalSeconds >= cooldownSeconds;
    }

    // ฟังก์ชันสำหรับไอเทมลดเวลา Cooldown รดน้ำ (reduce_watering_time)
    public bool TryReduceWateringCooldown(float multiplier)
    {
        // 1. เช็กว่าพืชพร้อมรดน้ำอยู่แล้วหรือไม่ (ถ้าพร้อมแล้ว ห้ามใช้เพื่อไม่ให้เสียของ)
        if (CanWater())
        {
            Debug.LogWarning("[PlantStatus] ⚠️ พืชพร้อมรดน้ำอยู่แล้ว ไม่สามารถใช้ไอเทมลดเวลาได้!");
            return false;
        }

        // 2. คำนวณเวลา Cooldown ที่เหลืออยู่ปัจจุบัน (วินาที)
        float remainingSeconds = GetRemainingCooldownSeconds();
        if (remainingSeconds <= 0) return false;

        // 3. คำนวณเวลาที่จะลด (เช่น เหลือ 3600 วิ * 0.3 = ลดไป 1080 วิ)
        float secondsToReduce = remainingSeconds * multiplier;

        // 4. แปลง lastWateredAt ปัจจุบัน แล้วย้อนเวลาไปในอดีตเพิ่มขึ้นตาม secondsToReduce
        if (TryParseDateTime(lastWateredAt, out DateTime lastTime))
        {
            DateTime newLastWateredAt = lastTime.AddSeconds(-secondsToReduce);

            // ป้องกันการย้อนเวลาเกินเวลา Cooldown ทั้งหมด
            float maxCooldownSeconds = testModeInSeconds ? waterIntervalHours : waterIntervalHours * 3600f;
            DateTime earliestAllowed = DateTime.UtcNow.AddSeconds(-maxCooldownSeconds);

            if (newLastWateredAt < earliestAllowed)
            {
                newLastWateredAt = earliestAllowed;
            }

            // บันทึกเวลาใหม่ลงพืช
            lastWateredAt = newLastWateredAt.ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
            SaveData();

            Debug.Log($"[PlantStatus] 🟢 ลดเวลา Cooldown สำเร็จ! ลดลง {secondsToReduce / 60f:F1} นาที");

            if (MainPlantStatusView.Instance != null)
            {
                MainPlantStatusView.Instance.UpdateUI();
            }

            return true;
        }

        return false;
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
        PlayerPrefs.SetString("Plant_BuddySkillType", activeBuddySkillType);
        PlayerPrefs.SetFloat("PLant_BuddySkillValue", activeBuddySkillValue);
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
        playerPlantId = PlayerPrefs.GetString("Plant_PlayerPlantId", "");
        userId = PlayerPrefs.GetString("Plant_UserId", "");
        speciesId = PlayerPrefs.GetString("Plant_SpeciesId", "SPEC_TOMATO");
        currentStageId = PlayerPrefs.GetString("Plant_StageId", "STAGE_TM_01");
        plantNickname = PlayerPrefs.GetString("Plant_Nickname", "");
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
        activeBuddySkillType = PlayerPrefs.GetString("Plant_BuddySkillType", "NONE");
        activeBuddySkillValue = PlayerPrefs.GetFloat("Plant_BuddySkillValue", 0f);

        RefreshPlantDataFromMaster();
        SyncCharacterVisual();
    }
}