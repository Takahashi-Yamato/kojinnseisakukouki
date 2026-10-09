using UnityEngine;

/// <summary>
/// 音が消える異変。ずっと鳴っていた環境音や時計の音が、このループだけ聞こえない。
/// 目に見える変化がないので、気づくとゾッとするタイプ。
/// targets が空なら、すべての環境音（AmbientAudio）を消す。
/// </summary>
public class SilenceAnomaly : Anomaly
{
    [Tooltip("止める音（時計のAudioSourceなど）。空なら環境音すべて")]
    [SerializeField] AudioSource[] targets;

    protected override void OnActivate() => Set(true);
    protected override void OnDeactivate() => Set(false);

    void Set(bool silent)
    {
        if (targets != null && targets.Length > 0)
        {
            foreach (var s in targets) if (s) s.mute = silent;
            return;
        }
        foreach (var a in AmbientAudio.All) a.SetMuted(silent, instant: true);
    }
}
