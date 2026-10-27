using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

// ==================== SupabaseManager Class ====================
public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance;

    [Header("Supabase Configuration")]
    public string supabaseUrl = "https://xwvqxyeybgwiaxpodnaq.supabase.co";
    public string supabaseAnonKey = "sb_publishable_14M9lB3dIwbc-eOX-LbK-w_A-3mv1UR";

    [Header("Current User")]
    public string currentUserId = "USER_001";

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

    public IEnumerator DeleteItemFromInventory(string itemId, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            onError?.Invoke("itemId is null or empty");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{currentUserId}&item_id=eq.{itemId}";

        using (UnityWebRequest request = UnityWebRequest.Delete(endpoint))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            request.SetRequestHeader("Content-Type", "application/json");

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

        string fetchEndpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{currentUserId}&item_id=eq.{itemId}";

        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            fetchRequest.SetRequestHeader("apikey", supabaseAnonKey);
            fetchRequest.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            fetchRequest.SetRequestHeader("Content-Type", "application/json");

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
                string jsonBody = $"{{\"user_id\":\"{currentUserId}\",\"item_id\":\"{itemId}\",\"quantity\":{addQuantity}}}";

                using (UnityWebRequest postRequest = new UnityWebRequest(postEndpoint, "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    postRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    postRequest.downloadHandler = new DownloadHandlerBuffer();

                    postRequest.SetRequestHeader("apikey", supabaseAnonKey);
                    postRequest.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
                    postRequest.SetRequestHeader("Content-Type", "application/json");
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

    // 🟢 สร้างพืชใหม่พร้อมรันเลข ID อัตโนมัติ (PLANT_SLOT_09, PLANT_SLOT_10, ...)
    public IEnumerator CreatePlayerPlant(string initialStageId, string nickname, Action onSuccess = null, Action<string> onError = null)
    {
        string fetchEndpoint = $"{supabaseUrl}/rest/v1/player_plant?select=player_plant_id";

        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            fetchRequest.SetRequestHeader("apikey", supabaseAnonKey);
            fetchRequest.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            fetchRequest.SetRequestHeader("Content-Type", "application/json");

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
                $"\"user_id\":\"{currentUserId}\"," +
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

                postRequest.SetRequestHeader("apikey", supabaseAnonKey);
                postRequest.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
                postRequest.SetRequestHeader("Content-Type", "application/json");
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

    // บันทึกประวัติการดูแลพืชลงตาราง care_log (รันเลข CARE_LOG_001, CARE_LOG_002, CARE_LOG_003...)
    public IEnumerator LogCareAction(string playerPlantId, string itemId, string actionType, int expGained, int relationshipGained, Action onSuccess = null, Action<string> onError = null)
    {
        string fetchEndpoint = $"{supabaseUrl}/rest/v1/care_log?select=care_id";

        // 1. ดึง care_id ทั้งหมดใน DB มาเช็กเพื่อหารหัสสูงสุด
        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            fetchRequest.SetRequestHeader("apikey", supabaseAnonKey);
            fetchRequest.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            fetchRequest.SetRequestHeader("Content-Type", "application/json");

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
                                    // กรองข้ามเลขสุ่ม Ticks เดิมที่เกิน 9999 ทิ้ง
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

            // 2. คำนวณรหัสถัดไป เช่น 2 -> 3 กลายเป็น "CARE_LOG_003"
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

            // 3. ยิง POST บันทึกประวัติเข้าตาราง care_log
            using (UnityWebRequest request = new UnityWebRequest(postEndpoint, "POST"))
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
}