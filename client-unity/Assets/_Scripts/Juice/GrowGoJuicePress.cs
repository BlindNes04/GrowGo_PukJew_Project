using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
//  GrowGoJuicePress : รับการกด ติดเฉพาะ GameObject ที่มี Button อยู่แล้ว (ไม่เปลี่ยนว่าใครรับการแตะ)
//  + เสียงติ๊กตอนกด (เล่นจากตรงนี้ ไม่ใช่ onClick เพราะบางสคริปต์ของเกม RemoveAllListeners ทิ้ง)
// =====================================================================================
public class GrowGoJuicePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.NonSerialized] public bool clickSound;    // true = เล่นเสียงติ๊กปกติ
    [System.NonSerialized] public bool customSound;   // true = มีสคริปต์อื่นเล่นเสียงเอง (เช่น ปุ่มคำตอบใน Quiz)

    private GrowGoJuiceScale scale;
    private Button button;
    private GrowGoJuiceScale S { get { if (scale == null) scale = GetComponent<GrowGoJuiceScale>(); return scale; } }
    public void OnPointerDown(PointerEventData e) { if (S != null) S.PressDown(); }
    public void OnPointerUp(PointerEventData e) { if (S != null) S.PressUp(e.dragging); }
    public void OnPointerExit(PointerEventData e) { if (S != null) S.PressCancel(); }

    public void OnPointerClick(PointerEventData e)
    {
        if (!clickSound || customSound || e.button != PointerEventData.InputButton.Left) return;
        if (button == null) button = GetComponent<Button>();
        if (button != null && !button.IsInteractable()) return;
        if (GrowGoJuice.I != null) GrowGoJuice.I.PlayClick();
    }
}
