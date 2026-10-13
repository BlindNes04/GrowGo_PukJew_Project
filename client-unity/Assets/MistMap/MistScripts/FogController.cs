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

        [Header("Push Aside Settings")]
        public float pushDistanceX = -200f;
        [HideInInspector] public Vector3 initialLocalPos;
        [HideInInspector] public Image cachedImage;
        [HideInInspector] public SpriteRenderer cachedSprite;
        [HideInInspector] public bool isDispersed = false;
        [HideInInspector] public float pushOffset = 0f; 
    }

    [Header("Fog GameObjects")]
    public List<FogItem> fogList = new List<FogItem>();

    [Header("Idle Alpha Settings")]
    [Range(0f, 1f)] public float minAlpha = 0.1f;
    [Range(0f, 1f)] public float maxAlpha = 0.85f;
    public float alphaPulseSpeed = 1.2f;

    [Header("Click Disperse Settings")]
    public float disperseDuration = 1.5f;

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
            fog.fogObject.transform.localPosition = fog.initialLocalPos + new Vector3(idleFloatX + fog.pushOffset, 0f, 0f);

            if (!fog.isDispersed)
            {
                float wave = (Mathf.Sin((Time.time + fog.timeOffset + (i * 1.5f)) * alphaPulseSpeed) + 1f) / 2f;
                float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
                SetObjectAlpha(fog, currentAlpha);
            }
        }
    }

    public void OnFogClicked(int fogIndex)
    {
        if (fogIndex >= 0 && fogIndex < fogList.Count)
        {
            var fog = fogList[fogIndex];
            if (!fog.isDispersed)
            {
                StartCoroutine(PushAsideFogRoutine(fog));
            }
        }
    }

    private IEnumerator PushAsideFogRoutine(FogItem fog)
    {
        fog.isDispersed = true;
        float elapsed = 0f;
        float pushDuration = 0.35f;
        float startAlpha = GetCurrentAlpha(fog);

        // สไลด์ไปด้านข้าง + จาง
        while (elapsed < pushDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pushDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fog.pushOffset = Mathf.Lerp(0f, fog.pushDistanceX, smoothT);
            SetObjectAlpha(fog, Mathf.Lerp(startAlpha, 0.05f, smoothT));
            yield return null;
        }

        yield return new WaitForSeconds(disperseDuration);

        // กลับมาตำแหน่งเดิม + ทึบขึ้น
        elapsed = 0f;
        float returnDuration = 1.0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            fog.pushOffset = Mathf.Lerp(fog.pushDistanceX, 0f, smoothT);
            SetObjectAlpha(fog, Mathf.Lerp(0.05f, maxAlpha, smoothT));
            yield return null;
        }

        fog.pushOffset = 0f;
        fog.isDispersed = false;
    }

    private float GetCurrentAlpha(FogItem fog)
    {
        if (fog.cachedImage != null) return fog.cachedImage.color.a;
        if (fog.cachedSprite != null) return fog.cachedSprite.color.a;
        return 1f;
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