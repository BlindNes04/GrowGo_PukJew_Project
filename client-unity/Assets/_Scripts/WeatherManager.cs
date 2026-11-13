using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance;

    [Header("🖼️ Background UI Elements (ครบ 5 สภาพอากาศ)")]
    public Image backgroundDisplayImage;
    public Sprite clearBGSprite; // สำหรับ WTH_CLEAR (รูปปกติ)
    public Sprite hotBGSprite;   // สำหรับ WTH_HOT (sunny.jpg)
    public Sprite mistBGSprite;  // สำหรับ WTH_MIST (รูปหมอก)
    public Sprite rainBGSprite;  // สำหรับ WTH_RAIN (rain.jpg)
    public Sprite stormBGSprite; // สำหรับ WTH_STORM (รูปพายุ)

    [Header("🗓️ Debug & Current Info (เช็กค่าตรงนี้ใน Inspector)")]
    [Tooltip("Index วันในสัปดาห์ (0 = จันทร์, ..., 5 = เสาร์, 6 = อาทิตย์)")]
    public int currentDayIndex = -1;
    public string currentDayName = "";
    public SupabaseWeather currentWeather;

    [Header("🧪 Test Mode (สำหรับทดสอบเปลี่ยนสภาพอากาศ)")]
    public string testWeatherId = "WTH_CLEAR";

    [Header("🌤️ Master Data From DB")]
    public List<SupabaseWeather> weatherMasterList = new List<SupabaseWeather>();

    private SupabaseWeather[] weeklyWeatherSchedule = new SupabaseWeather[7];
    private DateTime lastWeatherDate = DateTime.MinValue;   // วันที่ (เวลาไทย) ที่คำนวณอากาศล่าสุด ใช้เช็คข้ามเที่ยงคืน

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 🔁 กลับเข้า MainForest อีกรอบ: ตัวจริง (DontDestroyOnLoad) ยังถือ Background ของซีนเก่าที่ถูกทำลายไปแล้ว
            //    เลยส่ง Background ของซีนใหม่ให้ตัวจริง แล้วคำนวณ/เปลี่ยนรูปตามสภาพอากาศวันนี้ใหม่ ก่อนทำลายตัวซ้ำทิ้ง
            if (backgroundDisplayImage != null)
                Instance.backgroundDisplayImage = backgroundDisplayImage;
            Instance.UpdateWeeklyWeather();

            Destroy(gameObject);
        }
    }

    // 🕛 เวลาไทย (UTC+7) ให้วันเปลี่ยนตอนเที่ยงคืนไทย เหมือน stamina / daily reward / popup นับถอยหลัง
    public static DateTime GetThaiNow()
    {
        return DateTime.UtcNow.AddHours(7);
    }

    private void Start()
    {
        FetchWeatherAndInit();
    }

    // 🕛 เปิดเกมค้างข้ามเที่ยงคืน (เวลาไทย) -> คำนวณอากาศวันใหม่ + เปลี่ยนพื้นหลัง + อัปเดต UI ต้นไม้
    private void Update()
    {
        if (lastWeatherDate == DateTime.MinValue) return;   // ยังโหลดอากาศครั้งแรกไม่เสร็จ
        if (GetThaiNow().Date == lastWeatherDate) return;  // ยังเป็นวันเดิม

        UpdateWeeklyWeather();
        if (MainPlantStatusView.Instance != null)
            MainPlantStatusView.Instance.UpdateUI();
    }

    public void FetchWeatherAndInit()
    {
        if (SupabaseManager.Instance == null)
        {
            UseFallbackMasterData();
            UpdateWeeklyWeather();
            return;
        }

        StartCoroutine(SupabaseManager.Instance.GetWeatherMasterData(
            onSuccess: (list) =>
            {
                if (list == null || list.Count == 0)
                {
                    Debug.LogWarning("[WeatherManager] ⚠️ ตาราง weather ใน DB เป็นค่าว่าง! ใช้ค่า Fallback");
                    UseFallbackMasterData();
                }
                else
                {
                    weatherMasterList = list;
                }

                UpdateWeeklyWeather();
            },
            onError: (err) =>
            {
                Debug.LogError($"[WeatherManager] 🔴 โหลดตาราง weather ล้มเหลว: {err} -> ใช้ค่า Fallback");
                UseFallbackMasterData();
                UpdateWeeklyWeather();
            }
        ));
    }

    private void UseFallbackMasterData()
    {
        weatherMasterList = new List<SupabaseWeather>
        {
            new SupabaseWeather { weather_id = "WTH_CLEAR", name = "Clear Day", exp_multiplier = 1.0f, auto_water = false, water_damage_chance = 0.0f, heat_damage_chance = 0.05f },
            new SupabaseWeather { weather_id = "WTH_HOT", name = "Heat Wave", exp_multiplier = 1.4f, auto_water = false, water_damage_chance = 0.0f, heat_damage_chance = 0.15f },
            new SupabaseWeather { weather_id = "WTH_MIST", name = "Mystic Mist", exp_multiplier = 1.0f, relationship_multiplier = 1.1f, auto_water = false, water_damage_chance = 0.05f, heat_damage_chance = 0.0f },
            new SupabaseWeather { weather_id = "WTH_RAIN", name = "Light Rain", exp_multiplier = 1.2f, auto_water = true, water_damage_chance = 0.10f, heat_damage_chance = 0.0f },
            new SupabaseWeather { weather_id = "WTH_STORM", name = "Heavy Storm", exp_multiplier = 1.5f, auto_water = true, water_damage_chance = 0.30f, heat_damage_chance = 0.0f }
        };
    }

    public void UpdateWeeklyWeather()
    {
        if (weatherMasterList == null || weatherMasterList.Count == 0) return;

        DateTime nowUtc = GetThaiNow();   // เวลาไทย (ชื่อตัวแปร nowUtc เป็นชื่อเก่า)

        // 🟢 คำนวณ Day Index (จันทร์ = 0, ..., เสาร์ = 5, อาทิตย์ = 6)
        currentDayIndex = ((int)nowUtc.DayOfWeek + 6) % 7;
        currentDayName = nowUtc.DayOfWeek.ToString();

        DateTime mondayOfWeek = nowUtc.Date.AddDays(-currentDayIndex);

        // 🎲 สุ่มสร้างตาราง 7 วันด้วย UTC Seed
        GenerateScheduleFromUtcSeed(mondayOfWeek);

        currentWeather = weeklyWeatherSchedule[currentDayIndex];
        lastWeatherDate = nowUtc.Date;   // จำว่าคำนวณของวันนี้แล้ว (ใช้ใน Update)
        ApplyWeatherVisuals();

        Debug.Log($"[WeatherManager] 🌍 สภาพอากาศวันนี้ (Day Index {currentDayIndex} - {currentDayName}): {currentWeather.name} ({currentWeather.weather_id})");
    }

    /// <summary>
    /// 🟢 ฟังก์ชันสำหรับสคริปต์อื่น (เช่น WeatherPopupController) มาเรียกดูสภาพอากาศประจำวัน
    /// </summary>
    public SupabaseWeather GetWeatherForDayIndex(int dayIndex)
    {
        if (weeklyWeatherSchedule != null && dayIndex >= 0 && dayIndex < weeklyWeatherSchedule.Length)
        {
            return weeklyWeatherSchedule[dayIndex];
        }
        return null;
    }

    /// <summary>
    /// 🔮 ดูอากาศของ "อีก N วันข้างหน้า" ข้ามไปสัปดาห์หน้าได้
    /// (ใช้แทน GetWeatherForDayIndex ที่วน % 7 แล้วปลายสัปดาห์จะไปได้วันต้นสัปดาห์นี้ที่ผ่านไปแล้ว)
    /// </summary>
    public SupabaseWeather GetWeatherForDaysAhead(int daysAhead)
    {
        int targetIndex = currentDayIndex + daysAhead;
        if (targetIndex < 7) return GetWeatherForDayIndex(targetIndex);   // ยังอยู่สัปดาห์นี้

        if (weatherMasterList == null || weatherMasterList.Count == 0) return null;

        // ข้ามไปสัปดาห์หน้า: สุ่มตารางสัปดาห์หน้าชั่วคราว (seed เดียวกับที่จะใช้จริงในสัปดาห์หน้า) แล้วคืนตารางสัปดาห์นี้
        SupabaseWeather[] thisWeek = (SupabaseWeather[])weeklyWeatherSchedule.Clone();
        DateTime nextMonday = GetThaiNow().Date.AddDays(-currentDayIndex + 7 * (targetIndex / 7));
        GenerateScheduleFromUtcSeed(nextMonday);
        SupabaseWeather result = weeklyWeatherSchedule[targetIndex % 7];
        weeklyWeatherSchedule = thisWeek;
        return result;
    }

    /// <summary>
    /// 🎲 Clear เป็น Default (ปกติ 4 วัน) สุ่มวันพิเศษใส่ 3 วัน
    /// </summary>
    private void GenerateScheduleFromUtcSeed(DateTime mondayUtcDate)
    {
        int seed = mondayUtcDate.Year * 10000 + mondayUtcDate.Month * 100 + mondayUtcDate.Day;
        UnityEngine.Random.InitState(seed);

        SupabaseWeather clearWeather = weatherMasterList.Find(w => w.weather_id == "WTH_CLEAR") ?? weatherMasterList[0];

        // 1. ตั้งทั้ง 7 วันให้เป็น Clear (Default) ทั้งหมดก่อน
        for (int i = 0; i < 7; i++)
        {
            weeklyWeatherSchedule[i] = clearWeather;
        }

        List<int> availableDays = new List<int> { 0, 1, 2, 3, 4, 5, 6 };

        // 2. สุ่มล็อกวัน HOT 1 วัน
        AssignWeatherToRandomDay("WTH_HOT", availableDays);

        // 3. สุ่มล็อกวัน MIST 1 วัน
        AssignWeatherToRandomDay("WTH_MIST", availableDays);

        // 4. สุ่มล็อกวัน RAIN หรือ STORM 1 วัน
        string rainType = (UnityEngine.Random.value > 0.3f) ? "WTH_RAIN" : "WTH_STORM";
        AssignWeatherToRandomDay(rainType, availableDays);

        // คืนค่า Random State
        UnityEngine.Random.InitState((int)DateTime.Now.Ticks);
    }

    private void AssignWeatherToRandomDay(string weatherId, List<int> availableDays)
    {
        if (availableDays.Count == 0) return;

        SupabaseWeather targetWeather = weatherMasterList.Find(w => w.weather_id == weatherId);
        if (targetWeather == null) return;

        int randomIndex = UnityEngine.Random.Range(0, availableDays.Count);
        int selectedDay = availableDays[randomIndex];

        weeklyWeatherSchedule[selectedDay] = targetWeather;
        availableDays.RemoveAt(randomIndex);
    }

    /// <summary>
    /// 🎨 เปลี่ยนภาพ Background ตาม weather_id ทั้ง 5 แบบ
    /// </summary>
    public void ApplyWeatherVisuals()
    {
        if (backgroundDisplayImage == null || currentWeather == null) return;

        switch (currentWeather.weather_id)
        {
            case "WTH_HOT":
                if (hotBGSprite != null) backgroundDisplayImage.sprite = hotBGSprite;
                break;

            case "WTH_MIST":
                if (mistBGSprite != null) backgroundDisplayImage.sprite = mistBGSprite;
                else if (clearBGSprite != null) backgroundDisplayImage.sprite = clearBGSprite; // Fallback ถ้ายังไม่มีรูปหมอก
                break;

            case "WTH_RAIN":
                if (rainBGSprite != null) backgroundDisplayImage.sprite = rainBGSprite;
                break;

            case "WTH_STORM":
                if (stormBGSprite != null) backgroundDisplayImage.sprite = stormBGSprite;
                else if (rainBGSprite != null) backgroundDisplayImage.sprite = rainBGSprite; // Fallback ถ้ายังไม่มีรูปพายุ
                break;

            case "WTH_CLEAR":
            default:
                if (clearBGSprite != null) backgroundDisplayImage.sprite = clearBGSprite;
                break;
        }
    }

    // ==================== 🧪 TEST & INSPECTOR BUTTONS ====================

    /// <summary>
    /// 🧪 กดเปลี่ยนสภาพอากาศเพื่อทดสอบใน Editor (พิมพ์ weather_id ในช่อง testWeatherId แล้วกดปุ่มนี้)
    /// </summary>
    public void ApplyTestWeather()
    {
        if (string.IsNullOrEmpty(testWeatherId)) return;
        SetWeatherForTest(testWeatherId);
    }

    public void SetWeatherForTest(string weatherId)
    {
        if (weatherMasterList == null || weatherMasterList.Count == 0) UseFallbackMasterData();

        SupabaseWeather target = weatherMasterList.Find(w => w.weather_id == weatherId);
        if (target != null)
        {
            currentWeather = target;
            ApplyWeatherVisuals();
            Debug.Log($"[WeatherTest] 🧪 สลับสภาพอากาศเป็น: {target.name} ({target.weather_id})");

            if (MainPlantStatusView.Instance != null)
            {
                MainPlantStatusView.Instance.UpdateUI();
            }
        }
        else
        {
            Debug.LogWarning($"[WeatherTest] ⚠️ ไม่พบ weather_id: {weatherId}");
        }
    }

    [ContextMenu("Test: Set WTH_CLEAR (ปกติ)")]
    public void TestSetClear() { testWeatherId = "WTH_CLEAR"; ApplyTestWeather(); }

    [ContextMenu("Test: Set WTH_HOT (แดดจัด)")]
    public void TestSetHot() { testWeatherId = "WTH_HOT"; ApplyTestWeather(); }

    [ContextMenu("Test: Set WTH_MIST (หมอก)")]
    public void TestSetMist() { testWeatherId = "WTH_MIST"; ApplyTestWeather(); }

    [ContextMenu("Test: Set WTH_RAIN (ฝนตก)")]
    public void TestSetRain() { testWeatherId = "WTH_RAIN"; ApplyTestWeather(); }

    [ContextMenu("Test: Set WTH_STORM (พายุ)")]
    public void TestSetStorm() { testWeatherId = "WTH_STORM"; ApplyTestWeather(); }

    [ContextMenu("Test: Print 7 Days Schedule Log")]
    public void TestLog7DaysSchedule()
    {
        if (weatherMasterList == null || weatherMasterList.Count == 0) UseFallbackMasterData();

        DateTime nowUtc = GetThaiNow();   // เวลาไทย ให้ตรงกับ UpdateWeeklyWeather
        int dayIndex = ((int)nowUtc.DayOfWeek + 6) % 7;
        DateTime monday = nowUtc.Date.AddDays(-dayIndex);

        GenerateScheduleFromUtcSeed(monday);

        string[] days = { "จันทร์", "อังคาร", "พุธ", "พฤหัส", "ศุกร์", "เสาร์", "อาทิตย์" };
        Debug.Log("================ 🌤️ ตารางสภาพอากาศ 7 วันสัปดาห์นี้ ================");
        for (int i = 0; i < 7; i++)
        {
            Debug.Log($"[{i}] วัน{days[i]}: {weeklyWeatherSchedule[i].name} ({weeklyWeatherSchedule[i].weather_id})");
        }
        Debug.Log("=========================================================");
    }
}