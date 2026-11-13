using System;
using System.Collections.Generic;

// 1. ตาราง plant_species
[Serializable]
public class SupabasePlantSpecies
{
    public string species_id;
    public string species_name;
    public float base_growth_rate;
    public float water_interval_hours;
    public bool is_water_sensitive;
    public bool is_heat_sensitive;
    public int base_exp;
    public string description;
}

// 2. ตาราง plant_stages
[Serializable]
public class SupabasePlantStage
{
    public string stage_id;
    public string species_id;
    public int stage_level;
    public string stage_name;
    public float required_exp;
}

// 3. ตาราง evolution_rules
[Serializable]
public class SupabaseEvolutionRule
{
    public string rule_id;
    public string stage_id;
    public string target_metric;
    public string comparison_operator;
    public float threshold_value;
}

// 4. ตาราง player_plant
[Serializable]
public class SupabasePlayerPlant
{
    public string player_plant_id;
    public string user_id;
    public string stage_id;
    public string plant_nickname;
    public int current_exp; // 👈 เปลี่ยนเป็น int เพื่อให้ตรงกับประเภท Column ใน DB
    public int relationship_score;
    public string last_watered_at;
    public bool is_fully_grown;
    public bool is_sick;
    public string sick_until;
    public int immunity_shields;
    public string planted_at;
    public string friend_watered_at; // 🤝 เยี่ยมสวนเพื่อน: เวลาที่เพื่อนมารดน้ำให้ล่าสุด (ไม่แตะ last_watered_at ของเจ้าของ)
    // ⚖️ ระบบสมดุลใหม่ (คอลัมน์เพิ่ม 2026-11-13)
    public int water_pool_total;        // สารอาหารจากการรดน้ำรอบล่าสุด
    public int water_pool_absorbed;     // ดูดเข้าหลอดไปแล้ว
    public string water_pool_started_at;// เวลาเริ่มดูด
    public int fert_used_today;         // ใส่ปุ๋ยวันนี้กี่ครั้ง
    public int heart_gained_today;      // หัวใจจากปุ๋ย/ไอเทมวันนี้
    public string daily_count_date;     // วันที่ของตัวนับ (เวลาไทย)
    public string dry_penalty_at;       // 🥀 ป่วยจากขาดน้ำ: เวลาหักหัวใจครั้งล่าสุด (null = ไม่ได้ป่วยจากขาดน้ำ)
}

// 🛠️ Helper Classes สำหรับแปลง JSON Array จาก Supabase
[Serializable] public class SupabasePlayerPlantList { public List<SupabasePlayerPlant> items; }
[Serializable] public class SupabasePlantSpeciesList { public List<SupabasePlantSpecies> items; }
[Serializable] public class SupabasePlantStageList { public List<SupabasePlantStage> items; }
[Serializable] public class SupabaseEvolutionRuleList { public List<SupabaseEvolutionRule> items; }
