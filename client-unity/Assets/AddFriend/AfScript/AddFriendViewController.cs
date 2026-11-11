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

    // 1. ดึงรายการแนะนำผู้เล่น
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

        // รอโหลดตารางบัดดี้ (ใช้ทำรูปโปรไฟล์) ให้เสร็จก่อนสร้างรายการ
        yield return StartCoroutine(SupabaseManager.Instance.LoadBuddyMbtiMap());

        // Step C: กรองและสปอว์น Prefab แสดงบน UI
        // 🤝 สุ่มลำดับก่อนหยิบ 10 คน (เดิมได้ 10 คนแรกของตารางเสมอ ทุกคนเห็นชุดเดิม)
        if (allUsers != null)
        {
            var recommendedUsers = allUsers
                .Where(u => !excludedUserIds.Contains(u.userId))
                .OrderBy(u => UnityEngine.Random.value)
                .Take(10)
                .ToList();

            PopulateList(recommendedUsers);
        }
    }

    // 2. กดค้นหาด้วย user_tag
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
        List<UserData> searchResults = null;
        List<FriendshipData> friendships = null;

        yield return StartCoroutine(SupabaseManager.Instance.SearchUser(query,
            (json) => { searchResults = JsonConvert.DeserializeObject<List<UserData>>(json); },
            (error) => { Debug.LogError($"[Social] Search Error: {error}"); }
        ));
        if (searchResults == null) yield break;

        // 🤝 ตัดคนที่เป็นเพื่อนกันแล้ว และคนที่เราส่งคำขอไปแล้วออก (เดิมกดส่งซ้ำได้ แล้ว error 409)
        //    คนที่ส่งคำขอมาหาเรายังโชว์อยู่ กดแอด = เป็นเพื่อนกันทันที (DB auto accept)
        yield return StartCoroutine(SupabaseManager.Instance.GetFriendships(
            (json) => { friendships = JsonConvert.DeserializeObject<List<FriendshipData>>(json); },
            (error) => { Debug.LogError($"[Social] Get Friendships Error: {error}"); }
        ));

        HashSet<string> hiddenUserIds = new HashSet<string> { currentUserId };
        if (friendships != null)
        {
            foreach (var f in friendships)
            {
                bool iAmRequester = f.requesterId == currentUserId;
                string otherId = iAmRequester ? f.addresseeId : f.requesterId;
                if (f.status == "accepted") hiddenUserIds.Add(otherId);              // เป็นเพื่อนแล้ว
                else if (f.status == "pending" && iAmRequester) hiddenUserIds.Add(otherId); // เราส่งไปแล้ว
            }
        }

        yield return StartCoroutine(SupabaseManager.Instance.LoadBuddyMbtiMap());
        // 🤝 คนที่ id / ชื่อ / แท็ก ตรงกับคำค้นเป๊ะๆ ขึ้นก่อน ที่เหลือเรียงตามเดิม
        string kw = query.Trim().TrimStart('#').ToLower();
        PopulateList(searchResults
            .Where(u => !hiddenUserIds.Contains(u.userId))
            .OrderByDescending(u => (u.userId ?? "").ToLower() == kw
                                 || (u.username ?? "").ToLower() == kw
                                 || (u.userTag ?? "").TrimStart('#').ToLower() == kw)
            .ToList());
    }

    // 3. กดส่งคำขอเป็นเพื่อน
    private void OnSendFriendRequestClicked(string targetUserId)
    {
        StartCoroutine(SupabaseManager.Instance.SendFriendRequest(targetUserId,
            onSuccess: () => {
                Debug.Log($"[Social] ส่งคำขอเพื่อนไปหา {targetUserId} สำเร็จ!");
            },
            onError: (error) => {
                Debug.LogError($"[Social] ส่งคำขอไม่สำเร็จ: {error}");
            },
            onResult: (result) => {
                // 🤝 อีกฝ่ายขอเรามาก่อนแล้ว -> DB ตอบรับให้อัตโนมัติ เลยรีเฟรชหน้ารายชื่อเพื่อน
                if (result == "accepted")
                {
                    Debug.Log($"[Social] 🤝 {targetUserId} ส่งคำขอมาหาเราอยู่แล้ว -> เป็นเพื่อนกันทันที");
                    MyFriendsViewController myFriends = FindAnyObjectByType<MyFriendsViewController>(FindObjectsInactive.Include);
                    if (myFriends != null) myFriends.FetchMyFriends();
                }
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

    // 🤝 รูปโปรไฟล์ = รูปบัดดี้ของผู้เล่น (16 แบบตาม MBTI) ดูส่วน Buddy Avatar ใน SupabaseManager
    //    (เดิมโหลดจาก Resources/Sprites/Buddies ซึ่งไม่มีโฟลเดอร์นี้ รูปเลยไม่เคยขึ้น)
    private Sprite GetAvatarSprite(string buddyId)
    {
        return SupabaseManager.Instance.GetBuddyAvatarSprite(buddyId);
    }

    private void LoadBuddySprites()
    {
        StartCoroutine(SupabaseManager.Instance.LoadBuddyMbtiMap());
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