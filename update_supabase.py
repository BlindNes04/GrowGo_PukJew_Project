import pandas as pd
import joblib
from sqlalchemy import create_engine, text
from datetime import datetime

import os
from dotenv import load_dotenv

# 1. DATABASE CONNECTION
load_dotenv()
DB_URL = os.getenv("DATABASE_URL")

print("กำลังเชื่อมต่อกับ Supabase Database")
engine = create_engine(DB_URL)

# 2. FETCH DATA FROM DATABASE
query_log = """
    SELECT user_id, activity_type, COUNT(*) as act_count
    FROM behavior_log
    GROUP BY user_id, activity_type;
"""

query_user = """
    SELECT user_id, created_at
    FROM "user";
"""

print("กำลังดึงข้อมูลจาก Supabase...")
try:
    df_raw = pd.read_sql(query_log, con=engine)
    df_user = pd.read_sql(query_user, con=engine)
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

df_user['created_at'] = pd.to_datetime(df_user['created_at'])
df_user['days_played'] = (pd.to_datetime('now') - df_user['created_at']).dt.days

df_features = df_features.merge(df_user[['user_id', 'days_played']], on='user_id', how='left').set_index('user_id')
df_features['days_played'] = df_features['days_played'].fillna(0)

df_features['total_acts'] = df_features[features].sum(axis=1)

MIN_DAYS = 7
MIN_ACTS = 100

is_new_player = (df_features['days_played'] < MIN_DAYS) | (df_features['total_acts'] < MIN_ACTS)

# 4. MODEL PREDICTION 
print("กำลังโหลดโมเดลและจัดกลุ่มผู้เล่น...")
try:
    scaler = joblib.load('scaler.joblib')
    kmeans = joblib.load('kmeans_model.joblib')

    df_features['cluster_num'] = -1 
    
    main_players = ~is_new_player
    if main_players.any():
        X_scaled = scaler.transform(df_features.loc[main_players, features])
        df_features.loc[main_players, 'cluster_num'] = kmeans.predict(X_scaled)

except Exception as e:
    print(f"เกิดข้อผิดพลาดในการโหลดโมเดล joblib: {e}")
    exit()

# 5. MAP NUMERIC CLUSTER TO CLUSTER_ID
cluster_id_mapping = {
    0: 'CLS_HELPER',
    1: 'CLS_COLLECTOR',
    2: 'CLS_EXPLORER',
    3: 'CLS_GAMER',
    4: 'CLS_CASUAL',     
    5: 'CLS_GUARDIAN'
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