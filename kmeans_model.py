import pandas as pd
import seaborn as sns
import matplotlib.pyplot as plt
import joblib
from sklearn.preprocessing import StandardScaler
from sklearn.cluster import KMeans
from sklearn.metrics import silhouette_score

log_df = pd.read_csv('behaviour_log.csv')

# Feature Engineering
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

# Scaling
scaler = StandardScaler()
X_scaled = scaler.fit_transform(df_features[features])

# Silhouette Score Comparison
print("\n--- Silhouette Score Comparison ---")
for k in [3, 4, 5, 6]:
    km_test = KMeans(n_clusters=k, random_state=42, n_init=10)
    labels = km_test.fit_predict(X_scaled)
    score = silhouette_score(X_scaled, labels)
    print(f"k = {k} -> Silhouette Score: {score:.3f}")

# Train Model (k = 6)
kmeans = KMeans(n_clusters=6, random_state=42, n_init=10)
df_features['cluster_id'] = kmeans.fit_predict(X_scaled)

cluster_means = df_features.groupby('cluster_id')[features].mean().round(1)

# Save Model
joblib.dump(scaler, 'scaler.joblib')
joblib.dump(kmeans, 'kmeans_model.joblib')
print("\nบันทึก scaler.joblib และ kmeans_model.joblib เรียบร้อย")

# Plot Evaluation Graph (Elbow & Silhouette)
inertias = []
sil_scores = []
k_range = range(3, 9)

for k in k_range:
    km = KMeans(n_clusters=k, random_state=42, n_init=10)
    labels = km.fit_predict(X_scaled)
    inertias.append(km.inertia_)
    sil_scores.append(silhouette_score(X_scaled, labels))

fig, ax1 = plt.subplots(figsize=(9, 4.5))
color = 'tab:blue'
ax1.set_xlabel('Number of Clusters (k)', fontsize=11)
ax1.set_ylabel('Inertia (Elbow Method)', color=color, fontsize=11)
ax1.plot(k_range, inertias, marker='o', color=color, linewidth=2)
ax1.tick_params(axis='y', labelcolor=color)
ax1.grid(True, linestyle='--', alpha=0.5)

ax2 = ax1.twinx()
color = 'tab:orange'
ax2.set_ylabel('Silhouette Score', color=color, fontsize=11)
ax2.plot(k_range, sil_scores, marker='s', color=color, linewidth=2, linestyle='--')
ax2.tick_params(axis='y', labelcolor=color)

plt.title('Optimal k Evaluation: Elbow & Silhouette Score', fontsize=13, fontweight='bold')
plt.tight_layout()
plt.show()

# Heatmap Visualization
cluster_names = {
    0: 'Helper',
    1: 'Collector',     
    2: 'Explorer',  
    3: 'Gamer',  
    4: 'Casual',
    5: 'Guardian'     
}

cluster_means_named = cluster_means.rename(index=cluster_names)
feature_labels = [
    'Fertilizer Craft', 'Fruit Merge', 'Block Blast',
    'Island Explore', 'Species Collected', 'Tree Certs', 'Friend Assists'
]
cluster_means_named.columns = feature_labels

plt.figure(figsize=(11, 5.5))
sns.heatmap(
    cluster_means_named,
    annot=True,
    fmt='.1f',
    cmap='YlGnBu',
    cbar=True,
    annot_kws={'size': 10}
)
plt.title('Player Persona Profiling (k = 6)', fontsize=14, fontweight='bold', pad=15)
plt.xlabel('Game Activities / Features', fontsize=11, labelpad=10)
plt.ylabel('Persona Type', fontsize=11, labelpad=10)
plt.xticks(rotation=30, ha='right')
plt.yticks(rotation=0)
plt.tight_layout()
plt.show()

print("\nประมวลผลและแสดงผลกราฟ Cluster เรียบร้อย\n")