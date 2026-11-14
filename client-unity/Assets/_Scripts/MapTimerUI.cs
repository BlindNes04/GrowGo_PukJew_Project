using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

// ==================== MapItemUI: การ์ดแมพตามฤดูกาล (หน้าเปลี่ยนแมพ MapChangeContainer) ====================
// เปิด/ปิดแมพตามช่วงอีเวนต์ในตาราง event ของ Supabase (เช่น ISL_MIST เปิด 1 พ.ย. - 1 ธ.ค.)
//   - ยังไม่ถึงเวลาเริ่ม   -> ล็อก + "เริ่มต้นใน ..."
//   - อยู่ในช่วงอีเวนต์    -> เปิด + "จบลงใน ..."
//   - อีเวนต์จบ / ไม่มีอีเวนต์รอบถัดไป -> ล็อก + "ยังไม่มีอีเวนต์"
// เวลาในตาราง event เป็นเวลาไทย (Asia/Bangkok) และเทียบกับเวลาไทยเสมอ
// โหลด DB ไม่ได้ (เน็ตหลุด) -> ใช้วันที่ตั้งใน Inspector ด้านล่างแทน
public class MapItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button mapButton;         // ปุ่มกดของการ์ด
    [SerializeField] private GameObject mapImage;       // รูปแมพจริง ( MapBox2 )
    [SerializeField] private GameObject lockOverlay;   // กรอบแม่กุญแจล็อค ( MapBox1 )
    [SerializeField] private GameObject timeContainer; // กล่องรูปนาฬิกา + ข้อความเวลา
    [SerializeField] private TextMeshProUGUI timerText; // ข้อความแสดงเวลา

    [Header("📅 อีเวนต์จาก DB (ตาราง event)")]
    [Tooltip("island_id ในตาราง event เช่น ISL_MIST (ว่าง = ใช้วันที่ด้านล่างอย่างเดียว)")]
    [SerializeField] private string islandId = "ISL_MIST";

    [Header("Unlock Date Settings (ค่าสำรองถ้าโหลด DB ไม่ได้ / เวลาไทย)")]
    [SerializeField] private int year = 2026;
    [SerializeField] private int month = 8;
    [SerializeField] private int day = 1;
    [SerializeField] private int hour = 0;
    [SerializeField] private int minute = 0;

    private DateTime unlockDateTime;      // เวลาเริ่ม (เวลาไทย)
    private DateTime? endDateTime;        // เวลาจบ (เวลาไทย) null = ไม่มีวันจบ
    private bool noUpcomingEvent = false; // DB ตอบว่าไม่มีอีเวนต์ที่ยังไม่จบ

    void Awake()
    {
        unlockDateTime = new DateTime(year, month, day, hour, minute, 0);   // ค่าสำรอง ระหว่างรอ DB
    }

    // ทุกครั้งที่การ์ดถูกเปิดขึ้นมา (เปิดหน้าเปลี่ยนแมพ)
    //   1) แสดงจาก cache อีเวนต์ที่ SupabaseManager โหลดไว้ตั้งแต่หน้าโหลดทันที (ไม่กระพริบ)
    //   2) แล้วดึง DB ใหม่เงียบๆ ถ้ามีอะไรเปลี่ยนค่อยอัปเดตการ์ด
    void OnEnable()
    {
        SupabaseManager db = SupabaseManager.Instance;
        if (db != null && !string.IsNullOrEmpty(db.cachedEventsJson)) ApplyEvents(db.cachedEventsJson);
        UpdateMapStatus();

        // ให้ SupabaseManager (อยู่ข้ามฉาก ไม่ถูกปิด) เป็นคนรันการโหลด
        // ถ้าหน้านี้ถูกปิดระหว่างรอ DB จะไม่ error "Coroutine couldn't be started ... inactive"
        if (db != null && !string.IsNullOrEmpty(islandId))
        {
            db.StartCoroutine(db.RefreshEventsCache(() =>
            {
                if (this == null || string.IsNullOrEmpty(db.cachedEventsJson)) return;   // เปลี่ยนฉากไปแล้ว / โหลดไม่ได้ -> ใช้ค่าเดิม
                ApplyEvents(db.cachedEventsJson);
                UpdateMapStatus();
            }));
        }

        // ตอนโหลดฉาก หน้าเปลี่ยนแมพจะถูกเปิดแล้วซ่อนตัวเองทันที (OnEnable ทำงานตอนการ์ดถูกปิดไปแล้ว)
        // -> ยังไม่ต้องเริ่มตัวนับ รอเปิดหน้าจริงแล้ว OnEnable จะถูกเรียกอีกรอบ
        if (isActiveAndEnabled) StartCoroutine(RefreshLoop());
    }

    // อัปเดตเวลาที่เหลือ / เปิด-ปิดเองเมื่อถึงเวลา ทุก 30 วินาที ระหว่างหน้านี้เปิดอยู่ (ปิดหน้า = หยุดเอง)
    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f);
            UpdateMapStatus();
        }
    }

    // ==================== เลือกอีเวนต์ของเกาะนี้จาก cache ====================
    // จากทุกแถวในตาราง event: เอาเฉพาะของ islandId ที่ยังไม่จบ (end_time > ตอนนี้) แล้วเลือกที่เริ่มเร็วสุด
    // = รอบปัจจุบัน หรือรอบถัดไป / ไม่มีเลย = ยังไม่มีอีเวนต์
    private void ApplyEvents(string json)
    {
        List<Dictionary<string, string>> rows;
        try { rows = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json); }
        catch { return; }   // อ่านไม่ได้ -> ใช้ค่าเดิม
        if (rows == null) return;

        DateTime now = ThaiNow();
        DateTime? bestStart = null, bestEnd = null;
        foreach (var row in rows)
        {
            if (row.GetValueOrDefault("island_id") != islandId) continue;
            if (!TryParseThai(row.GetValueOrDefault("start_time"), out DateTime start)) continue;
            DateTime? end = TryParseThai(row.GetValueOrDefault("end_time"), out DateTime e) ? e : (DateTime?)null;
            if (end.HasValue && end.Value <= now) continue;   // จบไปแล้ว
            if (bestStart == null || start < bestStart.Value) { bestStart = start; bestEnd = end; }
        }

        noUpcomingEvent = bestStart == null;
        if (bestStart != null) { unlockDateTime = bestStart.Value; endDateTime = bestEnd; }
    }

    // ==================== อัปเดตการ์ดตามเวลาไทย ====================
    public void UpdateMapStatus()
    {
        DateTime now = ThaiNow();
        bool started = now >= unlockDateTime;
        bool ended = endDateTime.HasValue && now >= endDateTime.Value;
        bool isUnlocked = !noUpcomingEvent && started && !ended;

        if (isUnlocked)
        {
            // === อยู่ในช่วงอีเวนต์ (ปลดล็อค) ===
            if (mapButton != null) mapButton.interactable = true;
            if (mapImage != null) mapImage.SetActive(true);           // แสดงรูปแมพจริง
            if (lockOverlay != null) lockOverlay.SetActive(false);     // ซ่อนกรอบล็อค
            if (timeContainer != null) timeContainer.SetActive(true);
            if (timerText != null)
                timerText.text = endDateTime.HasValue ? $"จบลงใน {FormatTimeLeft(endDateTime.Value - now)}" : "เริ่มต้นขึ้นแล้ว!";
        }
        else
        {
            // === ยังไม่ถึงเวลา / อีเวนต์จบแล้ว (ล็อค) ===
            if (mapButton != null) mapButton.interactable = false;
            if (mapImage != null) mapImage.SetActive(false);          // ซ่อนรูปแมพจริง
            if (lockOverlay != null) lockOverlay.SetActive(true);      // แสดงแค่กรอบล็อค
            if (timeContainer != null) timeContainer.SetActive(true);
            if (timerText != null)
                timerText.text = (noUpcomingEvent || ended) ? "ยังไม่มีอีเวนต์" : $"เริ่มต้นใน {unlockDateTime:d/M/yyyy H:mm}";
        }
    }

    public void OnClickMap()
    {
        Debug.Log("กดเข้าเล่นแมพนี้แล้ว!");
    }

    // ==================== ตัวช่วยเรื่องเวลา ====================
    private static DateTime ThaiNow()
    {
        return DateTime.UtcNow.AddHours(7);
    }

    // เวลาจาก DB: ไม่มี timezone = ถือว่าเป็นเวลาไทยอยู่แล้ว / มี timezone (เช่น +00:00) = แปลงเป็นเวลาไทย
    private static bool TryParseThai(string text, out DateTime thaiTime)
    {
        thaiTime = DateTime.MinValue;
        if (string.IsNullOrEmpty(text)) return false;

        bool hasZone = text.EndsWith("Z") || System.Text.RegularExpressions.Regex.IsMatch(text, @"[+-]\d{2}(:?\d{2})?$");
        if (hasZone && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dto))
        {
            thaiTime = dto.UtcDateTime.AddHours(7);
            return true;
        }
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out thaiTime);
    }

    // เช่น "12 วัน 5 ชม." / "3 ชม. 20 นาที" / "15 นาที"
    private static string FormatTimeLeft(TimeSpan t)
    {
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays} วัน {t.Hours} ชม.";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ชม. {t.Minutes} นาที";
        return $"{Math.Max(1, t.Minutes)} นาที";
    }
}
