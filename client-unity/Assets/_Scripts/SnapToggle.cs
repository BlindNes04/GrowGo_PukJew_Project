using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Slider))]
public class SnapToggle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private Slider slider;
    private RectTransform barRect;
    private Coroutine snapCoroutine;

    [Header("Fill Effect (Mask)")]
    public RectTransform fillMask;       // ลาก FillMask มาใส่
    public float handleWidth = 170f;     // ขนาดความกว้างของ Knob (170px)

    [Header("Settings")]
    public float snapSpeed = 22f;        // เพิ่มสปีดดีดเข้ามุมให้ไวทันใจ ไม่หนืด
    public bool isOn = false;

    private Vector2 pointerDownPos;
    private bool isDragging = false;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        barRect = GetComponent<RectTransform>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void Start()
    {
        SetState(isOn, instant: true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPos = eventData.position;
        isDragging = false;
        if (snapCoroutine != null) StopCoroutine(snapCoroutine);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Vector2.Distance(pointerDownPos, eventData.position) > 5f)
        {
            isDragging = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging || eventData.dragging)
        {
            bool targetState = slider.value >= 0.5f;
            SetState(targetState, instant: false);
        }
        else
        {
            SetState(!isOn, instant: false);
        }

        isDragging = false;
    }

    public void SetState(bool state, bool instant = false)
    {
        isOn = state;
        float targetValue = isOn ? 1f : 0f;

        if (snapCoroutine != null) StopCoroutine(snapCoroutine);

        if (instant)
        {
            slider.value = targetValue;
            UpdateFillMask(targetValue);
        }
        else
        {
            snapCoroutine = StartCoroutine(SnapRoutine(targetValue));
        }
    }

    private void OnSliderChanged(float val)
    {
        UpdateFillMask(val);
    }

    private void UpdateFillMask(float val)
    {
        if (fillMask == null || barRect == null) return;

        float totalWidth = barRect.rect.width;
        float knobRadius = handleWidth * 0.5f;

        if (val <= 0.001f)
        {
            fillMask.sizeDelta = new Vector2(0f, 0f);
        }
        else if (val >= 0.999f)
        {
            fillMask.sizeDelta = new Vector2(totalWidth, 0f);
        }
        else
        {
            // คำนวณตำแหน่งกึ่งกลาง Knob จากค่า slider.value โดยตรง ขอบม่านจะล็อกติดกับ Knob ทันทีไม่มีหลุด
            float knobCenter = Mathf.Lerp(knobRadius, totalWidth - knobRadius, val);
            fillMask.sizeDelta = new Vector2(knobCenter, 0f);
        }
    }

    private IEnumerator SnapRoutine(float targetValue)
    {
        while (Mathf.Abs(slider.value - targetValue) > 0.002f)
        {
            slider.value = Mathf.Lerp(slider.value, targetValue, Time.unscaledDeltaTime * snapSpeed);
            yield return null;
        }
        slider.value = targetValue;
        UpdateFillMask(targetValue);
    }
}