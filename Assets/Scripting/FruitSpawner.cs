using System.Collections;
using UnityEngine;
using UnityEngine.UI;               // สำหรับคุม UI Image ของ Next Fruit
using UnityEngine.InputSystem;      // สำหรับ New Input System ของ Unity 6
using UnityEngine.EventSystems;

public class FruitSpawner : MonoBehaviour
{
    [Header("Fruit Prefabs (ใส่ Prefab ผลไม้ลูกเล็ก 2-3 ชนิดแรก)")]
    public GameObject[] fruitPrefabs;

    [Header("Next Fruit Preview UI")]
    public Image nextFruitImage;        // ลาก GameObject NextFruitPreview ที่มีคอมโพเนนต์ Image มาใส่

    [Header("Spawn Settings")]
    public Transform spawnPoint;         // จุดปล่อยผลไม้ (เว้นว่างไว้จะใช้ตำแหน่งตัวเอง)
    public float dropCooldown = 0.8f;    // เวลาหน่วงก่อนลูกถัดไปจะเกิด (วินาที)
    
    [Header("Movement Range (ขอบเขตเลื่อนซ้าย-ขวา)")]
    public float leftLimit = -1.5f;      // ขอบซ้ายจากกึ่งกลาง (ใส่ค่าลบ เช่น -1.8)
    public float rightLimit = 1.5f;      // ขอบขวาจากกึ่งกลาง (ใส่ค่าบวก เช่น 1.8)

    private GameObject currentFruit;
    private Rigidbody2D currentRb;
    private bool canDrop = false;
    private Camera mainCamera;
    private int nextFruitIndex;          // ดัชนีจำว่าลูกถัดไปคือชนิดไหน

    void Start()
    {
        mainCamera = Camera.main;
        if (spawnPoint == null)
        {
            spawnPoint = this.transform;
        }

        // สุ่มเลือกลูกแรก และสุ่มลูกถัดไปเตรียมไว้
        nextFruitIndex = GetRandomFruitIndex();
        SpawnFruit();
    }

    void Update()
    {
        if (currentFruit == null || !canDrop) return;

        // --- 1. ระบบตรวจจับตำแหน่ง Pointer (เมาส์ / การแตะหน้าจอมือถือ) ---
        Vector2 pointerScreenPos = Vector2.zero;
        bool hasPointer = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            pointerScreenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            hasPointer = true;
        }
        else if (Mouse.current != null)
        {
            pointerScreenPos = Mouse.current.position.ReadValue();
            hasPointer = true;
        }

        // ขยับผลไม้ตามตำแหน่งเมาส์ทันที
        if (hasPointer)
        {
            FollowPointer(pointerScreenPos);
        }

        // --- 2. ระบบตรวจจับการคลิกปล่อย (Release) ---
        bool isReleased = false;

        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isReleased = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            isReleased = true;
        }

        if (isReleased)
        {
            // ถ้าคลิกโดนปุ่ม UI จะไม่สั่งปล่อยผลไม้
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            DropFruit();
        }
    }

    // ฟังก์ชันเลื่อนผลไม้ตามพิกัดเมาส์/สัมผัส
    void FollowPointer(Vector2 screenPosition)
    {
        // คำนวณระยะลึก Z จากเลนส์กล้อง
        float distanceToCamera = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 screenPosWithZ = new Vector3(screenPosition.x, screenPosition.y, distanceToCamera);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPosWithZ);

        // กำหนดขอบเขตซ้าย-ขวาโดยอิงจากตำแหน่ง Transform X ของ Spawner
        float minX = transform.position.x + leftLimit;
        float maxX = transform.position.x + rightLimit;
        float clampedX = Mathf.Clamp(worldPos.x, minX, maxX);

        // ขยับผลไม้ลูกปัจจุบันไปตามแกน X ที่เล็ง
        currentFruit.transform.position = new Vector3(clampedX, spawnPoint.position.y, 0f);
    }

    // ฟังก์ชันสร้างผลไม้ขึ้นมาเล็ง
    void SpawnFruit()
    {
        if (fruitPrefabs == null || fruitPrefabs.Length == 0)
        {
            Debug.LogError("กรุณาใส่ Prefabs ผลไม้ในช่อง Fruit Prefabs ก่อน!");
            return;
        }

        // 1. นำผลไม้ลูกที่เตรียมไว้จาก Next Fruit มาสร้าง
        GameObject selectedPrefab = fruitPrefabs[nextFruitIndex];
        Vector3 spawnPos = new Vector3(transform.position.x, spawnPoint.position.y, 0f);
        currentFruit = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);

        currentRb = currentFruit.GetComponent<Rigidbody2D>();
        if (currentRb != null)
        {
            // ล็อกฟิสิกส์ให้เป็น Kinematic เพื่อไม่ให้ร่วงลงมาขณะกำลังเล็ง
            currentRb.bodyType = RigidbodyType2D.Kinematic;
            currentRb.linearVelocity = Vector2.zero;
            currentRb.angularVelocity = 0f;
        }

        canDrop = true;

        // 2. สุ่มลูกถัดไปลูกใหม่ แล้วอัปเดตรูปภาพที่ช่อง Next Fruit บน UI
        nextFruitIndex = GetRandomFruitIndex();
        UpdateNextFruitUI();
    }

    // ฟังก์ชันสุ่มดัชนีผลไม้ลูกเล็กเริ่มต้น (ลำดับ 0 ถึง 2)
    int GetRandomFruitIndex()
    {
        return Random.Range(0, Mathf.Min(3, fruitPrefabs.Length));
    }

    // ฟังก์ชันอัปเดตรูปผลไม้ลูกถัดไปบน UI
    void UpdateNextFruitUI()
    {
        if (nextFruitImage == null || fruitPrefabs.Length == 0) return;

        // ดึง Sprite จาก SpriteRenderer ของ Prefab ลูกถัดไป
        SpriteRenderer sr = fruitPrefabs[nextFruitIndex].GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            nextFruitImage.sprite = sr.sprite;
            nextFruitImage.color = Color.white; // ปรับให้ภาพชัด ไม่โปร่งใส
        }
    }

    // ฟังก์ชันสั่งปล่อยผลไม้
    void DropFruit()
    {
        canDrop = false;

        if (currentRb != null)
        {
            // เปลี่ยนเป็น Dynamic เพื่อให้ร่วงลงไปตามแรงโน้มถ่วง
            currentRb.bodyType = RigidbodyType2D.Dynamic;
            currentRb.gravityScale = 1f;
        }

        // ปลดการเชื่อมโยงลูกปัจจุบัน
        currentFruit = null;
        currentRb = null;

        // หน่วงเวลาแล้วเสกผลไม้ลูกถัดไปออกมา
        StartCoroutine(SpawnNextFruitRoutine());
    }

    IEnumerator SpawnNextFruitRoutine()
    {
        yield return new WaitForSeconds(dropCooldown);
        SpawnFruit();
    }

    // วาดเส้นไกด์ไลน์สีเหลืองในหน้า Scene เพื่อให้เห็นระยะเล็งซ้าย-ขวา
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 from = new Vector3(transform.position.x + leftLimit, transform.position.y, 0);
        Vector3 to = new Vector3(transform.position.x + rightLimit, transform.position.y, 0);
        Gizmos.DrawLine(from, to);
    }
}