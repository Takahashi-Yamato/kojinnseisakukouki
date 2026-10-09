using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ろうそく・ランタン・暖炉の火が消える。
/// target 以下にある Light、ParticleSystem、名前に flame / fire / 炎 を含むオブジェクトをまとめて消す。
/// 宿屋アセットのろうそくプレハブをそのまま target に指定すればOK。
/// </summary>
public class CandleOutAnomaly : Anomaly
{
    [SerializeField] Transform target;
    [Tooltip("このキーワードを名前に含む子オブジェクトも消す")]
    [SerializeField] string[] flameKeywords = { "flame", "fire", "炎" };

    readonly List<Light> lights = new();
    readonly List<ParticleSystem> particles = new();
    readonly List<GameObject> flames = new();

    void Awake()
    {
        if (!target) target = transform;
        lights.AddRange(target.GetComponentsInChildren<Light>(true));
        particles.AddRange(target.GetComponentsInChildren<ParticleSystem>(true));
        foreach (var t in target.GetComponentsInChildren<Transform>(true))
        {
            if (t == target) continue;
            string n = t.name.ToLowerInvariant();
            foreach (var k in flameKeywords)
                if (n.Contains(k.ToLowerInvariant())) { flames.Add(t.gameObject); break; }
        }
    }

    protected override void OnActivate() => Set(false);
    protected override void OnDeactivate() => Set(true);

    void Set(bool on)
    {
        foreach (var l in lights) if (l) l.enabled = on;
        foreach (var p in particles)
        {
            if (!p) continue;
            if (on) p.Play(); else p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var f in flames) if (f) f.SetActive(on);
    }
}
