using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

// ==================== FriendInfoPopupView ====================
// ป๊อปอัปข้อมูลเพื่อน (กดปุ่มคลิปบอร์ดในหน้าเยี่ยมสวนเพื่อน)
// โครงเดียวกับส่วน "ป๊อปอัปโปรไฟล์" ของ PlayerProfileView แต่เปลี่ยนจากข้อมูลของเรา เป็นข้อมูลของเพื่อน
// โชว์ : ชื่อ, #tag, เลเวล, ฉายา, หลอด EXP, สถิติ 5 อย่าง
// แต้มดอกไม้/หัวใจ/ใบไม้ของเพื่อน = ข้อมูลส่วนตัว -> โชว์เป็นไอคอนสีเทาเข้ม + "???" (ล็อกไว้)
public class FriendInfoPopupView : MonoBehaviour
{
    // ---------- ข้อมูลหลัก ----------
    [Header("ป๊อปอัปข้อมูลเพื่อน")]
    public Button closeButton;
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI tagText;
    public TextMeshProUGUI levelText;           // "เลเวล 3"
    public string levelFormat = "เลเวล {0}";
    public TextMeshProUGUI titleText;           // (ไม่บังคับ) ฉายาแบบข้อความจาก cluster_profile

    // ---------- ป้ายฉายา (รูป) ----------
    // ถ้าไม่ใส่รูปในลิสต์นี้ จะยืมลิสต์จาก PlayerProfileView ในฉากเดียวกัน (รูปชุดเดียวกัน ไม่ต้องลากซ้ำ)
    public Image titleImage;
    public List<PlayerProfileView.TitleBadge> titleBadges = new List<PlayerProfileView.TitleBadge>();
    public string defaultClusterId = "CLS_NEW";

    // ---------- หลอด EXP ----------
    [Header("หลอด EXP")]
    public Image expBarFill;                    // LvlBar/FillBar
    public TextMeshProUGUI expText;             // LvlBar/LvlText

    // ---------- แต้ม (ล็อกไว้ เป็นข้อมูลส่วนตัว) ----------
    [Header("แต้มของเพื่อน (ล็อก)")]
    public Image[] lockedPointIcons;            // Icon ของ FlowerPoint / RelationshipPoint / GreenPoint
    public TextMeshProUGUI[] lockedPointTexts;  // text ของ FlowerPoint / RelationshipPoint / GreenPoint
    public Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);   // สีเทาเข้มเหมือนเงาดำ
    public string lockedText = "???";

    // ---------- สถิติ ----------
    [Header("สถิติ")]
    public TextMeshProUGUI statPlantsText;      // สะสมพืช (user_collection)
    public TextMeshProUGUI statRecipesText;     // ค้นพบสูตรปุ๋ย (user_recipe_discovery)
    public TextMeshProUGUI statExploreText;     // ออกผจญภัย (behavior_log: explore_island)
    public TextMeshProUGUI statMinigameText;    // เล่นมินิเกม (behavior_log: play_block_blast + play_fruit_merge)
    public TextMeshProUGUI statHelpText;        // ช่วยเพื่อนบ้าน (behavior_log: water_friend_tree)

    private class Row { public string cluster_name; public string activity_type; }

    // ==================== เริ่มต้น ====================
    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        // ยืมรูปป้ายฉายาจาก PlayerProfileView (แถบซ้ายบนของเรา) ถ้าลิสต์นี้ว่าง
        if (titleBadges.Count == 0)
        {
            PlayerProfileView profile = FindAnyObjectByType<PlayerProfileView>();
            if (profile != null) titleBadges = profile.titleBadges;
        }
        // ไม่สั่งปิดตัวเองตรงนี้ (FriendVisitController สั่ง Hide ตอนเริ่มฉากให้แล้ว)
    }

    // ==================== เปิด / ปิด ====================
    public void Show(FriendVisitController.FriendUserRow friend)
    {
        if (friend == null) return;
        gameObject.SetActive(true);

        SupabaseManager db = SupabaseManager.Instance;
        long totalExp = friend.total_exp ?? 0;
        int level = db.GetLevelFromTotalExp(totalExp);

        if (usernameText != null) usernameText.text = friend.username;
        if (tagText != null) tagText.text = friend.user_tag;
        if (levelText != null) levelText.text = string.Format(levelFormat, level);

        // หลอด EXP : สูตรเดียวกับ SupabaseManager.GetLevelProgress แต่ใช้ EXP ของเพื่อน
        long have = totalExp - db.GetTotalExpForLevel(level);
        long need = (long)db.expPerLevelStep * level;
        if (expBarFill != null) expBarFill.fillAmount = need > 0 ? Mathf.Clamp01((float)have / need) : 0f;
        if (expText != null) expText.text = $"{have}/{need}";

        // แต้มของเพื่อน : ทำเป็นสีเทาเข้ม + ??? (ไม่ได้ดึงค่าแต้มจาก DB เลย)
        if (lockedPointIcons != null)
            foreach (Image icon in lockedPointIcons) if (icon != null) icon.color = lockedColor;
        if (lockedPointTexts != null)
            foreach (TextMeshProUGUI t in lockedPointTexts) if (t != null) { t.text = lockedText; t.color = lockedColor; }

        // ป้ายฉายา : หา cluster_id ของเพื่อนในลิสต์ แล้วใส่รูปให้ตรง
        string clusterId = string.IsNullOrEmpty(friend.cluster_id) ? defaultClusterId : friend.cluster_id;
        if (titleImage != null)
        {
            PlayerProfileView.TitleBadge found = titleBadges.Find(t => t.clusterId == clusterId);
            if (found != null && found.badge != null)
            {
                titleImage.sprite = found.badge;
                titleImage.SetNativeSize();   // ป้ายแต่ละอันกว้างไม่เท่ากัน เลยปรับขนาดตามรูป
            }
        }

        // ล้างตัวเลขเก่าก่อน (กันเห็นสถิติของเพื่อนคนก่อนตอนกำลังโหลด)
        foreach (var t in new[] { statPlantsText, statRecipesText, statExploreText, statMinigameText, statHelpText })
            if (t != null) t.text = "-";

        StopAllCoroutines();
        StartCoroutine(LoadTitleAndStats(friend.user_id, clusterId));
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ---------- ฉายา + สถิติ (ดึงจาก DB ทุกครั้งที่เปิด) ----------
    private IEnumerator LoadTitleAndStats(string uid, string clusterId)
    {
        SupabaseManager db = SupabaseManager.Instance;

        // ฉายา : cluster_id -> cluster_profile.cluster_name
        if (titleText != null)
        {
            yield return db.GetRest($"cluster_profile?select=cluster_name&cluster_id=eq.{clusterId}", json =>
            {
                var rows = JsonConvert.DeserializeObject<List<Row>>(json);
                titleText.text = (rows != null && rows.Count > 0) ? rows[0].cluster_name : "";
            });
        }

        // สะสมพืช : จำนวนร่างที่ปลดล็อกใน Plant Dex
        yield return db.GetRest($"user_collection?select=stage_id&user_id=eq.{uid}", json =>
            SetCount(statPlantsText, json));

        // ค้นพบสูตรปุ๋ย
        yield return db.GetRest($"user_recipe_discovery?select=recipe_id&user_id=eq.{uid}", json =>
            SetCount(statRecipesText, json));

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
        });
    }

    private void SetCount(TextMeshProUGUI target, string json)
    {
        if (target == null) return;
        var rows = JsonConvert.DeserializeObject<List<Row>>(json);
        target.text = (rows != null ? rows.Count : 0).ToString();
    }
}
