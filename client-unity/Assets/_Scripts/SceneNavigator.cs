using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    [Header("Default Main Scene")]
    [SerializeField] private string mainSceneName = "MainForest";

    // สลับไปฉากไหนก็ได้ตามชื่อที่พิมพ์
    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // ปุ่มลัดกลับหน้าหลักทันที
    public void GoToMainScene()
    {
        SceneManager.LoadScene(mainSceneName);
    }

    // รีโหลดฉากปัจจุบัน
    public void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}