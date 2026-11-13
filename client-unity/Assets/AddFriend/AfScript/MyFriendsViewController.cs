using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

public class MyFriendsViewController : MonoBehaviour
{
    // ==================== UI References ====================
    [Header("UI Containers & Prefab")]
    public Transform contentContainer;
    public GameObject friendItemPrefab;

    [Header("Search Bar UI")]
    public TMP_InputField searchInputField;
    public Button searchButton;

    // ==================== Private Variables ====================
    private List<UserData> acceptedFriendsList = new List<UserData>();
    private Dictionary<string, string> friendToFriendshipIdMap = new Dictionary<string, string>();

    // ==================== Lifecycle Methods ====================
    void Start()
    {
        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchClicked);

        if (searchInputField != null)
            searchInputField.onSubmit.AddListener((val) => OnSearchClicked());

        LoadBuddySprites();
        // รายชื่อเพื่อนโหลดใน OnEnable แทน (OnEnable ทำงานก่อน Start อยู่แล้ว ไม่ต้องโหลดซ้ำตรงนี้)
    }

    // ==================== รีเฟรชรายชื่อเพื่อนทุกครั้งที่เปิดแท็บ ====================
    // Unity เริ่ม Coroutine บนวัตถุที่ปิดอยู่ไม่ได้ จึงไม่โหลดตอนหน้านี้ปิดอยู่
    // พอผู้เล่นเปิดแท็บ OnEnable จะโหลดรายชื่อล่าสุดจาก DB ให้เอง (อีกฝ่ายรับคำขอ/ลบเรา ก็เห็นทันที)
    private void OnEnable()
    {
        FetchMyFriends();
    }

    // ==================== Data Fetching Operations ====================
    
    // Initiates fetching sequence for accepted friendships
    public void FetchMyFriends()
    {
        if (!isActiveAndEnabled) return;   // หน้าปิดอยู่ -> รอโหลดตอนเปิดแท็บ (OnEnable)
        ClearContent();
        StartCoroutine(FetchMyFriendsRoutine());
    }

    private IEnumerator FetchMyFriendsRoutine()
    {
        string currentUserId = SupabaseManager.Instance.currentUserId;
        List<FriendshipData> friendships = new List<FriendshipData>();
        List<UserData> allUsers = new List<UserData>();

        // Step 1: Query friendship table
        yield return StartCoroutine(SupabaseManager.Instance.GetFriendships(
            (json) => { friendships = JsonConvert.DeserializeObject<List<FriendshipData>>(json); },
            (error) => { Debug.LogError($"[Social] Get Friendships Error: {error}"); }
        ));

        if (friendships == null) yield break;

        // Map accepted friend IDs to friendship record ID
        friendToFriendshipIdMap.Clear();
        HashSet<string> acceptedFriendIds = new HashSet<string>();

        foreach (var f in friendships)
        {
            if (f.status != "accepted") continue;

            if (f.requesterId == currentUserId)
            {
                acceptedFriendIds.Add(f.addresseeId);
                friendToFriendshipIdMap[f.addresseeId] = f.friendshipId;
            }
            else if (f.addresseeId == currentUserId)
            {
                acceptedFriendIds.Add(f.requesterId);
                friendToFriendshipIdMap[f.requesterId] = f.friendshipId;
            }
        }

        if (acceptedFriendIds.Count == 0) yield break;

        // Step 2: Fetch user profile table
        // 🤝 ดึงเฉพาะ user ที่เป็นเพื่อน (ไม่ดึงทั้งตาราง เพราะ Supabase ตอบได้สูงสุด 1000 แถว)
        yield return StartCoroutine(SupabaseManager.Instance.GetAllUsers(
            (json) => { allUsers = JsonConvert.DeserializeObject<List<UserData>>(json); },
            (error) => { Debug.LogError($"[Social] Get Users Error: {error}"); },
            acceptedFriendIds.ToList()
        ));

        // Step 3: Filter accepted user profiles
        if (allUsers != null)
        {
            acceptedFriendsList = allUsers
                .Where(u => acceptedFriendIds.Contains(u.userId))
                .ToList();

            // รอโหลดตารางบัดดี้ (ใช้ทำรูปโปรไฟล์) ให้เสร็จก่อนสร้างรายการ
            yield return StartCoroutine(SupabaseManager.Instance.LoadBuddyMbtiMap());

            PopulateList(acceptedFriendsList);
        }
    }

    // ==================== Search Operations ====================

    // Filters local friend list based on username or user_tag
    private void OnSearchClicked()
    {
        string query = searchInputField != null ? searchInputField.text.Trim().ToLower() : "";

        if (string.IsNullOrEmpty(query))
        {
            PopulateList(acceptedFriendsList);
            return;
        }

        string cleanQuery = query.StartsWith("#") ? query.Substring(1) : query;

        var filteredList = acceptedFriendsList.Where(u =>
            (u.username != null && u.username.ToLower().Contains(query)) ||
            (u.userTag != null && u.userTag.ToLower().Contains(cleanQuery))
        ).ToList();

        PopulateList(filteredList);
    }

    // ==================== Action Callbacks ====================

    // Callback for visiting friend island
    // 🤝 เยี่ยมสวนเพื่อน: ส่งลิสต์เพื่อนทั้งหมด + ลำดับของคนที่กด ไปให้ FriendVisitController
    //    (ปุ่ม "ไปบ้านถัดไป" ในหน้าเยี่ยมจะวนตามลิสต์นี้) แล้วเปิดฉาก FriendVisit
    private void OnVisitFriendClicked(string friendUserId)
    {
        Debug.Log($"[Social] 🟢 Visit Friend Triggered for User ID: {friendUserId}");

        FriendVisitController.friendIdList = acceptedFriendsList.Select(u => u.userId).ToList();
        FriendVisitController.currentFriendIndex = Mathf.Max(0, FriendVisitController.friendIdList.IndexOf(friendUserId));
        UnityEngine.SceneManagement.SceneManager.LoadScene(FriendVisitController.SCENE_NAME);
    }

    // Callback for deleting friendship
    private void OnDeleteFriendClicked(string friendshipId, GameObject itemObj)
    {
        StartCoroutine(SupabaseManager.Instance.DeleteFriendship(friendshipId,
            onSuccess: () =>
            {
                Debug.Log($"[Social] 🟢 Deleted friendship ID: {friendshipId}");
                Destroy(itemObj);

                acceptedFriendsList.RemoveAll(u => 
                    friendToFriendshipIdMap.ContainsKey(u.userId) && 
                    friendToFriendshipIdMap[u.userId] == friendshipId);
            },
            onError: (error) =>
            {
                Debug.LogError($"[Social] 🔴 Delete friendship failed: {error}");
            }
        ));
    }

    // ==================== UI Rendering Methods ====================

    // Instantiates item prefabs into scroll view container
    private void PopulateList(List<UserData> userList)
    {
        ClearContent();

        foreach (var user in userList)
        {
            if (!friendToFriendshipIdMap.TryGetValue(user.userId, out string friendshipId))
                continue;

            GameObject itemObj = Instantiate(friendItemPrefab, contentContainer);
            MyFriendItemView itemView = itemObj.GetComponent<MyFriendItemView>();

            Sprite avatarSprite = GetAvatarSprite(user.buddyId);
            itemView.SetData(user.userId, friendshipId, user.username, user.level, avatarSprite, OnVisitFriendClicked, OnDeleteFriendClicked);
        }
    }

    // 🤝 รูปโปรไฟล์ = รูปบัดดี้ของผู้เล่น (16 แบบตาม MBTI) ดูส่วน Buddy Avatar ใน SupabaseManager
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
}