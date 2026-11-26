using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MyFriendItemView : MonoBehaviour
{
    [Header("UI Components")]
    public Image profilePicture;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;

    [Header("Action Buttons")]
    public Button visitButton;
    public Button moreButton;
    public Button deleteButton;

    private string currentFriendUserId;
    private string currentFriendshipId;

    // ==================== ยืนยันก่อนลบเพื่อน (กด 2 ครั้งภายใน 3 วินาที) ====================
    private float deleteArmedUntil = -1f;
    private TextMeshProUGUI deleteLabel;
    private string deleteLabelOriginal;

    private void ArmDelete(bool armed)
    {
        deleteArmedUntil = armed ? Time.unscaledTime + 3f : -1f;
        if (deleteLabel == null && deleteButton != null)
        {
            deleteLabel = deleteButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (deleteLabel != null) deleteLabelOriginal = deleteLabel.text;
        }
        if (deleteLabel != null) deleteLabel.text = armed ? "ยืนยันลบ?" : deleteLabelOriginal;
    }

    void Update()
    {
        if (deleteArmedUntil > 0f && Time.unscaledTime > deleteArmedUntil) ArmDelete(false);
    }

    void Awake()
    {
        // Hide delete button on initialization
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(false);
        }
    }

    // Populates item UI and binds button click handlers
    public void SetData(
        string friendUserId, 
        string friendshipId, 
        string username, 
        int level, 
        Sprite avatarSprite, 
        Action<string> onVisitCallback, 
        Action<string, GameObject> onDeleteCallback)
    {
        currentFriendUserId = friendUserId;
        currentFriendshipId = friendshipId;

        nameText.text = username;
        levelText.text = $"Lv.{level}";

        if (avatarSprite != null)
        {
            profilePicture.sprite = avatarSprite;
        }

        // Toggle delete button visibility when clicking more button
        moreButton.onClick.RemoveAllListeners();
        moreButton.onClick.AddListener(() =>
        {
            if (deleteButton != null)
            {
                bool isCurrentlyActive = deleteButton.gameObject.activeSelf;
                deleteButton.gameObject.SetActive(!isCurrentlyActive);
                ArmDelete(false);
            }
        });

        // Visit friend button event
        visitButton.onClick.RemoveAllListeners();
        visitButton.onClick.AddListener(() =>
        {
            onVisitCallback?.Invoke(currentFriendUserId);
        });

        // Delete friend button event
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(() =>
        {
            // กดครั้งแรก = ขอยืนยัน / กดซ้ำภายใน 3 วินาที = ลบจริง
            if (deleteArmedUntil < 0f)
            {
                ArmDelete(true);
                GrowGoToast.Show("กดลบอีกครั้งเพื่อยืนยันการลบเพื่อน", 1.6f);
                return;
            }
            ArmDelete(false);
            onDeleteCallback?.Invoke(currentFriendshipId, gameObject);
        });
    }
}