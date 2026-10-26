using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// SupabaseManager.cs - Central Manager for Supabase REST API
public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance;

    [Header("Supabase Configuration")]
    public string supabaseUrl = "https://xwvqxyeybgwiaxpodnaq.supabase.co";
    public string supabaseAnonKey = "sb_publishable_14M9lB3dIwbc-eOX-LbK-w_A-3mv1UR";

    [Header("Current User")]
    public string currentUserId = "MCK_0001";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ==================== Inventory System ====================

    // Fetches inventory data for current user
    public IEnumerator GetUserInventory(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/inventory?select=user_id,item_id,quantity,item:item_id(item_id,item_name,item_type,rarity_tier,description,metadata)&user_id=eq.{currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] Fetch Inventory: {jsonResult}");
                onSuccess?.Invoke(jsonResult);
            }
            else
            {
                Debug.LogError($"[Supabase Error] {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Updates item quantity in inventory
    public IEnumerator UpdateItemQuantity(string itemId, int newQuantity, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogError("[Supabase Error] Item ID parameter is null or empty.");
            onError?.Invoke("itemId is null or empty");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{currentUserId}&item_id=eq.{itemId}";
        string jsonBody = $"{{\"quantity\": {newQuantity}}}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] Updated item {itemId} quantity to {newQuantity}.");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] Update quantity failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== Plant System ====================

    // Fetches player plant growth data
    public IEnumerator GetPlayerPlantData(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/player_plant?user_id=eq.{currentUserId}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] Fetch Player Plant: {jsonResult}");
                onSuccess?.Invoke(jsonResult);
            }
            else
            {
                Debug.LogError($"[Supabase Error] Fetch Player Plant Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Fetches plant master lookup tables
    public IEnumerator GetPlantMasterData(
        Action<string> onSpeciesSuccess, 
        Action<string> onStagesSuccess, 
        Action<string> onRulesSuccess, 
        Action<string> onError)
    {
        yield return StartCoroutine(FetchMasterTable("plant_species", onSpeciesSuccess, onError));
        yield return StartCoroutine(FetchMasterTable("evolution_stage", onStagesSuccess, onError));
        yield return StartCoroutine(FetchMasterTable("evolution_rule", onRulesSuccess, onError));
    }

    // Generic helper to fetch master tables
    private IEnumerator FetchMasterTable(string tableName, Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/{tableName}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] Fetch {tableName}: {request.downloadHandler.text}");
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[Supabase Error] Fetch {tableName} Failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Updates player plant state via PATCH
    public IEnumerator UpdatePlayerPlantStatus(PlantStatus plant, Action onSuccess = null, Action<string> onError = null)
    {
        if (plant == null || string.IsNullOrEmpty(plant.playerPlantId))
        {
            Debug.LogError("[Supabase Error] Plant status object or ID is invalid.");
            onError?.Invoke("Plant is null or playerPlantId is empty");
            yield break;
        }

        string cleanPlantId = plant.playerPlantId.Trim();
        string endpoint = $"{supabaseUrl}/rest/v1/player_plant?player_plant_id=eq.{cleanPlantId}";

        string lastWateredStr = string.IsNullOrEmpty(plant.lastWateredAt) ? "null" : $"\"{plant.lastWateredAt}\"";
        string sickUntilStr = string.IsNullOrEmpty(plant.sickUntil) ? "null" : $"\"{plant.sickUntil}\"";
        string plantedAtStr = string.IsNullOrEmpty(plant.plantedAt) ? "null" : $"\"{plant.plantedAt}\"";

        string jsonPayload = "{" +
            $"\"player_plant_id\":\"{cleanPlantId}\"," +
            $"\"user_id\":\"{plant.userId.Trim()}\"," +
            $"\"stage_id\":\"{plant.currentStageId.Trim()}\"," +
            $"\"plant_nickname\":\"{plant.plantNickname.Trim()}\"," +
            $"\"current_exp\":{Mathf.RoundToInt(plant.currentNutrients)}," +
            $"\"relationship_score\":{plant.relationshipScore}," +
            $"\"last_watered_at\":{lastWateredStr}," +
            $"\"is_fully_grown\":{plant.isFullyGrown.ToString().ToLower()}," +
            $"\"is_sick\":{plant.isSick.ToString().ToLower()}," +
            $"\"sick_until\":{sickUntilStr}," +
            $"\"immunity_shields\":{plant.immunityShields}," +
            $"\"planted_at\":{plantedAtStr}" +
        "}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Updated plant status: {plant.plantNickname}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Plant status update failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== Friendship System ====================

    // Fetches all friendship records
    public IEnumerator GetFriendships(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?select=*";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] 🟢 Fetch Friendships: {jsonResult}");
                onSuccess?.Invoke(jsonResult);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Get Friendship Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Fetches profile metadata for all users
    public IEnumerator GetAllUsers(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/user?select=user_id,username,user_tag,level,buddy_id";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] 🟢 Fetch All Users: {jsonResult}");
                onSuccess?.Invoke(jsonResult);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Get Users Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Searches users by username or user_tag
    public IEnumerator SearchUser(string query, Action<string> onSuccess, Action<string> onError)
    {
        string cleanQuery = query.Trim();
        if (string.IsNullOrEmpty(cleanQuery)) yield break;

        string tagQuery = cleanQuery.StartsWith("#") ? cleanQuery : $"#{cleanQuery}";

        string encodedName = UnityWebRequest.EscapeURL($"\"{cleanQuery}\"");
        string encodedTag = UnityWebRequest.EscapeURL($"\"{tagQuery}\"");

        string endpoint = $"{supabaseUrl}/rest/v1/user?select=user_id,username,user_tag,level,buddy_id&or=(username.eq.{encodedName},user_tag.eq.{encodedTag},user_tag.eq.{encodedName})";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] 🟢 Search User Result: {jsonResult}");
                onSuccess?.Invoke(jsonResult);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Search User Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Sends a new friend request
    public IEnumerator SendFriendRequest(string targetUserId, Action onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship";
        string fsId = $"FS_{DateTime.UtcNow.Ticks.ToString().Substring(10)}";

        string jsonPayload = "{" +
            $"\"friendship_id\":\"{fsId}\"," +
            $"\"requester_id\":\"{currentUserId}\"," +
            $"\"addressee_id\":\"{targetUserId}\"," +
            $"\"status\":\"pending\"" +
        "}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Prefer", "return=minimal");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Sent friend request to target: {targetUserId}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Send friend request failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Fetches pending incoming friend requests
    public IEnumerator GetPendingRequests(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?select=*&addressee_id=eq.{currentUserId}&status=eq.pending";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] 🟢 Fetch Pending Requests: {jsonResult}");
                onSuccess?.Invoke(jsonResult);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Get Pending Requests Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // Updates friend request status (accepted/rejected)
    public IEnumerator RespondToFriendRequest(string friendshipId, string status, Action onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?friendship_id=eq.{friendshipId}";
        string jsonPayload = $"{{\"status\":\"{status}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Updated friendship request {friendshipId} to status: {status}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Friendship request update failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== Delete Friendship ====================

    // Deletes friendship record from database using HTTP DELETE
    public IEnumerator DeleteFriendship(string friendshipId, Action onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?friendship_id=eq.{friendshipId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Deleted friendship record: {friendshipId}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Delete friendship failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }
}