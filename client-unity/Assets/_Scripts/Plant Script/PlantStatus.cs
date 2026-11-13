using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== PlantStatus: สถานะต้นไม้ที่อยู่บนแท่น (ฉาก MainForest) ====================
// ดูแลทุกอย่างของต้นไม้ 1 ต้น: รดน้ำ ใส่ปุ๋ย/ใช้ไอเทม ขึ้นร่าง ป่วย/เกราะ โควตารายวัน
// ข้อมูลจริงอยู่ในตาราง player_plant (บันทึกผ่าน SupabaseManager) และสำรองไว้ใน PlayerPrefs
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

    [Header("🥀 ป่วยจากขาดน้ำ (player_plant.dry_penalty_at)")]
    public float dryHoursBeforeSick = 24f;      // หลอดน้ำหมดค้างไว้กี่ชั่วโมงถึงป่วย (กระต่ายบวกเพิ่มอีก)
    public float dryPenaltyIntervalHours = 24f; // ป่วยจากขาดน้ำแล้วยังไม่รักษา หักหัวใจซ้ำทุกกี่ชั่วโมง
    public int sickHeartPenalty = 5;            // หักหัวใจครั้งละเท่าไหร่
    public string dryPenaltyAt = "";            // เวลาหักหัวใจครั้งล่าสุด (ว่าง = ไม่ได้ป่วยจากขาดน้ำ)

    [Header("❤️ แต้มสะสมพิเศษ")]
    public int relationshipScore = 90;
    public int flowerCoins = 0;
    public int playerEXP = 0;

    [Header("⚖️ สมดุลเกม (ปรับตัวเลขได้ใน Inspector)")]
    public int maxRelationship = 120;           // หัวใจสูงสุดของต้นไม้ 1 ต้น
    public int maxFertilizerPerDay = 5;         // ใส่ปุ๋ย/ไอเทมที่เพิ่มสารอาหาร ได้กี่ครั้งต่อต้นต่อวัน
    public int maxHeartFromItemsPerDay = 20;    // หัวใจที่ได้จากปุ๋ย/ไอเทม สูงสุดต่อต้นต่อวัน
    public int waterNutrientPerHour = 5;        // สารอาหารจากน้ำ = ค่านี้ x ชั่วโมงรดน้ำของสายพันธุ์
    public float waterAbsorbSeconds = 600f;     // เวลาที่พืชค่อยๆ ดูดสารอาหารจากน้ำ (600 วิ = 10 นาที)
    [Tooltip("ติ๊กตอนพรีเซนต์/สอบ = ไม่จำกัดปุ๋ยและหัวใจรายวัน (เพดานหัวใจรวมยังอยู่)")]
    public bool ignoreDailyLimits = false;

    [Header("💧 สารอาหารจากน้ำที่กำลังดูด (player_plant)")]
    public int waterPoolTotal = 0;              // รดน้ำรอบนี้ได้สารอาหารทั้งหมดเท่าไหร่
    public int waterPoolAbsorbed = 0;           // ดูดเข้าหลอดไปแล้วเท่าไหร่
    public string waterPoolStartedAt = "";      // เริ่มดูดตอนไหน (เวลารดน้ำ)

    [Header("📅 โควตารายวัน (player_plant)")]
    public int fertUsedToday = 0;               // วันนี้ใส่ปุ๋ยไปกี่ครั้ง
    public int heartGainedToday = 0;            // วันนี้ได้หัวใจจากปุ๋ย/ไอเทมไปเท่าไหร่
    public string dailyCountDate = "";          // วันที่ของตัวนับ (เวลาไทย yyyy-MM-dd)

    [Header("⏱️ เวลาการบันทึก (Timestamp)")]
    public string lastWateredAt = "";
    public string plantedAt = "";

    // 🤝 เยี่ยมสวนเพื่อน: เวลาที่เพื่อนมารดน้ำให้ล่าสุด (player_plant.friend_watered_at)
    //    ใช้คิดหลอดความชุ่มชื้นอย่างเดียว ปุ่มรดน้ำของเรา (CanWater) ดูแค่ lastWateredAt
    //    -> เพื่อนรดให้แล้วหลอดเต็ม แต่เจ้าของยังรดเองได้ตามรอบปกติ และได้แต้มตามปกติ
    public string friendWateredAt = "";

    [Header("⏱️ ทดสอบ")]
    public bool testModeInSeconds = false;      // true = นับชั่วโมงรดน้ำเป็นวินาที (ไว้เทส)

    [Header("🎉 ตัวคูณแต้มที่ได้ (ปรับใน Inspector)")]
    public float expMultiplier = 1.0f;
    public float coinMultiplier = 1.0f;
    public float relationshipMultiplier = 1.0f;

    [Header("💧 ค่าพื้นฐานการรดน้ำ / ใส่ปุ๋ย")]
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

    [Header("🦎 ทดสอบสกิลกิ้งก่า (SPECIAL_EVO_CHANCE)")]
    [Tooltip("-1 = ใช้ค่าจริงจากตาราง buddy (0.05 = 5%) / ใส่ 1 = ติดทุกครั้ง ไว้เทส แล้วอย่าลืมเปลี่ยนกลับเป็น -1")]
    public float debugChameleonChance = -1f;

    [Header("⭐ EXP ผู้เล่นเมื่อต้นไม้ขึ้นสเตจ")]
    public int expFromStageUp = 50;

    [HideInInspector] public List<SupabasePlantSpecies> masterSpeciesList = new List<SupabasePlantSpecies>();
    [HideInInspector] public List<SupabasePlantStage> masterStagesList = new List<SupabasePlantStage>();
    [HideInInspector] public List<SupabaseEvolutionRule> masterRulesList = new List<SupabaseEvolutionRule>();

    // 🍒 Cherry: ค่าคงที่ + ตัวอ้างอิง popup เปลี่ยนสายพันธุ์ (ถ้าไม่ลากใส่ จะหาเองตอนเริ่ม)
    private const string CHERRY_SPECIES_ID = "SPEC_CHERRY";
    private const string CHERRY_DEFAULT_EVO_STAGE = "STAGE_CH_EVO_LILY";
    [Header("🍒 Cherry")]
    public CherryFormPopup cherryFormPopup;

    void Awake()
    {
        LoadData();
        LoadFromCache();   // 📦 มี cache จาก SupabaseManager -> ใช้ค่าจริงทันที (ไม่ต้องเห็นค่า default ก่อน DB ตอบ)
    }

    // ==================== 📦 โหลดจาก Cache ====================
    // ใช้ข้อมูล player_plant + ข้อมูลกลางของพืชที่ SupabaseManager จำไว้ตอนล็อกอิน ตั้งค่าได้ทันทีตอนเปิดฉาก
    // (หลอด/ชื่อร่างไม่กระโดดจากค่า default) จากนั้น Start จะโหลดจาก DB อีกรอบเพื่ออัปเดตค่าล่าสุด
    public bool LoadFromCache()
    {
        SupabaseManager db = SupabaseManager.Instance;
        if (db == null) return false;

        string plantsJson = db.GetCachedPlayerPlants();
        if (string.IsNullOrEmpty(plantsJson) || string.IsNullOrEmpty(db.cachedStagesJson) || string.IsNullOrEmpty(db.cachedSpeciesJson))
            return false;   // ยังไม่มี cache -> รอโหลดจาก DB ใน Start

        SupabasePlayerPlantList plants = JsonUtility.FromJson<SupabasePlayerPlantList>("{\"items\":" + plantsJson + "}");
        if (plants == null || plants.items == null) return false;
        if (plants.items.Count == 0)
        {
            // ผู้เล่นคนนี้ยังไม่มีต้นไม้ -> ซ่อนพืชเลย (ไม่โชว์ต้นของบัญชีก่อนที่ค้างใน PlayerPrefs)
            playerPlantId = "";
            plantNickname = "";
            SyncCharacterVisual();
            return true;
        }

        // หาต้นที่เลือกอยู่ ถ้าไม่เจอใช้ต้นแรก (เหมือน LoadAllDataFromSupabaseRoutine)
        SupabasePlayerPlant myPlant = plants.items.Find(p =>
            !string.IsNullOrEmpty(p.player_plant_id) &&
            p.player_plant_id.Trim().Equals(playerPlantId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (myPlant == null) myPlant = plants.items[0];

        var species = JsonUtility.FromJson<SupabasePlantSpeciesList>("{\"items\":" + db.cachedSpeciesJson + "}");
        var stages = JsonUtility.FromJson<SupabasePlantStageList>("{\"items\":" + db.cachedStagesJson + "}");
        var rules = string.IsNullOrEmpty(db.cachedRulesJson) ? null : JsonUtility.FromJson<SupabaseEvolutionRuleList>("{\"items\":" + db.cachedRulesJson + "}");

        SetupFromSupabase(myPlant, species?.items, stages?.items, rules?.items, fromCache: true);
        Debug.Log($"[PlantStatus] 📦 ใช้ข้อมูลจาก cache: {plantNickname} ({currentStageId})");

        if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
        return true;
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
        AbsorbWaterPool();   // 💧 ค่อยๆ ดูดสารอาหารจากน้ำเข้าหลอด
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
        List<SupabaseEvolutionRule> rulesList,
        bool fromCache = false)   // 📦 true = ตั้งค่าจาก cache (ไม่ต้องยิงปลดล็อก Dex ซ้ำ เดี๋ยว DB รอบจริงยิงให้)
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
        friendWateredAt = playerPlant.friend_watered_at; // 🤝 เยี่ยมสวนเพื่อน
        // ⚖️ ระบบสมดุลใหม่ (คอลัมน์ใหม่ใน player_plant)
        waterPoolTotal = playerPlant.water_pool_total;
        waterPoolAbsorbed = playerPlant.water_pool_absorbed;
        waterPoolStartedAt = playerPlant.water_pool_started_at;
        fertUsedToday = playerPlant.fert_used_today;
        heartGainedToday = playerPlant.heart_gained_today;
        dailyCountDate = playerPlant.daily_count_date;
        dryPenaltyAt = playerPlant.dry_penalty_at;
        relationshipScore = Mathf.Min(relationshipScore, maxRelationship);   // ต้นเก่าที่เกินเพดาน -> ตัดเหลือ 120

        RefreshPlantDataFromMaster();

        // ไม่รีเซ็ตเวลารดน้ำตอนโหลด -> UpdateMoistureStatus คิดการขาดน้ำย้อนหลังจากเวลาจริง (ปิดเกมไปก็ป่วยได้)
        UpdateMoistureStatus();
        SaveDataToLocalOnly();
        SyncCharacterVisual();

        // ปลดล็อกร่างปัจจุบัน + ร่างก่อนหน้าทั้งหมดลง DB ทันทีที่โหลดเกมสำเร็จ
        if (!fromCache && SupabaseManager.Instance != null && !string.IsNullOrEmpty(currentStageId))
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
            // ⚖️ ค่าสำรองตอนยังไม่มีข้อมูลจาก DB (ตัวเลขเดียวกับตาราง evolution_stage)
            if (speciesId == "SPEC_TOMATO") maxNutrients = currentStageLevel == 1 ? 300f : (currentStageLevel == 2 ? 400f : 650f);
            else if (speciesId == "SPEC_COCONUT") maxNutrients = currentStageLevel == 1 ? 400f : (currentStageLevel == 2 ? 500f : 850f);
            else if (speciesId == "SPEC_MELON") maxNutrients = currentStageLevel == 1 ? 650f : (currentStageLevel == 2 ? 1050f : 1400f);
            else if (speciesId == "SPEC_CHERRY") maxNutrients = currentStageLevel == 1 ? 400f : (currentStageLevel == 2 ? 650f : 1050f);
            else maxNutrients = 300f;
        }
    }

    public void SyncCharacterVisual()
    {
        MCController mc = GetComponent<MCController>();
        if (mc != null)
        {
            // ยังไม่มีต้นไม้ -> ซ่อนแค่รูปตัวละคร + ปุ่มกด (ไม่ปิดทั้ง GameObject)
            // เพราะ PlantStatus อยู่บน GameObject เดียวกัน ถ้าปิดไป coroutine โหลดพืชจะไม่ทำงาน
            // และสคริปต์อื่นที่ใช้ FindAnyObjectByType<PlantStatus>() จะหาไม่เจอ
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

        // 🍒 อัปเดตปุ่มเปลี่ยนสายพันธุ์ Cherry (โชว์เฉพาะตอนเลี้ยง Cherry + รูป seed ตามร่าง)
        if (cherryFormPopup == null) cherryFormPopup = FindAnyObjectByType<CherryFormPopup>(FindObjectsInactive.Include);
        if (cherryFormPopup != null) cherryFormPopup.RefreshCherryButton(this);
    }

    // ==================== 🍒 Cherry: เปลี่ยนสายพันธุ์ร่าง 4 ====================
    // เรียกจาก CherryFormPopup ตอนผู้เล่นกดเลือกร่าง (LILY / SAKURA / VIOLET)
    // เปลี่ยนได้ตลอด ไม่จำกัดจำนวนครั้ง แต่ต้องเป็น Cherry ที่ถึงขั้น 4 แล้วเท่านั้น
    public bool IsCherryAtStage4()
    {
        return speciesId == CHERRY_SPECIES_ID && currentStageLevel >= 4 && !string.IsNullOrEmpty(playerPlantId);
    }

    public void ChangeCherryForm(string newStageId)
    {
        if (!IsCherryAtStage4())
        {
            Debug.LogWarning("[PlantStatus] ⚠️ เปลี่ยนสายพันธุ์ไม่ได้: ต้องเป็น Cherry ที่ถึงขั้น 4 แล้ว");
            return;
        }

        // เช็กว่าร่างที่เลือกเป็นร่างขั้น 4 ของ Cherry จริงในตาราง evolution_stage
        SupabasePlantStage targetStage = masterStagesList.Find(s => s.stage_id == newStageId);
        if (targetStage == null || targetStage.species_id != CHERRY_SPECIES_ID || targetStage.stage_level != 4)
        {
            Debug.LogWarning($"[PlantStatus] ⚠️ stageId '{newStageId}' ไม่ใช่ร่างขั้น 4 ของ Cherry");
            return;
        }

        if (currentStageId == newStageId) return; // เป็นร่างนี้อยู่แล้ว

        currentStageId = newStageId;

        // บันทึก (stage_id ใน player_plant) + เปลี่ยนรูปตัวละคร
        SaveData();
        SyncCharacterVisual();

        // ปลดล็อกร่างนี้ใน Plant Dex (ปลดเฉพาะร่างที่เคยเปลี่ยนเป็น)
        CheckAndRecordPlantDex(currentStageId);

        Debug.Log($"[PlantStatus] 🍒 เปลี่ยนสายพันธุ์ Cherry เป็น {currentStageId}");
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
        // 🦫 บีเวอร์ LEVELUP_BONUS_POINTS: ต้นไม้ขึ้นร่าง (อัปเลเวล) -> ได้แต้มดอกไม้เพิ่ม (ค่าในตาราง buddy = 30)
        int beaverFlower = (activeBuddySkillType == "LEVELUP_BONUS_POINTS") ? Mathf.RoundToInt(activeBuddySkillValue) : 0;
        if (SupabaseManager.Instance != null) SupabaseManager.Instance.AddPlayerExpAndFlower(expFromStageUp, beaverFlower);
        if (beaverFlower > 0 && AlertPopupManager.Instance != null) AlertPopupManager.Instance.ShowAlerts(flower: beaverFlower);

        // ปลดล็อกร่างใหม่ + ร่างก่อนหน้าทั้งหมดลง DB เมื่อเปลี่ยนร่างสำเร็จ
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(currentStageId))
        {
            StartCoroutine(SupabaseManager.Instance.UnlockPlantAndPredecessors(currentStageId));
        }
    }

    private void EvaluateStage4EvolutionRules()
    {
        // ── 🍒 Cherry: ไม่ใช้ evolution_rule (ไม่เช็กความสัมพันธ์) ──
        // ถึงขั้น 4 แล้วเป็นร่าง LILY ก่อนเสมอ ผู้เล่นค่อยไปเปลี่ยนเป็น SAKURA/VIOLET เองในหน้า popup สายพันธุ์
        if (speciesId == CHERRY_SPECIES_ID)
        {
            currentStageId = CHERRY_DEFAULT_EVO_STAGE;
            evoForm = "None";
            return;
        }

        // ── หา rule ของพืชชนิดนี้ ──
        // ดูจากตาราง evolution_stage ว่า stage_id ของ rule เป็นของ species นี้ไหม
        // (stage_id ใช้ตัวย่อ เช่น STAGE_TM_ จึงเทียบจากชื่อพืชตรงๆ ไม่ได้)
        List<SupabaseEvolutionRule> speciesRules = masterRulesList.FindAll(r => masterStagesList.Exists(s => s.stage_id == r.stage_id && s.species_id == speciesId));

        // ==================== 🦎 กิ้งก่า SPECIAL_EVO_CHANCE: โอกาสได้ร่างพิเศษ ====================
        // ตอนขึ้นร่าง 4 มีโอกาส 5% (ค่าในตาราง buddy) ได้ร่างดีเสมอ แม้หัวใจไม่ถึง 80
        // สุ่มครั้งเดียวตอนขึ้นร่าง (ฟังก์ชันนี้ถูกเรียกครั้งเดียวต่อการวิวัฒนาการ) สุ่มใหม่ไม่ได้
        if (activeBuddySkillType == "SPECIAL_EVO_CHANCE")
        {
            SupabaseEvolutionRule goodRule = speciesRules.Find(r => r.target_metric == "relationship_score" && r.comparison_operator == ">=");
            float chance = debugChameleonChance >= 0f ? debugChameleonChance : activeBuddySkillValue;
            if (goodRule != null && relationshipScore < goodRule.threshold_value && UnityEngine.Random.value <= chance)
            {
                currentStageId = goodRule.stage_id;
                evoForm = goodRule.stage_id.Contains("ANGEL") || goodRule.stage_id.Contains("LORD") ? "Angel" : "Good";
                Debug.Log($"[PlantStatus] 🦎 กิ้งก่านำโชค! ได้ร่างพิเศษ {currentStageId} (หัวใจ {relationshipScore})");
                BuddyDisplayManager buddy = FindAnyObjectByType<BuddyDisplayManager>();
                if (buddy != null) buddy.ShowBuddyMessage("🦎 กิ้งก่านำโชค! ได้ร่างพิเศษ");
                return;
            }
        }

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

    // ==================== 🛡️ เกราะกันป่วย ====================
    // เรียกตอนพืชเข้าเงื่อนไขจะป่วย: ถ้ามีเกราะ -> ไม่ป่วย แต่เกราะลด 1 แล้วคืน true
    private bool UseShieldInsteadOfSick(string reason)
    {
        if (immunityShields <= 0) return false;
        immunityShields--;
        Debug.Log($"[PlantStatus] 🛡️ เกราะกันป่วยทำงาน ({reason})! เหลือเกราะ {immunityShields} ชั้น");
        SaveData();
        return true;
    }

    // ==================== 💧 รดน้ำ ====================
    public void WaterPlant()
    {
        // ---------- โอกาสป่วยจากฝนตก/พายุ (weather.water_damage_chance) ----------
        if (WeatherManager.Instance != null && WeatherManager.Instance.currentWeather != null)
        {
            var w = WeatherManager.Instance.currentWeather;
            if (w.water_damage_chance > 0f && UnityEngine.Random.value <= ReduceSickChance(w.water_damage_chance))
            {
                if (!UseShieldInsteadOfSick("ฝน/น้ำเกิน"))
                {
                    isSick = true;
                    sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    relationshipScore = Mathf.Max(0, relationshipScore - sickHeartPenalty);   // ป่วยทุกแบบหักหัวใจ
                    Debug.LogWarning($"[PlantStatus] 🌧️ พืชป่วยจากฝน/น้ำเกิน! โอกาสติด: {w.water_damage_chance * 100}% หัก -{sickHeartPenalty} หัวใจ");
                    // บันทึกทันที เพราะด้านล่าง CanWater() จะคืน false (ป่วยอยู่) แล้วออกจากฟังก์ชันก่อนถึง SaveData
                    SaveData();
                    if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
                }
            }
        }

        // ---------- 🐹 แฮมสเตอร์ CURE_SICK_CHANCE: มีโอกาสรักษาป่วยตอนรดน้ำ ----------
        if (isSick && activeBuddySkillType == "CURE_SICK_CHANCE")
        {
            float roll = UnityEngine.Random.value; // สุ่ม 0.0 ถึง 1.0
            if (roll <= activeBuddySkillValue)
            {
                isSick = false;
                sickUntil = "";
                ClearDrySickness();
                Debug.Log("[PlantStatus] 🐾 บัดดี้ช่วยรักษาอาการป่วยให้ตอนรดน้ำสำเร็จ!");
            }
        }

        if (!CanWater()) return;

        lastWateredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
        currentMoisture = maxMoisture;
        StartWaterPool();   // 💧 รดน้ำ = ได้สารอาหารก้อนใหม่ ค่อยๆ ดูดเข้าหลอดใน 10 นาที

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
        // 🦦 นาก WATER_POINTS_BURST: กดรดน้ำได้แต้มดอกไม้เพิ่ม (ค่าในตาราง buddy = 3)
        if (activeBuddySkillType == "WATER_POINTS_BURST") finalCoins += Mathf.RoundToInt(activeBuddySkillValue);
        int finalRelationship = Mathf.RoundToInt(baseRelationshipFromWater * relationshipMultiplier);

        playerEXP += finalExp;
        flowerCoins += finalCoins;

        // ⭐ บันทึก EXP ผู้เล่น + แต้มดอกไม้ ลงตาราง user (คิดเลเวลใน SupabaseManager)
        if (SupabaseManager.Instance != null) SupabaseManager.Instance.AddPlayerExpAndFlower(finalExp, finalCoins);
        finalRelationship = AddRelationship(finalRelationship, fromItem: false);   // ❤️ ไม่เกินเพดานหัวใจรวม (น้ำไม่นับโควตารายวัน)

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

    // ==================== 💧 หลอดความชุ่มชื้น + ป่วยจากแดด ====================
    private float nextHeatCheckTime = 0f;   // เวลาที่จะสุ่มป่วยจากแดดรอบถัดไป (สุ่มทุก 10 นาที ไม่ใช่ทุกเฟรม)

    // 🤝 เยี่ยมสวนเพื่อน: คืนเวลารดน้ำล่าสุดสำหรับคิดหลอดความชุ่มชื้น
    //    เทียบเวลาที่เรารดเอง (lastWateredAt) กับเวลาที่เพื่อนรดให้ (friendWateredAt) แล้วเอาอันที่ใหม่กว่า
    private string GetLatestWateredAt()
    {
        if (string.IsNullOrWhiteSpace(friendWateredAt)) return lastWateredAt;
        if (string.IsNullOrWhiteSpace(lastWateredAt)) return friendWateredAt;

        if (TryParseDateTime(lastWateredAt, out DateTime ownTime) && TryParseDateTime(friendWateredAt, out DateTime friendTime))
            return friendTime > ownTime ? friendWateredAt : lastWateredAt;

        return lastWateredAt;
    }

    public void UpdateMoistureStatus()
    {
        // 🤝 ใช้เวลาที่ใหม่กว่าระหว่างเจ้าของรดเอง กับเพื่อนมารดให้
        string moistureTime = GetLatestWateredAt();

        if (string.IsNullOrWhiteSpace(moistureTime))
        {
            currentMoisture = 0f;
            return;
        }

        DateTime lastTime;
        if (!TryParseDateTime(moistureTime, out lastTime)) return;

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

            // ☀️ ป่วยจากแดด: หลอดน้ำต่ำกว่า 20% ในวันที่มี heat_damage_chance -> สุ่มทุก 10 นาที (ไม่สุ่มทุกเฟรม)
            if (w.heat_damage_chance > 0f && currentMoisture < 20f && !isSick)
            {
                if (Time.time >= nextHeatCheckTime)
                {
                    nextHeatCheckTime = Time.time + 600f; // รอ 10 นาที (600 วินาที) ค่อยสุ่มใหม่

                    if (UnityEngine.Random.value <= ReduceSickChance(w.heat_damage_chance))
                    {
                        if (!UseShieldInsteadOfSick("แดดจัด"))
                        {
                            isSick = true;
                            sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                            relationshipScore = Mathf.Max(0, relationshipScore - sickHeartPenalty);
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

        // ---------- หายป่วยเองเมื่อครบ sickUntil (ฝน/แดด/ปุ๋ยเน่า ป่วย 8 ชม.; ขาดน้ำไม่มี sickUntil จึงไม่หายเอง) ----------
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

        CheckDrySickness(lastTime);   // 🥀 ป่วยจากขาดน้ำ (ดูกติกาที่ฟังก์ชันด้านล่าง)
    }

    // ==================== 🥀 ป่วยจากขาดน้ำ ====================
    // กติกา:
    //   1) หลอดน้ำหมด แล้วปล่อยค้างไว้ครบ dryHoursBeforeSick (24 ชม.) -> ป่วย หักหัวใจ -5
    //      🐇 กระต่าย DRY_GRACE_TIME ได้เวลาเพิ่ม (ค่าในตาราง buddy = 4 ชม.)
    //   2) มีเกราะ -> เกราะลด 1 แทนการป่วย แล้วเริ่มนับ 24 ชม. ใหม่จากตอนที่เกราะทำงาน
    //   3) ป่วยจากขาดน้ำไม่หายเอง ต้องใช้ยารักษา (cure_protect) ระหว่างนั้นหักหัวใจ -5 ทุก dryPenaltyIntervalHours (24 ชม.)
    //   คิดจากเวลาจริงทั้งหมด -> ปิดเกมไปหลายวัน เปิดมาก็ป่วยและหักย้อนหลังครบทุกรอบ
    //   dryPenaltyAt (player_plant.dry_penalty_at) = เวลาหักครั้งล่าสุด และใช้บอกว่ากำลังป่วยแบบนี้อยู่ (ว่าง = ไม่ใช่)
    private void CheckDrySickness(DateTime lastWaterTime)
    {
        // ---------- ป่วยจากขาดน้ำอยู่ -> หักหัวใจซ้ำทุก 24 ชม. จนกว่าจะรักษา ----------
        if (isSick)
        {
            if (string.IsNullOrEmpty(dryPenaltyAt) || !TryParseDateTime(dryPenaltyAt, out DateTime lastPenalty)) return;

            float interval = Mathf.Max(0.01f, dryPenaltyIntervalHours);   // กันตั้ง 0 ใน Inspector แล้ววนไม่จบ
            int times = 0;
            while ((DateTime.UtcNow - lastPenalty).TotalHours >= interval)
            {
                lastPenalty = lastPenalty.AddHours(interval);
                times++;
            }
            if (times == 0) return;

            relationshipScore = Mathf.Max(0, relationshipScore - sickHeartPenalty * times);
            dryPenaltyAt = lastPenalty.ToString("o");
            Debug.Log($"[PlantStatus] 🥀 ยังป่วยจากขาดน้ำ หักหัวใจ -{sickHeartPenalty * times} ({times} รอบ)");
            SaveData();
            if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
            return;
        }

        // ---------- ยังไม่ป่วย -> เช็กว่าหลอดน้ำหมดนานเกินกำหนดหรือยัง ----------
        float hoursToEmpty = GetHoursToEmpty();
        if (hoursToEmpty <= 0f) return;   // อากาศที่รดน้ำให้เอง (auto_water) หลอดไม่มีวันหมด

        float graceHours = dryHoursBeforeSick + ((activeBuddySkillType == "DRY_GRACE_TIME") ? activeBuddySkillValue : 0f);
        DateTime sickAt = lastWaterTime.AddHours(hoursToEmpty + graceHours);   // เวลาที่จะป่วย
        if (DateTime.UtcNow < sickAt) return;

        // ---------- มีเกราะ -> กันได้ 1 ครั้ง ----------
        if (immunityShields > 0)
        {
            immunityShields--;
            // เริ่มนับใหม่จากตอนที่เกราะทำงาน: ตั้งเวลารดน้ำให้หลอดน้ำหมดพอดีตอน sickAt (หลอดยังเป็น 0 เหมือนเดิม)
            lastWateredAt = sickAt.AddHours(-hoursToEmpty).ToString("o");
            Debug.Log($"[PlantStatus] 🛡️ เกราะกันป่วยจากขาดน้ำ! เหลือเกราะ {immunityShields} ชั้น");
            SaveData();
            return;
        }

        // ---------- ไม่มีเกราะ -> ป่วย ----------
        isSick = true;
        sickUntil = "";                       // ไม่หายเอง ต้องใช้ยารักษา
        dryPenaltyAt = sickAt.ToString("o");  // นับรอบหักถัดไปจากเวลาที่เริ่มป่วยจริง (ปิดเกมอยู่ก็นับ)
        relationshipScore = Mathf.Max(0, relationshipScore - sickHeartPenalty);
        Debug.Log($"[PlantStatus] 🥀 น้ำหมดนานเกิน {graceHours} ชม. พืชป่วย หักหัวใจ -{sickHeartPenalty}");
        SaveData();
        if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
    }

    // รักษาหายจากป่วยขาดน้ำ (ยารักษา / แฮมสเตอร์): หยุดหักหัวใจ แล้วเริ่มนับ 24 ชม. ใหม่
    // ตั้งเวลารดน้ำให้หลอดน้ำหมดพอดีตอนนี้ -> หลอดยัง 0 และกดรดน้ำได้ทันที (ไม่งั้นเฟรมถัดไปจะป่วยซ้ำทันที)
    private void ClearDrySickness()
    {
        if (string.IsNullOrEmpty(dryPenaltyAt)) return;   // ไม่ได้ป่วยจากขาดน้ำ ไม่ต้องทำอะไร
        dryPenaltyAt = "";
        lastWateredAt = DateTime.UtcNow.AddHours(-Mathf.Max(0f, GetHoursToEmpty())).ToString("o");
    }

    // หลอดน้ำเต็มจะหมดในกี่ชั่วโมง (สูตรเดียวกับ UpdateMoistureStatus: แดดจัด x1.1 อากาศอื่น x2)
    // คืน -1 = อากาศที่รดน้ำให้เอง (auto_water) หลอดไม่หมด
    private float GetHoursToEmpty()
    {
        SupabaseWeather w = WeatherManager.Instance != null ? WeatherManager.Instance.currentWeather : null;
        if (w != null && w.auto_water) return -1f;
        float dryTimeMultiplier = (w != null && w.weather_id == "WTH_HOT") ? 1.1f : 2.0f;
        return Mathf.Max(0.1f, waterIntervalHours * dryTimeMultiplier);
    }

    // ==================== 🧪 ใช้ปุ๋ย / ไอเทมกับต้นไม้ ====================
    // ปุ๋ยพื้นฐาน (ไม่มีข้อมูลไอเทม): ได้ค่าพื้นฐานทั้งหมด
    public void ApplyFertilizer()
    {
        ApplyFertilizerInternal(null, baseNutrientsFromFertilizer, 0f, baseExpFromFertilizer, baseCoinsFromFertilizer, baseRelationshipFromFertilizer);
    }

    // ใช้ไอเทมจากกระเป๋า (MainInventoryManager / NourishPopupView เรียก หลังเช็กเงื่อนไขแล้ว)
    // ผลลัพธ์ขึ้นกับ item.metadata.effect_type ในตาราง item
    public void ApplyFertilizerFromSupabase(SupabaseInventoryItem invItem)
    {
        if (invItem == null || invItem.item == null)
        {
            ApplyFertilizer();
            return;
        }

        string usedItemId = invItem.item.item_id;
        ItemMetadata meta = invItem.item.metadata;

        // 📅 โควตาปุ๋ยรายวัน: ของที่เพิ่มสารอาหาร ใช้ได้ไม่เกิน 5 ครั้ง/ต้น/วัน (กันกดซ้ำ - หน้ากระเป๋าเช็กให้ก่อนหักของแล้ว)
        if (!CanUseItemNow(invItem, out string quotaMessage))
        {
            Debug.LogWarning($"[PlantStatus] 📅 {quotaMessage}");
            return;
        }
        if (IsNutrientItem(invItem)) { ResetDailyIfNewDay(); fertUsedToday++; }   // นับ 1 ครั้ง (ยา/คูปอง/ยาย้อนร่าง ไม่นับ)

        if (meta == null)
        {
            ApplyFertilizerInternal(usedItemId, baseNutrientsFromFertilizer, 0f, baseExpFromFertilizer, baseCoinsFromFertilizer, baseRelationshipFromFertilizer);
            return;
        }

        // ---------- อ่านชนิดผลและค่าตัวเลขของไอเทม ----------
        // ชื่อผล: ปุ๋ยใช้ effect_type, ไอเทมพิเศษใช้ effect
        string currentEffect = !string.IsNullOrEmpty(meta.effect_type) ? meta.effect_type : meta.effect;

        // ค่าตัวเลข: effect_value หรือ multiplier
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

        // ---------- แยกผลตามชนิดไอเทม (ไม่ตรง case ไหน เช่น วัตถุดิบ = ได้ค่าพื้นฐานเหมือนปุ๋ยธรรมดา) ----------
        switch (currentEffect)
        {
            // ปุ๋ย ALL: เพิ่มทุกอย่างตามค่า value + เติมน้ำ
            case "all_rounder":
                gainNutrients += baseNutrientsFromFertilizer * value;
                gainMoisture += maxMoisture * value;
                gainExp = Mathf.RoundToInt(gainExp * (1f + value));
                gainCoins = Mathf.RoundToInt(gainCoins * (1f + value));
                gainRelationship = Mathf.RoundToInt(gainRelationship * (1f + value));
                break;

            // ยารักษา / ปุ๋ย CURE: หายป่วย + ได้เกราะตาม effect_value (CURE_C 0, B 1, A 2, S 3, ขนนก/สมุนไพร 1)
            // ไม่เติมน้ำ ไม่เพิ่มสารอาหาร
            case "cure_protect":
                gainNutrients = 0f;   // ⚖️ ยารักษาไม่เพิ่มสารอาหาร (เลยไม่นับโควตาปุ๋ยรายวัน)
                isSick = false;
                sickUntil = "";
                immunityShields += Mathf.Max(0, Mathf.RoundToInt(value));
                ClearDrySickness();
                break;

            // ปุ๋ย EVO (ITEM_FERT_EVO_*) และโพชั่นสาหร่าย (ITEM_ING_SEAWEED_POTION)
            // ได้สารอาหารพื้นฐาน + หัวใจเพิ่ม + โอกาสดรอปของเพิ่มตาม value (เช่น 0.10, 0.20, 0.35, 0.50)
            case "boost_evo":
                gainNutrients = baseNutrientsFromFertilizer;
                gainRelationship += Mathf.RoundToInt(baseRelationshipFromFertilizer * (1f + value));
                rareHarvestChance = value;
                break;

            // ปุ๋ย GROWTH: สารอาหารเพิ่มตาม value
            case "speed_growth":
                gainNutrients += baseNutrientsFromFertilizer * (1f + value);
                break;

            // คูปองลดเวลารดน้ำ (ไม่ได้แต้มอื่น)
            case "reduce_watering_time":
                TryReduceWateringCooldown(value);
                return;

            // ปุ๋ยเน่า: โตขึ้นนิดหน่อย แต่ป่วยทันที (มีเกราะ -> เกราะลด 1 แทน)
            case "rotten_stink":
                gainNutrients += baseNutrientsFromFertilizer * value;
                gainExp = Mathf.RoundToInt(gainExp * (1f + value));

                if (!UseShieldInsteadOfSick("ปุ๋ยเน่า"))
                {
                    isSick = true;
                    sickUntil = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
                    relationshipScore = Mathf.Max(0, relationshipScore - sickHeartPenalty);

                    Debug.Log($"[PlantStatus] 🦨 ใช้ปุ๋ยเน่า! พืชโตขึ้นนิดหน่อย แต่ป่วยทันทีและโดนหัก -{sickHeartPenalty} หัวใจ");
                }
                break;

            // ยาย้อนร่าง: ร่าง 4 -> กลับไปร่าง 3 (หลอดสารอาหารเริ่มใหม่)
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

        // ส่งโอกาสดรอปเพิ่มของปุ๋ย EVO (rareHarvestChance) ไปบวกกับโอกาสดรอปพื้นฐานใน CheckCareDrop
        ApplyFertilizerInternal(usedItemId, gainNutrients, gainMoisture, gainExp, gainCoins, gainRelationship, rareHarvestChance);
    }

    // ใส่แต้มที่คำนวณแล้วให้ต้นไม้ + บัฟบัดดี้/อากาศ + บันทึก + สุ่มดรอป + เช็กขึ้นร่าง
    private void ApplyFertilizerInternal(
        string itemId,
        float gainNutrients,
        float gainMoisture,
        int gainExp,
        int gainCoins,
        int gainRelationship,
        float customBonusChance = 0f) // โอกาสดรอปไอเทมเพิ่มจากปุ๋ย
    {
        // 🐈‍⬛ แมวดำ GROWTH_SPEED_BOOST: สารอาหารเพิ่ม (ค่าในตาราง buddy = 0.05 = 5%)
        float buddyGrowthBonus = (activeBuddySkillType == "GROWTH_SPEED_BOOST") ? activeBuddySkillValue : 0f;
        // 🦁 สิงโต FERTILIZER_BOOST: ปุ๋ยทุกชิ้นได้สารอาหารเพิ่ม (ค่าในตาราง buddy = 0.1 = 10%)
        if (activeBuddySkillType == "FERTILIZER_BOOST") buddyGrowthBonus += activeBuddySkillValue;
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
        finalRelationship = AddRelationship(finalRelationship, fromItem: true);   // ❤️ ปุ๋ย/ไอเทม: ได้ไม่เกิน 20/วัน และไม่เกินเพดานหัวใจรวม

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

    // ==================== ⚖️ ระบบสมดุลเกม ====================
    // กันไม่ให้ต้นไม้โตเร็วเกินไปและหัวใจสะสมไม่มีที่สิ้นสุด:
    //   1) หลอดสารอาหารแต่ละร่างกำหนดในตาราง evolution_stage
    //   2) รดน้ำได้สารอาหาร = 5 x ชั่วโมงรดน้ำของสายพันธุ์ (มะเขือเทศ 10, เมลอน 40) ค่อยๆ เข้าหลอดใน 10 นาที
    //   3) ปุ๋ย/ไอเทมที่เพิ่มสารอาหาร ใช้ได้ 5 ครั้งต่อต้นต่อวัน (วันตามเวลาไทย)
    //   4) หัวใจจากปุ๋ย/ไอเทม ได้ไม่เกิน 20 ต่อต้นต่อวัน และหัวใจรวมไม่เกิน 120
    // ตัวนับทั้งหมดเก็บในตาราง player_plant (ปิดเกม/เปลี่ยนเครื่องก็ไม่หาย)

    // วันที่ตามเวลาไทย (UTC+7) ใช้รีเซ็ตโควตาตอนเที่ยงคืน
    private string TodayThai()
    {
        return DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");
    }

    // ขึ้นวันใหม่ -> ล้างตัวนับปุ๋ย/หัวใจของวันก่อน
    private void ResetDailyIfNewDay()
    {
        string today = TodayThai();
        if (dailyCountDate != today)
        {
            dailyCountDate = today;
            fertUsedToday = 0;
            heartGainedToday = 0;
        }
    }

    // ไอเทมนี้เพิ่มสารอาหารไหม (นับโควตาปุ๋ย) - ยารักษา / คูปองลดเวลารดน้ำ / ยาย้อนร่าง ไม่นับ
    public bool IsNutrientItem(SupabaseInventoryItem invItem)
    {
        if (invItem == null || invItem.item == null) return false;
        ItemMetadata meta = invItem.item.metadata;
        if (meta == null) return true;   // ปุ๋ยธรรมดาไม่มี metadata -> ได้สารอาหารพื้นฐาน
        string effect = !string.IsNullOrEmpty(meta.effect_type) ? meta.effect_type : meta.effect;
        return effect != "cure_protect" && effect != "reduce_watering_time" && effect != "transform_back";
    }

    // เหลือใส่ปุ๋ยได้อีกกี่ครั้งวันนี้
    public int GetFertilizerLeftToday()
    {
        ResetDailyIfNewDay();
        return Mathf.Max(0, maxFertilizerPerDay - fertUsedToday);
    }

    /// <summary> เช็กก่อนใช้ไอเทม (หน้ากระเป๋า/บำรุง เรียกก่อนหักของ) false = ใช้ไม่ได้ + ข้อความบอกผู้เล่น </summary>
    public bool CanUseItemNow(SupabaseInventoryItem invItem, out string message)
    {
        message = "";
        if (ignoreDailyLimits || !IsNutrientItem(invItem)) return true;
        if (GetFertilizerLeftToday() > 0) return true;
        message = $"วันนี้ใส่ปุ๋ยต้นนี้ครบ {maxFertilizerPerDay} ครั้งแล้ว พรุ่งนี้มาใหม่นะ";
        return false;
    }

    // 🦌 กวาง SICK_CHANCE_REDUCE: ลดโอกาสป่วยจากฝน/แดด (ค่าในตาราง buddy = 0.15 = ลดลง 15%)
    private float ReduceSickChance(float chance)
    {
        return (activeBuddySkillType == "SICK_CHANCE_REDUCE") ? chance * (1f - activeBuddySkillValue) : chance;
    }

    // บวกหัวใจแบบมีเพดาน คืนค่าที่บวกได้จริง (เอาไปโชว์/บันทึก log)
    private int AddRelationship(int amount, bool fromItem)
    {
        ResetDailyIfNewDay();
        if (amount <= 0) return 0;
        if (fromItem && !ignoreDailyLimits)
            amount = Mathf.Min(amount, Mathf.Max(0, maxHeartFromItemsPerDay - heartGainedToday));   // โควตา 20/วัน
        amount = Mathf.Min(amount, Mathf.Max(0, maxRelationship - relationshipScore));              // เพดาน 120
        relationshipScore += amount;
        if (fromItem) heartGainedToday += amount;
        return amount;
    }

    // 💧 เริ่มก้อนสารอาหารจากน้ำรอบใหม่ (ถ้ารอบก่อนยังดูดไม่หมด ให้ใส่ส่วนที่เหลือเข้าหลอดก่อน)
    private void StartWaterPool()
    {
        int rest = waterPoolTotal - waterPoolAbsorbed;
        if (rest > 0 && !isFullyGrown)
        {
            currentNutrients = Mathf.Min(maxNutrients, currentNutrients + rest);
            CheckEvolution();
        }
        // 🐈‍⬛ แมวดำ GROWTH_SPEED_BOOST: ต้นไม้โตไวขึ้น 5% -> สารอาหารจากน้ำก็ได้เพิ่ม 5% ด้วย (ปุ๋ยได้อยู่แล้ว)
        float growthBonus = (activeBuddySkillType == "GROWTH_SPEED_BOOST") ? activeBuddySkillValue : 0f;
        waterPoolTotal = Mathf.RoundToInt(waterNutrientPerHour * waterIntervalHours * (1f + growthBonus));
        waterPoolAbsorbed = 0;
        waterPoolStartedAt = DateTime.UtcNow.ToString("o");
    }

    // 💧 เรียกทุกเฟรม: คิดจากเวลาที่ผ่านไปตั้งแต่รดน้ำ ว่าควรดูดไปแล้วเท่าไหร่ แล้วเติมส่วนต่างเข้าหลอด
    //    ใช้เวลาจริง เลยปิดเกมแล้วกลับมาก็ได้ครบ
    //    ไม่หยุดดูดตอนน้ำแห้ง เพราะหลังรดน้ำหลอดน้ำเต็มและใช้หลายชั่วโมงกว่าจะแห้ง (10 นาทีไม่มีทางแห้ง)
    private void AbsorbWaterPool()
    {
        if (waterPoolTotal <= 0 || waterPoolAbsorbed >= waterPoolTotal) return;
        if (!TryParseDateTime(waterPoolStartedAt, out DateTime startTime)) { waterPoolAbsorbed = waterPoolTotal; return; }

        float progress = Mathf.Clamp01((float)(DateTime.UtcNow - startTime).TotalSeconds / Mathf.Max(1f, waterAbsorbSeconds));
        int shouldHave = Mathf.FloorToInt(waterPoolTotal * progress);
        int delta = shouldHave - waterPoolAbsorbed;
        if (delta <= 0) return;

        waterPoolAbsorbed = shouldHave;
        if (!isFullyGrown) currentNutrients = Mathf.Min(maxNutrients, currentNutrients + delta);

        // ดูดครบ -> บันทึก DB / ยังไม่ครบ -> บันทึกในเครื่องพอ (ไม่ยิง DB ทุกแต้ม)
        if (waterPoolAbsorbed >= waterPoolTotal) SaveData();
        else SaveDataToLocalOnly();

        CheckEvolution();   // หลอดเต็มจากน้ำ -> ขึ้นร่างได้เหมือนใส่ปุ๋ย
        if (MainPlantStatusView.Instance != null) MainPlantStatusView.Instance.UpdateUI();
    }

    // ==================== 🐾 สกิลบัดดี้ / ดรอปไอเทม / คูลดาวน์รดน้ำ ====================
    // BuddyDisplayManager เรียกตอนโหลดบัดดี้ของผู้เล่น
    public void EquipBuddyPassive(string skillType, float skillValue)
    {
        activeBuddySkillType = skillType;
        activeBuddySkillValue = skillValue;
        Debug.Log($"[PlantStatus] 🐾 เปิดใช้งานพาสซีฟบัดดี้: {skillType} (ค่าพลัง: {skillValue})");
    }

    // สุ่มดรอปไอเทมจากการดูแลพืช (รดน้ำ / ใส่ปุ๋ย)
    // โอกาส = 15% + โอกาสเพิ่มจากปุ๋ย EVO (ถ้ามี)
    // (สกิลนกฮูก/เต่า ทำงานตอนสำรวจเกาะและตอนกดรับรางวัล ไม่ได้บวกตรงนี้)
    private void CheckCareDrop(string actionType, float customBonusChance = 0f)
    {
        float baseDropChance = 0.15f; // โอกาสพื้นฐาน 15%
        float finalDropChance = baseDropChance + customBonusChance;

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
        // ⚖️ ระบบสมดุลใหม่
        PlayerPrefs.SetInt("Plant_WaterPoolTotal", waterPoolTotal);
        PlayerPrefs.SetInt("Plant_WaterPoolAbsorbed", waterPoolAbsorbed);
        PlayerPrefs.SetString("Plant_WaterPoolStartedAt", waterPoolStartedAt ?? "");
        PlayerPrefs.SetInt("Plant_FertUsedToday", fertUsedToday);
        PlayerPrefs.SetInt("Plant_HeartGainedToday", heartGainedToday);
        PlayerPrefs.SetString("Plant_DailyCountDate", dailyCountDate ?? "");
        PlayerPrefs.SetString("Plant_DryPenaltyAt", dryPenaltyAt ?? "");   // 🥀 ป่วยจากขาดน้ำ
        PlayerPrefs.Save();
    }

    // ==================== 💾 บันทึกลง DB ทีละครั้งตามลำดับ ====================
    // บางจังหวะเรียก SaveData หลายครั้งติดกัน (เช่น หลอดเต็ม -> ขึ้นร่างในเฟรมเดียว)
    // ถ้าส่งพร้อมกัน คำขอเก่าอาจไปถึง DB ทีหลังแล้วเขียนทับค่าใหม่ (ร่างย้อนกลับ)
    // จึงส่งทีละครั้ง: ถ้ากำลังบันทึกอยู่ ให้จำไว้ แล้วส่งค่าล่าสุดอีกรอบหลังครั้งแรกเสร็จ
    private bool isSavingToDB = false;
    private bool saveAgainAfterThis = false;

    public void SaveData()
    {
        SaveDataToLocalOnly();

        if (SupabaseManager.Instance != null)
        {
            if (isSavingToDB)
            {
                saveAgainAfterThis = true;   // มีงานบันทึกค้างอยู่ -> รอส่งรอบถัดไป
                return;
            }
            StartCoroutine(SaveToDBInOrder());
        }
    }

    private IEnumerator SaveToDBInOrder()
    {
        isSavingToDB = true;
        do
        {
            saveAgainAfterThis = false;
            // ข้อมูลถูกอ่านจากตัวแปรตอนส่ง เลยได้ค่าล่าสุดเสมอ (เช่น ร่างใหม่ + หลอด 0)
            yield return StartCoroutine(SupabaseManager.Instance.UpdatePlayerPlantStatus(this));
        } while (saveAgainAfterThis);
        isSavingToDB = false;
    }

    // ถ้าวัตถุถูกปิดระหว่างบันทึก coroutine จะหยุดกลางทาง -> ล้างสถานะไว้ ไม่ให้ค้างจนบันทึกไม่ได้อีก
    private void OnDisable()
    {
        isSavingToDB = false;
        saveAgainAfterThis = false;
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
        // ⚖️ ระบบสมดุลใหม่
        waterPoolTotal = PlayerPrefs.GetInt("Plant_WaterPoolTotal", 0);
        waterPoolAbsorbed = PlayerPrefs.GetInt("Plant_WaterPoolAbsorbed", 0);
        waterPoolStartedAt = PlayerPrefs.GetString("Plant_WaterPoolStartedAt", "");
        fertUsedToday = PlayerPrefs.GetInt("Plant_FertUsedToday", 0);
        heartGainedToday = PlayerPrefs.GetInt("Plant_HeartGainedToday", 0);
        dailyCountDate = PlayerPrefs.GetString("Plant_DailyCountDate", "");
        dryPenaltyAt = PlayerPrefs.GetString("Plant_DryPenaltyAt", "");   // 🥀 ป่วยจากขาดน้ำ

        RefreshPlantDataFromMaster();
        SyncCharacterVisual();
    }
}