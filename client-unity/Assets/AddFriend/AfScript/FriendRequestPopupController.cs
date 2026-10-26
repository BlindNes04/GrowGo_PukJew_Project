using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class FriendRequestPopupController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject popupPanel;              // ตัว GameObject 'FriendRequestPopup'
    public Transform popupContentContainer;  // Content ใน ScrollView ของ Popup
    public GameObject friendReqItemPrefab;   // Prefab 'FriendReqContainer'
    public Button closeButton;               // ปุ่ม CloseBtn (X)

    private Dictionary<string, Sprite> buddySpriteDict = new Dictionary<string, Sprite>();

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HidePopup);

        LoadBuddySprites();
    }

    public void ShowPopup()
    {
        popupPanel.SetActive(true);
        FetchPendingRequests();
    }

    public void HidePopup()
    {
        popupPanel.SetActive(false);
    }

    private void FetchPendingRequests()
    {
        // เคลียร์รายการเก่าในป๊อปอัปก่อน
        foreach (Transform child in popupContentContainer)
        {
            Destroy(child.gameObject);
        }

        StartCoroutine(FetchPendingRequestsRoutine());
    }

    private IEnumerator FetchPendingRequestsRoutine()
    {
        List<FriendshipData> pendingFriendships = new List<FriendshipData>();
        List<UserData> allUsers = new List<UserData>();

        // 1. ดึงคำขอที่ส่งมาหาเราที่มี status = 'pending'
        yield return StartCoroutine(SupabaseManager.Instance.GetPendingRequests(
            (json) => { pendingFriendships = JsonConvert.DeserializeObject<List<FriendshipData>>(json); },
            (error) => { Debug.LogError($"[Social] Fetch Pending Error: {error}"); }
        ));

        if (pendingFriendships == null || pendingFriendships.Count == 0)
            yield break;

        // 2. ดึงตาราง user เพื่อนำข้อมูลชื่อ/เลเวล/รูป มาแสดง
        yield return StartCoroutine(SupabaseManager.Instance.GetAllUsers(
            (json) => { allUsers = JsonConvert.DeserializeObject<List<UserData>>(json); },
            (error) => { Debug.LogError($"[Social] Fetch Users Error: {error}"); }
        ));

        if (allUsers == null) yield break;

        // 3. จับคู่ข้อมูลคนส่งคำขอกับตาราง user แล้วสปอว์นลง Popup UI
        Dictionary<string, UserData> userDict = allUsers.ToDictionary(u => u.userId);

        foreach (var fs in pendingFriendships)
        {
            if (userDict.TryGetValue(fs.requesterId, out UserData requesterUser))
            {
                GameObject itemObj = Instantiate(friendReqItemPrefab, popupContentContainer);
                FriendRequestItemView itemView = itemObj.GetComponent<FriendRequestItemView>();

                Sprite avatarSprite = GetAvatarSprite(requesterUser.buddyId);
                itemView.SetData(fs.friendshipId, requesterUser.userId, requesterUser.username, requesterUser.level, avatarSprite, OnRespondToRequest);
            }
        }
    }

    private void OnRespondToRequest(string friendshipId, bool isAccepted)
    {
        string newStatus = isAccepted ? "accepted" : "rejected";
        StartCoroutine(SupabaseManager.Instance.RespondToFriendRequest(friendshipId, newStatus,
            onSuccess: () => {
                Debug.Log($"[Social] ตอบรับ/ปฏิเสธ คำขอ {friendshipId} เรียบร้อย!");
            },
            onError: (error) => {
                Debug.LogError($"[Social] ตอบรับคำขอไม่สำเร็จ: {error}");
            }
        ));
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
}