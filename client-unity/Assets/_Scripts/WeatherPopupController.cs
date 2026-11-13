using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public class WeatherDisplayInfo
{
    public string weatherId;       // เช่น WTH_HOT, WTH_RAIN, WTH_STORM
    public string eventCustomName; // ชื่ออีเวนต์ที่อยากให้โชว์บน UI (เช่น คลื่นความร้อนระอุ)
    [TextArea] public string customDescription; // รายละเอียดและคำแนะนำที่จะให้โชว์
}

public class WeatherPopupController : MonoBehaviour
{
    public static WeatherPopupController Instance;

    [Header("UI Elements ใน Pop-up")]
    public GameObject popupContainer;        // GameObject กรอบ Pop-up ทั้งหมด
    public TextMeshProUGUI weatherTitleText; // ชื่อสภาพอากาศ
    public TextMeshProUGUI weatherDescText;  // รายละเอียดเอฟเฟกต์
    public TextMeshProUGUI timeLeftText;     // เวลาถอยหลัง หรือ วันที่จะเริ่ม
    public Button closeButton;               // ปุ่มกากบาทปิด

    [Header("🔗 ปุ่มกดเปิด Pop-up (มุมจอหลัก)")]
    public Button openWeatherButton;         // 🟢 ลากปุ่ม Event หน้าจอหลักมาใส่ช่องนี้

    [Header("✍️ กำหนดชื่อ Event และคำอธิบายตามใจชอบ (ตั้งค่าใน Inspector)")]
    public List<WeatherDisplayInfo> customWeatherList = new List<WeatherDisplayInfo>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // ผูกฟังก์ชันปุ่มปิด
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
            closeButton.onClick.AddListener(ClosePopup);
        }

        // ผูกฟังก์ชันปุ่มเปิด (กดแล้วเรียก OpenWeatherPopup)
        if (openWeatherButton != null)
        {
            openWeatherButton.onClick.RemoveListener(OpenWeatherPopup);
            openWeatherButton.onClick.AddListener(OpenWeatherPopup);
        }

        if (popupContainer != null) popupContainer.SetActive(false);
    }

    private void Update()
    {
        // อัปเดตเวลาเฉพาะตอนที่ Pop-up เปิดอยู่ และวันนี้มีสภาพอากาศพิเศษ
        if (popupContainer != null && popupContainer.activeSelf)
        {
            UpdateWeatherStatusText();
        }
    }

    /// <summary>
    /// 🟢 เปิด Pop-up แสดงข้อมูลสภาพอากาศ
    /// </summary>
    public void OpenWeatherPopup()
    {
        if (WeatherManager.Instance == null) return;

        var currentWeather = WeatherManager.Instance.currentWeather;

        if (currentWeather != null && currentWeather.weather_id != "WTH_CLEAR")
        {
            // เคสที่ 1: กำลังเกิด Weather พิเศษอยู่ (เช่น HOT, RAIN ฯลฯ)
            WeatherDisplayInfo info = customWeatherList.Find(x => x.weatherId == currentWeather.weather_id);

            if (weatherTitleText != null)
                weatherTitleText.text = info != null && !string.IsNullOrEmpty(info.eventCustomName) ? info.eventCustomName : currentWeather.name;

            if (weatherDescText != null)
                weatherDescText.text = info != null && !string.IsNullOrEmpty(info.customDescription) ? info.customDescription : GetWeatherDescription(currentWeather.weather_id);
        }
        else
        {
            // เคสที่ 2: วันนี้อากาศปกติ (CLEAR) -> ค้นหาว่าสภาพอากาศพิเศษอันถัดไปจะมาในอีกกี่วัน
            FindNextUpcomingWeatherEvent();
        }

        if (popupContainer != null)
        {
            popupContainer.SetActive(true);
        }
    }

    public void ClosePopup()
    {
        if (popupContainer != null)
        {
            popupContainer.SetActive(false);
        }
    }

    /// <summary>
    /// ⏱️ อัปเดตข้อความเวลา (ถ้ากำลังเกิดอีเวนต์ นับถอยหลังถึงเที่ยงคืน)
    /// </summary>
    private void UpdateWeatherStatusText()
    {
        if (WeatherManager.Instance == null || timeLeftText == null) return;

        var currentWeather = WeatherManager.Instance.currentWeather;

        // ถ้าวันนี้อากาศปกติ ไม่ต้องนับถอยหลังรายชั่วโมง (เพราะแสดงวันที่จะมาถึงแทนแล้ว)
        if (currentWeather == null || currentWeather.weather_id == "WTH_CLEAR") return;

        DateTime now = WeatherManager.GetThaiNow();   // เวลาไทย ให้ตรงกับวันที่ WeatherManager ใช้
        DateTime midnight = now.Date.AddDays(1); // เที่ยงคืนของวันถัดไป
        TimeSpan remaining = midnight - now;

        if (remaining.TotalSeconds <= 0)
        {
            timeLeftText.text = "กำลังเปลี่ยนสภาพอากาศ...";
            return;
        }

        int hours = remaining.Hours;
        int minutes = remaining.Minutes;

        timeLeftText.text = $"จบลงใน {hours} ชม. {minutes} น.";
    }

    /// <summary>
    /// 🔍 ค้นหาว่าสภาพอากาศพิเศษถัดไปในสัปดาห์นี้คืออะไร และอีกกี่วันจะถึง
    /// </summary>
    private void FindNextUpcomingWeatherEvent()
    {
        if (WeatherManager.Instance == null) return;

        DateTime nowUtc = WeatherManager.GetThaiNow();   // เวลาไทย ให้ตรงกับ WeatherManager
        int currentDayIndex = ((int)nowUtc.DayOfWeek + 6) % 7; // วันนี้ Index อะไร (0-6)

        // วนลูปเช็กวันถัดไป 1 ถึง 6 วันข้างหน้า
        for (int i = 1; i <= 6; i++)
        {
            int checkIndex = (currentDayIndex + i) % 7;

            // ดึงสภาพอากาศของวันนั้นมาจาก WeatherManager
            // ใช้ GetWeatherForDaysAhead ให้ดูข้ามไปสัปดาห์หน้าได้
            SupabaseWeather targetWeather = WeatherManager.Instance.GetWeatherForDaysAhead(i);

            // ถ้าเจอวันที่มีสภาพอากาศพิเศษ (ไม่ใช่ WTH_CLEAR)
            if (targetWeather != null && targetWeather.weather_id != "WTH_CLEAR")
            {
                // ดึงชื่อและคำอธิบายภาษาไทยที่เราตั้งไว้ใน Inspector Custom List
                WeatherDisplayInfo info = customWeatherList.Find(x => x.weatherId == targetWeather.weather_id);

                string title = (info != null && !string.IsNullOrEmpty(info.eventCustomName))
                    ? info.eventCustomName
                    : targetWeather.name;

                string desc = (info != null && !string.IsNullOrEmpty(info.customDescription))
                    ? info.customDescription
                    : GetWeatherDescription(targetWeather.weather_id);

                // แสดงผลขึ้น Pop-up
                if (weatherTitleText != null) weatherTitleText.text = title;
                if (weatherDescText != null) weatherDescText.text = desc;
                if (timeLeftText != null) timeLeftText.text = $"{title} กำลังจะเริ่มขึ้นใน {i} วัน";

                return; // เจอสภาพอากาศพิเศษอันแรกสุดแล้ว แสดงผลเสร็จออกจากลูปทันที
            }
        }

        // กรณีสัปดาห์นี้ไม่มีวันไหนเป็นอีเวนต์พิเศษเลย
        if (weatherTitleText != null) weatherTitleText.text = "ท้องฟ้าแจ่มใส";
        if (weatherDescText != null) weatherDescText.text = "ไม่มีสภาพอากาศพิเศษในเร็วๆ นี้";
        if (timeLeftText != null) timeLeftText.text = "สภาพอากาศปกติยาวๆ";
    }

    private string GetWeatherDescription(string weatherId)
    {
        switch (weatherId)
        {
            case "WTH_HOT":
                return "แดดจัดระอุ! น้ำแห้งไวขึ้น 1.8x ระวังพืชเหี่ยวเฉา";
            case "WTH_MIST":
                return "หมอกลงจัด บรรยากาศเงียบสงบ เพิ่มความสัมพันธ์ดี";
            case "WTH_RAIN":
                return "ฝนตกปรอยๆ รดน้ำให้อัตโนมัติ! แต่ระวังพืชป่วยนะ";
            case "WTH_STORM":
                return "พายุฝนฟ้าคะนองรุนแรง! รดน้ำให้อัตโนมัติ แต่ระวังน้องป่วยสูง";
            case "WTH_CLEAR":
            default:
                return "ท้องฟ้าแจ่มใส เหมาะกับการปลูกต้นไม้ตามปกติ";
        }
    }
}