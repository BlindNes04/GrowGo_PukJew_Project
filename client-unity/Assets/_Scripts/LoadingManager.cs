using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// ==================== LoadingManager: หน้าโหลดตอนเปิดเกม ====================
// หลอดโหลดแสดงงานที่โหลดอยู่จริง 2 ส่วน:
//   1) โหลดฉาก MainMenu เบื้องหลัง (LoadSceneAsync)
//   2) ถ้าเคยล็อกอินค้างไว้ -> ดึงข้อมูลผู้เล่นจาก Supabase เก็บ cache (SupabaseManager.PreloadAll)
//      ข้อมูลพร้อมตั้งแต่หน้าโหลด เข้า MainMenu / MainForest แล้วไม่ต้องรอโหลดหรือเห็นค่ากระพริบ
// ทั้งสองส่วนเสร็จแล้วค่อยเปลี่ยนไปฉาก MainMenu
public class LoadingManager : MonoBehaviour
{
    [Header("UI Reference")]
    public Slider loadingSlider;

    [Header("Target Scene")]
    public string nextSceneName = "MainMenu"; // หลอดเต็มแล้วไปหน้า MainMenu เสมอ

    [Header("หลอดโหลด")]
    [Range(0f, 1f)] public float sceneShare = 0.3f;   // ส่วนของหลอดที่เป็นการโหลดฉาก (ที่เหลือเป็นการโหลดข้อมูลผู้เล่น)
    public float maxWaitSeconds = 15f;                 // เน็ตช้า/หลุด รอข้อมูลได้นานสุดเท่านี้ แล้วเข้าเกมต่อ (หน้าอื่นจะโหลดเองทีหลัง)

    void Awake()
    {
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        StartCoroutine(LoadTargetSceneAsync());
    }

    IEnumerator LoadTargetSceneAsync()
    {
        // ---------- เริ่มโหลดข้อมูลผู้เล่น ----------
        // เรียก Instance ครั้งแรก = สร้าง SupabaseManager (อยู่ข้ามฉาก) -> Awake อ่าน SavedUserId แล้วเริ่ม PreloadAll ให้เอง
        SupabaseManager db = SupabaseManager.Instance;
        bool hasUser = !string.IsNullOrEmpty(db.currentUserId);

        // ---------- เริ่มโหลดฉาก MainMenu เบื้องหลัง (ยังไม่สลับฉากจนกว่าทุกอย่างพร้อม) ----------
        AsyncOperation operation = SceneManager.LoadSceneAsync(nextSceneName);
        operation.allowSceneActivation = false;

        float startTime = Time.unscaledTime;
        while (true)
        {
            // Unity โหลดฉากเสร็จที่ progress = 0.9 (ที่เหลือคือตอนสลับฉาก)
            float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
            bool sceneReady = operation.progress >= 0.9f;

            // ไม่มีผู้เล่นค้างไว้ = ไม่มีข้อมูลต้องโหลด
            float dataProgress = hasUser ? db.preloadProgress : 1f;
            bool dataReady = !hasUser || !db.IsPreloading;

            // หลอด = ค่าจริงของงานที่โหลดอยู่ (ไม่ทำให้นุ่ม จะได้เห็นว่าขยับตามงานจริง)
            float progress = hasUser ? sceneProgress * sceneShare + dataProgress * (1f - sceneShare) : sceneProgress;
            if (loadingSlider != null) loadingSlider.value = progress;

            bool timeout = Time.unscaledTime - startTime > maxWaitSeconds;
            if (sceneReady && (dataReady || timeout))
            {
                if (timeout && !dataReady) Debug.LogWarning("[Loading] ⏳ โหลดข้อมูลผู้เล่นนานเกิน เข้าเกมต่อ (หน้าอื่นจะโหลดเองทีหลัง)");
                break;
            }
            yield return null;
        }

        // ---------- ทุกอย่างพร้อม -> หลอดเต็ม แล้วสลับไปหน้า MainMenu ----------
        if (loadingSlider != null) loadingSlider.value = 1f;
        yield return new WaitForSeconds(0.2f);
        operation.allowSceneActivation = true;
    }
}
