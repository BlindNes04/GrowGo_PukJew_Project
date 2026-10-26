import pandas as pd
import joblib
from sqlalchemy import create_engine, text

import os
from dotenv import load_dotenv

# 1. DATABASE CONNECTION
load_dotenv()
DB_URL = os.getenv("DATABASE_URL")

print("กำลังเชื่อมต่อกับ Supabase Database")
engine = create_engine(DB_URL)

# 2. FETCH DATA FROM DATABASE
query = """
    SELECT user_id, activity_type, COUNT(*) as act_count
    FROM behavior_log
    GROUP BY user_id, activity_type;
"""

print("กำลังดึงข้อมูลจากตาราง behavior_log")
try:
    df_raw = pd.read_sql(query, con=engine)
except Exception as e:
    print(f"เกิดข้อผิดพลาดในการดึงข้อมูล: {e}")
    exit()

if df_raw.empty:
    print("ไม่พบข้อมูลพฤติกรรมในตาราง behavior_log")
    exit()

# 3. FEATURE ENGINEERING & PIVOT
df_pivot = df_raw.pivot(
    index="user_id", columns="activity_type", values="act_count"
).fillna(0)

act_mapping = {
    'craft_fertilizer': 'fert_craft_count',
    'play_fruit_merge': 'fruit_merge_count',
    'play_block_blast': 'block_blast_count',
    'explore_island': 'island_explore_count',
    'collect_species': 'species_collected',
    'submit_tree_cert': 'tree_certs_submitted',
    'water_friend_tree': 'friend_water_assists'
}

df_pivot = df_pivot.rename(columns=act_mapping)

features = [
    'fert_craft_count',
    'fruit_merge_count',
    'block_blast_count',
    'island_explore_count',
    'species_collected',
    'tree_certs_submitted',
    'friend_water_assists'
]

for f in features:
    if f not in df_pivot.columns:
        df_pivot[f] = 0

df_features = df_pivot[features].copy()

df_features['total_acts'] = df_features[features].sum(axis=1)
MIN_THRESHOLD = 10
is_new_player = df_features['total_acts'] < MIN_THRESHOLD

# 4. MODEL PREDICTION (LOAD FROM JOBLIB)
print("กำลังโหลดโมเดลและจัดกลุ่มผู้เล่น...")
try:
    scaler = joblib.load('scaler.joblib')
    kmeans = joblib.load('kmeans_model.joblib')

    X_scaled = scaler.transform(df_features[features])
    df_features['cluster_num'] = kmeans.predict(X_scaled)

except Exception as e:
    print(f"เกิดข้อผิดพลาดในการโหลดโมเดล joblib: {e}")
    exit()

# 5. MAP NUMERIC CLUSTER TO SUPABASE CLUSTER_ID
cluster_id_mapping = {
    0: 'CLS_GUARDIAN',
    1: 'CLS_CASUAL',
    2: 'CLS_EXPLORER',
    3: 'CLS_COLLECTOR',
    4: 'CLS_HELPER'     
}

df_features['cluster_id'] = df_features['cluster_num'].map(cluster_id_mapping)
df_features.loc[is_new_player, 'cluster_id'] = 'CLS_NEW'
result_df = df_features[['cluster_id']].reset_index()

# 6. UPDATE RESULTS BACK TO 'user' TABLE
print("กำลังบันทึก cluster_id กลับลงตาราง user บน Supabase")
update_sql = text("""
    UPDATE "user"
    SET cluster_id = :cluster_id
    WHERE user_id = :user_id;
""")

with engine.begin() as conn:
    for idx, row in result_df.iterrows():
        conn.execute(
            update_sql,
            {
                "cluster_id": str(row["cluster_id"]),
                "user_id": row["user_id"]
            }
        )

print("อัปเดตข้อมูล cluster_id ให้ผู้เล่นทั้งหมดบน Supabase เรียบร้อย")