using UnityEngine;

public class LockBgPosition : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector3 initialWorldPosition;
    private bool isInitialized = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        // 💡 จำตำแหน่งจริงจากหน้าฉาก ตั้งแต่สร้างวัตถุครั้งแรกครั้งเดียวเท่านั้น
        if (rectTransform != null)
        {
            initialWorldPosition = rectTransform.position;
            isInitialized = true;
        }
    }

    void OnEnable()
    {
        // 💡 เมื่อเปิดใช้งานใหม่ ให้บังคับคืนค่ากลับจุดเริ่มต้นตั้งต้นเสมอ
        if (isInitialized && rectTransform != null)
        {
            rectTransform.position = initialWorldPosition;
        }
    }

    void LateUpdate()
    {
        if (isInitialized && rectTransform != null)
        {
            rectTransform.position = initialWorldPosition;
        }
    }
}