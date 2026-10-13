using UnityEngine;
using UnityEngine.UI;

public class GroupedMushroomAutoGlow : MonoBehaviour
{
    [Header("Parent Groups")]
    public GameObject layer1Parent;
    public GameObject layer2Parent; 

    [Header("Speed Settings")]
    public float glowSpeed = 2.0f;

    [Header("Alpha Settings")]
    [Range(0f, 1f)] public float minAlpha = 0.0f;
    [Range(0f, 1f)] public float maxAlpha = 1.0f;

    [Header("Timing Offsets")]
    public float innerLayerOffset = 0.3f;
    public float timeStepBetweenMushrooms = 0.65f;

    private Image[] l1Images;
    private Image[] l2Images;

    void Start()
    {
        if (layer1Parent != null)
        {
            l1Images = layer1Parent.GetComponentsInChildren<Image>();
        }
        if (layer2Parent != null)
        {
            l2Images = layer2Parent.GetComponentsInChildren<Image>();
        }
    }

    void Update()
    {
        if (l1Images == null) return;

        for (int i = 0; i < l1Images.Length; i++)
        {
            float mushroomBaseOffset = i * timeStepBetweenMushrooms;

            if (l1Images[i] != null)
            {
                ApplyGlow(l1Images[i], mushroomBaseOffset);
            }

            if (l2Images != null && i < l2Images.Length && l2Images[i] != null)
            {
                ApplyGlow(l2Images[i], mushroomBaseOffset + innerLayerOffset);
            }
        }
    }

    private void ApplyGlow(Image targetImage, float offset)
    {
        float wave = (Mathf.Sin((Time.time + offset) * glowSpeed) + 1f) / 2f;
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

        Color c = targetImage.color;
        c.a = currentAlpha;
        targetImage.color = c;
    }
}