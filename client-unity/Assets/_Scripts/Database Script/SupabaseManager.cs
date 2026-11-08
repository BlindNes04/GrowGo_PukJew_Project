using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[Serializable]
public class SupabaseIslandItem
{
    public string island_id;
    public string item_id;
    public float drop_rate;
    public int respawn_time_minutes;
    public bool is_special_seasonal;
}

[Serializable]
public class SupabaseWeather
{
    public string weather_id;
    public string name;
    public float exp_multiplier;
    public bool auto_water;
    public float water_damage_chance;
    public float heat_damage_chance;
}

[Serializable]
public class SupabaseEvolutionStage
{
    public string stage_id;
    public string stage_name;
    public string species_id;
    public int stage_number;
    public int stage_level;
}



// ==================== SupabaseManager Class ====================
public class SupabaseManager : MonoBehaviour
{
    private static SupabaseManager instance;

    public static SupabaseManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SupabaseManager>();
                if (instance == null)
                {
                    instance = new GameObject("[SupabaseManager]").AddComponent<SupabaseManager>();
                }
            }
            return instance;
        }
    }

    [Header("Supabase Configuration")]
    public string supabaseUrl = "https://xwvqxyeybgwiaxpodnaq.supabase.co";
    public string supabaseAnonKey = "sb_publishable_14M9lB3dIwbc-eOX-LbK-w_A-3mv1UR";

    [Header("Current User")]
    [Tooltip("ID ของผู้เล่นที่ล็อกอินอยู่ในปัจจุบัน")]
    public string currentUserId = "";

    void Awake()
    {
        if (Instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSavedSession();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void LoadSavedSession()
    {
        string savedId = PlayerPrefs.GetString("SavedUserId", "");
        if (!string.IsNullOrEmpty(savedId))
        {
            currentUserId = savedId;
            Debug.Log($"[SupabaseManager] 🟢 Auto-loaded Session ID: {currentUserId}");
        }
    }

    // ==================== GLOBAL HELPER ====================
    /// <summary>
    /// กำหนด Header พื้นฐานสำหรับการยิง HTTP Request ไปยัง Supabase
    /// </summary>
    private void SetSupabaseHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("apikey", supabaseAnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
        request.SetRequestHeader("Content-Type", "application/json");
    }

    /// <summary>
    /// ดึง User ID ปัจจุบันที่ล็อกอินอยู่ (ดึงจาก Memory หรือ PlayerPrefs ให้อัตโนมัติ)
    /// </summary>
    public string GetActiveUserId()
    {
        if (!string.IsNullOrEmpty(currentUserId))
        {
            return currentUserId;
        }

        string savedId = PlayerPrefs.GetString("SavedUserId", "");
        if (!string.IsNullOrEmpty(savedId))
        {
            currentUserId = savedId;
            return currentUserId;
        }

        Debug.LogWarning("[SupabaseManager] ⚠️ ไม่พบ Active User ID ในระบบ!");
        return "";
    }

    // ==================== Inventory System ====================

    public IEnumerator GetUserInventory(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/inventory?select=user_id,item_id,quantity,item:item_id(item_id,item_name,item_type,rarity_tier,description,metadata)&user_id=eq.{GetActiveUserId()}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
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

    public IEnumerator UpdateItemQuantity(string itemId, int newQuantity, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogError("[Supabase Error] Item ID parameter is null or empty.");
            onError?.Invoke("itemId is null or empty");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{GetActiveUserId()}&item_id=eq.{itemId}";
        string jsonBody = $"{{\"quantity\": {newQuantity}}}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

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

    public IEnumerator DeleteItemFromInventory(string itemId, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            onError?.Invoke("itemId is null or empty");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{GetActiveUserId()}&item_id=eq.{itemId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Deleted item '{itemId}' from inventory DB.");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Delete item failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    public IEnumerator AddItemToInventory(string itemId, int addQuantity = 1, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogError("[Supabase Error] Item ID parameter is null or empty.");
            onError?.Invoke("itemId is null or empty");
            yield break;
        }

        string activeId = GetActiveUserId();
        string fetchEndpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{activeId}&item_id=eq.{itemId}";

        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            SetSupabaseHeaders(fetchRequest);
            yield return fetchRequest.SendWebRequest();

            if (fetchRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Supabase Error] 🔴 Fetch Inventory Item Failed: {fetchRequest.error}");
                onError?.Invoke(fetchRequest.error);
                yield break;
            }

            string jsonResult = fetchRequest.downloadHandler.text;
            List<InventoryItemData> items = JsonConvert.DeserializeObject<List<InventoryItemData>>(jsonResult);

            if (items != null && items.Count > 0)
            {
                int newQty = items[0].quantity + addQuantity;

                if (newQty <= 0)
                {
                    yield return StartCoroutine(DeleteItemFromInventory(itemId, onSuccess, onError));
                }
                else
                {
                    yield return StartCoroutine(UpdateItemQuantity(itemId, newQty, onSuccess, onError));
                }
            }
            else
            {
                if (addQuantity <= 0)
                {
                    onSuccess?.Invoke();
                    yield break;
                }

                string postEndpoint = $"{supabaseUrl}/rest/v1/inventory";
                string jsonBody = $"{{\"user_id\":\"{activeId}\",\"item_id\":\"{itemId}\",\"quantity\":{addQuantity}}}";

                using (UnityWebRequest postRequest = new UnityWebRequest(postEndpoint, "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    postRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    postRequest.downloadHandler = new DownloadHandlerBuffer();
                    SetSupabaseHeaders(postRequest);
                    postRequest.SetRequestHeader("Prefer", "return=minimal");

                    yield return postRequest.SendWebRequest();

                    if (postRequest.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[Supabase Success] 🟢 Inserted new item {itemId} into DB with quantity {addQuantity}.");
                        onSuccess?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"[Supabase Error] 🔴 Insert item failed: {postRequest.error} | {postRequest.downloadHandler.text}");
                        onError?.Invoke(postRequest.error);
                    }
                }
            }
        }
    }

    // ==================== Plant System ====================

    public IEnumerator CreatePlayerPlant(string initialStageId, string nickname, Action onSuccess = null, Action<string> onError = null)
    {
        string fetchEndpoint = $"{supabaseUrl}/rest/v1/player_plant?select=player_plant_id";

        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            SetSupabaseHeaders(fetchRequest);
            yield return fetchRequest.SendWebRequest();

            int maxSlotNum = 0;

            if (fetchRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = fetchRequest.downloadHandler.text;
                List<Dictionary<string, string>> existingPlants = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(jsonResult);

                if (existingPlants != null)
                {
                    foreach (var plant in existingPlants)
                    {
                        if (plant.ContainsKey("player_plant_id") && !string.IsNullOrEmpty(plant["player_plant_id"]))
                        {
                            string idStr = plant["player_plant_id"].Trim();
                            if (idStr.StartsWith("PLANT_SLOT_"))
                            {
                                string numPart = idStr.Replace("PLANT_SLOT_", "");
                                if (int.TryParse(numPart, out int currentNum))
                                {
                                    if (currentNum > maxSlotNum) maxSlotNum = currentNum;
                                }
                            }
                        }
                    }
                }
            }

            int nextSlotNum = maxSlotNum + 1;
            string newPlantId = $"PLANT_SLOT_{nextSlotNum:D2}";
            string nowIso = DateTime.UtcNow.ToString("o");

            string postEndpoint = $"{supabaseUrl}/rest/v1/player_plant";
            string jsonPayload = "{" +
                $"\"player_plant_id\":\"{newPlantId}\"," +
                $"\"user_id\":\"{GetActiveUserId()}\"," +
                $"\"stage_id\":\"{initialStageId}\"," +
                $"\"plant_nickname\":\"{nickname}\"," +
                $"\"current_exp\":0," +
                $"\"relationship_score\":0," +
                $"\"last_watered_at\":\"{nowIso}\"," +
                $"\"is_fully_grown\":false," +
                $"\"is_sick\":false," +
                $"\"sick_until\":null," +
                $"\"immunity_shields\":0," +
                $"\"planted_at\":\"{nowIso}\"" +
            "}";

            using (UnityWebRequest postRequest = new UnityWebRequest(postEndpoint, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                postRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                postRequest.downloadHandler = new DownloadHandlerBuffer();
                SetSupabaseHeaders(postRequest);
                postRequest.SetRequestHeader("Prefer", "return=minimal");

                yield return postRequest.SendWebRequest();

                if (postRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Supabase Success] 🟢 Created new plant: {nickname} (ID: {newPlantId}, Stage: {initialStageId})");
                    onSuccess?.Invoke();
                }
                else
                {
                    Debug.LogError($"[Supabase Error] 🔴 Create player_plant failed: {postRequest.error} | {postRequest.downloadHandler.text}");
                    onError?.Invoke(postRequest.error);
                }
            }
        }
    }

    public IEnumerator GetPlayerPlantData(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/player_plant?user_id=eq.{GetActiveUserId()}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
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

    private IEnumerator FetchMasterTable(string tableName, Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/{tableName}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
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
            SetSupabaseHeaders(request);

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

    public IEnumerator GetFriendships(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?select=*";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
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

    public IEnumerator GetAllUsers(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/user?select=user_id,username,user_tag,level,buddy_id";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
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
            SetSupabaseHeaders(request);
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

    public IEnumerator SendFriendRequest(string targetUserId, Action onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship";
        string fsId = $"FS_{DateTime.UtcNow.Ticks.ToString().Substring(10)}";

        string jsonPayload = "{" +
            $"\"friendship_id\":\"{fsId}\"," +
            $"\"requester_id\":\"{GetActiveUserId()}\"," +
            $"\"addressee_id\":\"{targetUserId}\"," +
            $"\"status\":\"pending\"" +
        "}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
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

    public IEnumerator GetPendingRequests(Action<string> onSuccess, Action<string> onError)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?select=*&addressee_id=eq.{GetActiveUserId()}&status=eq.pending";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
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

    public IEnumerator RespondToFriendRequest(string friendshipId, string status, Action onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?friendship_id=eq.{friendshipId}";
        string jsonPayload = $"{{\"status\":\"{status}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

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

    public IEnumerator DeleteFriendship(string friendshipId, Action onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?friendship_id=eq.{friendshipId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(endpoint))
        {
            SetSupabaseHeaders(request);
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

    // บันทึกประวัติการดูแลพืชลงตาราง care_log
    public IEnumerator LogCareAction(string playerPlantId, string itemId, string actionType, int expGained, int relationshipGained, Action onSuccess = null, Action<string> onError = null)
    {
        string fetchEndpoint = $"{supabaseUrl}/rest/v1/care_log?select=care_id";

        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            SetSupabaseHeaders(fetchRequest);
            yield return fetchRequest.SendWebRequest();

            int maxCareNum = 0;

            if (fetchRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = fetchRequest.downloadHandler.text;
                List<Dictionary<string, string>> existingLogs = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(jsonResult);

                if (existingLogs != null)
                {
                    foreach (var log in existingLogs)
                    {
                        if (log.ContainsKey("care_id") && !string.IsNullOrEmpty(log["care_id"]))
                        {
                            string idStr = log["care_id"].Trim();
                            if (idStr.StartsWith("CARE_LOG_"))
                            {
                                string numPart = idStr.Replace("CARE_LOG_", "");
                                if (int.TryParse(numPart, out int currentNum))
                                {
                                    if (currentNum <= 9999 && currentNum > maxCareNum)
                                    {
                                        maxCareNum = currentNum;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            int nextCareNum = maxCareNum + 1;
            string careId = $"CARE_LOG_{nextCareNum:D3}";
            string nowIso = DateTime.UtcNow.ToString("o");

            string itemIdValue = string.IsNullOrEmpty(itemId) ? "null" : $"\"{itemId}\"";

            string jsonPayload = "{" +
                $"\"care_id\":\"{careId}\"," +
                $"\"player_plant_id\":\"{playerPlantId}\"," +
                $"\"item_id\":{itemIdValue}," +
                $"\"action_type\":\"{actionType}\"," +
                $"\"exp_gained\":{expGained}," +
                $"\"relationship_gained\":{relationshipGained}," +
                $"\"created_at\":\"{nowIso}\"" +
            "}";

            string postEndpoint = $"{supabaseUrl}/rest/v1/care_log";

            using (UnityWebRequest request = new UnityWebRequest(postEndpoint, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                SetSupabaseHeaders(request);
                request.SetRequestHeader("Prefer", "return=minimal");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Supabase Success] 🟢 Logged care action '{actionType}' for plant {playerPlantId} (ID: {careId})");
                    onSuccess?.Invoke();
                }
                else
                {
                    Debug.LogError($"[Supabase Error] 🔴 Log care action failed: {request.error} | {request.downloadHandler.text}");
                    onError?.Invoke(request.error);
                }
            }
        }
    }

    // ==================== Auth / Register System ====================

    public IEnumerator RegisterUser(string username, Action<string> onSuccess, Action<string> onError)
    {
        string cleanUsername = username.Trim();

        if (string.IsNullOrEmpty(cleanUsername))
        {
            onError?.Invoke("กรุณากรอกชื่อผู้ใช้!");
            yield break;
        }

        string checkUrl = $"{supabaseUrl}/rest/v1/user?select=user_id&username=eq.{UnityWebRequest.EscapeURL(cleanUsername)}";

        using (UnityWebRequest checkReq = UnityWebRequest.Get(checkUrl))
        {
            SetSupabaseHeaders(checkReq);
            yield return checkReq.SendWebRequest();

            if (checkReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Supabase Error] 🔴 Check Username Failed: {checkReq.error}");
                onError?.Invoke("ไม่สามารถเชื่อมต่อฐานข้อมูลได้");
                yield break;
            }

            List<Dictionary<string, string>> existingUsers = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(checkReq.downloadHandler.text);

            if (existingUsers != null && existingUsers.Count > 0)
            {
                onError?.Invoke("ชื่อผู้ใช้นี้มีผู้ใช้งานแล้ว!");
                yield break;
            }
        }

        string newUserId = $"USER_{UnityEngine.Random.Range(100000, 999999)}";
        string newUserTag = $"#{UnityEngine.Random.Range(1000, 9999)}";
        string nowIso = DateTime.UtcNow.ToString("o");

        var newUserPayload = new Dictionary<string, object>
        {
            { "user_id", newUserId },
            { "username", cleanUsername },
            { "user_tag", newUserTag },
            { "level", 1 },
            { "total_exp", 0 },
            { "flower_points", 0 },
            { "green_points", 0 },
            { "stamina", 30 },
            { "last_stamina_update", nowIso },
            { "created_at", nowIso },
            { "last_login_at", nowIso }
        };

        string jsonBody = JsonConvert.SerializeObject(newUserPayload);
        string registerUrl = $"{supabaseUrl}/rest/v1/user";

        using (UnityWebRequest postReq = new UnityWebRequest(registerUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            postReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            postReq.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(postReq);
            postReq.SetRequestHeader("Prefer", "return=representation");

            yield return postReq.SendWebRequest();

            if (postReq.result == UnityWebRequest.Result.Success)
            {
                currentUserId = newUserId;
                Debug.Log($"[Supabase Success] 🟢 สมัครสมาชิกสำเร็จ! ID: {newUserId} | Tag: {newUserTag}");
                onSuccess?.Invoke(newUserId);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Register Failed: {postReq.error} | {postReq.downloadHandler.text}");
                onError?.Invoke("เกิดข้อผิดพลาดในการสร้างบัญชี");
            }
        }
    }

    // ==================== Buddy / MBTI System ====================

    public IEnumerator GetUserBuddyId(Action<string> onResult, Action<string> onError = null)
    {
        string activeId = GetActiveUserId();
        if (string.IsNullOrEmpty(activeId))
        {
            onError?.Invoke("User ID is empty");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/user?select=buddy_id&user_id=eq.{activeId}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                List<Dictionary<string, string>> result = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(request.downloadHandler.text);
                if (result != null && result.Count > 0 && result[0].ContainsKey("buddy_id"))
                {
                    string buddyId = result[0]["buddy_id"];
                    onResult?.Invoke(buddyId);
                }
                else
                {
                    onResult?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Get User Buddy Failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    public IEnumerator UpdateUserBuddyId(string buddyId, Action onSuccess = null, Action<string> onError = null)
    {
        string activeId = GetActiveUserId();
        string endpoint = $"{supabaseUrl}/rest/v1/user?user_id=eq.{activeId}";
        string jsonPayload = $"{{\"buddy_id\":\"{buddyId}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Updated user buddy_id to: {buddyId}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Update buddy_id failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== Island / Drop System ====================

    public IEnumerator GetIslandItems(string islandId, Action<List<SupabaseIslandItem>> onSuccess, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/island_item?select=*&island_id=eq.{islandId}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] 🟢 Fetch Island Items ({islandId}): {jsonResult}");
                List<SupabaseIslandItem> items = JsonConvert.DeserializeObject<List<SupabaseIslandItem>>(jsonResult);
                onSuccess?.Invoke(items);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Fetch Island Items Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    public IEnumerator GetWeatherMasterData(Action<List<SupabaseWeather>> onSuccess, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/weather?select=*";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"[Supabase Success] 🟢 Fetch Weather Master Data: {jsonResult}");
                List<SupabaseWeather> weatherList = JsonConvert.DeserializeObject<List<SupabaseWeather>>(jsonResult);
                onSuccess?.Invoke(weatherList);
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Fetch Weather Failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== PlantDex / Collection System ====================

    /// <summary>
    /// 1. บันทึกการปลดล็อกพืชร่างใหม่ลง user_collection (ถ้ายังไม่เคยมี)
    /// </summary>
    public IEnumerator UnlockPlantCollection(string userId, string stageId, Action onSuccess = null, Action<string> onError = null)
    {
        string targetUserId = string.IsNullOrEmpty(userId) ? GetActiveUserId() : userId;

        if (string.IsNullOrEmpty(stageId))
        {
            onError?.Invoke("stageId parameter is null or empty");
            yield break;
        }

        // เช็กก่อนว่าเคยปลดล็อกร่างนี้ไปหรือยัง
        string checkUrl = $"{supabaseUrl}/rest/v1/user_collection?user_id=eq.{targetUserId}&stage_id=eq.{stageId}";

        using (UnityWebRequest checkReq = UnityWebRequest.Get(checkUrl))
        {
            SetSupabaseHeaders(checkReq);
            yield return checkReq.SendWebRequest();

            if (checkReq.result == UnityWebRequest.Result.Success)
            {
                // ถ้ามีข้อมูลใน DB แล้ว (เคยปลดล็อกแล้ว) -> ข้าม ไม่ต้อง Insert ซ้ำ
                if (checkReq.downloadHandler.text != "[]" && !string.IsNullOrEmpty(checkReq.downloadHandler.text))
                {
                    onSuccess?.Invoke();
                    yield break;
                }
            }
            else
            {
                Debug.LogError($"[PlantDex Error] 🔴 Check Collection Failed: {checkReq.error}");
                onError?.Invoke(checkReq.error);
                yield break;
            }
        }

        // ถ้ายังไม่เคยปลดล็อก -> Insert บันทึกลงตาราง user_collection
        string insertUrl = $"{supabaseUrl}/rest/v1/user_collection";
        string jsonBody = $"{{\"user_id\":\"{targetUserId}\",\"stage_id\":\"{stageId}\"}}";

        using (UnityWebRequest insertReq = new UnityWebRequest(insertUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            insertReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            insertReq.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(insertReq);
            insertReq.SetRequestHeader("Prefer", "return=minimal");

            yield return insertReq.SendWebRequest();

            if (insertReq.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[PlantDex] 📖 ปลดล็อกพืชร่างใหม่สำเร็จ: {stageId}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[PlantDex Error] 🔴 Unlock Plant Collection Failed: {insertReq.error} | {insertReq.downloadHandler.text}");
                onError?.Invoke(insertReq.error);
            }
        }
    }

    /// <summary>
    /// 2. ดึง List ของ stage_id ทั้งหมดที่ผู้เล่นคนนี้เคยปลดล็อก
    /// </summary>
    public IEnumerator GetUserCollection(string userId, Action<List<string>> onSuccess, Action<string> onError = null)
    {
        string targetUserId = string.IsNullOrEmpty(userId) ? GetActiveUserId() : userId;
        string url = $"{supabaseUrl}/rest/v1/user_collection?select=stage_id&user_id=eq.{targetUserId}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            SetSupabaseHeaders(req);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = req.downloadHandler.text;
                List<UserCollectionData> rawList = JsonConvert.DeserializeObject<List<UserCollectionData>>(jsonResult);
                List<string> unlockedStages = new List<string>();

                if (rawList != null)
                {
                    foreach (var item in rawList)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.stage_id))
                        {
                            unlockedStages.Add(item.stage_id);
                        }
                    }
                }

                Debug.Log($"[PlantDex Success] 🟢 Fetch User Collection: Found {unlockedStages.Count} unlocked stages.");
                onSuccess?.Invoke(unlockedStages);
            }
            else
            {
                Debug.LogError($"[PlantDex Error] 🔴 Get User Collection Failed: {req.error} | {req.downloadHandler.text}");
                onError?.Invoke(req.error);
            }
        }
    }

    // ดึงข้อมูลตาราง master 'evolution_stage' ทั้งหมดจาก Supabase
    public IEnumerator GetEvolutionStageMasterData(Action<List<SupabaseEvolutionStage>> onSuccess, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/evolution_stage?select=*";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                List<SupabaseEvolutionStage> masterList = JsonConvert.DeserializeObject<List<SupabaseEvolutionStage>>(jsonResult);
                Debug.Log($"[PlantDex Success] 🟢 Fetch Master Evolution Stages: {masterList.Count} stages.");
                onSuccess?.Invoke(masterList);
            }
            else
            {
                Debug.LogError($"[PlantDex Error] 🔴 Fetch Master Evolution Stages Failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ปลดล็อกพืชร่างปัจจุบัน + ร่างก่อนหน้าเฉพาะสายที่ผู้เล่นโตมาจริงๆ
    public IEnumerator UnlockPlantAndPredecessors(string stageId, Action onSuccess = null, Action<string> onError = null)
    {
        string activeUserId = GetActiveUserId();
        if (string.IsNullOrEmpty(activeUserId) || string.IsNullOrEmpty(stageId))
        {
            onError?.Invoke("User ID หรือ stageId ว่างเปล่า");
            yield break;
        }

        List<SupabaseEvolutionStage> masterStages = null;
        yield return StartCoroutine(GetEvolutionStageMasterData(
            onSuccess: (list) => masterStages = list,
            onError: (err) => onError?.Invoke(err)
        ));

        if (masterStages == null || masterStages.Count == 0) yield break;

        // 1. หาข้อมูลร่างปัจจุบัน
        var currentStage = masterStages.Find(s => s.stage_id == stageId);
        if (currentStage == null) yield break;

        // 2. กรองหาเฉพาะร่างย้อนหลังในสายเดียวกัน (ใช้ stage_number)
        var stagesToUnlock = masterStages.FindAll(s =>
            s.species_id == currentStage.species_id &&
            (
                s.stage_number < currentStage.stage_number || // 🟢 กวาดเฉพาะร่างก่อนหน้า (1, 2, 3)
                s.stage_id == currentStage.stage_id          // 🟢 เอาเฉพาะร่างปัจจุบันไอดีนี้เป๊ะๆ
            )
        );

        // 3. บันทึกลง user_collection
        foreach (var stage in stagesToUnlock)
        {
            yield return StartCoroutine(UnlockPlantCollection(activeUserId, stage.stage_id));
        }

        onSuccess?.Invoke();
    }

    [Serializable]
    private class UserCollectionData
    {
        public string stage_id;
    }

    // ==================== Generic GET (ใช้กับ Plant Dex ปุ๋ย) ====================
    // query เช่น "crafting_log?select=materials_used,result_item_id,recipe_id&user_id=eq.USER_001"
    public IEnumerator GetRest(string query, Action<string> onSuccess, Action<string> onError = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/{query}";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[Supabase Error] GET {query} : {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }
}
