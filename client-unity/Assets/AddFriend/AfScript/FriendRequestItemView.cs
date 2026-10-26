using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendRequestItemView : MonoBehaviour
{
    [Header("UI Components")]
    public Image profilePicture;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public Button acceptButton; // ปุ่มยอมรับ
    public Button rejectButton; // ปุ่มปฏิเสธ

    private string currentFriendshipId;
    private string requesterUserId;

    // 💡 เซ็ตข้อมูลคนส่งคำขอ + ผูกปุ่มกด ยอมรับ / ปฏิเสธ
    public void SetData(string friendshipId, string userId, string userName, int level, Sprite avatarSprite, System.Action<string, bool> onResponseCallback)
    {
        currentFriendshipId = friendshipId;
        requesterUserId = userId;
        nameText.text = userName;
        levelText.text = $"Lv.{level}";

        if (avatarSprite != null)
            profilePicture.sprite = avatarSprite;

        // กดปุ่มยอมรับ
        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(() =>
        {
            onResponseCallback?.Invoke(currentFriendshipId, true); // true = accepted
            Destroy(gameObject); // ลบแถบคำขอออกทันที
        });

        // กดปุ่มปฏิเสธ
        rejectButton.onClick.RemoveAllListeners();
        rejectButton.onClick.AddListener(() =>
        {
            onResponseCallback?.Invoke(currentFriendshipId, false); // false = rejected
            Destroy(gameObject); // ลบแถบคำขอออกทันที
        });
    }
}