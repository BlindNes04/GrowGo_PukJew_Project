using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip bgmClip;     
    public AudioClip clickSFX;   
    public AudioClip errorSFX;   
    public AudioClip itemFoundSFX;   

    void Awake()
    {
        // กำหนด Instance ไว้ใช้สั่งงานภายใน Scene นี้
        Instance = this;
    }

    void Start()
    {
        if (bgmClip != null)
        {
            PlayBGM(bgmClip);
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource != null && clip != null)
        {
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
}