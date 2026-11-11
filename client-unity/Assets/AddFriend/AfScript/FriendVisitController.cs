using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json;

// ==================== FriendVisitController ====================
// คุมฉาก FriendVisit (หน้าเยี่ยมสวนเพื่อน) ทั้งหมด:
//   1) แถบเพื่อน      : รูป ชื่อ เลเวลของเพื่อน + ปุ่มคลิปบอร์ด (คัดลอกชื่อเพื่อน) + กดรูปเพื่อนเปิดป๊อปอัปข้อมูล
//   2) ต้นไม้ของเพื่อน : โชว์ร่างตาม stage_id ที่เพื่อนเลี้ยงอยู่ (ใช้ MCController ตัวเดิม)
//   3) บัดดี้ของเพื่อน : รูปบัดดี้ตาม buddy_id ของเพื่อน
//   4) ปุ่มล่าง 3 ปุ่ม : กลับบ้าน / รดน้ำให้เพื่อน / ไปบ้านเพื่อนคนถัดไป
//
// ⚠️ ฉากนี้ "ดูอย่างเดียว" ไม่ใช้ PlantStatus เพราะ PlantStatus จะเซฟทับต้นไม้ของเราเอง
//    การรดน้ำให้เพื่อนเขียนลง DB ผ่าน RPC water_friend_plant เท่านั้น (SupabaseManager.WaterFriendPlant)
//
// กติการดน้ำให้เพื่อน (คุยกับเจ้าของเกม 11/11/2026):
//   - รดได้เฉพาะต้นที่ "หิวน้ำ" (ครบเวลารดน้ำแล้ว) และไม่ป่วย
//   - แต่ละต้นช่วยรดได้วันละครั้ง (รีเที่ยงคืนเวลาไทย)
//   - ต้นแรกของเพื่อนคนนั้นในวันนี้ : ได้ EXP เต็ม + แต้มดอกไม้สุ่ม
//   - ต้นถัดไปของเพื่อนคนเดิม       : ได้ EXP ครึ่งเดียว ไม่ได้แต้มดอกไม้
//   - เจ้าของต้นได้แค่ความชุ่มชื้น 60% (ไม่เต็มหลอด เจ้าของยังต้องเข้ามารดเอง)
//     DB บันทึก friend_watered_at ไม่แตะ last_watered_at ของเจ้าของ
public class FriendVisitController : MonoBehaviour
{
    public const string SCENE_NAME = "FriendVisit";

    // ---------- ลิสต์เพื่อน (ส่งมาจากหน้า My Friends ก่อนเปลี่ยนฉาก) ----------
    // static = ค่ายังอยู่ตอนเปลี่ยนฉาก (MyFriendsViewController เป็นคนใส่ค่าให้)
    public static List<string> friendIdList = new List<string>();
    public static int currentFriendIndex = 0;

    // ---------- 0) แถบซ้ายบน (PlayerStatusView) : ในหน้านี้โชว์ข้อมูล "เพื่อน" แทนของเรา ----------
    [Header("แถบซ้ายบน (ข้อมูลเพื่อน)")]
    public TextMeshProUGUI topLevelText;      // เลขเลเวลในวงกลม (Lvl)
    public string topLevelFormat = "{0}";
    public Image topExpRing;                  // วงแหวน EXP (BlueFillBar)
    public TextMeshProUGUI topUsernameText;   // Username
    public TextMeshProUGUI[] topLockedTexts;  // FlowerCoins, RelationshipPoint (แต้มเพื่อน = ข้อมูลส่วนตัว ล็อกไว้)
    public Button topProfileButton;           // กดวงกลมเลเวล = เปิดป๊อปอัปข้อมูลเพื่อน (ExpContainer)
    public Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public string lockedText = "???";

    // ---------- 1) แถบเพื่อน ----------
    [Header("แถบเพื่อน")]
    public Image friendPfpImage;              // รูปในวงกลม (FriendPFP/Picture)
    public TextMeshProUGUI friendNameText;    // FriendName
    public TextMeshProUGUI friendLevelText;   // LvlText
    public string friendLevelFormat = "Lv.{0}";
    public Button infoButton;                 // ปุ่มเปิดป๊อปอัปข้อมูลเพื่อน (ใส่ Button ให้ FriendPFP แล้วลากมาใส่)
    public FriendInfoPopupView infoPopup;     // ป๊อปอัปข้อมูลเพื่อน (PlayerInfoPopup)
    public Button copyNameButton;             // ปุ่มคลิปบอร์ด : คัดลอกชื่อเพื่อน (เอาไปค้นหา/แชร์ต่อได้)

    // ---------- 2) ต้นไม้ของเพื่อน ----------
    [Header("ต้นไม้ของเพื่อน")]
    public MCController plantCharacter;       // MainCharacter (ใช้ภาพร่างพืชชุดเดียวกับหน้าหลัก)
    public GameObject plantSickPopup;         // PlantSickPopup (โชว์ตอนต้นเพื่อนป่วย)
    public Button switchPlantButton;          // ปุ่มสลับดูต้นอื่นของเพื่อน (SwitchCharacterBtn)
    public FriendPlantSwitchPopup switchPlantPopup;

    // ---------- 3) บัดดี้ของเพื่อน ----------
    [Header("บัดดี้ของเพื่อน")]
    public Image buddyImage;                  // BuddyCharacter
    public Button buddyButton;                // (ไม่บังคับ) กดที่บัดดี้แล้วโชว์ข้อมูล
    public GameObject buddyPopup;             // (ไม่บังคับ) BuddyPopup
    public TextMeshProUGUI buddyNameText;     // BuddyPopup/name
    public TextMeshProUGUI buddyMbtiText;     // BuddyPopup/mbti
    public TextMeshProUGUI buddyDescText;     // BuddyPopup/desc
    public float buddyPopupSeconds = 3.5f;

    // ---------- 4) ปุ่มล่าง ----------
    [Header("ปุ่มล่าง")]
    public Button homeButton;
    public Button wateringButton;
    public Button nextFriendButton;
    public string homeSceneName = "MainForest";

    // ---------- ข้อความลอย (floating text) ----------
    [Header("ข้อความลอย")]
    public GameObject floatingTextObject;     // กล่องข้อความที่จะเด้งขึ้นมาแล้วหายไป
    public TextMeshProUGUI floatingText;
    public float floatingTextSeconds = 2.5f;

    // ---------- รางวัลรดน้ำให้เพื่อน (แก้ใน Inspector ได้) ----------
    [Header("รางวัลรดน้ำให้เพื่อน")]
    public int expFirstPlant = 15;            // ต้นแรกของเพื่อนคนนี้ในวันนี้
    [Range(0f, 1f)] public float expOtherPlantRate = 0.5f;   // ต้นถัดไป = EXP x ค่านี้ (ปัดลง)
    public int flowerMin = 5;                 // แต้มดอกไม้สุ่ม (เฉพาะต้นแรก)
    public int flowerMax = 15;
    public float moistureShown = 100f;        // ความชุ่มชื้นที่เพื่อนรดให้ (0-100) ใช้ทั้งตัวเลขที่โชว์ และค่าที่หลอดของเจ้าของจะขึ้นไปถึงจริง
    public float dryTimeMultiplier = 2f;      // ค่าเดียวกับ PlantStatus ตอนอากาศปกติ (หลอดหมดใน interval x ค่านี้ ชั่วโมง)

    [Header("เวลา")]
    public float defaultWaterIntervalHours = 2f;   // ใช้ถ้าหา water_interval_hours ของสายพันธุ์ไม่เจอ
    public bool testModeInSeconds = false;          // เหมือน PlantStatus : true = นับชั่วโมงเป็นวินาที (ไว้เทสต์)

    // ---------- ข้อมูลที่โหลดมา (ป๊อปอัปสลับต้นไม้ก็อ่านจากตรงนี้) ----------
    [HideInInspector] public List<SupabasePlantSpecies> masterSpeciesList = new List<SupabasePlantSpecies>();
    [HideInInspector] public List<SupabasePlantStage> masterStagesList = new List<SupabasePlantStage>();
    [HideInInspector] public List<SupabasePlayerPlant> friendPlants = new List<SupabasePlayerPlant>();

    private FriendUserRow currentFriend;
    private SupabasePlayerPlant currentPlant;
    private HashSet<string> helpedTodayPlantIds = new HashSet<string>();   // ต้นที่เราช่วยรดไปแล้ววันนี้
    private bool isBusy = false;              // กันกดซ้ำตอนรอ DB
    private Coroutine floatingRoutine;
    private Coroutine buddyPopupRoutine;

    // ข้อมูลเพื่อนจากตาราง user (ใช้กับ Newtonsoft)
    public class FriendUserRow
    {
        public string user_id;
        public string username;
        public string user_tag;
        public string buddy_id;
        public string cluster_id;
        public long? total_exp { get; set; }
    }

    private class BuddyRow { public string mbti_type; public string buddy_name; public string skill_description; }
    private class FriendshipRow { public string requester_id; public string addressee_id; public string status; }
    private class LogRow { public Dictionary<string, object> meta_data; }

    // ==================== เริ่มต้น ====================
    void Start()
    {
        if (homeButton != null) homeButton.onClick.AddListener(GoHome);
        if (wateringButton != null) wateringButton.onClick.AddListener(OnWateringClicked);
        if (nextFriendButton != null) nextFriendButton.onClick.AddListener(GoNextFriend);
        if (infoButton != null) infoButton.onClick.AddListener(OpenFriendInfo);
        if (topProfileButton != null) topProfileButton.onClick.AddListener(OpenFriendInfo);
        if (copyNameButton != null) copyNameButton.onClick.AddListener(CopyFriendName);
        if (switchPlantButton != null) switchPlantButton.onClick.AddListener(OpenSwitchPlant);
        if (buddyButton != null) buddyButton.onClick.AddListener(ShowBuddyPopup);

        if (floatingTextObject != null) floatingTextObject.SetActive(false);
        if (plantSickPopup != null) plantSickPopup.SetActive(false);
        if (buddyPopup != null) buddyPopup.SetActive(false);
        if (infoPopup != null) infoPopup.Hide();
        if (plantCharacter != null) plantCharacter.gameObject.SetActive(false);   // ซ่อนไว้ก่อน โหลดเสร็จค่อยโชว์
        SetWateringInteractable(false);

        StartCoroutine(StartRoutine());
    }

    private IEnumerator StartRoutine()
    {
        // ตารางสายพันธุ์/ร่าง โหลดครั้งเดียวพอ (ใช้หาชื่อสายพันธุ์ + เวลารดน้ำ)
        yield return LoadMasterData();

        // ถ้าเปิดฉากนี้ตรงๆ (เช่นกด Play ใน Editor) จะยังไม่มีลิสต์เพื่อน -> โหลดเองจาก friendship
        if (friendIdList == null || friendIdList.Count == 0)
            yield return LoadFriendIdList();

        if (friendIdList == null || friendIdList.Count == 0)
        {
            ShowFloatingText("ยังไม่มีเพื่อนให้ไปเยี่ยม");
            yield break;
        }

        currentFriendIndex = Mathf.Clamp(currentFriendIndex, 0, friendIdList.Count - 1);
        yield return LoadFriend(friendIdList[currentFriendIndex]);
    }

    // ==================== โหลดข้อมูล ====================
    private IEnumerator LoadMasterData()
    {
        yield return SupabaseManager.Instance.GetPlantMasterData(
            onSpeciesSuccess: json => masterSpeciesList = JsonConvert.DeserializeObject<List<SupabasePlantSpecies>>(json) ?? new List<SupabasePlantSpecies>(),
            onStagesSuccess: json => masterStagesList = JsonConvert.DeserializeObject<List<SupabasePlantStage>>(json) ?? new List<SupabasePlantStage>(),
            onRulesSuccess: json => { },   // หน้านี้ไม่ต้องใช้กฎวิวัฒนาการ
            onError: err => Debug.LogError($"[FriendVisit] 🔴 โหลด Master Data ไม่สำเร็จ: {err}")
        );
    }

    // สำรองไว้ตอนเปิดฉากตรงๆ : หาเพื่อนที่ accepted ทั้งหมดของเรา
    private IEnumerator LoadFriendIdList()
    {
        string me = SupabaseManager.Instance.GetActiveUserId();
        friendIdList = new List<string>();

        yield return SupabaseManager.Instance.GetFriendships(json =>
        {
            var rows = JsonConvert.DeserializeObject<List<FriendshipRow>>(json) ?? new List<FriendshipRow>();
            foreach (var f in rows)
            {
                if (f.status != "accepted") continue;
                if (f.requester_id == me) friendIdList.Add(f.addressee_id);
                else if (f.addressee_id == me) friendIdList.Add(f.requester_id);
            }
        }, err => Debug.LogError($"[FriendVisit] 🔴 โหลดรายชื่อเพื่อนไม่สำเร็จ: {err}"));

        currentFriendIndex = 0;
    }

    // โหลดเพื่อน 1 คน : ข้อมูลผู้เล่น -> บัดดี้ -> ต้นไม้ทั้งหมด -> ต้นที่เราช่วยรดไปแล้ววันนี้
    private IEnumerator LoadFriend(string friendUserId)
    {
        SupabaseManager db = SupabaseManager.Instance;
        currentFriend = null;
        currentPlant = null;
        friendPlants.Clear();
        SetWateringInteractable(false);

        // 1. ข้อมูลเพื่อน
        yield return db.GetRest($"user?select=user_id,username,user_tag,buddy_id,cluster_id,total_exp&user_id=eq.{friendUserId}", json =>
        {
            var rows = JsonConvert.DeserializeObject<List<FriendUserRow>>(json);
            if (rows != null && rows.Count > 0) currentFriend = rows[0];
        });

        if (currentFriend == null)
        {
            ShowFloatingText("ไม่พบข้อมูลเพื่อน");
            yield break;
        }

        int friendLevel = db.GetLevelFromTotalExp(currentFriend.total_exp ?? 0);   // คิดเลเวลจาก EXP สูตรเดียวกับของเรา
        if (friendNameText != null) friendNameText.text = currentFriend.username;
        if (friendLevelText != null) friendLevelText.text = string.Format(friendLevelFormat, friendLevel);

        // แถบซ้ายบน : เลเวล + วงแหวน EXP + ชื่อของเพื่อน, แต้มล็อกเป็น ???
        long friendExp = currentFriend.total_exp ?? 0;
        long expInLevel = friendExp - db.GetTotalExpForLevel(friendLevel);
        long expNeeded = (long)db.expPerLevelStep * friendLevel;
        if (topLevelText != null) topLevelText.text = string.Format(topLevelFormat, friendLevel);
        if (topExpRing != null) topExpRing.fillAmount = expNeeded > 0 ? Mathf.Clamp01((float)expInLevel / expNeeded) : 0f;
        if (topUsernameText != null) topUsernameText.text = currentFriend.username;
        if (topLockedTexts != null)
            foreach (TextMeshProUGUI t in topLockedTexts) if (t != null) { t.text = lockedText; t.color = lockedColor; }

        // 2. บัดดี้ของเพื่อน (รูปในวงกลม + ตัวบัดดี้ข้างต้นไม้)
        yield return LoadFriendBuddy(currentFriend.buddy_id);

        // 3. ต้นไม้ทั้งหมดของเพื่อน (เรียงตามวันที่ปลูก)
        yield return db.GetRest($"player_plant?user_id=eq.{friendUserId}&order=planted_at.asc", json =>
        {
            friendPlants = JsonConvert.DeserializeObject<List<SupabasePlayerPlant>>(json) ?? new List<SupabasePlayerPlant>();
        });

        // 4. ต้นที่เราช่วยรดไปแล้ววันนี้ (ไว้ปิดปุ่มรดน้ำ)
        yield return LoadHelpedToday();

        // 5. โชว์ต้นแรก (ถ้าเพื่อนยังไม่มีต้นไม้ ให้ขึ้นข้อความแทน)
        if (friendPlants.Count == 0)
        {
            if (plantCharacter != null) plantCharacter.gameObject.SetActive(false);
            if (plantSickPopup != null) plantSickPopup.SetActive(false);
            ShowFloatingText("เพื่อนยังไม่มีต้นไม้");
        }
        else
        {
            ShowPlant(friendPlants[0].player_plant_id);
        }
    }

    private IEnumerator LoadFriendBuddy(string buddyId)
    {
        if (buddyImage != null) buddyImage.gameObject.SetActive(false);
        if (string.IsNullOrEmpty(buddyId)) yield break;

        BuddyRow buddy = null;
        yield return SupabaseManager.Instance.GetRest($"buddy?select=mbti_type,buddy_name,skill_description&buddy_id=eq.{buddyId}", json =>
        {
            var rows = JsonConvert.DeserializeObject<List<BuddyRow>>(json);
            if (rows != null && rows.Count > 0) buddy = rows[0];
        });
        if (buddy == null || string.IsNullOrEmpty(buddy.mbti_type)) yield break;

        string mbti = buddy.mbti_type.Trim().ToLower();

        // รูปในวงกลม : Resources/buddy/{mbti} (ชุดเดียวกับ BuddyDisplay)
        Sprite pfp = SupabaseManager.Instance.LoadBuddySprite("buddy/" + mbti);   // 🤝 รองรับรูปแบบ Multiple ด้วย
        if (friendPfpImage != null && pfp != null) { friendPfpImage.sprite = pfp; friendPfpImage.preserveAspect = true; }

        // ตัวบัดดี้ข้างต้นไม้ : Resources/main_buddy/{mbti} (ชุดเดียวกับ BuddyDisplayManager)
        Sprite body = SupabaseManager.Instance.LoadBuddySprite("main_buddy/" + mbti);   // 🤝 main_buddy/infj ตั้งเป็น Multiple เดิมโหลดได้ null บัดดี้เลยหาย
        if (buddyImage != null && body != null)
        {
            buddyImage.sprite = body;
            buddyImage.preserveAspect = true;
            buddyImage.gameObject.SetActive(true);
        }

        // ข้อมูลไว้โชว์ตอนกดที่บัดดี้
        if (buddyNameText != null) buddyNameText.text = buddy.buddy_name;
        if (buddyMbtiText != null)
        {
            buddyMbtiText.text = buddy.mbti_type.ToUpper();
            buddyMbtiText.color = GetMbtiColor(buddy.mbti_type);   // สีตามบ้าน MBTI เหมือนหน้าหลัก
        }
        if (buddyDescText != null) buddyDescText.text = buddy.skill_description;
    }

    // อ่าน behavior_log ของเราวันนี้ (ตั้งแต่เที่ยงคืนเวลาไทย) ว่าช่วยรดต้นไหนไปแล้วบ้าง
    private IEnumerator LoadHelpedToday()
    {
        helpedTodayPlantIds.Clear();
        string me = SupabaseManager.Instance.GetActiveUserId();

        // เที่ยงคืนไทยวันนี้ แปลงเป็นเวลา UTC (ไทย = UTC+7)
        DateTime todayStartUtc = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
        string since = todayStartUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

        yield return SupabaseManager.Instance.GetRest(
            $"behavior_log?select=meta_data&user_id=eq.{me}&activity_type=eq.water_friend_tree&timestamp=gte.{since}", json =>
        {
            var rows = JsonConvert.DeserializeObject<List<LogRow>>(json) ?? new List<LogRow>();
            foreach (var r in rows)
            {
                if (r.meta_data != null && r.meta_data.TryGetValue("player_plant_id", out object id) && id != null)
                    helpedTodayPlantIds.Add(id.ToString());
            }
        });
    }

    // ==================== โชว์ต้นไม้ ====================
    /// <summary> เปลี่ยนต้นที่กำลังดู (ป๊อปอัปสลับต้นไม้ก็เรียกอันนี้) ไม่บันทึกอะไรลง DB </summary>
    public void ShowPlant(string playerPlantId)
    {
        currentPlant = friendPlants.Find(p => p.player_plant_id == playerPlantId);
        if (currentPlant == null) return;

        // ร่างพืช : ใช้ stage_id ของเพื่อนตรงๆ (เชอรี่ร่าง 4 ก็โชว์ร่างที่เพื่อนเลือกไว้ เปลี่ยนจากหน้านี้ไม่ได้)
        if (plantCharacter != null)
        {
            plantCharacter.currentStageId = currentPlant.stage_id;
            plantCharacter.gameObject.SetActive(true);
            plantCharacter.SetStageById(currentPlant.stage_id);
        }

        // ต้นป่วย : โชว์ป๊อปอัปป่วย
        if (plantSickPopup != null) plantSickPopup.SetActive(currentPlant.is_sick);

        RefreshWateringButton();
    }

    // ปุ่มรดน้ำกดได้เมื่อ : มีต้นไม้ + ต้นไม่ป่วย + วันนี้ยังไม่ได้ช่วยรดต้นนี้
    // (ยังไม่หิวน้ำ = ยังกดได้ แต่จะขึ้นข้อความบอกเวลาแทน)
    private void RefreshWateringButton()
    {
        bool can = currentPlant != null
                   && !currentPlant.is_sick
                   && !helpedTodayPlantIds.Contains(currentPlant.player_plant_id);
        SetWateringInteractable(can);
    }

    private void SetWateringInteractable(bool value)
    {
        if (wateringButton != null) wateringButton.interactable = value;
    }

    // ==================== รดน้ำให้เพื่อน ====================
    private void OnWateringClicked()
    {
        if (isBusy || currentPlant == null) return;

        if (currentPlant.is_sick)
        {
            ShowFloatingText("ต้นไม้ป่วยอยู่ รดน้ำไม่ได้");
            return;
        }
        if (helpedTodayPlantIds.Contains(currentPlant.player_plant_id))
        {
            ShowFloatingText("วันนี้ช่วยรดต้นนี้ไปแล้ว");
            return;
        }

        // ยังไม่หิวน้ำ -> บอกเวลาที่เหลือ
        double secondsLeft = GetSecondsUntilThirsty(currentPlant);
        if (secondsLeft > 0)
        {
            ShowFloatingText($"ยังไม่หิวน้ำ อีก {FormatTime(secondsLeft)}");
            return;
        }

        StartCoroutine(WaterRoutine(currentPlant));
    }

    private IEnumerator WaterRoutine(SupabasePlayerPlant plant)
    {
        isBusy = true;
        SetWateringInteractable(false);

        // ความชุ่มชื้นไม่ได้เก็บใน DB (PlantStatus คิดจากเวลารดน้ำ) เลยให้เพื่อนรด "เติมได้แค่ moistureShown %"
        // ด้วยการบันทึกเวลาย้อนหลังไป เท่ากับเวลาที่หลอดเต็มต้องใช้ลดลงมาเหลือ moistureShown %
        //   เช่น interval 2 ชม. x 2 = หลอดหมดใน 4 ชม. -> เหลือ 60% ต้องย้อนเวลาไป 40% ของ 4 ชม. = 1.6 ชม.
        float decayHours = GetWaterIntervalHours(plant) * dryTimeMultiplier;
        float hoursBack = (1f - Mathf.Clamp01(moistureShown / 100f)) * decayHours;
        if (testModeInSeconds) hoursBack /= 3600f;
        string wateredAt = DateTime.UtcNow.AddHours(-hoursBack).ToString("o");

        // 100% = ไม่ต้องย้อนเวลา ส่ง null ให้ DB ใช้เวลาปัจจุบันเอง
        SupabaseManager.WaterFriendResult result = null;
        yield return SupabaseManager.Instance.WaterFriendPlant(plant.player_plant_id, hoursBack > 0 ? wateredAt : null, r => result = r);

        isBusy = false;

        if (result == null)
        {
            ShowFloatingText("เชื่อมต่อไม่สำเร็จ ลองใหม่อีกครั้ง");
            RefreshWateringButton();
            yield break;
        }

        if (!result.success)
        {
            // DB ไม่ให้รด -> อัปเดตสถานะในเครื่องให้ตรงกับ DB แล้วบอกเหตุผล
            if (result.message == "Already helped today") helpedTodayPlantIds.Add(plant.player_plant_id);
            if (result.message == "Plant is sick") plant.is_sick = true;
            ShowFloatingText(GetThaiMessage(result.message));
            if (plant == currentPlant) ShowPlant(plant.player_plant_id);
            yield break;
        }

        // ---------- รดสำเร็จ ----------
        helpedTodayPlantIds.Add(plant.player_plant_id);
        plant.friend_watered_at = wateredAt;   // ต้นนี้เพิ่งถูกรด (ไว้คิดเวลาหิวน้ำในเครื่อง)

        // รางวัลของเรา : ต้นแรกของเพื่อนคนนี้วันนี้ = EXP เต็ม + แต้มดอกไม้สุ่ม / ต้นถัดไป = EXP ครึ่งเดียว
        int exp = result.is_first_today ? expFirstPlant : Mathf.FloorToInt(expFirstPlant * expOtherPlantRate);
        int flower = result.is_first_today ? UnityEngine.Random.Range(flowerMin, flowerMax + 1) : 0;

        // บัฟบัดดี้ FRIEND_FLOWER_BONUS (โลมา) : ช่วยรดทุกครั้งได้แต้มดอกไม้เพิ่ม
        if (BuffManager.Instance != null && BuffManager.Instance.activeBuddySkill == SkillType.FRIEND_FLOWER_BONUS)
            flower += Mathf.RoundToInt(BuffManager.Instance.activeBuddySkillValue);

        SupabaseManager.Instance.AddPlayerExpAndFlower(exp, flower);   // บันทึกลงตาราง user + อัปเดตแถบบน

        // เด้งข้อความรางวัล (ถ้ามี AlertPopupManager ในฉากใช้ตัวนั้น ถ้าไม่มีใช้ข้อความลอยแทน)
        if (AlertPopupManager.Instance != null)
        {
            AlertPopupManager.Instance.ShowAlerts(moisture: moistureShown, exp: exp, flower: flower);
        }
        else
        {
            string msg = $"+{moistureShown} ความชุ่มชื้น\n+{exp} ค่าประสบการณ์";
            if (flower > 0) msg += $"\n+{flower} แต้มดอกไม้";
            ShowFloatingText(msg);
        }

        RefreshWateringButton();
    }

    // ---------- เวลาหิวน้ำ ----------
    // ใช้เวลารดล่าสุดที่ใหม่กว่า ระหว่างเจ้าของรดเอง (last_watered_at) กับเพื่อนรด (friend_watered_at)
    // แล้วเทียบกับ water_interval_hours ของสายพันธุ์ (สูตรเดียวกับ PlantStatus.CanWater)
    public double GetSecondsUntilThirsty(SupabasePlayerPlant plant)
    {
        DateTime? latest = null;
        if (TryParseDateTime(plant.last_watered_at, out DateTime own)) latest = own;
        if (TryParseDateTime(plant.friend_watered_at, out DateTime friend) && (latest == null || friend > latest)) latest = friend;
        if (latest == null) return 0;   // ยังไม่เคยถูกรดเลย = หิวน้ำ

        double cooldown = GetWaterIntervalHours(plant) * (testModeInSeconds ? 1.0 : 3600.0);
        double passed = (DateTime.UtcNow - latest.Value).TotalSeconds;
        return Math.Max(0, cooldown - passed);
    }

    private float GetWaterIntervalHours(SupabasePlayerPlant plant)
    {
        SupabasePlantSpecies species = masterSpeciesList.Find(sp => sp.species_id == GetSpeciesIdOfStage(plant.stage_id));
        return (species != null && species.water_interval_hours > 0) ? species.water_interval_hours : defaultWaterIntervalHours;
    }

    public string GetSpeciesIdOfStage(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return "";
        SupabasePlantStage stage = masterStagesList.Find(s => s.stage_id == stageId.Trim());
        return stage != null ? stage.species_id : "";
    }

    public string GetSpeciesName(string speciesId)
    {
        SupabasePlantSpecies species = masterSpeciesList.Find(sp => sp.species_id == speciesId);
        return species != null ? species.species_name : "";
    }

    // อ่านเวลาจาก DB (รองรับปี พ.ศ. แบบเดียวกับ PlantStatus)
    private bool TryParseDateTime(string timeStr, out DateTime result)
    {
        result = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(timeStr)) return false;
        if (DateTimeOffset.TryParse(timeStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTimeOffset dto))
        {
            result = dto.UtcDateTime;
            if (result.Year > 2400) result = result.AddYears(-543);
            return true;
        }
        return false;
    }

    private string FormatTime(double seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ชม. {t.Minutes} นาที";
        if (t.TotalMinutes >= 1) return $"{t.Minutes} นาที";
        return $"{t.Seconds} วินาที";
    }

    private string GetThaiMessage(string dbMessage)
    {
        switch (dbMessage)
        {
            case "Already helped today": return "วันนี้ช่วยรดต้นนี้ไปแล้ว";
            case "Plant is sick": return "ต้นไม้ป่วยอยู่ รดน้ำไม่ได้";
            case "Not friend": return "ต้องเป็นเพื่อนกันก่อนถึงจะรดน้ำได้";
            case "Plant not found": return "ไม่พบต้นไม้ต้นนี้";
            default: return "รดน้ำไม่สำเร็จ";
        }
    }

    // ==================== ปุ่มล่าง ====================
    public void GoHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }

    // ไปบ้านเพื่อนคนถัดไปในลิสต์ (วนกลับคนแรกเมื่อสุดลิสต์) เฉพาะเพื่อนเท่านั้น
    public void GoNextFriend()
    {
        if (isBusy || friendIdList == null || friendIdList.Count == 0) return;
        if (friendIdList.Count == 1)
        {
            ShowFloatingText("มีเพื่อนแค่คนเดียว ลองแอดเพื่อนเพิ่มดูสิ");
            return;
        }

        CloseAllPopups();
        currentFriendIndex = (currentFriendIndex + 1) % friendIdList.Count;
        StopAllCoroutines();
        isBusy = false;
        StartCoroutine(LoadFriend(friendIdList[currentFriendIndex]));
    }

    // ==================== ป๊อปอัป ====================
    private void OpenFriendInfo()
    {
        if (infoPopup != null && currentFriend != null) infoPopup.Show(currentFriend);
    }

    // ปุ่มคลิปบอร์ด : คัดลอกชื่อเพื่อนลง clipboard ของเครื่อง
    private void CopyFriendName()
    {
        if (currentFriend == null) return;
        GUIUtility.systemCopyBuffer = currentFriend.username;
        ShowFloatingText($"คัดลอกชื่อ {currentFriend.username} แล้ว");
    }

    private void OpenSwitchPlant()
    {
        if (switchPlantPopup == null) return;
        if (friendPlants.Count == 0)
        {
            ShowFloatingText("เพื่อนยังไม่มีต้นไม้");
            return;
        }
        switchPlantPopup.OpenPopup();
    }

    private void ShowBuddyPopup()
    {
        if (buddyPopup == null) return;
        buddyPopup.SetActive(true);
        if (buddyPopupRoutine != null) StopCoroutine(buddyPopupRoutine);
        buddyPopupRoutine = StartCoroutine(HideLater(buddyPopup, buddyPopupSeconds));
    }

    // สีตัวอักษร MBTI ตามบ้าน (ก๊อปมาจาก BuddyDisplayManager.GetMbtiColor)
    private Color GetMbtiColor(string mbti)
    {
        if (string.IsNullOrEmpty(mbti)) return Color.white;
        string code = mbti.Trim().ToUpper();
        string hex = "";
        if (code.Contains("NF")) hex = "#4EBA6F";                              // บ้านเขียว
        else if (code.Contains("NT")) hex = "#8A38F5";                         // บ้านม่วง
        else if (code.Contains("S") && code.EndsWith("J")) hex = "#3B82F6";    // บ้านฟ้า
        else if (code.Contains("S") && code.EndsWith("P")) hex = "#EAC326";    // บ้านเหลือง
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
    }

    private void CloseAllPopups()
    {
        if (infoPopup != null) infoPopup.Hide();
        if (switchPlantPopup != null) switchPlantPopup.ClosePopup();
        if (buddyPopup != null) buddyPopup.SetActive(false);
        if (floatingTextObject != null) floatingTextObject.SetActive(false);
    }

    // ==================== ข้อความลอย ====================
    public void ShowFloatingText(string message)
    {
        Debug.Log($"[FriendVisit] 💬 {message}");
        if (floatingTextObject == null) return;

        if (floatingText != null) floatingText.text = message;
        if (floatingRoutine != null) StopCoroutine(floatingRoutine);
        floatingTextObject.SetActive(true);

        // ถ้ามี Animator ให้เล่นใหม่ตั้งแต่ต้น (เหมือน AlertPopupManager)
        Animator anim = floatingTextObject.GetComponent<Animator>();
        if (anim != null) anim.Play(0, -1, 0f);

        floatingRoutine = StartCoroutine(HideLater(floatingTextObject, floatingTextSeconds));
    }

    private IEnumerator HideLater(GameObject target, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (target != null) target.SetActive(false);
    }
}
