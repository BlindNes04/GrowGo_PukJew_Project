using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // เพิ่มตัวนี้เพื่อจัดการปุ่ม UI

public class FruitSpawner : MonoBehaviour
{
    [Header("ตั้งค่าผลไม้ (สุ่มเลเวล 1-5)")]
    public GameObject[] fruitPrefabs;
    public Image nextFruitImage;

    [Header("ขอบเขตการเลื่อนซ้าย-ขวา")]
    public float leftBound = 173f;
    public float rightBound = 1000f;

    [Header("เส้นเล็ง (Aim Line)")]
    public LineRenderer aimLine; // ลาก Line Renderer มาใส่ช่องนี้

    private GameObject currentFruit;
    private int nextFruitIndex;
    private bool isReadyToDrop = false;

    void Start()
    {
        if (aimLine != null) aimLine.enabled = false; // ปิดเส้นเล็งตอนเริ่ม

        nextFruitIndex = Random.Range(0, 4); // สุ่มแค่เลเวล 1 ถึง 4
        SpawnNewFruit();
    }

    void Update()
    {
        if (isReadyToDrop && currentFruit != null)
        {
            // เช็คว่ากดเมาส์ค้าง และ "ไม่ได้" กดโดนปุ่ม UI อยู่
            if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                float clampedX = Mathf.Clamp(mousePos.x, leftBound, rightBound);

                // 1. เลื่อนผลไม้ตามแนวแกน X
                currentFruit.transform.position = new Vector3(clampedX, transform.position.y, 0);

                // 2. วาดเส้นเล็งจากผลไม้ลงไปที่ก้นกล่อง
                if (aimLine != null)
                {
                    aimLine.enabled = true;
                    aimLine.SetPosition(0, currentFruit.transform.position); // จุดเริ่มที่ผลไม้
                    aimLine.SetPosition(1, new Vector3(clampedX, -10f, 0));  // จุดปลายชี้ลงพื้น (ปรับเลข -10f ได้ถ้ายาวไม่พอ)
                }
            }
            else
            {
                // ถ้าปล่อยเมาส์ ให้ซ่อนเส้นเล็ง
                if (aimLine != null) aimLine.enabled = false;
            }
        }
    }

    public void SpawnNewFruit()
    {
        currentFruit = Instantiate(fruitPrefabs[nextFruitIndex], transform.position, Quaternion.identity);

        Rigidbody2D rb = currentFruit.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        nextFruitIndex = Random.Range(0, 4);
        nextFruitImage.sprite = fruitPrefabs[nextFruitIndex].GetComponent<SpriteRenderer>().sprite;

        isReadyToDrop = true;
    }

    public void DropFruit()
    {
        if (isReadyToDrop && currentFruit != null)
        {
            isReadyToDrop = false;
            if (aimLine != null) aimLine.enabled = false; // ปิดเส้นเล็ง

            Rigidbody2D rb = currentFruit.GetComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
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