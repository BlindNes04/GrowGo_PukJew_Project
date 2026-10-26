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
    private Dictionary<string, Sprite> buddySpriteDict = new Dictionary<string, Sprite>();
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
        FetchMyFriends();
    }

    // ==================== Data Fetching Operations ====================
    
    // Initiates fetching sequence for accepted friendships
    public void FetchMyFriends()
    {
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
        yield return StartCoroutine(SupabaseManager.Instance.GetAllUsers(
            (json) => { allUsers = JsonConvert.DeserializeObject<List<UserData>>(json); },
            (error) => { Debug.LogError($"[Social] Get Users Error: {error}"); }
        ));

        // Step 3: Filter accepted user profiles
        if (allUsers != null)
        {
            acceptedFriendsList = allUsers
                .Where(u => acceptedFriendIds.Contains(u.userId))
                .ToList();

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
    private void OnVisitFriendClicked(string friendUserId)
    {
        Debug.Log($"[Social] 🟢 Visit Friend Triggered for User ID: {friendUserId}");
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
}