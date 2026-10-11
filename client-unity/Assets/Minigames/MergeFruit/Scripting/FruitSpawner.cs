using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;

public class FruitSpawner : MonoBehaviour
{
    [Header("ตั้งค่าผลไม้")]
    public GameObject[] fruitPrefabs;
    public Image nextFruitImage;
    public float leftBound = 173f;
    public float rightBound = 1000f;

    [Header("เส้นเล็ง (Aim Line)")]
    public LineRenderer aimLine; // กลับมาใช้ Line Renderer

    private GameObject currentFruit;
    private int nextFruitIndex;
    private bool isReadyToDrop = false;

    void Start()
    {
        if (aimLine != null) aimLine.enabled = false;

        nextFruitIndex = Random.Range(0, fruitPrefabs.Length);
        SpawnNewFruit();
    }

    void Update()
    {
        // ถ้ารอปล่อยอยู่ และมีผลไม้อยู่บนหัว
        if (isReadyToDrop && currentFruit != null)
        {
            // 1. จัดการการเลื่อนผลไม้ (เฉพาะตอนกดเมาส์ค้าง)
            if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                float clampedX = Mathf.Clamp(mousePos.x, leftBound, rightBound);
                currentFruit.transform.position = new Vector3(clampedX, transform.position.y, 0);
            }

            // 2. วาดเส้นเล็ง (ทำตลอดเวลา ไม่ต้องรอกดเมาส์)
            if (aimLine != null)
            {
                aimLine.enabled = true;
                Vector2 startPos = currentFruit.transform.position;
                aimLine.SetPosition(0, startPos); // จุดเริ่มที่ผลไม้

                // ปิดกรอบชนของผลไม้ที่ถืออยู่ชั่วคราว
                //Collider2D col = currentFruit.GetComponent<Collider2D>();
                //if (col != null) col.enabled = false;

                // ยิงเรดาร์ลงด้านล่าง
                RaycastHit2D hit = Physics2D.Raycast(startPos, Vector2.down);

                // เปิดกรอบชนกลับคืน
                //if (col != null) col.enabled = true;

                // อัปเดตจุดปลายเส้น ให้หยุดตรงที่ชนพอดี
                if (hit.collider != null)
                {
                    aimLine.SetPosition(1, hit.point);
                }
                else
                {
                    aimLine.SetPosition(1, new Vector3(startPos.x, -10f, 0));
                }
            }
        }
        else
        {
            // ถ้าปล่อยไปแล้ว ให้ซ่อนเส้น
            if (aimLine != null) aimLine.enabled = false;
        }
    }

    public void SpawnNewFruit()
    {
        currentFruit = Instantiate(fruitPrefabs[nextFruitIndex], transform.position, Quaternion.identity);
        Rigidbody2D rb = currentFruit.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        // ปิดการชนตอนเพิ่งเสก เพื่อไม่ให้กระเด็นตอนผลไม้ล้นกล่อง
        Collider2D col = currentFruit.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        nextFruitIndex = Random.Range(0, fruitPrefabs.Length);
        if (nextFruitImage != null)
        {
            nextFruitImage.sprite = fruitPrefabs[nextFruitIndex].GetComponent<SpriteRenderer>().sprite;
        }

        isReadyToDrop = true;
    }

    public void DropFruit()
    {
        if (isReadyToDrop && currentFruit != null)
        {
            isReadyToDrop = false;
            if (aimLine != null) aimLine.enabled = false; // ปิดเส้นทันทีที่กดปล่อย

            Rigidbody2D rb = currentFruit.GetComponent<Rigidbody2D>();
            rb.gravityScale = 1f;

            // 💡 เพิ่มบรรทัดนี้: แอบใส่แรงผลักซ้ายหรือขวาแบบสุ่มนิดๆ เพื่อทำลายสมดุลไม่ให้มันซ้อนเป็นหอคอย
            rb.linearVelocity = new Vector2(Random.Range(-0.2f, 0.2f), 0f);

            // เปิดการชนตอนปล่อย ให้ร่วงไปทับลูกอื่นได้
            Collider2D col = currentFruit.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;

            currentFruit = null;
            StartCoroutine(WaitAndSpawn(1.5f));
        }
    }

    IEnumerator WaitAndSpawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnNewFruit();
    }
}