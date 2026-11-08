using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class StaminaManager : MonoBehaviour
{
    public static StaminaManager Instance { get; private set; }

    [Header("Debug / Development Settings")]
    [Tooltip("เปิดไว้เพื่อทดสอบแบบ Offline / ปิดเมื่อต้องการเชื่อม Supabase DB จริง")]
    public bool useMockData = false; 

    [Header("Stamina Settings")]
    [SerializeField] private int maxStamina = 30;
    [SerializeField] private int currentStamina = 30;

    public int MaxStamina => maxStamina;
    public int CurrentStamina
    {
        get => currentStamina;
        private set
        {
            currentStamina = value;
            // แจ้งเตือนไปยัง UI ให้รีเฟรชหลอดพลังงาน
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    // Event สำหรับให้ UI มาดักรับค่าไปอัปเดตหน้าจอ
    public static event Action<int, int> OnStaminaChanged;

    private void Awake()
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

    private async void Start()
    {
        // ดึงค่า Stamina ล่าสุดเมื่อเข้าเกม
        await FetchStaminaAsync();
    }

    // ==================== 1. ดึงค่า Stamina ล่าสุด ====================
    public async Task FetchStaminaAsync()
    {
        if (useMockData)
        {
            CurrentStamina = maxStamina;
            await Task.Yield();
            return;
        }

        // เรียก GetActiveUserId()   
        string userId = SupabaseManager.Instance != null ? SupabaseManager.Instance.GetActiveUserId() : "";
        
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[StaminaManager] 🔴 ไม่พบ User ID สำหรับดึง Stamina! (กรุณาล็อกอินก่อน)");
            return;
        }

        try
        {
            bool success = await ConsumeStaminaAsync(0);
            if (!success)
            {
                Debug.LogWarning("[StaminaManager] ⚠️ ไม่สามารถดึงค่า Stamina ล่าสุดได้");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StaminaManager] 🔴 Fetch Error: {ex.Message}");
        }
    }

    // ==================== 2. หักค่า Stamina (ผ่าน RPC) ====================
    public async Task<bool> ConsumeStaminaAsync(int cost)
    {
        if (useMockData)
        {
            if (currentStamina >= cost)
            {
                CurrentStamina -= cost;
                return true;
            }
            return false;
        }

        // เรียก GetActiveUserId()   
        string userId = SupabaseManager.Instance != null ? SupabaseManager.Instance.GetActiveUserId() : "";
        
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[StaminaManager] 🔴 ไม่พบ User ID สำหรับหัก Stamina!");
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();

        StartCoroutine(CallConsumeStaminaRpc(userId, cost, (success, updatedStamina, message) =>
        {
            if (success)
            {
                CurrentStamina = updatedStamina;
                Debug.Log($"[StaminaManager] 🟢 หัก Stamina {cost} สำเร็จ! คงเหลือ: {updatedStamina} ({message})");
                tcs.SetResult(true);
            }
            else
            {
                Debug.LogWarning($"[StaminaManager] 🟡 หัก Stamina ไม่สำเร็จ: {message} (คงเหลือ: {updatedStamina})");
                CurrentStamina = updatedStamina;
                tcs.SetResult(false);
            }
        }));

        return await tcs.Task;
    }

    // ==================== 3. Coroutine ยิง Web Request ไปยัง Supabase RPC ====================
    private IEnumerator CallConsumeStaminaRpc(string userId, int cost, Action<bool, int, string> callback)
    {
        if (SupabaseManager.Instance == null)
        {
            Debug.LogError("[StaminaManager] 🔴 SupabaseManager.Instance เป็น NULL!");
            callback?.Invoke(false, currentStamina, "SupabaseManager is null");
            yield break;
        }

        string url = $"{SupabaseManager.Instance.supabaseUrl}/rest/v1/rpc/consume_stamina";
        
        var requestData = new ConsumeStaminaRequest
        {
            p_user_id = userId,
            p_cost = cost
        };

        string jsonBody = JsonConvert.SerializeObject(requestData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", SupabaseManager.Instance.supabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {SupabaseManager.Instance.supabaseAnonKey}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Debug.Log($"[StaminaManager] 📩 Raw JSON Response: {request.downloadHandler.text}");

                    var responseList = JsonConvert.DeserializeObject<ConsumeStaminaResponse[]>(request.downloadHandler.text);

                    if (responseList != null && responseList.Length > 0)
                    {
                        var res = responseList[0];
                        int actualStamina = res.GetStaminaValue();

                        callback?.Invoke(res.success, actualStamina, res.message);
                    }
                    else
                    {
                        callback?.Invoke(false, currentStamina, "Empty response array");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[StaminaManager] 🔴 JSON Parse Error: {ex.Message} | Response: {request.downloadHandler.text}");
                    callback?.Invoke(false, currentStamina, "JSON Parse Error");
                }
            }
            else
            {
                Debug.LogError($"[StaminaManager] 🔴 RPC Request Failed: {request.error} | Response: {request.downloadHandler.text}");
                callback?.Invoke(false, currentStamina, request.error);
            }
        }
    }

    // ==================== Data Classes สำหรับ JSON ====================
    [Serializable]
    private class ConsumeStaminaRequest
    {
        public string p_user_id;
        public int p_cost;
    }

    [Serializable]
    public class ConsumeStaminaResponse
    {
        [JsonProperty("success")]
        public bool success;

        [JsonProperty("current_stamina")]
        public int current_stamina;

        [JsonProperty("stamina")]
        public int stamina;

        [JsonProperty("v_stamina")]
        public int v_stamina;

        [JsonProperty("message")]
        public string message;

        public int GetStaminaValue()
        {
            if (current_stamina != 0) return current_stamina;
            if (stamina != 0) return stamina;
            if (v_stamina != 0) return v_stamina;
            return 0;
        }
    }
}