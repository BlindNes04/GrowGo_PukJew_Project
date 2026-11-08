using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FertilizerDexCard : MonoBehaviour
{
    public Image fertilizerIcon;
    public TextMeshProUGUI fertilizerName;
    public TextMeshProUGUI fertilizerDesc;

    [Header("Ingredients")]
    public Image[] ingredientIcons; // อาร์เรย์เก็บรูปรองรับวัตถุดิบ 2-3 ช่อง

    public void SetupCard(string fName, string fDesc, Sprite fSprite, Sprite[] ingSprites, bool isUnlocked)
    {
        fertilizerIcon.sprite = fSprite;

        if (isUnlocked)
        {
            fertilizerIcon.color = Color.white; // สีปกติ
            fertilizerName.text = fName;
            fertilizerDesc.text = fDesc;
        }
        else
        {
            fertilizerIcon.color = Color.black; // เงาดำ
            fertilizerName.text = "???";
            fertilizerDesc.text = "สูตรลับที่ยังไม่ถูกค้นพบ";
        }

        // เซ็ตรูปวัตถุดิบ (ถ้ามี)
        for (int i = 0; i < ingredientIcons.Length; i++)
        {
            if (i < ingSprites.Length && ingSprites[i] != null)
            {
                ingredientIcons[i].sprite = ingSprites[i];
                ingredientIcons[i].gameObject.SetActive(true);
            }
            else
            {
                ingredientIcons[i].gameObject.SetActive(false); // ซ่อนช่องที่ไม่ได้ใช้
            }
        }
    }
}