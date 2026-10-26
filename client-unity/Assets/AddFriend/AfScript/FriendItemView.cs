using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendItemView : MonoBehaviour
{
    [Header("UI Components")]
    public Image profilePicture;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;       // จะโชว์แค่เลข เพราะเราจะเซ็ต Prefix เป็น "Lv."
    public Button addFriendButton;
    public Image addBtnIcon;                // รูปไอคอนในปุ่มแอดเพื่อน (สำหรับเปลี่ยนสีเทา)

    [Header("Button Faded State Settings")]
    public Color sentButtonColor = new Color(0.6f, 0.6f, 0.6f, 0.5f); // สีเทาจางๆ ตอนกดแอดแล้ว

    private string currentUserId;
    private bool isRequestSent = false;

    // 💡 ฟังก์ชันใส่ข้อมูลจาก DB เข้า Prefab
    public void SetData(string userId, string userName, int level, Sprite avatarSprite, System.Action<string> onAddFriendCallback)
    {
        currentUserId = userId;
        nameText.text = userName;
        
        // 💡 ล็อคคำว่า "Lv." ไว้ด้านหน้า เปลี่ยนแค่ตัวเลขด้านหลัง
        levelText.text = $"Lv.{level}";

        if (avatarSprite != null)
        {
            profilePicture.sprite = avatarSprite;
        }

        // รีเซ็ตปุ่มให้อยู่ในสถานะปกติก่อน
        ResetButtonState();

        // สั่งทำงานเมื่อกดปุ่มแอดเพื่อน
        addFriendButton.onClick.RemoveAllListeners();
        addFriendButton.onClick.AddListener(() =>
        {
            if (!isRequestSent)
            {
                SetRequestSentState();
                onAddFriendCallback?.Invoke(currentUserId);
            }
        });
    }

    // 💡 ปรับปุ่มให้เป็นสีเทาจางๆ + กดซ้ำไม่ได้ (สถานะส่งคำขอแล้ว)
    public void SetRequestSentState()
    {
        isRequestSent = true;
        addFriendButton.interactable = false; // ปิดไม่ให้กดซ้ำ

        if (addBtnIcon != null)
        {
            addBtnIcon.color = sentButtonColor;
        }
    }

    public void ResetButtonState()
    {
        isRequestSent = false;
        addFriendButton.interactable = true;

        if (addBtnIcon != null)
        {
            addBtnIcon.color = Color.white;
        }
    }
}