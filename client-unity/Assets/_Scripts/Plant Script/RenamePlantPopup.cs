using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RenamePlantPopup : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField nameInputField; // ช่องพิมพ์ชื่อ
    public Button confirmButton;          // ปุ่มยืนยัน
    public Button cancelButton;           // ปุ่มยกเลิก

    private PlantStatus targetPlant;

    void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    // 💡 ฟังก์ชันเปิด Popup เปลี่ยนชื่อ
    public void OpenPopup(PlantStatus plant)
    {
        targetPlant = plant;

        if (targetPlant != null && nameInputField != null)
        {
            nameInputField.text = targetPlant.plantName; // ดึงชื่อปัจจุบันมาโชว์ในช่องพิมพ์
        }

        gameObject.SetActive(true); // เปิดหน้าต่าง Popup
    }

    // 🟢 พอกดปุ่ม "ยืนยัน"
    private void OnConfirm()
    {
        if (targetPlant != null && nameInputField != null)
        {
            string newName = nameInputField.text.Trim();
            
            if (!string.IsNullOrEmpty(newName))
            {
                targetPlant.SetPlantName(newName); // บันทึกชื่อใหม่ลง PlantStatus + PlayerPrefs

                // อัปเดต UI หน้ารดน้ำทันที
                if (MainPlantStatusView.Instance != null)
                {
                    MainPlantStatusView.Instance.UpdateUI();
                }
            }
        }

        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        ClosePopup();
    }

    // 🔴 พอกดปุ่ม "ยกเลิก"
    private void OnCancel()
    {
        if (MAudioManager.instance != null)
            MAudioManager.instance.PlayClickSFX();

        ClosePopup(); // ปิด Popup โดยไม่เซฟค่าอะไร
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }
}