using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("ตัวเล่นเสียง")]
    public AudioSource sfxSource;

    [Header("ไฟล์เสียงต่างๆ")]
    public AudioClip dropSound;      // เสียงตอนปล่อยผลไม้
    public AudioClip mergeSound;     // เสียงตอนรวมร่าง

    void Awake()
    {
        if (instance == null) instance = this;
    }

    // ฟังก์ชันสำหรับเรียกใช้เสียงต่างๆ
    public void PlayDrop() 
    { 
        if (dropSound != null) sfxSource.PlayOneShot(dropSound); 
    }
    
    public void PlayMerge() 
    { 
        if (mergeSound != null) sfxSource.PlayOneShot(mergeSound); 
    }
    
}
