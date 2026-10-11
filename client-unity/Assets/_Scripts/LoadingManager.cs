using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    [Header("UI Reference")]
    public Slider loadingSlider;

    [Header("Target Scene")]
    public string nextSceneName = "MainMenu"; // หลอดเต็มแล้วไปหน้า MainMenu เสมอ

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
        // สั่งโหลดหน้า MainMenu เบื้องหลัง
        AsyncOperation operation = SceneManager.LoadSceneAsync(nextSceneName);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            
            if (loadingSlider != null)
            {
                loadingSlider.value = progress;
            }

            // เมื่อโหลดเสร็จสมบูรณ์
            if (operation.progress >= 0.9f)
            {
                yield return new WaitForSeconds(0.5f);
                operation.allowSceneActivation = true; // สลับไปหน้า MainMenu
            }

            yield return null;
        }
    }
}