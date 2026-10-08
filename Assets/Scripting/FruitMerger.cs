using UnityEngine;

public class FruitMerger : MonoBehaviour
{
    [Header("ข้อมูลผลไม้")]
    public int fruitLevel;             // ระดับของผลไม้ลูกนี้ (เช่น 1, 2, 3...)
    public GameObject nextFruitPrefab; // ผลไม้ร่างต่อไปที่จะเสกออกมาเมื่อรวมร่าง
    public int scoreValue = 10; // <--- (คะแนนที่จะได้เมื่อรวมลูกนี้สำเร็จ)

    // ตัวล็อกไม่ให้ผลไม้รวมร่างซ้ำซ้อนกันจนเกิดบักเสกเบิ้ล
    private bool hasMerged = false;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasMerged) return;

        FruitMerger otherFruit = collision.gameObject.GetComponent<FruitMerger>();

        if (otherFruit != null)
        {
            if (this.fruitLevel == otherFruit.fruitLevel)
            {
                // 💡 เพิ่มบรรทัดนี้: ถ้าเป็นร่างสุดท้าย (ไม่มีร่างต่อไปให้เสก) ให้หยุดการทำงานไปเลย ผลไม้จะแค่กลิ้งชนกันเฉยๆ
                if (this.nextFruitPrefab == null) return;

                this.hasMerged = true;
                otherFruit.hasMerged = true;

                // สร้างผลไม้ร่างใหม่ที่จุดกึ่งกลาง
                Vector2 spawnPosition = (transform.position + otherFruit.transform.position) / 2f;
                GameObject newFruit = Instantiate(nextFruitPrefab, spawnPosition, Quaternion.identity);

                Rigidbody2D rb = newFruit.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;

                // 💡 เพิ่มบรรทัดนี้: เรียก GameManager ให้บวกคะแนน (เอาคะแนนของลูกที่ชนกัน 2 ลูกมารวมกัน)
                if (GameManager.instance != null)
                {
                    GameManager.instance.AddScore(this.scoreValue * 2);

                    // 💡 เพิ่มบรรทัดนี้ลงไป: ถ้าผลไม้ที่เราเพิ่งสร้างคือ LV9 (ถ้า LV8 ของคุณคือ fruitLevel 8 ให้ใส่ 8)
                    // (แปลว่าเมื่อ LV8 ชนกับ LV8 จะกลายเป็น LV9)
                    if (this.fruitLevel == 8)
                    {
                        GameManager.instance.UnlockMelon();
                    }
                }

                // ทำลายผลไม้ร่างเก่า
                Destroy(gameObject);
                Destroy(otherFruit.gameObject);
            }
        }
    }
}
