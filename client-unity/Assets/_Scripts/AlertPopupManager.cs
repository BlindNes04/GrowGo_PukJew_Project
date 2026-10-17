using System.Collections;
using UnityEngine;
using TMPro;

public class AlertPopupManager : MonoBehaviour
{
    public static AlertPopupManager Instance;

    [Header("Popup GameObjects")]
    public GameObject moisturePopup;     // Moisture_Popup
    public TextMeshProUGUI moistureText; // AlertText ข้างใน Moisture_Popup

    public GameObject expPopup;          // EXP_Popup
    public TextMeshProUGUI expText;

    public GameObject flowerPopup;       // Flower_Popup
    public TextMeshProUGUI flowerText;

    public GameObject relationshipPopup; // Relationship_Popup
    public TextMeshProUGUI relationshipText;

    [Header("ระยะเวลาโชว์ (วินาที)")]
    public float displayDuration = 2.5f;

    void Awake()
    {
        Instance = this;
        HideAll();
    }

    public void HideAll()
    {
        if (moisturePopup != null) moisturePopup.SetActive(false);
        if (expPopup != null) expPopup.SetActive(false);
        if (flowerPopup != null) flowerPopup.SetActive(false);
        if (relationshipPopup != null) relationshipPopup.SetActive(false);
    }

    // 💡 ฟังก์ชันสั่งเปิด Alert พร้อมข้อความรูปแบบเต็ม (+10 ความชุ่มชื้น)
    public void ShowAlerts(float moisture = 0, int exp = 0, int flower = 0, int relationship = 0)
    {
        // 1. ความชุ่มชื้น
        if (moisture > 0 && moisturePopup != null)
        {
            if (moistureText != null) 
                moistureText.text = $"+{moisture} ความชุ่มชื้น"; // 👈 ตรงตามรูปมึงเป๊ะๆ
            
            StartCoroutine(ShowAndHideRoutine(moisturePopup));
        }

        // 2. EXP
        if (exp > 0 && expPopup != null)
        {
            if (expText != null) 
                expText.text = $"+{exp} ค่าประสบการณ์";
            
            StartCoroutine(ShowAndHideRoutine(expPopup));
        }

        // 3. ดอกไม้
        if (flower > 0 && flowerPopup != null)
        {
            if (flowerText != null) 
                flowerText.text = $"+{flower} แค้มดอกไม้";
            
            StartCoroutine(ShowAndHideRoutine(flowerPopup));
        }

        // 4. ความสัมพันธ์
        if (relationship > 0 && relationshipPopup != null)
        {
            if (relationshipText != null) 
                relationshipText.text = $"+{relationship} ค่าความสัมพันธ์";
            
            StartCoroutine(ShowAndHideRoutine(relationshipPopup));
        }
    }

    private IEnumerator ShowAndHideRoutine(GameObject popup)
    {
        popup.SetActive(true);

        Animator anim = popup.GetComponent<Animator>();
        if (anim != null)
        {
            anim.Play(0, -1, 0f);
        }

        yield return new WaitForSeconds(displayDuration);

        popup.SetActive(false);
    }
}