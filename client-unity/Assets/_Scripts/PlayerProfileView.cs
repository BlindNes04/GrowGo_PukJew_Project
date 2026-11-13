using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

// ==================== PlayerProfileView ====================
// คุม UI ผู้เล่นใน MainForest 3 ส่วน:
//   1) แถบซ้ายบน : วงกลมเลเวล + วงแหวน EXP, ชื่อ, แต้มดอกไม้, แต้มความสัมพันธ์
//   2) ป๊อปอัปโปรไฟล์ (กดที่วงกลม) : ชื่อ, #tag, เลเวล, ฉายา, หลอด EXP, แต้ม 3 แบบ, สถิติการเล่น
//   3) ป๊อปอัป Level Up : ขึ้นข้อความสั้นๆ ตอนเลเวลอัป
// ค่าทั้งหมดอ่านจาก SupabaseManager (ส่วน Player Level / Points)
public class PlayerProfileView : MonoBehaviour
{
    // ---------- 1) แถบซ้ายบน ----------
    [Header("แถบซ้ายบน")]
    public TextMeshProUGUI levelText;        // ตัวเลขเลเวลในวงกลม
    public Image expRingFill;                // วงแหวนสีฟ้า (Image Type = Filled, Radial 360)
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI flowerText;       // แต้มดอกไม้
    public TextMeshProUGUI heartText;        // แต้มความสัมพันธ์ของต้นไม้ที่เลือกอยู่
    public Button openProfileButton;         // ปุ่มที่วงกลม กดแล้วเปิดป๊อปอัปโปรไฟล์
    public float ringFillSpeed = 0.8f;       // ความเร็ววงแหวนวิ่ง (ต่อวินาที)
    public string levelFormat = "Lv {0}";    // รูปแบบข้อความเลเวลในวงกลม

    // ---------- 2) ป๊อปอัปโปรไฟล์ ----------
    [Header("ป๊อปอัปโปรไฟล์")]
    public GameObject profilePopup;
    public Button closeProfileButton;
    public TextMeshProUGUI popupUsernameText;
    public TextMeshProUGUI popupTagText;
    public TextMeshProUGUI popupLevelText;    // "เลเวล 3"
    public TextMeshProUGUI popupTitleText;    // (ไม่บังคับ) ฉายาแบบข้อความจาก cluster_profile

    // ---------- ป้ายฉายา (เป็นรูป) : เลือกรูปตาม cluster_id ของผู้เล่น ----------
    [System.Serializable]
    public class TitleBadge
    {
        public string clusterId;   // เช่น CLS_HELPER
        public Sprite badge;       // รูปป้ายใน Art_2D/UI/player_popup
    }
    public Image popupTitleImage;             // Image ของป้ายฉายาในป๊อปอัป
    public List<TitleBadge> titleBadges = new List<TitleBadge>
    {
        new TitleBadge { clusterId = "CLS_HELPER" },     // persona.png   เพื่อนบ้านที่แสนดี
        new TitleBadge { clusterId = "CLS_COLLECTOR" },  // persona-2.png นักสะสม
        new TitleBadge { clusterId = "CLS_EXPLORER" },   // persona-3.png นักสำรวจ
        new TitleBadge { clusterId = "CLS_GUARDIAN" },   // persona-4.png ผู้พิทักษ์ธรรมชาติ
        new TitleBadge { clusterId = "CLS_CASUAL" },     // persona-5.png ผู้เล่นสายชิล
        new TitleBadge { clusterId = "CLS_NEW" },        // persona-6.png ผู้เล่นใหม่
        new TitleBadge { clusterId = "CLS_GAMER" },      // persona-7.png ขาประจำมินิเกม
    };
    public string defaultClusterId = "CLS_NEW";       // ถ้าผู้เล่นยังไม่มี cluster_id ให้ใช้ป้ายนี้
    public Image popupExpBarFill;             // หลอด EXP แนวนอน (Image Type = Filled, Horizontal)
    public TextMeshProUGUI popupExpText;      // "600/900"
    public TextMeshProUGUI popupFlowerText;
    public TextMeshProUGUI popupHeartText;
    public TextMeshProUGUI popupGreenText;

    [Header("ป๊อปอัปโปรไฟล์ : สถิติ")]
    public TextMeshProUGUI statPlantsText;     // สะสมพืช (user_collection)
    public TextMeshProUGUI statRecipesText;    // ค้นพบสูตรปุ๋ย (user_recipe_discovery)
    public TextMeshProUGUI statExploreText;    // ออกผจญภัย (behavior_log: explore_island)
    public TextMeshProUGUI statMinigameText;   // เล่นมินิเกม (behavior_log: play_block_blast + play_fruit_merge)
    public TextMeshProUGUI statHelpText;       // ช่วยเพื่อนบ้าน (behavior_log: water_friend_tree)

    // ---------- 3) ป๊อปอัป Level Up ----------
    [Header("ป๊อปอัป Level Up")]
    public GameObject levelUpPopup;
    public TextMeshProUGUI levelUpText;
    public string levelUpFormat = "Level Up! เลเวล {0}";
    public float levelUpShowSeconds = 2f;

    private PlantStatus plant;
    private float ringTarget = 0f;       // ค่าที่วงแหวนต้องวิ่งไปหา (0-1)
    private int ringLevel = 0;           // เลเวลที่วงแหวนแสดงอยู่ (ใช้เช็คตอนเลเวลอัป)
    private int lastHeart = int.MinValue;

    // ==================== เริ่มต้น / ผูก event ====================
    void OnEnable()
    {
        SupabaseManager.OnPlayerStatsChanged += RefreshUI;
        SupabaseManager.OnPlayerLevelUp += ShowLevelUp;
    }

    void OnDisable()
    {
        SupabaseManager.OnPlayerStatsChanged -= RefreshUI;
        SupabaseManager.OnPlayerLevelUp -= ShowLevelUp;
    }

    void Start()
    {
        if (openProfileButton != null) openProfileButton.onClick.AddListener(OpenProfile);
        if (closeProfileButton != null) closeProfileButton.onClick.AddListener(CloseProfile);
        if (profilePopup != null) profilePopup.SetActive(false);
        if (levelUpPopup != null) levelUpPopup.SetActive(false);
        if (expRingFill != null) expRingFill.fillAmount = 0f;   // เริ่มจากหลอดว่าง (สีเทา) แล้ววิ่งขึ้น

        plant = FindAnyObjectByType<PlantStatus>();

        // ⚡ ถ้าเคยโหลดค่าผู้เล่นไว้แล้ว (SupabaseManager อยู่ข้ามฉาก) โชว์ค่าเดิมทันที ไม่ต้องเห็นค่า default แวบนึง
        if (SupabaseManager.Instance.playerStatsLoaded)
        {
            RefreshUI();
            if (expRingFill != null) expRingFill.fillAmount = ringTarget;   // ไม่ต้องวิ่งจาก 0 ใหม่ทุกครั้งที่เปลี่ยนฉาก
        }

        // โหลดค่าผู้เล่นทุกครั้งที่เข้าฉาก (ได้ค่าล่าสุดจาก DB)
        SupabaseManager.Instance.LoadPlayerStats();
    }

    // ==================== อัปเดตทุกเฟรม ====================
    void Update()
    {
        // วงแหวน EXP วิ่งไปหาค่าเป้าหมาย
        if (expRingFill != null)
        {
            expRingFill.fillAmount = Mathf.MoveTowards(expRingFill.fillAmount, ringTarget, ringFillSpeed * Time.deltaTime);

            // ถ้าเลเวลอัป : วิ่งจนเต็มหลอดก่อน แล้วรีเซ็ตเป็น 0 แล้ววิ่งต่อไปหาค่าของเลเวลใหม่
            if (ringTarget >= 1f && expRingFill.fillAmount >= 1f)
            {
                expRingFill.fillAmount = 0f;
                ringTarget = GetLevelProgress01();
            }
        }

        // แต้มความสัมพันธ์อยู่ใน PlantStatus เลยเช็คทุกเฟรม (เปลี่ยนเมื่อไหร่ก็อัปเดตตัวเลข)
        if (plant != null && plant.relationshipScore != lastHeart)
        {
            lastHeart = plant.relationshipScore;
            if (heartText != null) heartText.text = lastHeart.ToString();
            if (popupHeartText != null) popupHeartText.text = lastHeart.ToString();
        }
    }

    // ==================== แถบซ้ายบน ====================
    private void RefreshUI()
    {
        SupabaseManager db = SupabaseManager.Instance;

        if (levelText != null) levelText.text = string.Format(levelFormat, db.playerLevel);
        if (usernameText != null) usernameText.text = db.playerUsername;
        if (flowerText != null) flowerText.text = db.playerFlowerPoints.ToString();

        // ตั้งเป้าวงแหวน : ถ้าเลเวลเพิ่ม ให้วิ่งไปให้เต็มก่อน (ดูใน Update)
        if (ringLevel != 0 && db.playerLevel > ringLevel) ringTarget = 1f;
        else ringTarget = GetLevelProgress01();
        ringLevel = db.playerLevel;

        // ถ้าป๊อปอัปเปิดอยู่ ให้อัปเดตด้วย
        if (profilePopup != null && profilePopup.activeSelf) RefreshPopup();
    }

    /// <summary> % EXP ในเลเวลนี้ (0 ถึง 1) </summary>
    private float GetLevelProgress01()
    {
        SupabaseManager.Instance.GetLevelProgress(out long have, out long need);
        return need > 0 ? Mathf.Clamp01((float)have / need) : 0f;
    }

    // ==================== ป๊อปอัปโปรไฟล์ ====================
    public void OpenProfile()
    {
        if (profilePopup == null) return;
        profilePopup.SetActive(true);
        RefreshPopup();
        StartCoroutine(LoadTitleAndStats());
    }

    public void CloseProfile()
    {
        if (profilePopup != null) profilePopup.SetActive(false);
    }

    private void RefreshPopup()
    {
        SupabaseManager db = SupabaseManager.Instance;
        db.GetLevelProgress(out long have, out long need);

        if (popupUsernameText != null) popupUsernameText.text = db.playerUsername;
        if (popupTagText != null) popupTagText.text = db.playerTag;
        if (popupLevelText != null) popupLevelText.text = $"เลเวล {db.playerLevel}";
        if (popupExpBarFill != null) popupExpBarFill.fillAmount = need > 0 ? Mathf.Clamp01((float)have / need) : 0f;
        if (popupExpText != null) popupExpText.text = $"{have}/{need}";
        if (popupFlowerText != null) popupFlowerText.text = db.playerFlowerPoints.ToString();
        if (popupGreenText != null) popupGreenText.text = db.playerGreenPoints.ToString();
        if (popupHeartText != null && plant != null) popupHeartText.text = plant.relationshipScore.ToString();

        // ป้ายฉายา : หา cluster_id ของผู้เล่นในลิสต์ แล้วใส่รูปให้ตรง
        if (popupTitleImage != null)
        {
            string id = string.IsNullOrEmpty(db.playerClusterId) ? defaultClusterId : db.playerClusterId;
            TitleBadge found = titleBadges.Find(t => t.clusterId == id);
            if (found != null && found.badge != null)
            {
                popupTitleImage.sprite = found.badge;
                popupTitleImage.SetNativeSize();   // ป้ายแต่ละอันกว้างไม่เท่ากัน เลยปรับขนาดตามรูป
            }
        }
    }

    // ---------- ฉายา + สถิติ (ดึงจาก DB ทุกครั้งที่เปิดป๊อปอัป) ----------
    private class Row { public string cluster_name; public string activity_type; }

    // ⚡ จำสถิติรอบล่าสุดไว้ (static = อยู่ข้ามฉาก) เปิดป๊อปอัปแล้วโชว์ค่านี้ก่อน แล้วค่อยอัปเดตเมื่อโหลดเสร็จ
    //    ยังไม่เคยโหลดเลย = โชว์ "-"
    private static string cachedUserId = "";
    private static string cachedTitle = "";
    private static int[] cachedStats = null;   // สะสมพืช, สูตรปุ๋ย, ผจญภัย, มินิเกม, ช่วยเพื่อน

    private void ShowCachedStats(string uid)
    {
        bool has = cachedStats != null && cachedUserId == uid;   // ข้อมูลของคนอื่น (เปลี่ยนบัญชี) ไม่เอามาโชว์
        TextMeshProUGUI[] targets = { statPlantsText, statRecipesText, statExploreText, statMinigameText, statHelpText };
        for (int i = 0; i < targets.Length; i++)
            if (targets[i] != null) targets[i].text = has ? cachedStats[i].ToString() : "-";
        if (popupTitleText != null) popupTitleText.text = has ? cachedTitle : "";
    }

    private void SaveStatToCache(string uid, int index, int value)
    {
        if (cachedStats == null || cachedUserId != uid) { cachedStats = new int[5]; cachedUserId = uid; cachedTitle = ""; }
        cachedStats[index] = value;
    }

    private IEnumerator LoadTitleAndStats()
    {
        SupabaseManager db = SupabaseManager.Instance;
        string uid = db.GetActiveUserId();
        if (string.IsNullOrEmpty(uid)) yield break;
        ShowCachedStats(uid);

        // ฉายา : user.cluster_id -> cluster_profile.cluster_name (K-means เป็นคนอัปเดต cluster_id)
        if (popupTitleText != null && !string.IsNullOrEmpty(db.playerClusterId))
        {
            yield return db.GetRest($"cluster_profile?select=cluster_name&cluster_id=eq.{db.playerClusterId}", json =>
            {
                var rows = JsonConvert.DeserializeObject<List<Row>>(json);
                popupTitleText.text = (rows != null && rows.Count > 0) ? rows[0].cluster_name : "";
                if (cachedUserId == uid) cachedTitle = popupTitleText.text;
            });
        }

        // สะสมพืช : จำนวนร่างที่ปลดล็อกใน Plant Dex
        yield return db.GetRest($"user_collection?select=stage_id&user_id=eq.{uid}", json =>
            SaveStatToCache(uid, 0, SetCount(statPlantsText, json)));

        // ค้นพบสูตรปุ๋ย
        yield return db.GetRest($"user_recipe_discovery?select=recipe_id&user_id=eq.{uid}", json =>
            SaveStatToCache(uid, 1, SetCount(statRecipesText, json)));

        // ผจญภัย / มินิเกม / ช่วยเพื่อนบ้าน : นับจาก behavior_log
        yield return db.GetRest($"behavior_log?select=activity_type&user_id=eq.{uid}&activity_type=in.(explore_island,play_block_blast,play_fruit_merge,water_friend_tree)", json =>
        {
            var rows = JsonConvert.DeserializeObject<List<Row>>(json) ?? new List<Row>();
            int explore = 0, minigame = 0, help = 0;
            foreach (var r in rows)
            {
                if (r.activity_type == "explore_island") explore++;
                else if (r.activity_type == "water_friend_tree") help++;
                else minigame++;
            }
            if (statExploreText != null) statExploreText.text = explore.ToString();
            if (statMinigameText != null) statMinigameText.text = minigame.ToString();
            if (statHelpText != null) statHelpText.text = help.ToString();
            SaveStatToCache(uid, 2, explore); SaveStatToCache(uid, 3, minigame); SaveStatToCache(uid, 4, help);
        });
    }

    // นับจำนวนแถว แล้วใส่ตัวเลข (คืนค่าตัวเลขไว้เก็บลง cache ด้วย)
    private int SetCount(TextMeshProUGUI target, string json)
    {
        var rows = JsonConvert.DeserializeObject<List<Row>>(json);
        int count = rows != null ? rows.Count : 0;
        if (target != null) target.text = count.ToString();
        return count;
    }

    // ==================== ป๊อปอัป Level Up ====================
    private void ShowLevelUp(int newLevel)
    {
        if (levelUpPopup == null) return;
        if (levelUpText != null) levelUpText.text = string.Format(levelUpFormat, newLevel);
        StopCoroutine(nameof(HideLevelUpLater));
        levelUpPopup.SetActive(true);
        StartCoroutine(nameof(HideLevelUpLater));
    }

    private IEnumerator HideLevelUpLater()
    {
        yield return new WaitForSeconds(levelUpShowSeconds);
        levelUpPopup.SetActive(false);
    }
}
