using UnityEngine;

/// <summary>
/// 普通のオブジェクトと異変版オブジェクトを入れ替える。
/// 例：普通の絵画 ⇔ 顔が消えた絵画、空の椅子 ⇔ 人形が座った椅子
/// </summary>
public class ObjectSwapAnomaly : Anomaly
{
    [SerializeField] GameObject[] normalObjects;   // 普段表示されているもの
    [SerializeField] GameObject[] anomalyObjects;  // 異変時だけ表示されるもの

    void Awake() => SetState(false);

    protected override void OnActivate() => SetState(true);
    protected override void OnDeactivate() => SetState(false);

    void SetState(bool anomaly)
    {
        foreach (var go in normalObjects) if (go) go.SetActive(!anomaly);
        foreach (var go in anomalyObjects) if (go) go.SetActive(anomaly);
    }
}
