using System;
using System.Threading.Tasks;
using UnityEngine;

public class StaminaManager : MonoBehaviour
{
    public static StaminaManager Instance { get; private set; }

    [Header("Debug / Development Settings")]
    [Tooltip("Local Test")]
    public bool useMockData = true; 

    [Header("Stamina Settings")]
    [Tooltip("Max Stamina")]
    [SerializeField] private int maxStamina = 30;

    [Tooltip("Current Stamina")]
    [SerializeField] private int currentStamina = 30;

    public int MaxStamina => maxStamina;
    public int CurrentStamina => currentStamina;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public async Task FetchStaminaAsync()
    {
        if (useMockData)
        {
            // โหมดทดสอบ
            currentStamina = maxStamina;
            await Task.Yield();
            return;
        }

        // string userId = AuthManager.Instance.CurrentUserId;
        // CurrentStamina = await SupabaseService.GetStamina(userId);
    }

    public async Task<bool> ConsumeStaminaAsync(int cost)
    {
        if (useMockData)
        {
            // โหมดทดสอบ
            if (currentStamina >= cost)
            {
                currentStamina -= cost;
                return true;
            }
            return false;
        }

        try
        {
            // string userId = AuthManager.Instance.CurrentUserId;
            // var result = await SupabaseClient.Instance.Rpc("consume_stamina", new { p_user_id = userId, p_cost = cost });
            // if (result.IsSuccess) {
            //     CurrentStamina = result.CurrentStamina;
            //     return true;
            // }
            // return false;

            await Task.Yield();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StaminaManager] Deduct Error: {ex.Message}");
            return false;
        }
    }
}

    [System.Serializable]
    public class ConsumeStaminaResponse
    {
        public bool success;
        public int current_stamina;
        public string message;
    }