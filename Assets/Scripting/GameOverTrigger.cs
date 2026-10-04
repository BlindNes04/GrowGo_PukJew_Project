using System.Collections.Generic;
using UnityEngine;

public class GameOverTrigger : MonoBehaviour
{
    [Header("เวลาที่ยอมให้ล้ำเส้น (วินาที)")]
    public float timeLimit = 5f;

    private float timer = 0f;
    private List<GameObject> fruitsInZone = new List<GameObject>();

    void OnTriggerEnter2D(Collider2D other)
    {
        // เช็คว่าสิ่งที่เข้ามาชน เป็นผลไม้ที่มีสคริปต์รวมร่างหรือไม่
        if (other.GetComponent<FruitMerger>() != null)
        {
            fruitsInZone.Add(other.gameObject);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<FruitMerger>() != null)
        {
            fruitsInZone.Remove(other.gameObject);
        }
    }

    void Update()
    {
        // ล้างรายการผลไม้ที่อาจจะโดนทำลาย(รวมร่าง)ไปแล้ว
        fruitsInZone.RemoveAll(item => item == null);

        // ถ้ายังมีผลไม้แช่อยู่ในโซน ให้เริ่มนับเวลา
        if (fruitsInZone.Count > 0)
        {
            timer += Time.deltaTime;
            if (timer >= timeLimit)
            {
                GameManager.instance.GameOver();
                enabled = false; // จบเกมแล้ว ปิดสคริปต์นี้ไปเลย
            }
        }
        else
        {
            timer = 0f; // ถ้าไม่มีล้ำเส้นแล้ว ให้รีเซ็ตเวลา
        }
    }
}