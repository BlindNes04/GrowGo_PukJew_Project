using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button mapButton;         // ปุ่มกดของการ์ด
    [SerializeField] private GameObject mapImage;       // รูปแมพจริง ( MapBox2 )
    [SerializeField] private GameObject lockOverlay;   // กรอบแม่กุญแจล็อค ( MapBox1 )
    [SerializeField] private GameObject timeContainer; // กล่องรูปนาฬิกา + ข้อความเวลา
    [SerializeField] private TextMeshProUGUI timerText; // ข้อความแสดงเวลา

    [Header("Unlock Date Settings")]
    [SerializeField] private int year = 2026;
    [SerializeField] private int month = 8;
    [SerializeField] private int day = 1;
    [SerializeField] private int hour = 0;
    [SerializeField] private int minute = 0;

    private DateTime unlockDateTime;

    void Start()
    {
        unlockDateTime = new DateTime(year, month, day, hour, minute, 0);
        UpdateMapStatus();
    }

    public void UpdateMapStatus()
    {
        bool isUnlocked = DateTime.Now >= unlockDateTime;

        if (isUnlocked)
        {
            // === ถึงเวลาแล้ว (ปลดล็อค) ===
            if (mapButton != null) mapButton.interactable = true;
            if (mapImage != null) mapImage.SetActive(true);           // แสดงรูปแมพจริง
            if (lockOverlay != null) lockOverlay.SetActive(false);     // ซ่อนกรอบล็อค
            if (timeContainer != null) timeContainer.SetActive(true);  // เปิดกล่องเวลาไว้
            if (timerText != null) timerText.text = "เริ่มต้นขึ้นแล้ว!"; // เปลี่ยนเป็นคำว่า "เริ่มต้นขึ้นแล้ว!"
        }
        else
        {
            // === ยังไม่ถึงเวลา (ล็อค) ===
            if (mapButton != null) mapButton.interactable = false;
            if (mapImage != null) mapImage.SetActive(false);          // ซ่อนรูปแมพจริง (การ์ดจางล่างสุดจะหายไปทันที!)
            if (lockOverlay != null) lockOverlay.SetActive(true);      // แสดงแค่กรอบล็อคอย่างเดียว
            if (timeContainer != null) timeContainer.SetActive(true);
            if (timerText != null) timerText.text = $"เริ่มต้นใน {unlockDateTime:d/M/yyyy H:mm:ss}";
        }
    }

    public void OnClickMap()
    {
        Debug.Log("กดเข้าเล่นแมพนี้แล้ว!");
    }
}