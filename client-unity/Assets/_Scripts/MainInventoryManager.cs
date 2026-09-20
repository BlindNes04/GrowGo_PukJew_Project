using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainInventoryManager : MonoBehaviour
{
    [System.Serializable]
    public struct ItemData
    {
        public string itemName;
        public string tagText;             // ข้อความที่จะเด้งใต้ช่อง
        [TextArea(2, 5)]
        public string description;
        public string location;
        public Sprite itemSprite;
        public Sprite hintSprite;
    }

    [Header("Item Detail UI")]
    public GameObject itemDetailBox;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text locationText;
    public Image detailIconImage;
    public Image hintIconImage;

    [Header("Popup Floating Text")]
    public TMP_Text floatingText;          // ลาก FloatingText ที่อยู่นอก Mask มาใส่
    public float textOffsetY = -55f;       // ขยับลงมาอยู่ใต้ช่อง
    public float popupDuration = 1.8f;

    [Header("Item Grid Container")]
    public Transform itemGrid;

    [Header("Item Database")]
    public ItemData[] items;

    private int currentSelectedItem = -1;
    private Coroutine hideTextCoroutine;

    public void OnClickItemInGrid(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= items.Length) return;

        if (currentSelectedItem == itemIndex)
        {
            CloseDetail();
            return;
        }

        currentSelectedItem = itemIndex;

        if (nameText != null) nameText.text = items[itemIndex].itemName;
        if (descText != null) descText.text = items[itemIndex].description;
        if (locationText != null) locationText.text = items[itemIndex].location;

        if (detailIconImage != null && items[itemIndex].itemSprite != null)
        {
            detailIconImage.sprite = items[itemIndex].itemSprite;
            detailIconImage.gameObject.SetActive(true);
        }

        if (hintIconImage != null && items[itemIndex].hintSprite != null)
        {
            hintIconImage.sprite = items[itemIndex].hintSprite;
        }

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(true);
        }
    }

    public void OnClickSelectButton()
    {
        if (currentSelectedItem == -1) return;

        ShowFloatingTextPopup(currentSelectedItem);

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(false);
        }

        currentSelectedItem = -1;
    }

    private void ShowFloatingTextPopup(int itemIndex)
    {
        if (floatingText == null || itemGrid == null) return;
        if (itemIndex >= itemGrid.childCount) return;

        Transform targetSlot = itemGrid.GetChild(itemIndex);

        // วาร์ปตำแหน่งไปตรงกับช่องไอเทม โดยไม่ต้องย้ายเข้าไปเป็นลูกใน Mask
        floatingText.transform.position = targetSlot.position;
        floatingText.transform.localPosition += new Vector3(0, textOffsetY, 0);
        floatingText.transform.SetAsLastSibling(); // ดึงขึ้นมาอยู่หน้าสุด

        string textToShow = !string.IsNullOrEmpty(items[itemIndex].tagText)
            ? items[itemIndex].tagText
            : items[itemIndex].itemName;

        floatingText.text = textToShow;
        floatingText.gameObject.SetActive(true);

        if (hideTextCoroutine != null)
        {
            StopCoroutine(hideTextCoroutine);
        }
        hideTextCoroutine = StartCoroutine(HideTextRoutine());
    }

    private IEnumerator HideTextRoutine()
    {
        yield return new WaitForSeconds(popupDuration);
        if (floatingText != null)
        {
            floatingText.gameObject.SetActive(false);
        }
    }

    public void CloseDetail()
    {
        currentSelectedItem = -1;

        if (itemDetailBox != null)
        {
            itemDetailBox.SetActive(false);
        }
    }

    private void OnDisable()
    {
        CloseDetail();
        if (floatingText != null)
        {
            floatingText.gameObject.SetActive(false);
        }
    }
}