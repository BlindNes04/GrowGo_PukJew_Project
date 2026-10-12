import pandas as pd
from sklearn.preprocessing import StandardScaler
from sklearn.cluster import KMeans
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

# เติมคอลัมน์กิจกรรมที่ยังไม่มีข้อมูลใน DB ให้เป็น 0
for f in features:
    if f not in df_pivot.columns:
        df_pivot[f] = 0

df_features = df_pivot[features].copy()

# 4. MODEL PREDICTION (K-MEANS k=5)
print("กำลังคำนวณจัดกลุ่ม K-Means Clustering (k=5)")
scaler = StandardScaler()
X_scaled = scaler.fit_transform(df_features)

kmeans = KMeans(n_clusters=5, random_state=42, n_init=10)
df_features['cluster_num'] = kmeans.fit_predict(X_scaled)

# 5. MAP NUMERIC CLUSTER TO SUPABASE CLUSTER_ID
cluster_id_mapping = {
    0: 'CLS_GUARDIAN',
    1: 'CLS_COLLECTOR',
    2: 'CLS_EXPLORER',
    3: 'CLS_HELPER',
    4: 'CLS_CASUAL'     
}

df_features['cluster_id'] = df_features['cluster_num'].map(cluster_id_mapping)
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