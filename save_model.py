import joblib
import pandas as pd
from sklearn.preprocessing import StandardScaler
from sklearn.cluster import KMeans

log_df = pd.read_csv('behaviour_log.csv')

act_mapping = {
    'craft_fertilizer': 'fert_craft_count',
    'play_fruit_merge': 'fruit_merge_count',
    'play_block_blast': 'block_blast_count',
    'explore_island': 'island_explore_count',
    'collect_species': 'species_collected',
    'submit_tree_cert': 'tree_certs_submitted',
    'water_friend_tree': 'friend_water_assists'
}

log_df['feature_name'] = log_df['activity_type'].map(act_mapping)

df_features = (
    log_df.groupby(['user_id', 'feature_name'])
    .size()
    .unstack(fill_value=0)
    .reset_index()
)

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
    if f not in df_features.columns:
        df_features[f] = 0

# Fitting Scaler และ K-Means
scaler = StandardScaler()
X_scaled = scaler.fit_transform(df_features[features])

kmeans = KMeans(n_clusters=5, random_state=42, n_init=10)
kmeans.fit(X_scaled)

# บันทึกไฟล์
joblib.dump(scaler, 'scaler.joblib')
joblib.dump(kmeans, 'kmeans_model.joblib')

print("บันทึก scaler.joblib และ kmeans_model.joblib เรียบร้อย")