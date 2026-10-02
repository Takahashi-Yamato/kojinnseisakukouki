using System.Collections;
using UnityEngine;

/// <summary>
/// 画面全体の暗転。Canvas上の全画面Image（黒）にCanvasGroupを付けて使う。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
    }

    public void SetAlpha(float a)
    {
        if (!group) group = GetComponent<CanvasGroup>();
        group.alpha = a;
    }

    public IEnumerator Fade(float to, float duration)
    {
        float from = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        group.alpha = to;
    }
}
