using System;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
//  GrowGoJuiceScale : ปุ่มเด้งดึ๋งตอนกด / ต้นไม้หายใจเบาๆ
// -------------------------------------------------------------------------------------
//  GrowGoJuice ติดให้ปุ่มทุกปุ่มเองอัตโนมัติ (ไม่ต้องใส่ใน Inspector)
//  เขียน localScale เฉพาะตอนกำลังเด้งเท่านั้น เด้งเสร็จคืนค่าเดิมเป๊ะ (ไม่ทับอนิเมชันอื่นของเกม)
// =====================================================================================
public class GrowGoJuiceScale : MonoBehaviour
{
    public bool isButton;
    public bool breathe;            // ต้นไม้: หายใจ (ยืด-หด) ตลอด
    [NonSerialized] public float jitter;   // สั่น (ตอนกำลังวิวัฒนาการ)

    private Vector3 baseScale = Vector3.one;
    private bool active;
    private bool baseTaken;
    private float press, pressTarget;
    private float sx, sv;           // spring
    private Selectable sel;

    private void Awake()
    {
        sel = GetComponent<Selectable>();
        baseScale = transform.localScale;
        baseTaken = true;
    }

    private void Begin()
    {
        if (!active && !breathe) baseScale = transform.localScale;
        if (!baseTaken) { baseScale = transform.localScale; baseTaken = true; }
        active = true;
    }

    // เรียกจาก GrowGoJuicePress (ติดเฉพาะปุ่ม)
    public void PressDown()
    {
        if (!GrowGoJuice.Enabled) return;
        if (sel != null && !sel.IsInteractable()) return;
        Begin();
        pressTarget = 1f;
    }

    public void PressUp(bool dragging)
    {
        if (pressTarget > 0f)
        {
            pressTarget = 0f;
            if (!dragging) Kick(0.14f);   // ลากเลื่อน ScrollRect อยู่ ไม่ต้องเด้ง
        }
    }

    public void PressCancel() { pressTarget = 0f; }

    public void Kick(float amount)
    {
        if (!GrowGoJuice.Enabled) return;
        Begin();
        sv += amount * 28f;
    }

    private void LateUpdate()
    {
        if (!active && !breathe) return;
        if (!active) { Begin(); }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        press = Mathf.MoveTowards(press, pressTarget, dt * 14f);
        // สปริง: ดึงกลับที่ 0 แบบเด้งๆ
        float acc = -320f * sx - 13f * sv;
        sv += acc * dt;
        sx += sv * dt;

        float s = 1f - 0.12f * press + sx;
        float x = s, y = s;
        // squash & stretch: เด้งขึ้นยืดสูง แกน x หดนิดนึง
        y *= 1f + sx * 0.35f;
        x *= 1f - sx * 0.2f;
        if (breathe)
        {
            float b = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.022f;
            y *= 1f + b;
            x *= 1f - b * 0.6f;
        }
        if (jitter > 0f)
        {
            x *= 1f + UnityEngine.Random.Range(-jitter, jitter);
            y *= 1f + UnityEngine.Random.Range(-jitter, jitter);
        }
        transform.localScale = new Vector3(baseScale.x * x, baseScale.y * y, baseScale.z);

        if (!breathe && pressTarget == 0f && press < 0.001f && Mathf.Abs(sx) < 0.0008f && Mathf.Abs(sv) < 0.02f)
        {
            transform.localScale = baseScale;
            active = false; sx = 0f; sv = 0f; press = 0f;
        }
    }

    private void OnDisable()
    {
        if (active || breathe) transform.localScale = baseScale;
        active = false; press = 0f; pressTarget = 0f; sx = 0f; sv = 0f; jitter = 0f;
    }
}
