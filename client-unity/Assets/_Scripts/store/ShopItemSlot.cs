using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ==================== ShopItemSlot (ช่องของ 1 ชิ้นในร้าน) ====================
// ใช้เป็น prefab ใน ScrollView ของหน้าร้าน (PlantStore)
// PlantStoreController สร้างช่องนี้ตามจำนวนแถวในตาราง shop_item แล้วเรียก Setup()
public class ShopItemSlot : MonoBehaviour
{
    [Header("UI ในช่อง (ลากจาก prefab)")]
    public Image iconImage;              // รูปของ
    public TextMeshProUGUI nameText;     // ชื่อของ
    public TextMeshProUGUI descText;     // คำอธิบาย / โอกาสสุ่ม (ไม่ใส่ก็ได้)
    public TextMeshProUGUI priceText;    // ราคา
    public Image currencyIcon;           // ไอคอนแต้มที่ใช้ซื้อ (ดอกไม้ / ใบไม้เขียว)
    public Button buyButton;             // ปุ่มซื้อ

    private SupabaseManager.ShopItemData data;
    private Action<SupabaseManager.ShopItemData> onBuyClicked;

    // ---------- ตั้งค่าช่อง ----------
    public void Setup(SupabaseManager.ShopItemData itemData, Sprite icon, Sprite currency, string description, Action<SupabaseManager.ShopItemData> onBuy)
    {
        data = itemData;
        onBuyClicked = onBuy;

        if (nameText != null) nameText.text = itemData.item_name;
        if (priceText != null) priceText.text = itemData.price_amount.ToString();
        if (descText != null) descText.text = description;

        if (iconImage != null && icon != null) iconImage.sprite = icon;
        if (currencyIcon != null && currency != null) currencyIcon.sprite = currency;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuyClicked?.Invoke(data));
        }
    }

    // ---------- เปิด/ปิดปุ่ม (ปิดไว้ระหว่างรอ DB กันกดซื้อซ้อน) ----------
    public void SetInteractable(bool value)
    {
        if (buyButton != null) buyButton.interactable = value;
    }
}
