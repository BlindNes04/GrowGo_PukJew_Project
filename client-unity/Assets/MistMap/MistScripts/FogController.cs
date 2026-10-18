using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FogController : MonoBehaviour
{
    [System.Serializable]
    public class FogItem
    {
        public string fogName = "Fog Object";
        public GameObject fogObject;

        [Header("Idle Movement")]
        public float moveDistance = 25f;
        public float moveSpeed = 0.5f;
        public float timeOffset = 0f;

        [HideInInspector] public Vector3 initialLocalPos;
        [HideInInspector] public Image cachedImage;
        [HideInInspector] public SpriteRenderer cachedSprite;
    }

    [Header("Fog GameObjects")]
    public List<FogItem> fogList = new List<FogItem>();

    [Header("Idle Alpha Settings")]
    [Range(0f, 1f)] public float minAlpha = 0.1f;
    [Range(0f, 1f)] public float maxAlpha = 0.85f;
    public float alphaPulseSpeed = 1.2f;

    void Start()
    {
        for (int i = 0; i < fogList.Count; i++)
        {
            var fog = fogList[i];
            if (fog.fogObject != null)
            {
                fog.initialLocalPos = fog.fogObject.transform.localPosition;
                fog.cachedImage = fog.fogObject.GetComponent<Image>();
                fog.cachedSprite = fog.fogObject.GetComponent<SpriteRenderer>();
            }
        }
    }

    void Update()
    {
        for (int i = 0; i < fogList.Count; i++)
        {
            var fog = fogList[i];
            if (fog.fogObject == null) continue;

            // หมอกลอยไปมา
            float idleFloatX = Mathf.Sin((Time.time + fog.timeOffset) * fog.moveSpeed) * fog.moveDistance;
            fog.fogObject.transform.localPosition = fog.initialLocalPos + new Vector3(idleFloatX, 0f, 0f);

            // ลูปจาง-ทึบ
            float wave = (Mathf.Sin((Time.time + fog.timeOffset + (i * 1.5f)) * alphaPulseSpeed) + 1f) / 2f;
            float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
            SetObjectAlpha(fog, currentAlpha);
        }
    }

    private void SetObjectAlpha(FogItem fog, float alpha)
    {
        if (fog.cachedImage != null)
        {
            Color c = fog.cachedImage.color;
            c.a = alpha;
            fog.cachedImage.color = c;
        }
        else if (fog.cachedSprite != null)
        {
            Color c = fog.cachedSprite.color;
            c.a = alpha;
            fog.cachedSprite.color = c;
        }
    }
}