using UnityEngine;

/// <summary>
/// すべての異変の基底クラス。
/// シーン内のオブジェクトに付けておき、LoopManagerがループごとに1つだけActivateする。
/// 新しい異変はこのクラスを継承して OnActivate / OnDeactivate を書くだけ。
/// </summary>
public abstract class Anomaly : MonoBehaviour
{
    [Tooltip("デバッグ・ログ用の名前")]
    public string anomalyName = "Unnamed";

    [Tooltip("この周回数以上で出現する（序盤は分かりやすい異変だけにしたい時に使う）")]
    public int minLoop = 0;

    [Tooltip("出現しやすさ。大きいほど選ばれやすい")]
    [Min(0.01f)] public float weight = 1f;

    public bool IsActive { get; private set; }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        OnActivate();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        OnDeactivate();
    }

    /// <summary>異変を起こす。</summary>
    protected abstract void OnActivate();

    /// <summary>部屋を"普通"の状態に戻す。</summary>
    protected abstract void OnDeactivate();
}
