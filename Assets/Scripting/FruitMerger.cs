using UnityEngine;

public class FruitMerger : MonoBehaviour
{
    [Header("ข้อมูลผลไม้")]
    public int fruitLevel;             
    public GameObject nextFruitPrefab; 
    public int scoreValue = 10; 

    // ตัวล็อกไม่ให้ผลไม้รวมร่างซ้ำซ้อนกันจนเกิดบักbug
    private bool hasMerged = false;
    
    // GameObject ชนกับ Collider2D 
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasMerged) return;
        
        // หาค่าผลไม้ที่ไปชน
        FruitMerger otherFruit = collision.gameObject.GetComponent<FruitMerger>();

        if (otherFruit != null)
        {
            if (this.fruitLevel == otherFruit.fruitLevel)
            {            
                if (this.nextFruitPrefab == null) return;

                this.hasMerged = true;
                otherFruit.hasMerged = true;

                Vector2 spawnPosition = (transform.position + otherFruit.transform.position) / 2f;
                GameObject newFruit = Instantiate(nextFruitPrefab, spawnPosition, Quaternion.identity);

                //ป้องกันการเด้ง
                Rigidbody2D rb = newFruit.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;

                // เรียก GameManager
                if (GameManager.instance != null)
                {
                    GameManager.instance.AddScore(this.scoreValue * 2);

                    // UnlockMelon (เงื่อนไขรางวัล)
                    if (this.fruitLevel == 8)
                    {
                        GameManager.instance.UnlockMelon();
                    }
                }

                // เล่นเสียง pop ตอนรวมร่างสำเร็จ
                if (AudioManager.instance != null) AudioManager.instance.PlayMerge();
                
                Destroy(gameObject);
                Destroy(otherFruit.gameObject);
            }
        }
    }
}
