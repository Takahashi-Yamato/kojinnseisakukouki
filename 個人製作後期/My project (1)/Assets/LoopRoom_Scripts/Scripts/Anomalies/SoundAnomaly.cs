using UnityEngine;

/// <summary>
/// 音だけの異変。足音、ノック、ささやき声など。
/// AudioSourceを鳴らしたい場所（壁の裏、ベッドの下など）に置く。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundAnomaly : Anomaly
{
    [SerializeField] AudioClip[] clips;
    [SerializeField] float minInterval = 4f;
    [SerializeField] float maxInterval = 10f;
    [SerializeField] bool loop = false;  // trueなら1つのクリップをループ再生（テレビの砂嵐など）

    AudioSource source;
    float nextPlayTime;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f; // 3Dサウンド（方向が分かると怖い）
    }

    protected override void OnActivate()
    {
        if (loop && clips.Length > 0)
        {
            source.clip = clips[0];
            source.loop = true;
            source.Play();
        }
        else
        {
            nextPlayTime = Time.time + Random.Range(minInterval, maxInterval);
        }
    }

    protected override void OnDeactivate()
    {
        source.Stop();
        source.loop = false;
    }

    void Update()
    {
        if (!IsActive || loop || clips.Length == 0) return;
        if (Time.time >= nextPlayTime)
        {
            source.PlayOneShot(clips[Random.Range(0, clips.Length)]);
            nextPlayTime = Time.time + Random.Range(minInterval, maxInterval);
        }
    }
}
