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
            onDeleteCallback?.Invoke(currentFriendshipId, gameObject);
        });
    }
}