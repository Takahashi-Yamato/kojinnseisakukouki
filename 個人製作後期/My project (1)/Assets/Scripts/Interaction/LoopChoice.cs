using UnityEngine;

/// <summary>
/// ループを進める選択肢。ドアとベッドの両方にこのスクリプトを付け、
/// meansAnomaly を切り替えて使う。
///   ドア   … meansAnomaly = false（「異変はなかった」）
///   ベッド … meansAnomaly = true （「異変があった」）
/// </summary>
public class LoopChoice : MonoBehaviour, IInteractable
{
    [SerializeField] bool meansAnomaly;
    [SerializeField] string prompt = "ドアを開ける";
    [SerializeField] AudioSource sfx;   // ドアの開閉音、布団の音など（任意）

    public string Prompt => prompt;

    public void Interact()
    {
        if (sfx) sfx.Play();
        LoopManager.Instance.SubmitChoice(meansAnomaly);
    }
}
