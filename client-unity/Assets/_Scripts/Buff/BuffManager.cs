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
    CURE_SICK_CHANCE,
    LEVELUP_BONUS_POINTS,
    DAILY_SILVER_GIFT,
    SEED_PRICE_DISCOUNT,
    SPECIAL_EVO_CHANCE,
    ISLAND_RESPAWN_SPEED,
    WATER_POINTS_BURST
}

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    [Header("Active Buddy Passive Skill")]
    public SkillType activeBuddySkill = SkillType.NONE;
    public float activeBuddySkillValue = 0f;

    [Header("Temporary Item/Fertilizer Buffs")]
    public float itemExpMultiplier = 1.0f;
    public float itemNutrientMultiplier = 1.0f;

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

    // 🟢 1. ตั้งค่าบัฟติดตัวจาก Buddy ปัจจุบัน
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

        float.TryParse(skillValueStr, out activeBuddySkillValue);
        Debug.Log($"[BuffManager] 🟢 บัฟ Buddy ทำงาน: {activeBuddySkill} (Value: {activeBuddySkillValue})");
    }

    // 🟢 2. ฟังก์ชันคำนวณ EXP สุทธิที่ผู้เล่นควรได้รับ (รวม Buddy + ปุ๋ย)
    public int CalculateFinalExp(int baseExp, bool isWateringAction)
    {
        float finalExp = baseExp * itemExpMultiplier;

        // ถ้าบัฟ Buddy เป็นการเพิ่ม EXP รดน้ำ
        if (isWateringAction && activeBuddySkill == SkillType.WATER_PLANT_EXP)
        {
            finalExp += activeBuddySkillValue; // เพิ่ม EXP โบนัสตามค่าใน CSV
        }

        return Mathf.RoundToInt(finalExp);
    }

    // 🟢 3. ฟังก์ชันคำนวณ Nutrient ปุ๋ยสุทธิ
    public float CalculateFinalNutrients(float baseNutrients)
    {
        float multiplier = itemNutrientMultiplier;

        if (activeBuddySkill == SkillType.FERTILIZER_BOOST)
        {
            multiplier += (activeBuddySkillValue / 100f); // เช่น เพิ่ม 10%
        }

        return baseNutrients * multiplier;
    }

    // 🟢 4. ฟังก์ชันคำนวณเวลารดน้ำ/โตไว
    public float GetGrowthSpeedMultiplier()
    {
        if (activeBuddySkill == SkillType.GROWTH_SPEED_BOOST)
        {
            return 1.0f + (activeBuddySkillValue / 100f);
        }
        return 1.0f;
    }
}