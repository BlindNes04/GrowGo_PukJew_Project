using UnityEngine;

public class MFSoundManager : MonoBehaviour
{
    // กำหนดประเภทเป็น MFSoundManager ให้ถูกต้อง
    public static MFSoundManager instance;
    public static MFSoundManager Instance => instance; // ทำเผื่อไฟล์อื่นเรียกใช้ตัวพิมพ์ใหญ่ Instance

    [Header("ตัวเล่นเสียง")]
    public AudioSource sfxSource;

    [Header("ไฟล์เสียงต่างๆ")]
    public AudioClip dropSound;      // เสียงตอนปล่อยผลไม้
    public AudioClip mergeSound;     // เสียงตอนรวมร่าง

    void Awake()
    {
        if (instance == null) 
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // ฟังก์ชันสำหรับเรียกใช้เสียงต่างๆ
    public void PlayDrop() 
    { 
        if (sfxSource != null && dropSound != null) 
        {
            sfxSource.PlayOneShot(dropSound); 
        }
    }
    
    public void PlayMerge() 
    { 
        if (sfxSource != null && mergeSound != null) 
        {
            sfxSource.PlayOneShot(mergeSound); 
        }
    }
}