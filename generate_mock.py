import numpy as np
import pandas as pd
from datetime import datetime, timedelta

np.random.seed(42)

n_per_group = 500

personas_config = {
    'Explorer': {
        'rates': {
            'craft_fertilizer': 50,
            'play_fruit_merge': 15,
            'play_block_blast': 12,
            'explore_island': 125,
            'collect_species': 12,
            'submit_tree_cert': 0,
            'water_friend_tree': 15
        }
    },
    'Guardian': {
        'rates': {
            'craft_fertilizer': 15,
            'play_fruit_merge': 10,
            'play_block_blast': 15,
            'explore_island': 20,
            'collect_species': 3,
            'submit_tree_cert': 45,
            'water_friend_tree': 20
        }
    },
    'Helper': {
        'rates': {
            'craft_fertilizer': 20,
            'play_fruit_merge': 12,
            'play_block_blast': 85,
            'explore_island': 25,
            'collect_species': 4,
            'submit_tree_cert': 0,
            'water_friend_tree': 120
        }
    },
    'Collector': {
        'rates': {
            'craft_fertilizer': 85,
            'play_fruit_merge': 75,
            'play_block_blast': 18,
            'explore_island': 60,
            'collect_species': 36,
            'submit_tree_cert': 5,
            'water_friend_tree': 25
        }
    },
    'Casual': {
        'rates': {
            'craft_fertilizer': 8,
            'play_fruit_merge': 6,
            'play_block_blast': 5,
            'explore_island': 10,
            'collect_species': 2,
            'submit_tree_cert': 0,
            'water_friend_tree': 5
        }
    }
}

BUDDY_LIST = [
    'BUDDY_BEAR', 'BUDDY_BEAVER', 'BUDDY_BLACK_CAT', 'BUDDY_CHAMELEON',
    'BUDDY_DEER', 'BUDDY_DOLPHIN', 'BUDDY_FOX', 'BUDDY_HAMSTER',
    'BUDDY_LEOPARD', 'BUDDY_LION', 'BUDDY_OTTER', 'BUDDY_OWL',
    'BUDDY_PENGUIN', 'BUDDY_RABBIT', 'BUDDY_SQUIRREL', 'BUDDY_TURTLE'
]

start_date = datetime(2026, 8, 1, 0, 0, 0)
logs = []
users = []

log_counter = 1
user_counter = 1

for persona_name, cfg in personas_config.items():
    for _ in range(n_per_group):
        uid = f"MCK_{user_counter:04d}"
        assigned_buddy = np.random.choice(BUDDY_LIST)
        tag = f"#{user_counter:04d}"

        users.append({
            'user_id': uid,
            'user_tag': tag,
            'buddy_id': assigned_buddy,
            'cluster_id': None,
            'username': f"user_{user_counter:04d}",
            'email': f"user_{user_counter:04d}@example.com",
            'password': "hashed_password_placeholder",
            'level': int(np.random.randint(1, 50)),
            'total_exp': int(np.random.randint(0, 10000)),
            'flower_points': int(np.random.randint(0, 500)),
            'green_points': int(np.random.randint(0, 500)),
            'created_at': start_date.strftime("%Y-%m-%d %H:%M:%S"),
            'last_login_at': (start_date + timedelta(days=30)).strftime("%Y-%m-%d %H:%M:%S"),
        })

        user_counter += 1

        for act, mean_count in cfg['rates'].items():
            if mean_count == 0:
                count = 0
            else:
                count = max(0, int(np.random.normal(mean_count, max(1.5, mean_count * 0.12))))

            for _ in range(count):
                rand_seconds = np.random.randint(0, 30 * 86400)
                ts = start_date + timedelta(seconds=rand_seconds)
                logs.append({
                    'log_id': f"BEH_LOG_{log_counter:06d}",
                    'user_id': uid,
                    'activity_type': act,
                    'timestamp': ts.strftime("%Y-%m-%d %H:%M:%S")
                })
                log_counter += 1

# Export เป็น CSV
pd.DataFrame(users).to_csv('users.csv', index=False)
pd.DataFrame(logs).sample(frac=1, random_state=42).reset_index(drop=True).to_csv('behaviour_log.csv', index=False)

print("สร้างและบันทึกไฟล์ users.csv และ behavior_log.csv เรียบร้อย")