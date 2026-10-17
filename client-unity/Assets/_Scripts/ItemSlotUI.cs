using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour
{
    [Header("UI Inside Slot")]
    [SerializeField] private Image frameImage;            // ลากลูก 'Frame'
    [SerializeField] private Image iconImage;             // ลากลูก 'Item'
    [SerializeField] private TextMeshProUGUI amountText;  // ลากลูก 'Amount_Text'
    [SerializeField] private GameObject bbSelection;     // ลากลูก 'BB1'
    [SerializeField] private Button slotButton;          // ลากตัวมันเอง (Slot_Crab)

    [HideInInspector] public int slotIndex;
    private MainInventoryManager manager;

    // 💡 ฟังก์ชันเซ็ตค่าข้อมูลช่อง Slot
    public void SetupSlot(int index, Sprite icon, Sprite frame, int amount, MainInventoryManager mainManager)
    {
        slotIndex = index;
        manager = mainManager;

        // 1. เซ็ตรูปไอคอน
        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        // 2. เซ็ตรูปกรอบ Frame
        if (frameImage != null && frame != null)
        {
            frameImage.sprite = frame;
            frameImage.gameObject.SetActive(true);
        }

        // 3. เซ็ตตัวเลขจำนวน (ถ้ามีมากกว่า 1 ชิ้นค่อยโชว์)
        if (amountText != null)
        {
            if (amount > 1)
            {
                amountText.text = amount.ToString();
                amountText.gameObject.SetActive(true);
            }
            else
            {
                amountText.text = "";
                amountText.gameObject.SetActive(false);
            }
        }

        // 4. ปิดกรอบดำ BB1 ไว้ก่อน
        SetSelected(false);

        // 5. ผูก Event ปุ่มกด
        if (slotButton == null) slotButton = GetComponent<Button>();
        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    // 💡 เปิด-ปิดกรอบดำ BB1 ตอนกดเลือก
    public void SetSelected(bool isSelected)
    {
        if (bbSelection != null)
        {
            bbSelection.SetActive(isSelected);
        }
    }

    private void OnSlotClicked()
    {
        if (manager != null)
        {
            manager.OnClickItemInGrid(slotIndex);
        }
    }
} 