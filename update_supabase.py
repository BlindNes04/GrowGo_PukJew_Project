import os
import sys
import joblib
import pandas as pd
from dotenv import load_dotenv
from sqlalchemy import create_engine, text

# 1. DATABASE CONNECTION
load_dotenv()
DB_URL = os.getenv("DATABASE_URL")

if not DB_URL:
    print("ไม่พบค่า DATABASE_URL ในไฟล์ .env")
    sys.exit(1)

print("กำลังเชื่อมต่อกับ Supabase Database...")
try:
    engine = create_engine(DB_URL)
except Exception as e:
    print(f"ไม่สามารถสร้างการเชื่อมต่อฐานข้อมูลได้: {e}")
    sys.exit(1)

# 2. FETCH DATA FROM DATABASE
query_log = """
    SELECT user_id, activity_type, COUNT(*) as act_count
    FROM behavior_log
    GROUP BY user_id, activity_type;
"""

# ดึง cluster_id ปัจจุบันมาด้วย เพื่อใช้เทียบว่าใครเปลี่ยนกลุ่มบ้าง
query_user = """
    SELECT user_id, created_at, cluster_id
    FROM "user";
"""

print("กำลังดึงข้อมูลจาก Supabase...")
try:
    df_raw = pd.read_sql(query_log, con=engine)
    df_user = pd.read_sql(query_user, con=engine)
except Exception as e:
    print(f"เกิดข้อผิดพลาดในการดึงข้อมูล: {e}")
    sys.exit(1)

if df_user.empty:
    print("ไม่พบข้อมูลผู้เล่นในตาราง user")
    sys.exit(0)

# 3. FEATURE ENGINEERING & PIVOT
act_mapping = {
    'craft_fertilizer': 'fert_craft_count',
    'play_fruit_merge': 'fruit_merge_count',
    'play_block_blast': 'block_blast_count',
    'explore_island': 'island_explore_count',
    'collect_species': 'species_collected',
    'submit_tree_cert': 'tree_certs_submitted',
    'water_friend_tree': 'friend_water_assists'
}
features = list(act_mapping.values())

if not df_raw.empty:
    df_pivot = (
        df_raw.pivot(index="user_id", columns="activity_type", values="act_count")
        .rename(columns=act_mapping)
        .reindex(columns=features, fill_value=0)
    )
else:
    df_pivot = pd.DataFrame(columns=features)

df_user['created_at'] = pd.to_datetime(df_user['created_at'], utc=True)
df_user['days_played'] = (pd.Timestamp.now(tz='UTC') - df_user['created_at']).dt.days.fillna(0)

# เก็บ cluster_id เดิมไว้ในชื่อ old_cluster_id
df_user = df_user.rename(columns={'cluster_id': 'old_cluster_id'})

df_features = (
    df_user[['user_id', 'days_played', 'old_cluster_id']]
    .merge(df_pivot, on='user_id', how='left')
    .set_index('user_id')
)
df_features[features] = df_features[features].fillna(0)
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
    print(f"เกิดข้อผิดพลาดในการโหลดหรือรันโมเดล joblib: {e}")
    sys.exit(1)

# 5. MAP NUMERIC CLUSTER TO CLUSTER_ID & FILTER ONLY CHANGED
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

is_changed = df_features['cluster_id'] != df_features['old_cluster_id']
changed_df = df_features.loc[is_changed, ['cluster_id']].reset_index()

print(f"ผู้เล่นทั้งหมด {len(df_features)} คน | มีการเปลี่ยนกลุ่ม {len(changed_df)} คน")

# 6. BULK UPDATE RESULTS BACK TO 'user' TABLE
update_sql = text("""
    UPDATE "user"
    SET cluster_id = :cluster_id
    WHERE user_id = :user_id;
""")

records = (
    changed_df[['user_id', 'cluster_id']]
    .astype({'user_id': str, 'cluster_id': str})
    .to_dict('records')
)

try:
    if not records:
        print("ไม่มีผู้เล่นที่เปลี่ยนกลุ่ม ไม่จำเป็นต้องอัปเดตฐานข้อมูล")
    else:
        print(f"กำลังบันทึก cluster_id เฉพาะคนที่เปลี่ยน ({len(records)} รายการ)...")
        with engine.begin() as conn:
            BATCH_SIZE = 1000
            for i in range(0, len(records), BATCH_SIZE):
                batch = records[i : i + BATCH_SIZE]
                conn.execute(update_sql, batch)

        print("อัปเดตข้อมูล cluster_id บน Supabase เรียบร้อย")

except Exception as e:
    print(f"เกิดข้อผิดพลาดระหว่างบันทึกข้อมูลลง Supabase (ระบบได้ยกเลิกการเปลี่ยนแปลงแล้ว): {e}")
    sys.exit(1)