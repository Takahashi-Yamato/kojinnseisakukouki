using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 別シーン（タイトル）から読み込んだときに部屋が真っ暗になる問題の対策。
///
/// エディタで直接再生したときは Unity が環境光を自動計算してくれるが、
/// SceneManager.LoadScene で読み込んだシーンはライティングデータが無いと環境光がゼロになる。
/// ここで RenderSettings の設定から環境光を作り直す。
/// （根本的には Window > Rendering > Lighting の「Generate Lighting」で焼いておくのが一番確実）
/// </summary>
public static class LightingFix
{
    public static void Apply()
    {
        switch (RenderSettings.ambientMode)
        {
            case AmbientMode.Flat:
            {
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(RenderSettings.ambientLight * RenderSettings.ambientIntensity);
                RenderSettings.ambientProbe = sh;
                break;
            }
            case AmbientMode.Trilight:
            {
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(RenderSettings.ambientEquatorColor);
                sh.AddDirectionalLight(Vector3.up, RenderSettings.ambientSkyColor - RenderSettings.ambientEquatorColor, 0.5f);
                sh.AddDirectionalLight(Vector3.down, RenderSettings.ambientGroundColor - RenderSettings.ambientEquatorColor, 0.5f);
                RenderSettings.ambientProbe = sh;
                break;
            }
            default:
                DynamicGI.UpdateEnvironment(); // スカイボックスから計算し直す
                break;
        }
    }
}
