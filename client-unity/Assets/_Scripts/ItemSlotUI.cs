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
    private System.Action<int> onClickCallback; // ใช้ตอนไม่มี MainInventoryManager เช่นหน้าผสมปุ๋ย

    // 💡 ใช้ในหน้าผสมปุ๋ย: กดช่องแล้วเรียก callback แทน MainInventoryManager
    public void SetupSlotWithCallback(int index, Sprite icon, Sprite frame, int amount, System.Action<int> onClick)
    {
        SetupSlot(index, icon, frame, amount, (MainInventoryManager)null);
        onClickCallback = onClick;
    }

    // 💡 ฟังก์ชันเซ็ตค่าข้อมูลช่อง Slot
    public void SetupSlot(int index, Sprite icon, Sprite frame, int amount, MainInventoryManager mainManager)
    {
        slotIndex = index;
        manager = mainManager;
        onClickCallback = null;

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

    // ==========================================
    // 📍 ตัวช่วยสำหรับ popup รายละเอียดไอเทม (ใช้ร่วมกันหลายหน้า)
    // ==========================================

    // วาง popup ไว้ใต้ช่องที่เลือก ถ้าข้างล่างไม่พอจะย้ายไปไว้ข้างบน และไม่ให้ล้นกรอบของ parent
    public static void PlacePopupNear(RectTransform popup, RectTransform target, float gap = 12f)
    {
        if (popup == null || target == null) return;
        RectTransform parent = popup.parent as RectTransform;
        if (parent == null) return;

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners); // 0 = ล่างซ้าย, 2 = บนขวา
        Vector2 bottomLeft = parent.InverseTransformPoint(corners[0]);
        Vector2 topRight = parent.InverseTransformPoint(corners[2]);

        Vector2 size = Vector2.Scale(popup.rect.size, (Vector2)popup.localScale);
        Rect area = parent.rect;

        float centerX = (bottomLeft.x + topRight.x) * 0.5f;
        float top = bottomLeft.y - gap;                 // ขอบบนของ popup = ใต้ช่องลงมานิดนึง
        if (top - size.y < area.yMin)                   // ข้างล่างไม่พอ -> ไปอยู่ข้างบนช่อง
        {
            top = Mathf.Min(topRight.y + gap + size.y, area.yMax);
        }

        float left = Mathf.Clamp(centerX - size.x * 0.5f, area.xMin, Mathf.Max(area.xMin, area.xMax - size.x));

        Vector2 pivot = popup.pivot;
        Vector3 local = popup.localPosition;
        local.x = left + size.x * pivot.x;
        local.y = top - size.y * (1f - pivot.y);
        popup.localPosition = local;
    }

    // เช็คว่านิ้ว/เมาส์อยู่ในกรอบ rect นี้ไหม
    public static bool IsPointerInside(RectTransform rect, Vector2 screenPoint)
    {
        if (rect == null) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
    }

    private void OnSlotClicked()
    {
        if (manager != null)
        {
            manager.OnClickItemInGrid(slotIndex);
        }
        else
        {
            onClickCallback?.Invoke(slotIndex);
        }
    }
} 