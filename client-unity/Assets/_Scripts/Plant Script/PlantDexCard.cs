using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlantDexCard : MonoBehaviour
{
    public Image plantIcon;
    public TextMeshProUGUI plantNameText;

    public void SetupCard(string name, Sprite spriteIcon, bool isUnlocked)
    {
        if (plantIcon != null)
        {
            plantIcon.sprite = spriteIcon;

            // 🟢 ถ้าปลดล็อกแล้ว = สีปกติ (Color.white), ถ้าย่านไม่ได้ = เงาดำ (Color.black หรือ เทาเข้ม)
            plantIcon.color = isUnlocked ? Color.white : new Color(0.15f, 0.15f, 0.15f, 1f);
        }

        if (plantNameText != null)
        {
            // 🟢 ถ้าปลดล็อกแล้ว = โชว์ชื่อพืช, ถ้าย่านไม่ได้ = โชว์ "???"
            plantNameText.text = isUnlocked ? name : "???";
        }
    }
}