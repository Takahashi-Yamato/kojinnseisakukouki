using UnityEngine;

/// <summary>
/// 窓の外（skybox）がいつもと違う異変。
///
///  ・Anomaly Skybox に別のマテリアルを入れる → 丸ごと差し替え（夜→昼、赤い空、星がない など）
///  ・空欄のまま → 今の skybox をコピーして、色・向き・明るさだけ変える（さりげない異変向け）
///
/// 元の skybox アセットは書き換えないので、再生を止めても元に戻る。
/// </summary>
public class SkyboxAnomaly : Anomaly
{
    [Header("差し替える場合")]
    [Tooltip("異変時に使う skybox マテリアル。空なら下の設定で今の skybox を変化させる")]
    [SerializeField] Material anomalySkybox;

    [Header("今の skybox を変化させる場合")]
    [Tooltip("色味（白 = 変えない）")]
    [SerializeField] Color tint = new Color(1f, 0.35f, 0.3f);
    [Tooltip("回転（度）。月や雲の位置がずれる")]
    [SerializeField, Range(0f, 360f)] float rotation = 0f;
    [Tooltip("明るさの倍率（1 = 変えない）")]
    [SerializeField, Range(0f, 3f)] float exposure = 1f;

    [Header("共通")]
    [Tooltip("空の色に合わせて部屋の環境光も変える（環境光の Source が Skybox のとき）")]
    [SerializeField] bool updateLighting = false;

    Material original;
    Material modified;

    protected override void OnActivate()
    {
        original = RenderSettings.skybox;

        if (anomalySkybox)
        {
            RenderSettings.skybox = anomalySkybox;
        }
        else if (original)
        {
            modified = new Material(original) { name = original.name + " (異変)" };
            Apply(modified);
            RenderSettings.skybox = modified;
        }
        else
        {
            Debug.LogWarning("[SkyboxAnomaly] シーンに skybox が設定されていません（Lighting > Environment > Skybox Material）");
            return;
        }
        Refresh();
    }

    protected override void OnDeactivate()
    {
        if (original) RenderSettings.skybox = original;
        if (modified) Destroy(modified);
        modified = null;
        Refresh();
    }

    void Apply(Material m)
    {
        // 6 Sided / Cubemap / Panoramic
        if (m.HasProperty("_Tint")) m.SetColor("_Tint", m.GetColor("_Tint") * tint);
        if (m.HasProperty("_Rotation")) m.SetFloat("_Rotation", (m.GetFloat("_Rotation") + rotation) % 360f);
        // Procedural
        if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", m.GetColor("_SkyTint") * tint);
        if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", m.GetColor("_GroundColor") * tint);
        // 共通
        if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", m.GetFloat("_Exposure") * exposure);
    }

    void Refresh()
    {
        if (updateLighting) DynamicGI.UpdateEnvironment();
    }
}
