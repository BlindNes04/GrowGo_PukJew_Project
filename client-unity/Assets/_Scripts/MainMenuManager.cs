using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject loginPanel;

    [Header("UI Cards (Canvas Group)")]
    [SerializeField] private CanvasGroup loginMethodCard;
    [SerializeField] private CanvasGroup loginCard;
    [SerializeField] private CanvasGroup registerCard;

    [Header("Login Card Inputs")]
    [SerializeField] private TMP_InputField loginUsernameInput; // กรอกได้ทั้ง User ID หรือ Email
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private TextMeshProUGUI loginStatusText;
    [SerializeField] private Button loginSubmitButton;

    [Header("Register Card Inputs")]
    [SerializeField] private TMP_InputField registerUsernameInput;
    [SerializeField] private TMP_InputField registerEmailInput;
    [SerializeField] private TMP_InputField registerPasswordInput;
    [SerializeField] private TMP_InputField registerConfirmPasswordInput;
    [SerializeField] private TextMeshProUGUI registerStatusText;
    [SerializeField] private Button registerSubmitButton;

    [Header("Sequence Settings")]
    [Tooltip("ระยะเวลารอ Logo เล่นจบ (วินาที) ก่อนเปิดการ์ด")]
    [SerializeField] private float logoWaitTime = 0.8f;
    [SerializeField] private float fadeDuration = 0.2f;

    private CanvasGroup currentActiveCard;
    private bool isSwitching = false;

    private void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);

        // ดึง ID ที่เคยล็อกอินค้างไว้ในเครื่อง (ถ้ามี)
        string savedUserId = PlayerPrefs.GetString("SavedUserId", "");
        if (!string.IsNullOrEmpty(savedUserId) && SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.currentUserId = savedUserId;
            Debug.Log($"[MainMenuManager] 🟢 สานต่อ Session ผู้เล่นเดิม ID: {savedUserId}");
        }

        // ผูก Event ปุ่มกด
        if (loginSubmitButton != null) loginSubmitButton.onClick.AddListener(OnSubmitLogin);
        if (registerSubmitButton != null) registerSubmitButton.onClick.AddListener(OnSubmitRegister);

        ClearStatusTexts();
    }

    // --- 1. ปุ่ม "เข้าสู่ระบบ" จากหน้าแรก ---
    public void OnClickOpenLogin()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loginPanel != null) loginPanel.SetActive(true);

        ClearStatusTexts();

        SetupCard(loginMethodCard, false);
        SetupCard(loginCard, false);
        SetupCard(registerCard, false);

        StartCoroutine(ShowMethodCardAfterLogo());
    }

    public void OnClickPlay()
    {
        // 1. ดึง Active User ID ผ่าน SupabaseManager
        string activeUserId = SupabaseManager.Instance != null ? SupabaseManager.Instance.GetActiveUserId() : "";

        // ถ้ายังไม่ได้ล็อกอิน ให้ไปหน้าล็อกอินตามปกติ
        if (string.IsNullOrEmpty(activeUserId))
        {
            OnClickOpenLogin();
            ShowStatus(loginStatusText, "กรุณาเข้าสู่ระบบก่อนเข้าเล่น!", Color.red);
            return;
        }

        // 2. ล็อกอินแล้ว -> ยิงเช็ก buddy_id จาก Supabase DB
        StartCoroutine(SupabaseManager.Instance.GetUserBuddyId(
            onResult: (buddyId) =>
            {
                // เช็ค buddy_id
                if (string.IsNullOrEmpty(buddyId))
                {
                    Debug.Log("[MainMenu] 🟢 ผู้เล่นใหม่ยังไม่มี Buddy -> ไปหน้า Quiz");
                    SceneManager.LoadScene("Quiz");
                }
                else
                {
                    Debug.Log($"[MainMenu] 🟢 ผู้เล่นมี Buddy แล้ว ({buddyId}) -> ไปหน้า MainForest");
                    SceneManager.LoadScene("MainForest");
                }
            },
            onError: (err) =>
            {
                Debug.LogError($"[MainMenu] 🔴 เช็ก Buddy จาก DB ล้มเหลว: {err}");
                ShowStatus(loginStatusText, "ไม่สามารถเชื่อมต่อฐานข้อมูลได้ กรุณาลองใหม่!", Color.red);
            }
        ));
    }

    private IEnumerator ShowMethodCardAfterLogo()
    {
        yield return new WaitForSeconds(logoWaitTime);
        SetupCard(loginMethodCard, true);
        currentActiveCard = loginMethodCard;
    }

    // --- 2. ฟังก์ชันสลับการ์ด ---
    public void OpenLogin()
    {
        ClearStatusTexts();
        SwitchToCard(loginCard);
    }

    public void OpenRegister()
    {
        ClearStatusTexts();
        SwitchToCard(registerCard);
    }

    public void BackToMethod()
    {
        ClearStatusTexts();
        SwitchToCard(loginMethodCard);
    }

    public void BackToMainMenu()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    // ==================== 3. ระบบ LOGIN (รองรับ ID/Email + เช็กรหัสผ่านจริง) ====================
    private void OnSubmitLogin()
    {
        string identifier = loginUsernameInput != null ? loginUsernameInput.text.Trim() : "";
        string password = loginPasswordInput != null ? loginPasswordInput.text : "";

        // เช็กความถูกต้องของ Input
        if (string.IsNullOrEmpty(identifier))
        {
            ShowStatus(loginStatusText, "กรุณากรอกชื่อผู้ใช้หรืออีเมล!", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus(loginStatusText, "กรุณากรอกรหัสผ่าน!", Color.red);
            return;
        }

        Color orangeColor = new Color(1f, 0.5f, 0f);
        ShowStatus(loginStatusText, "กำลังเข้าสู่ระบบ...", orangeColor);
        if (loginSubmitButton != null) loginSubmitButton.interactable = false;

        StartCoroutine(LoginCoroutine(identifier, password));
    }

    private IEnumerator LoginCoroutine(string identifier, string password)
    {
        // ค้นหาด้วย user_id หรือ email ที่ตรงกับ identifier ที่กรอกมา
        string encodedIdentifier = UnityWebRequest.EscapeURL(identifier);
        string url = $"{SupabaseManager.Instance.supabaseUrl}/rest/v1/user?select=user_id,username,user_tag,password&or=(user_id.eq.{encodedIdentifier},email.eq.{encodedIdentifier})";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("apikey", SupabaseManager.Instance.supabaseAnonKey);
            req.SetRequestHeader("Authorization", $"Bearer {SupabaseManager.Instance.supabaseAnonKey}");
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (loginSubmitButton != null) loginSubmitButton.interactable = true;

            if (req.result == UnityWebRequest.Result.Success)
            {
                var users = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(req.downloadHandler.text);

                if (users != null && users.Count > 0)
                {
                    var user = users[0];
                    string dbPassword = user.ContainsKey("password") ? user["password"] : "";

                    // เช็กรหัสผ่านว่าตรงกับใน DB หรือไม่
                    if (dbPassword == password)
                    {
                        string userId = user["user_id"];
                        string username = user.ContainsKey("username") ? user["username"] : userId;

                        SupabaseManager.Instance.currentUserId = userId;
                        PlayerPrefs.SetString("SavedUserId", userId);
                        PlayerPrefs.Save();

                        StartCoroutine(UpdateLastLoginTime(userId));

                        ShowStatus(loginStatusText, $"ยินดีต้อนรับคุณ {username}!", Color.green);
                        Debug.Log($"[Auth] 🟢 ล็อกอินสำเร็จ! User ID: {userId}");

                        yield return new WaitForSeconds(1.0f);
                        OnClickPlay();
                    }
                    else
                    {
                        // กรณีรหัสผ่านไม่ตรง
                        ShowStatus(loginStatusText, "รหัสผ่านไม่ถูกต้อง!", Color.red);
                    }
                }
                else
                {
                    // ไม่พบ User ID หรือ Email นี้ใน DB
                    ShowStatus(loginStatusText, "ไม่พบชื่อผู้ใช้หรืออีเมลนี้!", Color.red);
                }
            }
            else
            {
                Debug.LogError($"[Auth Error] {req.error} | {req.downloadHandler.text}");
                ShowStatus(loginStatusText, "เชื่อมต่อฐานข้อมูลล้มเหลว", Color.red);
            }
        }
    }

    // ==================== 4. ระบบ REGISTER ====================
    private void OnSubmitRegister()
    {
        string username = registerUsernameInput != null ? registerUsernameInput.text.Trim() : "";
        string email = registerEmailInput != null ? registerEmailInput.text.Trim() : "";
        string password = registerPasswordInput != null ? registerPasswordInput.text : "";
        string confirmPassword = registerConfirmPasswordInput != null ? registerConfirmPasswordInput.text : "";

        if (string.IsNullOrEmpty(username))
        {
            ShowStatus(registerStatusText, "กรุณากรอกชื่อผู้ใช้!", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(email))
        {
            ShowStatus(registerStatusText, "กรุณากรอกอีเมล!", Color.red);
            return;
        }

        if (!email.Contains("@") || !email.Contains("."))
        {
            ShowStatus(registerStatusText, "รูปแบบอีเมลไม่ถูกต้อง!", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus(registerStatusText, "กรุณากรอกรหัสผ่าน!", Color.red);
            return;
        }

        if (password != confirmPassword)
        {
            ShowStatus(registerStatusText, "ยืนยันรหัสผ่านไม่ถูกต้อง", Color.red);
            return;
        }

        Color orangeColor = new Color(1f, 0.5f, 0f);
        ShowStatus(registerStatusText, "กำลังตรวจสอบข้อมูล...", orangeColor);
        if (registerSubmitButton != null) registerSubmitButton.interactable = false;

        StartCoroutine(RegisterCoroutine(username, email, password));
    }

    private IEnumerator RegisterCoroutine(string username, string email, string password)
    {
        // 1. เช็กก่อนว่ามี ID นี้อยู่ใน DB หรือยัง
        string checkUrl = $"{SupabaseManager.Instance.supabaseUrl}/rest/v1/user?select=user_id&user_id=eq.{UnityWebRequest.EscapeURL(username)}";

        using (UnityWebRequest checkReq = UnityWebRequest.Get(checkUrl))
        {
            checkReq.SetRequestHeader("apikey", SupabaseManager.Instance.supabaseAnonKey);
            checkReq.SetRequestHeader("Authorization", $"Bearer {SupabaseManager.Instance.supabaseAnonKey}");
            checkReq.SetRequestHeader("Content-Type", "application/json");

            yield return checkReq.SendWebRequest();

            if (checkReq.result != UnityWebRequest.Result.Success)
            {
                if (registerSubmitButton != null) registerSubmitButton.interactable = true;
                ShowStatus(registerStatusText, "เชื่อมต่อฐานข้อมูลล้มเหลว", Color.red);
                yield break;
            }

            var existing = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(checkReq.downloadHandler.text);
            if (existing != null && existing.Count > 0)
            {
                if (registerSubmitButton != null) registerSubmitButton.interactable = true;
                ShowStatus(registerStatusText, "ชื่อผู้ใช้นี้มีการใช้งานแล้ว", Color.red);
                yield break;
            }
        }

        // 2. สุ่ม user_tag ที่ไม่ซ้ำกับใครใน DB
        string newUserTag = "";
        bool isTagUnique = false;
        int safetyLoop = 0;

        while (!isTagUnique && safetyLoop < 10)
        {
            safetyLoop++;
            string candidateTag = $"#{UnityEngine.Random.Range(1000, 9999)}";
            string tagCheckUrl = $"{SupabaseManager.Instance.supabaseUrl}/rest/v1/user?select=user_tag&user_tag=eq.{UnityWebRequest.EscapeURL(candidateTag)}";

            using (UnityWebRequest tagReq = UnityWebRequest.Get(tagCheckUrl))
            {
                tagReq.SetRequestHeader("apikey", SupabaseManager.Instance.supabaseAnonKey);
                tagReq.SetRequestHeader("Authorization", $"Bearer {SupabaseManager.Instance.supabaseAnonKey}");
                tagReq.SetRequestHeader("Content-Type", "application/json");

                yield return tagReq.SendWebRequest();

                if (tagReq.result == UnityWebRequest.Result.Success)
                {
                    var existingTags = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(tagReq.downloadHandler.text);
                    if (existingTags == null || existingTags.Count == 0)
                    {
                        newUserTag = candidateTag;
                        isTagUnique = true;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(newUserTag))
        {
            newUserTag = $"#{UnityEngine.Random.Range(10000, 99999)}";
        }

        // 3. บันทึกข้อมูลลงตาราง user
        string fixedUserId = username;
        string nowIso = DateTime.UtcNow.ToString("o");

        var payload = new Dictionary<string, object>
        {
            { "user_id", fixedUserId },
            { "username", username },
            { "email", email },
            { "password", password },
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

        string jsonBody = JsonConvert.SerializeObject(payload);
        string regUrl = $"{SupabaseManager.Instance.supabaseUrl}/rest/v1/user";

        using (UnityWebRequest postReq = new UnityWebRequest(regUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            postReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            postReq.downloadHandler = new DownloadHandlerBuffer();

            postReq.SetRequestHeader("apikey", SupabaseManager.Instance.supabaseAnonKey);
            postReq.SetRequestHeader("Authorization", $"Bearer {SupabaseManager.Instance.supabaseAnonKey}");
            postReq.SetRequestHeader("Content-Type", "application/json");
            postReq.SetRequestHeader("Prefer", "return=minimal");

            yield return postReq.SendWebRequest();

            if (registerSubmitButton != null) registerSubmitButton.interactable = true;

            if (postReq.result == UnityWebRequest.Result.Success)
            {
                SupabaseManager.Instance.currentUserId = fixedUserId;
                PlayerPrefs.SetString("SavedUserId", fixedUserId);
                PlayerPrefs.Save();

                ShowStatus(registerStatusText, $"สมัครสำเร็จ! แท็กของคุณคือ {newUserTag}", Color.green);
                Debug.Log($"[Auth] 🟢 สมัครสมาชิกสำเร็จ! ID: {fixedUserId} | Tag: {newUserTag}");

                yield return new WaitForSeconds(1.2f);
                OnClickPlay();
            }
            else
            {
                Debug.LogError($"[Auth Error] {postReq.error} | {postReq.downloadHandler.text}");
                string responseError = postReq.downloadHandler != null ? postReq.downloadHandler.text : "";

                if (responseError.Contains("23505") || responseError.Contains("duplicate"))
                {
                    ShowStatus(registerStatusText, "ชื่อผู้ใช้นี้มีการใช้งานแล้ว", Color.red);
                }
                else
                {
                    ShowStatus(registerStatusText, "สร้างบัญชีล้มเหลว กรุณาลองใหม่", Color.red);
                }
            }
        }
    }

    private IEnumerator UpdateLastLoginTime(string userId)
    {
        string url = $"{SupabaseManager.Instance.supabaseUrl}/rest/v1/user?user_id=eq.{userId}";
        string jsonBody = $"{{\"last_login_at\":\"{DateTime.UtcNow.ToString("o")}\"}}";

        using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();

            req.SetRequestHeader("apikey", SupabaseManager.Instance.supabaseAnonKey);
            req.SetRequestHeader("Authorization", $"Bearer {SupabaseManager.Instance.supabaseAnonKey}");
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();
        }
    }

    // --- Helper Functions ---
    private void SwitchToCard(CanvasGroup nextCard)
    {
        if (isSwitching || currentActiveCard == nextCard || nextCard == null) return;
        StartCoroutine(SwitchRoutine(currentActiveCard, nextCard));
    }

    private IEnumerator SwitchRoutine(CanvasGroup fromCard, CanvasGroup toCard)
    {
        isSwitching = true;

        if (fromCard != null)
        {
            fromCard.interactable = false;
            fromCard.blocksRaycasts = false;

            float timer = 0f;
            float startAlpha = fromCard.alpha;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fromCard.alpha = Mathf.Lerp(startAlpha, 0f, timer / fadeDuration);
                yield return null;
            }

            fromCard.alpha = 0f;
            fromCard.gameObject.SetActive(false);
        }

        SetupCard(toCard, true);
        currentActiveCard = toCard;

        isSwitching = false;
    }

    private void SetupCard(CanvasGroup cg, bool show)
    {
        if (cg == null) return;

        cg.gameObject.SetActive(show);
        cg.alpha = show ? 1f : 0f;
        cg.interactable = show;
        cg.blocksRaycasts = show;
    }

    private void ShowStatus(TextMeshProUGUI statusLabel, string message, Color color)
    {
        if (statusLabel != null)
        {
            statusLabel.gameObject.SetActive(true);
            statusLabel.text = message;
            statusLabel.color = color;
        }
    }

    private void ClearStatusTexts()
    {
        if (loginStatusText != null)
        {
            loginStatusText.text = "";
            loginStatusText.gameObject.SetActive(false);
        }
        if (registerStatusText != null)
        {
            registerStatusText.text = "";
            registerStatusText.gameObject.SetActive(false);
        }
    }
}