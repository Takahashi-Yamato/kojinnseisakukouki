using UnityEngine;

/// <summary>
/// ライトの色を変えたり、不規則にちらつかせたりする。
/// </summary>
public class LightAnomaly : Anomaly
{
    [SerializeField] Light targetLight;
    [SerializeField] bool changeColor = false;
    [SerializeField] Color anomalyColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] bool flicker = true;
    [SerializeField, Range(0f, 1f)] float flickerChancePerSecond = 0.6f;

    Color baseColor;
    float baseIntensity;
    float nextFlickerTime;

    void Awake()
    {
        if (!targetLight) targetLight = GetComponent<Light>();
        baseColor = targetLight.color;
        baseIntensity = targetLight.intensity;
    }

    protected override void OnActivate()
    {
        if (changeColor) targetLight.color = anomalyColor;
    }

    protected override void OnDeactivate()
    {
        targetLight.color = baseColor;
        targetLight.intensity = baseIntensity;
        targetLight.enabled = true;
    }

    void Update()
    {
        if (!IsActive || !flicker) return;
        if (Time.time < nextFlickerTime) return;

        // 一瞬消える → すぐ戻る、を不規則に繰り返す
        if (Random.value < flickerChancePerSecond * 0.1f)
        {
            targetLight.enabled = !targetLight.enabled;
            nextFlickerTime = Time.time + (targetLight.enabled ? Random.Range(0.3f, 2f) : Random.Range(0.03f, 0.15f));
        }
        else
        {
            nextFlickerTime = Time.time + 0.1f;
        }
    }
}
