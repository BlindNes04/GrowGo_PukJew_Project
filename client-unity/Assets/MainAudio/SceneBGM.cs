using UnityEngine;

public class SceneBGM : MonoBehaviour
{
    [Header("ใส่เพลงประจำ Scene (ถ้าอยากให้ใช้เพลง Main เดิม ให้ปล่อย None ไว้)")]
    public AudioClip sceneBGM;

    void Start()
    {
        if (MAudioManager.instance != null && sceneBGM != null)
        {
            // ถ้ามีเพลงเฉพาะหน้า เช่น มินิเกม ให้สั่งเปลี่ยนเพลง
            MAudioManager.instance.PlayBGM(sceneBGM);
        }
    }
}