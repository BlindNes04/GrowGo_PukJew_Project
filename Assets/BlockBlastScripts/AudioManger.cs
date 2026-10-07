using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioSource audioSource;
    public AudioClip placeClip;  
    public AudioClip breakClip;  
    public AudioClip gameOverClip; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlayPlaceSound()
    {
        if (audioSource != null && placeClip != null)
        {
            audioSource.PlayOneShot(placeClip, 0.8f);
        }
    }

    public void PlayBreakSound()
    {
        if (audioSource != null && breakClip != null)
        {
            audioSource.PlayOneShot(breakClip, 1.0f);
        }
    }

    public void PlayGameOverSound()
    {
        if (audioSource != null && gameOverClip != null)
        {
            audioSource.PlayOneShot(gameOverClip);
        }
    }

}