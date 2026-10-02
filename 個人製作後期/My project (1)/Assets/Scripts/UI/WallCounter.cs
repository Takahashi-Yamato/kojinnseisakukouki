using UnityEngine;

/// <summary>
/// 壁に貼った紙に「連続正解数」を表示する（8番出口の看板のような役割）。
/// </summary>
public class WallCounter : MonoBehaviour
{
    [SerializeField] TextMesh text;

    void Start()
    {
        if (!text) text = GetComponent<TextMesh>();
        Show(0);
        if (LoopManager.Instance) LoopManager.Instance.onStreakChanged.AddListener(Show);
    }

    void Show(int streak) => text.text = streak.ToString();
}
