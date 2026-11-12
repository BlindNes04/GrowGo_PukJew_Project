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
        string nowIso = DateTime.UtcNow.ToString("o");
        string jsonBody = $"{{\"quantity\": {newQuantity}, \"updated_at\": \"{nowIso}\"}}";

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
                string jsonBody = $"{{\"user_id\":\"{activeId}\",\"item_id\":\"{itemId}\",\"quantity\":{addQuantity},\"updated_at\":\"{DateTime.UtcNow:o}\"}}";

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
                // 🔧 แก้บั๊ก: เดิมใส่เวลาปัจจุบัน ทำให้ต้นที่เพิ่งปลูกเหมือนถูกรดน้ำไปแล้ว (ความชื้นเต็ม + ติดคูลดาวน์)
                //    ใส่ null = ยังไม่เคยรดน้ำ ผู้เล่นต้องกดรดน้ำเองครั้งแรก
                $"\"last_watered_at\":null," +
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
        // 🤝 แก้ระบบเพื่อน: ดึงเฉพาะแถวที่เกี่ยวกับเรา (เดิมดึงทั้งตาราง)
        string me = GetActiveUserId();
        string endpoint = $"{supabaseUrl}/rest/v1/friendship?select=*&or=(requester_id.eq.{me},addressee_id.eq.{me})";

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

    public IEnumerator GetAllUsers(Action<string> onSuccess, Action<string> onError, List<string> onlyUserIds = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/user?select=user_id,username,user_tag,level,buddy_id";

        // 🤝 Supabase ส่งกลับได้มากสุด 1000 แถวต่อครั้ง แต่ตาราง user มี 3000+ แถว (รวม mock)
        //    ถ้าดึงทั้งตาราง USER_002 / USER_003 จะหลุดไปอยู่หลัง 1000 แถวแรก เลยหาไม่เจอ
        //    ส่ง onlyUserIds มา = ดึงเฉพาะคนที่ต้องใช้ (เช่น รายชื่อเพื่อน, คนที่ส่งคำขอมา)
        if (onlyUserIds != null)
        {
            if (onlyUserIds.Count == 0) { onSuccess?.Invoke("[]"); yield break; }
            List<string> quoted = new List<string>();
            foreach (string id in onlyUserIds) quoted.Add($"\"{id}\"");
            string idList = string.Join(",", quoted);
            endpoint += "&user_id=in.(" + UnityWebRequest.EscapeURL(idList) + ")";
        }

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

        // 🤝 แก้ระบบเพื่อน: ค้นหาบางส่วนได้ ไม่ต้องพิมพ์ตรงเป๊ะ (ilike = ไม่สนตัวพิมพ์เล็ก/ใหญ่)
        //    เช่นพิมพ์ "man" เจอ "Mango", พิมพ์ "0001" หรือ "#0001" เจอแท็ก #00001
        //    ตัดตัวอักษรที่ใช้แยกเงื่อนไขใน URL ออก ( , ( ) " ) กันคำค้นทำ URL พัง
        string keyword = cleanQuery.TrimStart('#').Replace(",", "").Replace("(", "").Replace(")", "").Replace("\"", "");
        if (string.IsNullOrEmpty(keyword)) yield break;
        // 🤝 ค้นได้ทั้ง username / tag / user_id (เช่น พิมพ์ "user_003" หรือ "003")
        //    ใน ilike ตัว _ กับ % เป็นอักษรพิเศษ (_ = อะไรก็ได้ 1 ตัว) ต้องใส่ \ นำหน้า
        //    ไม่งั้นพิมพ์ "USER_002" จะไปเจอ user_0020-0029 ของ mock จนเต็ม 20 คนก่อน
        string likeKeyword = keyword.Replace("\\", "").Replace("_", "\\_").Replace("%", "\\%");
        string pattern = UnityWebRequest.EscapeURL($"*{likeKeyword}*");

        string endpoint = $"{supabaseUrl}/rest/v1/user?select=user_id,username,user_tag,level,buddy_id&or=(username.ilike.{pattern},user_tag.ilike.{pattern},user_id.ilike.{pattern})&limit=50";

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

    // 🤝 แก้ระบบเพื่อน: ส่งคำขอผ่าน RPC send_friend_request (ไฟล์ friend_request_fix.sql)
    //    DB เช็คให้หมด กันคำขอซ้ำ/ซ้อนสองทาง และถ้าอีกฝ่ายขอเรามาก่อนแล้ว จะเป็นเพื่อนกันทันที
    //    onResult ได้ค่า: sent / accepted / already_sent / already_friends / self
    private class FriendRequestResult { public string result; }

    public IEnumerator SendFriendRequest(string targetUserId, Action onSuccess = null, Action<string> onError = null, Action<string> onResult = null)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/rpc/send_friend_request";
        string jsonPayload = JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            { "p_requester_id", GetActiveUserId() },
            { "p_target_id", targetUserId }
        });

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var rows = JsonConvert.DeserializeObject<List<FriendRequestResult>>(request.downloadHandler.text);
                string result = (rows != null && rows.Count > 0) ? rows[0].result : "";
                Debug.Log($"[Supabase Success] 🟢 Friend request to {targetUserId}: {result}");
                onSuccess?.Invoke();
                onResult?.Invoke(result);
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

                // 📝 Behavior Log: ได้ร่างพืชใหม่เข้าสมุดภาพ
                LogBehavior("collect_species", "stage_id", stageId);
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

    // ==================== Crafting Log (มินิเกมผสมปุ๋ย) ====================
    /// <summary>
    /// บันทึกผลการผสมปุ๋ยลงตาราง crafting_log (craft_id รันต่อจากเลขสูงสุด เช่น CRAFT_009)
    /// </summary>
    public IEnumerator InsertCraftingLog(List<string> materialsUsed, string difficultyMode, string timingResult, int totalScore,
        string resultGrade, string resultItemId, string recipeId, Action onSuccess = null, Action<string> onError = null)
    {
        int maxNum = 0;
        string fetchEndpoint = $"{supabaseUrl}/rest/v1/crafting_log?select=craft_id";
        using (UnityWebRequest fetchRequest = UnityWebRequest.Get(fetchEndpoint))
        {
            SetSupabaseHeaders(fetchRequest);
            yield return fetchRequest.SendWebRequest();

            if (fetchRequest.result == UnityWebRequest.Result.Success)
            {
                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(fetchRequest.downloadHandler.text);
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        if (row.TryGetValue("craft_id", out string id) && !string.IsNullOrEmpty(id) && id.StartsWith("CRAFT_"))
                        {
                            if (int.TryParse(id.Substring(6), out int n) && n > maxNum) maxNum = n;
                        }
                    }
                }
            }
        }

        var payload = new Dictionary<string, object>
        {
            { "craft_id", $"CRAFT_{maxNum + 1:D3}" },
            { "user_id", GetActiveUserId() },
            { "materials_used", materialsUsed ?? new List<string>() },
            { "difficulty_mode", difficultyMode },
            { "timing_result", timingResult },
            { "total_score", totalScore },
            { "result_grade", resultGrade },
            { "result_item_id", resultItemId },
            { "recipe_id", string.IsNullOrEmpty(recipeId) ? null : recipeId },
            { "crafted_at", DateTime.UtcNow.ToString("o") }
        };
        string jsonBody = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest($"{supabaseUrl}/rest/v1/crafting_log", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Logged craft {resultItemId} ({resultGrade})");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Insert crafting_log failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    /// <summary>
    /// บันทึกว่าผู้เล่นค้นพบสูตรนี้แล้ว (ตาราง user_recipe_discovery) ถ้ามีอยู่แล้วจะข้ามไป ไม่ทับวันที่เดิม
    /// </summary>
    public IEnumerator UnlockRecipeDiscovery(string recipeId, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(recipeId))
        {
            onSuccess?.Invoke();
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/user_recipe_discovery?on_conflict=user_id,recipe_id";
        string jsonBody = $"{{\"user_id\":\"{GetActiveUserId()}\",\"recipe_id\":\"{recipeId}\",\"unlocked_at\":\"{DateTime.UtcNow:o}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
            request.SetRequestHeader("Prefer", "resolution=ignore-duplicates,return=minimal");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Supabase Success] 🟢 Recipe discovered: {recipeId}");
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[Supabase Error] 🔴 Unlock recipe failed: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== Player Level / Points (เลเวลผู้เล่น + แต้ม) ====================
    // ตาราง user : level, total_exp, flower_points, green_points, cluster_id
    // กติกา (ตกลงกับ Pakin 2026-11-09):
    //   - เลเวลคิดจาก total_exp ทั้งหมด ไม่มีเลเวลตัน
    //   - จากเลเวล L ไปเลเวล L+1 ต้องใช้ EXP = L x expPerLevelStep (ค่าเริ่ม 100)
    //     เช่น Lv1->2 ใช้ 100, Lv2->3 ใช้ 200, Lv3->4 ใช้ 300 ...
    //   - ขึ้นเลเวลได้แต้มดอกไม้ = เลเวลใหม่ x levelUpFlowerPerLevel (ค่าเริ่ม 50)
    //   - EXP ได้จาก: รดน้ำ/ใส่ปุ๋ย/ต้นไม้ขึ้นสเตจ (PlantStatus), ผสมปุ๋ย/มินิเกม/สำรวจเกาะ (ผ่าน LogBehavior ด้านล่าง)

    [Header("⭐ Player Level Settings")]
    public int expPerLevelStep = 100;          // EXP ที่ต้องใช้ต่อเลเวล (คูณกับเลเวลปัจจุบัน)
    public int levelUpFlowerPerLevel = 50;     // รางวัลขึ้นเลเวล = เลเวลใหม่ x ค่านี้
    public int expFromCraftFertilizer = 10;    // ผสมปุ๋ยเสร็จ
    public int expFromMinigame = 10;           // เล่นมินิเกมจบ (BlockBlast, Merge Fruit)
    public int expFromExploreIsland = 10;      // สำรวจเกาะแล้วรับของ

    // ---------- ค่าของผู้เล่นที่โหลดมาเก็บไว้ (UI อ่านจากตรงนี้) ----------
    [Header("⭐ Player Stats (โหลดจาก DB)")]
    public string playerUsername = "";
    public string playerTag = "";
    public string playerClusterId = "";
    public int playerLevel = 1;
    public long playerTotalExp = 0;
    public int playerFlowerPoints = 0;
    public int playerGreenPoints = 0;

    public bool playerStatsLoaded = false;
    private bool playerStatsLoading = false;
    private int pendingExp = 0;      // EXP ที่ได้มาก่อนโหลดข้อมูลเสร็จ (เก็บไว้บวกทีหลัง กันเขียนทับค่าใน DB)
    private int pendingFlower = 0;

    // UI ฟัง event พวกนี้เพื่ออัปเดตหน้าจอ
    public static event Action OnPlayerStatsChanged;   // ค่าเปลี่ยน (EXP/แต้ม/เลเวล)
    public static event Action<int> OnPlayerLevelUp;   // ขึ้นเลเวล (ส่งเลเวลใหม่)

    // ใช้กับ JsonConvert (Newtonsoft) อย่างเดียว
    private class UserStatsRow
    {
        public string username;
        public string user_tag;
        public string cluster_id;
        // ค่าตัวเลขอาจเป็น null ใน DB เลยใช้ int?/long? และเขียนเป็น property เพื่อไม่ให้ Unity เตือน (UAC1001)
        public int? level { get; set; }
        public long? total_exp { get; set; }
        public int? flower_points { get; set; }
        public int? green_points { get; set; }
    }

    // ---------- สูตรเลเวล ----------
    /// <summary> EXP รวมที่ต้องมีถึงจะเป็นเลเวลนี้ : step x (1+2+...+(level-1)) </summary>
    public long GetTotalExpForLevel(int level)
    {
        return (long)expPerLevelStep * level * (level - 1) / 2;
    }

    /// <summary> คำนวณเลเวลจาก EXP รวม (ไล่บวกทีละเลเวลจนกว่า EXP จะไม่พอ) </summary>
    public int GetLevelFromTotalExp(long totalExp)
    {
        int level = 1;
        while (totalExp >= GetTotalExpForLevel(level + 1)) level++;
        return level;
    }

    /// <summary> EXP ที่มีในเลเวลนี้ / EXP ที่ต้องใช้ไปเลเวลถัดไป (ใช้กับหลอด EXP) </summary>
    public void GetLevelProgress(out long expInLevel, out long expNeeded)
    {
        expInLevel = playerTotalExp - GetTotalExpForLevel(playerLevel);
        expNeeded = (long)expPerLevelStep * playerLevel;
    }

    // ---------- โหลดค่าผู้เล่นจาก DB ----------
    public void LoadPlayerStats()
    {
        if (!playerStatsLoading) StartCoroutine(LoadPlayerStatsRoutine());
    }

    private IEnumerator LoadPlayerStatsRoutine()
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId)) yield break;
        playerStatsLoading = true;

        string endpoint = $"{supabaseUrl}/rest/v1/user?select=username,user_tag,cluster_id,level,total_exp,flower_points,green_points&user_id=eq.{userId}";
        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                List<UserStatsRow> rows = JsonConvert.DeserializeObject<List<UserStatsRow>>(request.downloadHandler.text);
                if (rows != null && rows.Count > 0)
                {
                    UserStatsRow row = rows[0];
                    playerUsername = row.username ?? "";
                    playerTag = row.user_tag ?? "";
                    playerClusterId = row.cluster_id ?? "";
                    playerTotalExp = row.total_exp ?? 0;
                    playerFlowerPoints = row.flower_points ?? 0;
                    playerGreenPoints = row.green_points ?? 0;
                    // เลเวลคิดใหม่จาก total_exp เสมอ (ค่า level ใน DB เป็นแค่สำเนาไว้ดู)
                    playerLevel = GetLevelFromTotalExp(playerTotalExp);
                    playerStatsLoaded = true;
                }
            }
            else
            {
                Debug.LogError($"[PlayerLevel] 🔴 โหลดข้อมูลผู้เล่นล้มเหลว: {request.error}");
            }
        }
        playerStatsLoading = false;

        // ถ้ามี EXP/แต้มค้างอยู่ระหว่างโหลด ให้บวกตอนนี้
        if (playerStatsLoaded && (pendingExp > 0 || pendingFlower > 0))
        {
            int e = pendingExp, f = pendingFlower;
            pendingExp = 0; pendingFlower = 0;
            AddPlayerExpAndFlower(e, f);
        }
        else
        {
            OnPlayerStatsChanged?.Invoke();
        }
    }

    // ---------- บวก EXP + แต้มดอกไม้ แล้วบันทึกลง DB ----------
    public void AddPlayerExpAndFlower(int exp, int flower)
    {
        if (exp <= 0 && flower <= 0) return;

        // ยังไม่ได้โหลดค่าจาก DB -> เก็บไว้ก่อน แล้วสั่งโหลด
        if (!playerStatsLoaded)
        {
            pendingExp += Mathf.Max(0, exp);
            pendingFlower += Mathf.Max(0, flower);
            LoadPlayerStats();
            return;
        }

        int oldLevel = playerLevel;
        playerTotalExp += Mathf.Max(0, exp);
        playerFlowerPoints += Mathf.Max(0, flower);
        playerLevel = GetLevelFromTotalExp(playerTotalExp);

        // ขึ้นเลเวล -> ให้รางวัลแต้มดอกไม้ทุกเลเวลที่ข้าม
        for (int lv = oldLevel + 1; lv <= playerLevel; lv++)
        {
            playerFlowerPoints += lv * levelUpFlowerPerLevel;
        }

        OnPlayerStatsChanged?.Invoke();
        if (playerLevel > oldLevel) OnPlayerLevelUp?.Invoke(playerLevel);

        StartCoroutine(SavePlayerStatsRoutine());
    }

    private IEnumerator SavePlayerStatsRoutine()
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId)) yield break;

        var payload = new Dictionary<string, object>
        {
            { "level", playerLevel },
            { "total_exp", playerTotalExp },
            { "flower_points", playerFlowerPoints }
        };
        string jsonBody = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest($"{supabaseUrl}/rest/v1/user?user_id=eq.{userId}", "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogError($"[PlayerLevel] 🔴 บันทึก EXP/แต้มล้มเหลว: {request.error} | {request.downloadHandler.text}");
        }
    }

    // ---------- EXP จากกิจกรรมที่บันทึกใน behavior_log ----------
    // เรียกจาก LogBehavior อัตโนมัติ เลยไม่ต้องไปแก้โค้ดมินิเกม/เกาะทีละไฟล์
    private void GrantExpFromActivity(string activityType)
    {
        int exp = 0;
        switch (activityType)
        {
            case "craft_fertilizer": exp = expFromCraftFertilizer; break;
            case "play_block_blast":
            case "play_fruit_merge": exp = expFromMinigame; break;
            case "explore_island": exp = expFromExploreIsland; break;
        }
        if (exp > 0) AddPlayerExpAndFlower(exp, 0);
    }

    // ==================== Update Account (แก้ชื่อ / รหัสผ่าน ในหน้าตั้งค่า) ====================
    // column = "username" หรือ "password"
    // ถ้าชื่อซ้ำกับคนอื่น DB จะตอบ 409 (ตาราง user ตั้ง username เป็น UNIQUE ไว้) -> ส่ง "duplicate" กลับไป
    public IEnumerator UpdateUserField(string column, string value, Action onSuccess = null, Action<string> onError = null)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId)) { onError?.Invoke("User ID is empty"); yield break; }

        string jsonBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { column, value } });

        using (UnityWebRequest request = new UnityWebRequest($"{supabaseUrl}/rest/v1/user?user_id=eq.{userId}", "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // เปลี่ยนชื่อแล้ว ให้แถบซ้ายบน/ป๊อปอัปโปรไฟล์อัปเดตด้วย
                if (column == "username")
                {
                    playerUsername = value;
                    OnPlayerStatsChanged?.Invoke();
                }
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[UpdateAccount] 🔴 แก้ {column} ล้มเหลว: {request.responseCode} {request.downloadHandler.text}");
                onError?.Invoke(request.responseCode == 409 ? "duplicate" : request.error);
            }
        }
    }

    // ==================== Delete Account (ลบบัญชีผู้เล่น) ====================
    // เรียกฟังก์ชัน delete_user_account ใน Supabase (ลบข้อมูลของผู้เล่นทุกตารางในครั้งเดียว)
    // ลบสำเร็จแล้วล้าง id ที่จำไว้ในเครื่อง เพื่อให้กลับไปหน้าเริ่มเกมเป็นผู้เล่นใหม่
    public IEnumerator DeleteAccount(Action onSuccess = null, Action<string> onError = null)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId))
        {
            onError?.Invoke("User ID is empty");
            yield break;
        }

        string jsonBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { "p_user_id", userId } });

        using (UnityWebRequest request = new UnityWebRequest($"{supabaseUrl}/rest/v1/rpc/delete_user_account", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[DeleteAccount] 🟢 ลบบัญชี {userId} สำเร็จ");

                // ล้างข้อมูลผู้เล่นที่จำไว้ในเครื่อง
                PlayerPrefs.DeleteKey("SavedUserId");
                PlayerPrefs.Save();
                currentUserId = "";
                playerStatsLoaded = false;

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[DeleteAccount] 🔴 ลบบัญชีล้มเหลว: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
        }
    }

    // ==================== Behavior Log (เก็บพฤติกรรมผู้เล่น) ====================
    // ใช้ตาราง behavior_log : log_id, user_id, activity_type, timestamp, meta_data
    // activity_type ที่ใช้ (ตรงกับข้อมูลเดิมใน DB):
    //   play_block_blast, play_fruit_merge, craft_fertilizer, explore_island, collect_species
    //   (water_friend_tree, submit_tree_cert ยังไม่มีระบบในเกม)
    // วิธีเรียก: SupabaseManager.Instance.LogBehavior("play_block_blast", "score", 1200, "reward_item", "ITEM_ING_EGG");
    //   ค่าหลังชื่อกิจกรรมใส่เป็นคู่ "ชื่อ", ค่า จะถูกเก็บลง meta_data (jsonb)

    /// <summary>
    /// สั่งบันทึกพฤติกรรม 1 ครั้ง (ยิงแล้วไม่ต้องรอผล)
    /// </summary>
    public void LogBehavior(string activityType, params object[] metaPairs)
    {
        StartCoroutine(InsertBehaviorLog(activityType, metaPairs));

        // ⭐ กิจกรรมบางอย่างให้ EXP ผู้เล่นด้วย (ดูส่วน Player Level ด้านบน)
        GrantExpFromActivity(activityType);
    }

    private IEnumerator InsertBehaviorLog(string activityType, object[] metaPairs)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(activityType)) yield break;

        // แปลงคู่ "ชื่อ", ค่า ให้เป็น meta_data
        Dictionary<string, object> meta = null;
        if (metaPairs != null && metaPairs.Length >= 2)
        {
            meta = new Dictionary<string, object>();
            for (int i = 0; i + 1 < metaPairs.Length; i += 2)
            {
                meta[metaPairs[i].ToString()] = metaPairs[i + 1];
            }
        }

        // log_id = BH_ + เวลา (ถึงมิลลิวินาที) + เลขสุ่ม 3 หลัก กันซ้ำ
        // (ไม่ไล่หาเลขล่าสุดแบบ crafting_log เพราะตารางนี้มีข้อมูลหลายแสนแถว)
        string logId = $"BH_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{UnityEngine.Random.Range(0, 1000):D3}";

        var payload = new Dictionary<string, object>
        {
            { "log_id", logId },
            { "user_id", userId },
            { "activity_type", activityType },
            { "timestamp", DateTime.UtcNow.ToString("o") },
            { "meta_data", meta }
        };
        string jsonBody = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest($"{supabaseUrl}/rest/v1/behavior_log", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log($"[BehaviorLog] 🟢 บันทึก {activityType} ({logId})");
            else
                Debug.LogError($"[BehaviorLog] 🔴 บันทึก {activityType} ล้มเหลว: {request.error} | {request.downloadHandler.text}");
        }
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

    // ==================== Daily Reward (รางวัลประจำวัน) ====================
    // เรียก RPC claim_daily_reward บน Supabase
    // - DB เช็คเองว่า "วันที่" (เวลาไทย) ของ last_daily_reward_at ตรงกับวันนี้ไหม ถ้าตรง = รับไปแล้ว
    // - ถ้ายังไม่รับ DB จะสุ่ม silver 85% / gold 15% แล้วใส่ของลง inventory ให้เลย
    // ทำฝั่ง DB เพื่อไม่ให้ผู้เล่นโกงด้วยการเปลี่ยนเวลาในเครื่อง
    [Serializable]
    public class DailyRewardResult
    {
        public bool claimed;            // true = วันนี้ได้ของใหม่, false = รับไปแล้ว/ผิดพลาด
        public string reward_item_id;   // เช่น ITEM_ING_EGG
        public string reward_item_name; // ชื่อจากตาราง item
        public string reward_tier;      // silver / gold
        public int reward_quantity;     // จำนวนที่ได้ (ตอนนี้ 1)
        public string message;          // Success / Already claimed today / ...
    }

    public IEnumerator ClaimDailyReward(Action<DailyRewardResult> onResult, Action<string> onError = null)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[DailyReward] ⚠️ ไม่มี User ID (ยังไม่ได้ล็อกอิน) ข้ามการเช็ค");
            onError?.Invoke("No active user");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/rpc/claim_daily_reward";
        string jsonBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { "p_user_id", userId } });

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[DailyReward] 🔴 RPC ล้มเหลว: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
                yield break;
            }

            // RPC แบบ returns table จะตอบกลับเป็น array เสมอ เอาแถวแรก
            var list = JsonConvert.DeserializeObject<List<DailyRewardResult>>(request.downloadHandler.text);
            if (list == null || list.Count == 0)
            {
                Debug.LogError($"[DailyReward] 🔴 RPC ตอบกลับว่าง: {request.downloadHandler.text}");
                onError?.Invoke("Empty response");
                yield break;
            }

            Debug.Log($"[DailyReward] 📩 {list[0].message} ({list[0].reward_item_id})");
            onResult?.Invoke(list[0]);
        }
    }

    // ==================== Friend Visit (เยี่ยมสวนเพื่อน) ====================
    // เรียก RPC water_friend_plant บน Supabase (ไฟล์ SQL: water_friend_plant.sql)
    // DB เช็คเองทั้งหมด : เป็นเพื่อนกันไหม / ต้นป่วยไหม / วันนี้ช่วยรดต้นนี้ไปแล้วไหม (รีเที่ยงคืนเวลาไทย)
    // ถ้าผ่าน DB จะบันทึก player_plant.friend_watered_at (= wateredAtIso) + behavior_log (water_friend_tree) ให้เลย
    // wateredAtIso = เวลาที่ย้อนไปแล้ว ให้หลอดเจ้าของขึ้นแค่ 60% (คิดใน FriendVisitController)
    // แล้วตอบกลับว่าเป็น "ต้นแรกของเพื่อนคนนี้ในวันนี้" ไหม (FriendVisitController ใช้คิดรางวัล)
    [Serializable]
    public class WaterFriendResult
    {
        public bool success;          // true = รดสำเร็จ
        public bool is_first_today;   // true = ต้นแรกของเพื่อนคนนี้ในวันนี้ (ได้แต้มดอกไม้)
        public string message;        // Success / Already helped today / Plant is sick / Not friend / ...
    }

    public IEnumerator WaterFriendPlant(string playerPlantId, string wateredAtIso, Action<WaterFriendResult> onResult, Action<string> onError = null)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId))
        {
            onError?.Invoke("No active user");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/rpc/water_friend_plant";
        var args = new Dictionary<string, object>
        {
            { "p_helper_id", userId },
            { "p_player_plant_id", playerPlantId }
        };
        // ส่งเวลาย้อนหลังไปด้วยเฉพาะตอนตั้งให้เพื่อนรดไม่เต็มหลอด (ต้องรัน water_friend_plant_v2.sql ก่อน)
        // ถ้าไม่ส่ง DB จะใช้เวลาปัจจุบัน = หลอดเต็ม 100
        if (!string.IsNullOrEmpty(wateredAtIso)) args["p_watered_at"] = wateredAtIso;
        string jsonBody = JsonConvert.SerializeObject(args);

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FriendVisit] 🔴 RPC water_friend_plant ล้มเหลว: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
                yield break;
            }

            // RPC แบบ returns table จะตอบกลับเป็น array เสมอ เอาแถวแรก
            var list = JsonConvert.DeserializeObject<List<WaterFriendResult>>(request.downloadHandler.text);
            if (list == null || list.Count == 0)
            {
                onError?.Invoke("Empty response");
                yield break;
            }

            Debug.Log($"[FriendVisit] 💧 {list[0].message} (first today: {list[0].is_first_today})");
            onResult?.Invoke(list[0]);
        }
    }

    // ==================== Buddy Avatar (รูปโปรไฟล์ = รูปบัดดี้ของผู้เล่น) ====================
    // รูปโปรไฟล์ในหน้าเพื่อนทุกหน้าใช้รูปบัดดี้ตาม MBTI (16 แบบ) จาก Resources/buddy/{mbti}
    // ตาราง user เก็บแค่ buddy_id (เช่น BUDDY_LION) เลยต้องโหลดตาราง buddy มาจับคู่ buddy_id -> mbti_type ก่อน (โหลดครั้งเดียว)
    private Dictionary<string, string> buddyMbtiMap = null;
    private class BuddyMbtiRow { public string buddy_id; public string mbti_type; }

    public IEnumerator LoadBuddyMbtiMap()
    {
        if (buddyMbtiMap != null) yield break;   // โหลดแล้ว

        yield return GetRest("buddy?select=buddy_id,mbti_type", json =>
        {
            var rows = JsonConvert.DeserializeObject<List<BuddyMbtiRow>>(json) ?? new List<BuddyMbtiRow>();
            buddyMbtiMap = new Dictionary<string, string>();
            foreach (var r in rows)
            {
                if (!string.IsNullOrEmpty(r.buddy_id) && !string.IsNullOrEmpty(r.mbti_type))
                    buddyMbtiMap[r.buddy_id.Trim().ToUpper()] = r.mbti_type.Trim().ToLower();
            }
        });
    }

    /// <summary> รูปโปรไฟล์จาก buddy_id (ต้องเรียก LoadBuddyMbtiMap ก่อน) ถ้าหาไม่เจอคืน null = ใช้รูปเดิมใน prefab </summary>
    public Sprite GetBuddyAvatarSprite(string buddyId)
    {
        if (string.IsNullOrEmpty(buddyId) || buddyMbtiMap == null) return null;
        if (!buddyMbtiMap.TryGetValue(buddyId.Trim().ToUpper(), out string mbti)) return null;
        return LoadBuddySprite("buddy/" + mbti);
    }

    /// <summary> โหลดรูปบัดดี้จาก Resources ให้ได้ทั้งรูปแบบ Single และ Multiple
    /// (รูปที่ตั้ง Sprite Mode = Multiple เช่น main_buddy/infj, buddy/enfj ใช้ Resources.Load ตรงๆ จะได้ null) </summary>
    public Sprite LoadBuddySprite(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;
        // รูปแบบ Multiple : ในไฟล์มีหลายชิ้น (เช่น infj, infj short_1, ...) เลือกชิ้นที่ชื่อตรงกับชื่อไฟล์
        Sprite[] parts = Resources.LoadAll<Sprite>(path);
        string fileName = path.Substring(path.LastIndexOf('/') + 1);
        foreach (Sprite part in parts) if (part.name == fileName) return part;
        return parts.Length > 0 ? parts[0] : null;
    }
    // ==================== Plant Store (ร้านค้า) ====================
    // ตาราง shop_item = ของที่ขาย (ราคา + ใช้แต้มอะไร), shop_item_drop = โอกาสสุ่มของกล่อง/ซองสุ่ม
    // การซื้อเรียก RPC buy_shop_item (ไฟล์ SQL: store_setup.sql)
    //   DB ทำให้ทั้งหมดในครั้งเดียว: เช็คแต้มพอไหม -> หักแต้ม -> สุ่มตาม weight (ถ้าเป็นกล่องสุ่ม) -> ใส่ inventory
    //   ทำฝั่ง DB เพื่อไม่ให้ผู้เล่นโกงผลสุ่ม/แต้มจากในเครื่อง
    [Serializable]
    public class ShopItemData
    {
        public string shop_item_id;     // เช่น POOL_NORMAL, SHOP_SEED_CHERRY
        public string item_name;        // ชื่อที่โชว์ในร้าน
        public string item_type;        // GACHA_POOL (สุ่ม) / DIRECT_ITEM (ได้ชัวร์)
        public string target_item_id;   // ของที่ได้ (เฉพาะ DIRECT_ITEM)
        public int price_amount;        // ราคา
        public string price_type;       // flower_points / green_points
        public string description;
    }

    [Serializable]
    public class ShopDropData
    {
        public string shop_item_id;
        public string item_id;
        public int weight;
        public string chance_display;   // เช่น "50%" ไว้โชว์โอกาสสุ่ม
        public DropItemName item;       // ชื่อของจากตาราง item (ดึงมาพร้อมกันผ่าน foreign key)
    }

    [Serializable]
    public class DropItemName
    {
        public string item_name;
    }

    [Serializable]
    public class BuyShopResult
    {
        public bool success;            // true = ซื้อสำเร็จ
        public string got_item_id;      // ของที่ได้ (สุ่มได้อะไร)
        public string got_item_name;
        public int new_flower_points;   // แต้มหลังซื้อ (เอาไปอัปเดตหน้าจอ)
        public int new_green_points;
        public string message;          // Success / Not enough flower points / ...
    }

    /// <summary> โหลดรายการของในร้าน (เรียงตามราคา) </summary>
    public IEnumerator GetShopItems(Action<List<ShopItemData>> onResult, Action<string> onError = null)
    {
        yield return GetRest("shop_item?select=*&order=price_amount.asc", json =>
        {
            onResult?.Invoke(JsonConvert.DeserializeObject<List<ShopItemData>>(json) ?? new List<ShopItemData>());
        }, onError);
    }

    /// <summary> โหลดโอกาสสุ่มของกล่อง/ซองสุ่ม (ไว้โชว์ว่ามีอะไรบ้าง) </summary>
    public IEnumerator GetShopDrops(Action<List<ShopDropData>> onResult, Action<string> onError = null)
    {
        yield return GetRest("shop_item_drop?select=*,item:item_id(item_name)&order=weight.desc", json =>
        {
            onResult?.Invoke(JsonConvert.DeserializeObject<List<ShopDropData>>(json) ?? new List<ShopDropData>());
        }, onError);
    }

    public IEnumerator BuyShopItem(string shopItemId, Action<BuyShopResult> onResult, Action<string> onError = null)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId)) { onError?.Invoke("No active user"); yield break; }

        string endpoint = $"{supabaseUrl}/rest/v1/rpc/buy_shop_item";
        string jsonBody = JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            { "p_user_id", userId },
            { "p_shop_item_id", shopItemId }
        });

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            SetSupabaseHeaders(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Store] 🔴 RPC buy_shop_item ล้มเหลว: {request.error} | {request.downloadHandler.text}");
                onError?.Invoke(request.error);
                yield break;
            }

            // RPC แบบ returns table จะตอบกลับเป็น array เสมอ เอาแถวแรก
            var list = JsonConvert.DeserializeObject<List<BuyShopResult>>(request.downloadHandler.text);
            if (list == null || list.Count == 0) { onError?.Invoke("Empty response"); yield break; }

            BuyShopResult result = list[0];
            // ⭐ DB หักแต้มไปแล้ว ต้องอัปเดตค่าในเครื่องให้ตรง
            // (ไม่งั้นตอนบันทึก EXP ครั้งถัดไป SavePlayerStatsRoutine จะเขียนแต้มเก่าทับกลับไป)
            playerFlowerPoints = result.new_flower_points;
            playerGreenPoints = result.new_green_points;
            OnPlayerStatsChanged?.Invoke();

            Debug.Log($"[Store] 🛒 {result.message} ({shopItemId} -> {result.got_item_id})");
            onResult?.Invoke(result);
        }
    }

    // ==================== Certificate OCR (แลก Green Point ด้วยวุฒิบัตร) ====================
    // ส่งรูปวุฒิบัตรไปที่ Edge Function "verify-certificate" บน Supabase
    // ฝั่งเซิร์ฟเวอร์ใช้ Typhoon OCR อ่านข้อความ แล้วเช็คตามโค้ดเพื่อน (ocr_pukjiw.py) + กันใช้ใบเดิมซ้ำ
    // ผ่าน = +100 green_points, บันทึก certificate_submission + behavior_log (submit_tree_cert) ให้เลย
    // คีย์ Typhoon เก็บใน Supabase Secrets ไม่ได้อยู่ในเกม
    [Serializable]
    public class CertificateResult
    {
        public bool success;            // true = ผ่าน ได้แต้ม
        public string reason;           // เหตุผล (โชว์ให้ผู้เล่นได้เลย เป็นภาษาไทย)
        public int points_awarded;      // แต้มที่ได้ (100)
        public int new_green_points;    // แต้มเขียวหลังได้รับ
        public string certificate_no;   // เลขลำดับบนวุฒิบัตร
    }

    public IEnumerator SubmitCertificate(byte[] imageBytes, string fileName, Action<CertificateResult> onResult, Action<string> onError = null)
    {
        string userId = GetActiveUserId();
        if (string.IsNullOrEmpty(userId)) { onError?.Invoke("No active user"); yield break; }

        // ส่งแบบ form (user_id + ไฟล์รูป)
        WWWForm form = new WWWForm();
        form.AddField("user_id", userId);
        form.AddBinaryData("file", imageBytes, fileName, "image/jpeg");

        using (UnityWebRequest request = UnityWebRequest.Post($"{supabaseUrl}/functions/v1/verify-certificate", form))
        {
            // ห้ามใช้ SetSupabaseHeaders ตรงนี้ เพราะมันตั้ง Content-Type เป็น json แต่ตัวนี้ต้องเป็น form
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.timeout = 90;   // OCR ใช้เวลาหลายวินาที

            yield return request.SendWebRequest();

            string text = request.downloadHandler != null ? request.downloadHandler.text : "";
            CertificateResult result = null;
            try { result = JsonConvert.DeserializeObject<CertificateResult>(text); } catch { }

            // ฝั่งเซิร์ฟเวอร์ตอบเหตุผลมาเสมอ ถึงจะเป็น error ก็เอาเหตุผลนั้นไปโชว์
            if (result == null || string.IsNullOrEmpty(result.reason))
            {
                Debug.LogError($"[Certificate] 🔴 ส่งวุฒิบัตรล้มเหลว: {request.error} | {text}");
                onError?.Invoke(string.IsNullOrEmpty(request.error) ? "Unknown error" : request.error);
                yield break;
            }

            if (result.success)
            {
                playerGreenPoints = result.new_green_points;
                OnPlayerStatsChanged?.Invoke();
            }
            Debug.Log($"[Certificate] 📜 success={result.success} reason={result.reason} no={result.certificate_no}");
            onResult?.Invoke(result);
        }
    }
}
