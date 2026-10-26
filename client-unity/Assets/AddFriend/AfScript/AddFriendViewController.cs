using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

public class AddFriendViewController : MonoBehaviour
{
    [Header("UI Containers & Prefab")]
    public Transform contentContainer;      // ลาก Content ใน ScrollView มาใส่
    public GameObject addFriendItemPrefab;  // ลาก Prefab 'AddFriendContainer' มาใส่

    [Header("Search & Buttons")]
    public TMP_InputField searchInputField; // ลาก search_bar มาใส่
    public Button searchButton;             // ลากปุ่มแว่นขยายมาใส่
    public Button friendReqButton;          // ลากปุ่ม คำขอเป็นเพื่อน มาใส่

    private Dictionary<string, Sprite> buddySpriteDict = new Dictionary<string, Sprite>();

    void Start()
    {
        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchClicked);

        if (searchInputField != null)
            searchInputField.onSubmit.AddListener((val) => OnSearchClicked());

        if (friendReqButton != null)
            friendReqButton.onClick.AddListener(OnPendingRequestsClicked);

        LoadBuddySprites();
        FetchRecommendedFriends();
    }

    // 💡 1. ดึงรายการแนะนำผู้เล่น
    public void FetchRecommendedFriends()
    {
        ClearContent();
        StartCoroutine(FetchRecommendedFriendsRoutine());
    }

    private IEnumerator FetchRecommendedFriendsRoutine()
    {
        string currentUserId = SupabaseManager.Instance.currentUserId;
        List<FriendshipData> friendships = new List<FriendshipData>();
        List<UserData> allUsers = new List<UserData>();

        // Step A: ดึงข้อมูลความสัมพันธ์ทั้งหมดในตาราง friendship
        yield return StartCoroutine(SupabaseManager.Instance.GetFriendships(
            (json) => { friendships = JsonConvert.DeserializeObject<List<FriendshipData>>(json); },
            (error) => { Debug.LogError($"[Social] Get Friendships Error: {error}"); }
        ));

        // คัดคนที่มีสถานะเป็นเพื่อน หรือส่งคำขอค้างไว้ออก
        HashSet<string> excludedUserIds = new HashSet<string> { currentUserId };
        if (friendships != null)
        {
            foreach (var f in friendships)
            {
                if (f.requesterId == currentUserId) excludedUserIds.Add(f.addresseeId);
                if (f.addresseeId == currentUserId) excludedUserIds.Add(f.requesterId);
            }
        }

        // Step B: ดึงตาราง user ทั้งหมด
        yield return StartCoroutine(SupabaseManager.Instance.GetAllUsers(
            (json) => { allUsers = JsonConvert.DeserializeObject<List<UserData>>(json); },
            (error) => { Debug.LogError($"[Social] Get Users Error: {error}"); }
        ));

        // Step C: กรองและสปอว์น Prefab แสดงบน UI
        if (allUsers != null)
        {
            var recommendedUsers = allUsers
                .Where(u => !excludedUserIds.Contains(u.userId))
                .Take(10)
                .ToList();

            PopulateList(recommendedUsers);
        }
    }

    // 💡 2. กดค้นหาด้วย user_tag
    private void OnSearchClicked()
    {
        string queryTag = searchInputField != null ? searchInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(queryTag))
        {
            FetchRecommendedFriends();
            return;
        }

        ClearContent();
        StartCoroutine(SearchUserRoutine(queryTag));
    }

    private IEnumerator SearchUserRoutine(string query)
    {
        string currentUserId = SupabaseManager.Instance.currentUserId;

        yield return StartCoroutine(SupabaseManager.Instance.SearchUser(query,
            (json) =>
            {
                List<UserData> searchResults = JsonConvert.DeserializeObject<List<UserData>>(json);
                if (searchResults != null)
                {
                    var filtered = searchResults.Where(u => u.userId != currentUserId).ToList();
                    PopulateList(filtered);
                }
            },
            (error) => { Debug.LogError($"[Social] Search Error: {error}"); }
        ));
    }

    // 💡 3. กดส่งคำขอเป็นเพื่อน
    private void OnSendFriendRequestClicked(string targetUserId)
    {
        StartCoroutine(SupabaseManager.Instance.SendFriendRequest(targetUserId,
            onSuccess: () => {
                Debug.Log($"[Social] ส่งคำขอเพื่อนไปหา {targetUserId} สำเร็จ!");
            },
            onError: (error) => {
                Debug.LogError($"[Social] ส่งคำขอไม่สำเร็จ: {error}");
            }
        ));
    }

    private void PopulateList(List<UserData> userList)
    {
        foreach (var user in userList)
        {
            GameObject itemObj = Instantiate(addFriendItemPrefab, contentContainer);
            FriendItemView itemView = itemObj.GetComponent<FriendItemView>();

            Sprite avatarSprite = GetAvatarSprite(user.buddyId);
            itemView.SetData(user.userId, user.username, user.level, avatarSprite, OnSendFriendRequestClicked);
        }
    }

    private Sprite GetAvatarSprite(string buddyId)
    {
        if (!string.IsNullOrEmpty(buddyId) && buddySpriteDict.ContainsKey(buddyId.ToUpper()))
            return buddySpriteDict[buddyId.ToUpper()];

        return null;
    }

    private void LoadBuddySprites()
    {
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>("Sprites/Buddies");
        foreach (var sprite in loadedSprites)
        {
            buddySpriteDict[sprite.name.ToUpper()] = sprite;
        }
    }

    private void ClearContent()
    {
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
    }

    [Header("Popup References")]
    public FriendRequestPopupController friendRequestPopupController;

    private void OnPendingRequestsClicked()
    {
        if (friendRequestPopupController != null)
        {
            friendRequestPopupController.ShowPopup();
        }

        Debug.Log("[Social] 📩 เปิดหน้าดูคำขอเป็นเพื่อน");
    }
}