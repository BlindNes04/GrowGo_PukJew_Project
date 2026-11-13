using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillType
{
    NONE,
    GROWTH_SPEED_BOOST,
    ISLAND_RARE_CHANCE,
    FERTILIZER_BOOST,
    WATER_PLANT_EXP,
    SICK_CHANCE_REDUCE,
    DRY_GRACE_TIME,
    FRIEND_FLOWER_BONUS,
    SEED_CASHBACK,
    DOUBLE_DROP_CHANCE,
    WATER_SHIELD_CHANCE,
    LEVELUP_BONUS_POINTS,
    DAILY_SILVER_GIFT,
    SEED_PRICE_DISCOUNT,
    SPECIAL_EVO_CHANCE,
    ISLAND_RESPAWN_SPEED,
    WATER_POINTS_BURST
}

public class BuffManager : MonoBehaviour
{
    // ==================== หา/สร้าง BuffManager เองได้ทุกฉาก ====================
    // ไม่ต้องวาง BuffManager ไว้ในฉาก: เรียก Instance เมื่อไหร่ ถ้ายังไม่มีจะสร้างขึ้นมาเอง
    // และอยู่ข้ามฉาก (DontDestroyOnLoad) สกิลบัดดี้เลยใช้ได้ทุกฉาก
    private static BuffManager instance;
    public static BuffManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<BuffManager>();
                if (instance == null) instance = new GameObject("[BuffManager]").AddComponent<BuffManager>();
            }
            return instance;
        }
    }

    [Header("Active Buddy Passive Skill")]
    public SkillType activeBuddySkill = SkillType.NONE;
    public float activeBuddySkillValue = 0f;

    private void Awake()
    {
        if (instance == null || instance == this)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ==================== ตั้งค่าสกิลจากบัดดี้ปัจจุบัน ====================
    public void SetActiveBuddySkill(string skillTypeStr, string skillValueStr)
    {
        if (Enum.TryParse(skillTypeStr, true, out SkillType parsedType))
        {
            activeBuddySkill = parsedType;
        }
        else
        {
            activeBuddySkill = SkillType.NONE;
        }

        // อ่านทศนิยมแบบ InvariantCulture (กันเครื่องที่ตั้งภาษาใช้ , แทน . แล้วอ่าน 0.1 ผิด)
        float.TryParse(skillValueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out activeBuddySkillValue);
        Debug.Log($"[BuffManager] 🟢 บัฟ Buddy ทำงาน: {activeBuddySkill} (Value: {activeBuddySkillValue})");
    }

    // ==================== ใช้เช็กสกิลบัดดี้จากทุกฉาก ====================
    // เช่น BuffManager.Instance.Has(SkillType.SEED_CASHBACK) แล้วใช้ค่า BuffManager.Instance.activeBuddySkillValue
    public bool Has(SkillType skill)
    {
        return activeBuddySkill == skill;
    }

    // โหลดสกิลบัดดี้ของผู้เล่นจาก DB (ตาราง user.buddy_id -> ตาราง buddy.skill_type / skill_value)
    // เรียกตอน Preload ข้อมูลผู้เล่น และตอนเปลี่ยนบัดดี้ -> ไม่ต้องรอเข้าฉาก MainForest ก่อน สกิลก็ทำงานทุกฉาก
    // คืนค่า Coroutine ให้คนเรียกรอจนโหลดเสร็จได้ (หน้าโหลดใช้นับความคืบหน้า)
    public Coroutine RefreshFromDatabase()
    {
        return StartCoroutine(RefreshFromDatabaseRoutine());
    }

    private System.Collections.IEnumerator RefreshFromDatabaseRoutine()
    {
        SupabaseManager db = SupabaseManager.Instance;
        string buddyId = db.GetCachedBuddyId();
        if (string.IsNullOrEmpty(buddyId))
            yield return db.StartCoroutine(db.GetUserBuddyId(id => buddyId = id));

        if (string.IsNullOrEmpty(buddyId))
        {
            SetActiveBuddySkill("NONE", "0");   // ยังไม่มีบัดดี้
            yield break;
        }

        yield return db.StartCoroutine(db.GetRest($"buddy?select=skill_type,skill_value&buddy_id=eq.{buddyId}", json =>
        {
            var rows = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>>(json);
            if (rows == null || rows.Count == 0) return;
            string type = rows[0]["skill_type"]?.ToString() ?? "NONE";
            string value = Convert.ToString(rows[0]["skill_value"], System.Globalization.CultureInfo.InvariantCulture);
            SetActiveBuddySkill(type, value);
        }));
    }

    // ==================== คำนวณ EXP จากการรดน้ำ ====================
    // 🦊 จิ้งจอก WATER_PLANT_EXP: รดน้ำได้ EXP เพิ่ม (ค่าในตาราง buddy = 2)
    public int CalculateFinalExp(int baseExp, bool isWateringAction)
    {
        float finalExp = baseExp;

        if (isWateringAction && activeBuddySkill == SkillType.WATER_PLANT_EXP)
        {
            finalExp += activeBuddySkillValue;
        }

        return Mathf.RoundToInt(finalExp);
    }
}