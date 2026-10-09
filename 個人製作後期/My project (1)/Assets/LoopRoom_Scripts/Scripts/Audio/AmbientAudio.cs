using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 部屋にずっと流れている環境音（低いうなり、風、きしみ）を鳴らす。
/// SilenceAnomaly から「音が消える」異変として止められる。
/// </summary>
public class AmbientAudio : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.5f;
    }

    [SerializeField] List<Layer> layers = new();
    [SerializeField] float fadeSpeed = 2f;

    public static readonly List<AmbientAudio> All = new();

    readonly List<AudioSource> sources = new();
    float target = 1f, current = 1f;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        foreach (var l in layers)
        {
            if (!l.clip) continue;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = l.clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = l.volume;
            s.time = Random.Range(0f, l.clip.length); // 毎回同じ所から始まらないように
            s.Play();
            sources.Add(s);
        }
    }

    /// <summary>環境音を消す／戻す（フェード付き）。</summary>
    public void SetMuted(bool muted, bool instant = false)
    {
        target = muted ? 0f : 1f;
        if (instant) current = target;
    }

    void Update()
    {
        current = Mathf.MoveTowards(current, target, Time.deltaTime * fadeSpeed);
        for (int i = 0; i < sources.Count; i++)
            sources[i].volume = layers[i].volume * current;
    }
}
