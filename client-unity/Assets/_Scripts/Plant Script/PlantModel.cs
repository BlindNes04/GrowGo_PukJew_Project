using System;
using System.Collections.Generic;

// 📦 1. ตาราง plant_species
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

// 📦 2. ตาราง plant_stages
[Serializable]
public class SupabasePlantStage
{
    public string stage_id;
    public string species_id;
    public int stage_level;
    public string stage_name;
    public float required_exp;
}

// 📦 3. ตาราง evolution_rules
[Serializable]
public class SupabaseEvolutionRule
{
    public string rule_id;
    public string stage_id;
    public string target_metric;
    public string comparison_operator;
    public float threshold_value;
}

// 📦 4. ตาราง player_plant
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
}

// 🛠️ Helper Classes สำหรับแปลง JSON Array จาก Supabase
[Serializable] public class SupabasePlayerPlantList { public List<SupabasePlayerPlant> items; }
[Serializable] public class SupabasePlantSpeciesList { public List<SupabasePlantSpecies> items; }
[Serializable] public class SupabasePlantStageList { public List<SupabasePlantStage> items; }
[Serializable] public class SupabaseEvolutionRuleList { public List<SupabaseEvolutionRule> items; }