using UnityEngine;

/// <summary>
/// オブジェクトの位置・回転・大きさを少しだけ変える。
/// 例：時計が傾いている、椅子の向きが壁を向いている、ドアが少し大きい
/// </summary>
public class TransformAnomaly : Anomaly
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 positionOffset;
    [SerializeField] Vector3 rotationOffset;
    [SerializeField] Vector3 scaleMultiplier = Vector3.one;

    Vector3 basePos, baseScale;
    Quaternion baseRot;

    void Awake()
    {
        if (!target) target = transform;
        basePos = target.localPosition;
        baseRot = target.localRotation;
        baseScale = target.localScale;
    }

    protected override void OnActivate()
    {
        target.localPosition = basePos + positionOffset;
        target.localRotation = baseRot * Quaternion.Euler(rotationOffset);
        target.localScale = Vector3.Scale(baseScale, scaleMultiplier);
    }

    protected override void OnDeactivate()
    {
        target.localPosition = basePos;
        target.localRotation = baseRot;
        target.localScale = baseScale;
    }
}
