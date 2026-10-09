using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 連続正解数を表示する。3DのTextMesh（壁の紙など）でも、画面のUI Textでもどちらでも使える。
/// format の {0} が連続正解数に置き換わる（例：「{0} 泊目」）。
/// </summary>
public class WallCounter : MonoBehaviour
{
    [SerializeField] TextMesh text;     // 3D用
    [SerializeField] Text uiText;       // 画面UI用
    [SerializeField] string format = "{0}";
    [Tooltip("表示する数に足す値。1にすると「0泊目」ではなく「1泊目」から始まる")]
    [SerializeField] int offset = 0;

    void Start()
    {
        if (!text) text = GetComponent<TextMesh>();
        if (!uiText) uiText = GetComponent<Text>();
        Show(0);
        if (LoopManager.Instance) LoopManager.Instance.onStreakChanged.AddListener(Show);
    }

    void Show(int streak)
    {
        string s = string.Format(format, streak + offset);
        if (text) text.text = s;
        if (uiText) uiText.text = s;
    }
}
