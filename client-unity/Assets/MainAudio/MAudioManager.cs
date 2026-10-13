using UnityEngine;
using UnityEngine.SceneManagement;

public class MAudioManager : MonoBehaviour
{
    public static MAudioManager instance;

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("Default BGM")]
    public AudioClip defaultBGM; // เพลง main garden bg music

    [Header("SFX Clips")]
    public AudioClip clickSFX;   // 💡 เพิ่มช่องใส่ไฟล์เสียงกดปุ่ม (Click SFX) กลับมาให้แล้วครับ

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // สั่งหยุด BGM หลักถ้าเข้าหน้ามินิเกม
        if (scene.name == "MistIsland" || scene.name == "Mist_Island" || scene.name == "MergeFruit" || scene.name == "BlockBlast")
        {
            StopBGM();
        }
        else
        {
            if (defaultBGM != null)
            {
                PlayBGM(defaultBGM);
            }
        }
    }

    public void PlayBGM(AudioClip newClip)
    {
        if (bgmSource == null || newClip == null) return;

        if (bgmSource.clip == newClip && bgmSource.isPlaying) return;

        bgmSource.clip = newClip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // 💡 ฟังก์ชันเล่นเสียงคลิกปุ่ม (สามารถเอาไปผูกกับ Button OnClick ได้เลย)
    public void PlayClickSFX()
    {
        if (sfxSource != null && clickSFX != null)
        {
            sfxSource.PlayOneShot(clickSFX);
        }
    }
}