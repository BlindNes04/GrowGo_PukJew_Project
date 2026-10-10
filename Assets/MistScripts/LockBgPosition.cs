using UnityEngine;

public class LockBgPosition : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector3 initialWorldPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        initialWorldPosition = rectTransform.position;
    }

    void LateUpdate()
    {
        rectTransform.position = initialWorldPosition;
    }
}