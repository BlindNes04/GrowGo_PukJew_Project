using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;

[RequireComponent(typeof(CanvasGroup))]
public class SettingPopupController : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDuration = 0.2f;

    [Header("ปรับเสียง (สไลเดอร์ 0-100%)")]
    public Slider musicSlider;   // MusicSlideBar
    public Slider sfxSlider;     // SFXSlideBar

    [Header("ข้อมูลบัญชี (ดึงค่าของผู้เล่นมาใส่ Placeholder)")]
    public TextMeshProUGUI userTagText;             // ข้อความใต้รูปโปรไฟล์ : user_id + user_tag เช่น USER_001#00001
    public Image profileImage;                      // 🤝 รูปโปรไฟล์ (PFP/Picture) = รูปบัดดี้ตาม MBTI เหมือนหน้าเพื่อน
    public TMP_InputField idInput;                  // ดูได้อย่างเดียว : โชว์ user_id ของผู้เล่น (เช่น USER_001)
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;               // ดูได้อย่างเดียว แก้ไม่ได้
    public TMP_InputField passwordInput;
    public Button editUsernameButton;               // ปุ่มดินสอข้างช่องชื่อ
    public Button editPasswordButton;               // ปุ่มดินสอข้างช่องรหัสผ่าน
    public TextMeshProUGUI accountMessageText;      // (ไม่บังคับ) ข้อความแจ้ง เช่น "ชื่อนี้มีคนใช้แล้ว"
    public int minPasswordLength = 6;

    [Header("ลบบัญชี")]
    public GameObject confirmDeletePopup;          // ป๊อปอัปถามยืนยัน (ไม่ใส่ก็ได้ จะลบทันทีเมื่อกดปุ่ม)
    public string sceneAfterDelete = "MainMenu";   // ลบเสร็จแล้วกลับไปฉากนี้

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void OnEnable()
    {
        LoadAccountInfo();
    }

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    // ==================== ปรับเสียง ====================
    // ตั้งค่าสไลเดอร์ตามระดับเสียงที่บันทึกไว้ แล้วผูกให้เลื่อนแล้วเปลี่ยนเสียงทันที
    private void Start()
    {
        // ค่าเริ่มอ่านจาก PlayerPrefs (ที่เดียวกับ MAudioManager) เลยตรงกันเสมอ
        // หมายเหตุ: MAudioManager อยู่ในฉาก MainMenu ถ้ากด Play จาก MainForest ตรงๆ จะไม่มีตัวเล่นเสียง
        //           แต่ค่ายังถูกบันทึก และจะใช้ตอนเข้าเกมผ่าน MainMenu
        if (musicSlider != null)
        {
            musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }

        // ข้อมูลบัญชี : ปุ่มดินสอ + ตอนพิมพ์เสร็จ
        if (editUsernameButton != null) editUsernameButton.onClick.AddListener(() => BeginEdit(usernameInput));
        if (editPasswordButton != null) editPasswordButton.onClick.AddListener(() => BeginEdit(passwordInput));
        if (usernameInput != null) usernameInput.onEndEdit.AddListener(SaveUsername);
        if (passwordInput != null)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;   // พิมพ์แล้วขึ้นเป็นจุด
            passwordInput.onEndEdit.AddListener(SavePassword);
        }
        LockAccountInputs();
    }

    private void OnMusicSliderChanged(float v)
    {
        PlayerPrefs.SetFloat("MusicVolume", v);
        if (MAudioManager.instance != null) MAudioManager.instance.SetMusicVolume(v);
    }

    private void OnSfxSliderChanged(float v)
    {
        PlayerPrefs.SetFloat("SfxVolume", v);
        if (MAudioManager.instance != null) MAudioManager.instance.SetSfxVolume(v);
    }

    // ==================== ข้อมูลบัญชี ====================
    // เปิดหน้าตั้งค่า -> ดึง username / email ของผู้เล่นจาก DB มาใส่ Placeholder
    private void LoadAccountInfo()
    {
        SupabaseManager db = SupabaseManager.Instance;
        string uid = db.GetActiveUserId();
        if (string.IsNullOrEmpty(uid)) { Debug.LogWarning("[Setting] ไม่มี user id เลยดึงข้อมูลบัญชีไม่ได้"); return; }
        if (!gameObject.activeInHierarchy) return;

        // 📦 โชว์จาก cache ก่อน (โหลดไว้ตอนเปิดเกม) ไม่ต้องเห็นช่องว่าง/ค่า default ระหว่างรอ DB
        if (db.playerStatsLoaded)
        {
            if (userTagText != null) userTagText.text = uid + db.playerTag;
            SetPlaceholder(idInput, uid);
            SetPlaceholder(usernameInput, db.playerUsername);
            SetPlaceholder(emailInput, db.playerEmail);
            SetPlaceholder(passwordInput, "********");
            if (profileImage != null && !string.IsNullOrEmpty(db.playerBuddyId)) StartCoroutine(LoadProfileImage(db.playerBuddyId));
        }

        // แล้วค่อยดึงจาก DB ใหม่เงียบๆ
        StartCoroutine(db.GetRest($"user?select=user_id,user_tag,username,email,buddy_id&user_id=eq.{uid}", json =>
        {
            Debug.Log($"[Setting] ข้อมูลบัญชี: {json}");
            var rows = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
            if (rows == null || rows.Count == 0) return;
            string userId = rows[0].ContainsKey("user_id") ? rows[0]["user_id"] : "";
            string userTag = rows[0].ContainsKey("user_tag") ? rows[0]["user_tag"] : "";
            if (userTagText != null) userTagText.text = userId + userTag;   // ต่อกัน เช่น USER_001#00001
            SetPlaceholder(idInput, userId);
            SetPlaceholder(usernameInput, rows[0].ContainsKey("username") ? rows[0]["username"] : "");
            SetPlaceholder(emailInput, rows[0].ContainsKey("email") ? rows[0]["email"] : "");
            SetPlaceholder(passwordInput, "********");   // ไม่โชว์รหัสจริง

            // 🤝 รูปโปรไฟล์ : buddy_id -> mbti (ตาราง buddy) -> Resources/buddy/{mbti} ชุดเดียวกับหน้าเพื่อน
            string buddyId = rows[0].ContainsKey("buddy_id") ? rows[0]["buddy_id"] : "";
            if (profileImage != null && gameObject.activeInHierarchy) StartCoroutine(LoadProfileImage(buddyId));
        }));
    }

    // 🤝 โหลดรูปบัดดี้มาเป็นรูปโปรไฟล์ (ถ้าหาไม่เจอ ใช้รูปเดิมที่ตั้งไว้ใน Inspector)
    private IEnumerator LoadProfileImage(string buddyId)
    {
        yield return SupabaseManager.Instance.LoadBuddyMbtiMap();
        Sprite avatar = SupabaseManager.Instance.GetBuddyAvatarSprite(buddyId);
        if (avatar != null)
        {
            profileImage.sprite = avatar;
            profileImage.preserveAspect = true;
        }
    }

    private void SetPlaceholder(TMP_InputField input, string value)
    {
        if (input == null || input.placeholder == null) return;
        TMP_Text ph = input.placeholder as TMP_Text;
        if (ph != null) ph.text = value;
    }

    // ทุกช่องล็อกไว้ก่อน (กดพิมพ์ไม่ได้) จนกว่าจะกดปุ่มดินสอ
    private void LockAccountInputs()
    {
        foreach (TMP_InputField input in new[] { idInput, usernameInput, emailInput, passwordInput })
        {
            if (input == null) continue;
            input.text = "";
            input.interactable = false;
        }
    }

    // กดปุ่มดินสอ -> ปลดล็อกช่องนั้นแล้วเปิดคีย์บอร์ด
    private void BeginEdit(TMP_InputField input)
    {
        if (input == null) return;
        ShowMessage("");
        input.interactable = true;
        input.text = "";
        input.Select();
        input.ActivateInputField();
    }

    // พิมพ์เสร็จ (กด Enter / แตะที่อื่น) -> บันทึกชื่อใหม่
    private void SaveUsername(string newName)
    {
        newName = newName.Trim();
        StartCoroutine(LockNextFrame(usernameInput));
        if (string.IsNullOrEmpty(newName)) { usernameInput.text = ""; return; }   // ไม่ได้พิมพ์อะไร = ยกเลิก

        StartCoroutine(SupabaseManager.Instance.UpdateUserField("username", newName,
            onSuccess: () => { SetPlaceholder(usernameInput, newName); usernameInput.text = ""; ShowMessage("เปลี่ยนชื่อแล้ว"); },
            onError: (err) => { usernameInput.text = ""; ShowMessage(err == "duplicate" ? "ชื่อนี้มีคนใช้แล้ว" : "เปลี่ยนชื่อไม่สำเร็จ"); }));
    }

    // พิมพ์เสร็จ -> บันทึกรหัสผ่านใหม่
    private void SavePassword(string newPassword)
    {
        StartCoroutine(LockNextFrame(passwordInput));
        if (string.IsNullOrEmpty(newPassword)) { passwordInput.text = ""; return; }
        if (newPassword.Length < minPasswordLength)
        {
            passwordInput.text = "";
            ShowMessage($"รหัสผ่านต้องมีอย่างน้อย {minPasswordLength} ตัว");
            return;
        }

        StartCoroutine(SupabaseManager.Instance.UpdateUserField("password", newPassword,
            onSuccess: () => { passwordInput.text = ""; ShowMessage("เปลี่ยนรหัสผ่านแล้ว"); },
            onError: (err) => { passwordInput.text = ""; ShowMessage("เปลี่ยนรหัสผ่านไม่สำเร็จ"); }));
    }

    private IEnumerator LockNextFrame(TMP_InputField input)
    {
        yield return null;
        if (input != null) input.interactable = false;
    }

    private void ShowMessage(string msg)
    {
        if (accountMessageText != null) accountMessageText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log($"[Setting] {msg}");
    }

    // ฟังก์ชันสำหรับ "ปุ่มเปิด"
    public void OpenPopup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // เปิดวัตถุขึ้นมาก่อน แล้วเซ็ตความโปร่งใสเป็น 0 ทันทีเพื่อเตรียม Fade In
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;

        // ดึงข้อมูลบัญชีล่าสุดมาโชว์ทุกครั้งที่เปิด
        LockAccountInputs();
        LoadAccountInfo();   // (ถ้าป๊อปอัปเพิ่งถูกเปิด OnEnable จะโหลดให้ด้วย ไม่เป็นไร)

        FadeTo(1f, true);
    }

    // ฟังก์ชันสำหรับ "ปุ่มปิด"
    public void ClosePopup()
    {
        PlayerPrefs.Save();   // บันทึกระดับเสียงลงเครื่องทันทีตอนปิดหน้าตั้งค่า
        FadeTo(0f, false);
    }

    // ==================== ลบบัญชี ====================
    // ปุ่ม "ลบบัญชี" ในหน้าตั้งค่า -> เปิดป๊อปอัปยืนยันก่อน (กันกดพลาด)
    public void OnDeleteAccountButton()
    {
        if (confirmDeletePopup != null) confirmDeletePopup.SetActive(true);
        else ConfirmDeleteAccount();
    }

    // ปุ่ม "ยกเลิก" ในป๊อปอัปยืนยัน
    public void CancelDeleteAccount()
    {
        if (confirmDeletePopup != null) confirmDeletePopup.SetActive(false);
    }

    // ปุ่ม "ยืนยันลบ" -> ลบข้อมูลใน Supabase ทุกตาราง แล้วกลับไปหน้าเริ่มเกม
    public void ConfirmDeleteAccount()
    {
        // ใช้ coroutine ของ SupabaseManager เพราะอยู่ข้ามฉาก (ป๊อปอัปนี้อาจถูกปิดระหว่างรอ)
        SupabaseManager.Instance.StartCoroutine(SupabaseManager.Instance.DeleteAccount(
            onSuccess: () => SceneManager.LoadScene(sceneAfterDelete),
            onError: (err) => Debug.LogError($"[Setting] 🔴 ลบบัญชีไม่สำเร็จ: {err}")
        ));
    }

    // ==================== ออกจากระบบ ====================
    // ปุ่ม "ออกจากระบบ" -> ลืม id ผู้เล่นที่จำไว้ในเครื่อง แล้วกลับไปหน้าเริ่มเกม (ข้อมูลใน Supabase ยังอยู่ครบ ล็อกอินใหม่ได้)
    public void SignOut()
    {
        // ล้าง id ผู้เล่น + รหัส session + cache เลเวล/แต้ม ให้โหลดของคนใหม่ตอนล็อกอิน
        if (SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.ClearLocalSession();
        }

        SceneManager.LoadScene(sceneAfterDelete);   // ใช้ฉากเดียวกับตอนลบบัญชี (MainMenu)
    }

    private void FadeTo(float targetAlpha, bool isOpening)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, isOpening));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool isOpening)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        if (isOpening)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (!isOpening)
        {
            gameObject.SetActive(false);
        }
    }
}