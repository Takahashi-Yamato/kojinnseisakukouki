using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// びっくり演出用の効果音を、その場で合成して作る（音素材なしで動く）。
/// 好きな音素材がある場合は ScareFX のインスペクターで差し替えられる。
/// </summary>
public static class ScareSounds
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> cache = new();
    static readonly System.Random rnd = new(13);

    /// <summary>「ジャーン！」という不協和音の衝撃音</summary>
    public static AudioClip Stinger => Get("stinger", MakeStinger);
    /// <summary>甲高い叫び声</summary>
    public static AudioClip Scream => Get("scream", MakeScream);
    /// <summary>ドアを激しく叩く音</summary>
    public static AudioClip Bang => Get("bang", MakeBang);
    /// <summary>重い足音1歩</summary>
    public static AudioClip Footstep => Get("footstep", MakeFootstep);
    /// <summary>心臓の鼓動（ループ用）</summary>
    public static AudioClip Heartbeat => Get("heartbeat", MakeHeartbeat);
    /// <summary>耳元のささやき・息づかい</summary>
    public static AudioClip Whisper => Get("whisper", MakeWhisper);

    static AudioClip Get(string key, Func<float[]> make)
    {
        if (cache.TryGetValue(key, out var c) && c) return c;
        var data = Normalize(make());
        c = AudioClip.Create("scare_" + key, data.Length, 1, Rate, false);
        c.SetData(data, 0);
        cache[key] = c;
        return c;
    }

    static float Noise() => (float)(rnd.NextDouble() * 2.0 - 1.0);
    static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];
    const float TwoPi = Mathf.PI * 2f;

    static float[] MakeStinger()
    {
        var d = Buf(2.2f);
        float[] freqs = { 196f, 207.7f, 277.2f, 415.3f, 587.3f, 622.3f };
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float env = t < 0.008f ? t / 0.008f : Mathf.Exp(-t * 1.6f);
            float tones = 0f;
            foreach (var f in freqs)
                tones += Mathf.Sin(TwoPi * f * t + 0.4f * Mathf.Sin(TwoPi * 5.5f * t));
            tones /= freqs.Length;
            lp += (Noise() - lp) * 0.25f;
            float hit = lp * Mathf.Exp(-t * 7f);
            float boom = Mathf.Sin(TwoPi * (38f + 30f * Mathf.Exp(-t * 6f)) * t) * Mathf.Exp(-t * 2.5f);
            d[i] = (tones * 0.7f + hit * 0.9f + boom * 0.8f) * env;
        }
        return d;
    }

    static float[] MakeScream()
    {
        var d = Buf(1.4f);
        float ph1 = 0f, ph2 = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 780f + 520f * (t / 1.4f) + 70f * Mathf.Sin(TwoPi * 12f * t);
            ph1 += f / Rate; ph2 += f * 1.49f / Rate;
            float saw = 2f * (ph1 % 1f) - 1f;
            float saw2 = 2f * (ph2 % 1f) - 1f;
            float n = Noise();
            lp += (n - lp) * 0.08f;
            float hiss = n - lp;
            float env = Mathf.Min(1f, t / 0.02f) * Mathf.Clamp01((1.4f - t) / 0.35f);
            d[i] = (saw * 0.45f + saw2 * 0.25f + hiss * 0.55f) * env;
        }
        return d;
    }

    static float[] MakeBang()
    {
        var d = Buf(0.7f);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            ph += (42f + 90f * Mathf.Exp(-t * 25f)) / Rate;
            float thump = Mathf.Sin(TwoPi * ph) * Mathf.Exp(-t * 7f);
            lp += (Noise() - lp) * 0.3f;
            float crack = lp * Mathf.Exp(-t * 30f);
            d[i] = thump * 1.0f + crack * 0.9f;
        }
        return d;
    }

    static float[] MakeFootstep()
    {
        var d = Buf(0.3f);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            lp += (Noise() - lp) * 0.12f;
            float thud = Mathf.Sin(TwoPi * 85f * t) * Mathf.Exp(-t * 20f);
            float scuff = lp * Mathf.Exp(-t * 45f);
            d[i] = thud * 0.9f + scuff * 0.7f;
        }
        return d;
    }

    static float[] MakeHeartbeat()
    {
        var d = Buf(0.9f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            foreach (var (start, amp) in new[] { (0f, 1f), (0.26f, 0.7f) })
            {
                float u = t - start;
                if (u < 0f) continue;
                s += Mathf.Sin(TwoPi * 48f * u) * Mathf.Exp(-u * 16f) * amp;
            }
            d[i] = s;
        }
        return d;
    }

    static float[] MakeWhisper()
    {
        var d = Buf(2.2f);
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float n = Noise();
            lp1 += (n - lp1) * 0.35f;
            lp2 += (lp1 - lp2) * 0.05f;
            float band = lp1 - lp2; // 「シー…」という帯域だけ残す
            float syll = 0.5f + 0.5f * Mathf.Sin(TwoPi * 6.5f * t) * Mathf.Sin(TwoPi * 0.9f * t);
            float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((2.2f - t) / 0.5f);
            d[i] = band * syll * env;
        }
        return d;
    }

    static float[] Normalize(float[] d)
    {
        float max = 0.0001f;
        foreach (var s in d) max = Mathf.Max(max, Mathf.Abs(s));
        float k = 0.95f / max;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
        return d;
    }
}
