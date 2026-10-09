using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトルシーン用のメニュー。
/// ・背景（部屋のスクショ）がゆっくりズームし、ろうそくの灯りのように明るさが揺らぐ
/// ・W/S か ↑↓ で選択、Enter / Space / クリックで決定（マウスを乗せても選べる）
/// ・「はじめる」でゲームシーンを読み込む
/// </summary>
public class TitleSceneMenu : MonoBehaviour
{
    [Header("ゲームシーン")]
    [Tooltip("「はじめる」で読み込むシーン名（Build Profiles / Build Settings に登録されている必要あり）")]
    [SerializeField] string gameSceneName = "LoopRoom";

    [Header("表示する文字")]
    [SerializeField] string title = "八泊目";
    [SerializeField] string subtitle = "― 異変に気づいたら、眠れ ―";
    [SerializeField] string[] glitchTexts = { "にげられない", "また、この部屋", "ねむれ" };

    [Header("参照（自動設定）")]
    [SerializeField] Text titleText;
    [SerializeField] Text subtitleText;
    [SerializeField] Text[] menuItems;     // 0:はじめる 1:遊び方 2:おわる
    [SerializeField] GameObject howToPanel;
    [SerializeField] RectTransform background;
    [SerializeField] Image backgroundImage;
    [SerializeField] CanvasGroup fader;
    [SerializeField] AudioSource ambient;
    [SerializeField] AudioSource sfx;
    [SerializeField] AudioClip moveSound;
    [SerializeField] AudioClip decideSound;

    static readonly Color Normal = new(0.62f, 0.6f, 0.56f);
    static readonly Color Selected = new(0.95f, 0.92f, 0.85f);
    static readonly Color Blood = new(0.75f, 0.08f, 0.06f);

    int index;
    bool busy;
    float nextGlitch;
    Color bgBase;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        titleText.text = title;
        subtitleText.text = subtitle;
        if (howToPanel) howToPanel.SetActive(false);
        if (backgroundImage) bgBase = backgroundImage.color;
        nextGlitch = Time.time + Random.Range(2f, 4f);
        Refresh();

        // 黒からふわっと出てくる
        if (fader) { fader.alpha = 1f; StartCoroutine(Fade(0f, 2f)); }
        if (ambient) { ambient.volume = 0f; ambient.Play(); StartCoroutine(FadeAudio(ambient, 0.6f, 3f)); }
    }

    void Update()
    {
        AnimateBackground();
        if (busy) return;
        Glitch();

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (howToPanel && howToPanel.activeSelf)
        {
            if ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame))
                howToPanel.SetActive(false);
            return;
        }

        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Move(-1);
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Move(1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Decide();
        }
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            for (int i = 0; i < menuItems.Length; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(menuItems[i].rectTransform, mp, null)) continue;
                if (i != index) { index = i; Refresh(); Play(moveSound, 0.3f); }
                if (mouse.leftButton.wasPressedThisFrame) Decide();
            }
        }
    }

    // 背景：ゆっくりズーム＋ろうそくのような明るさの揺らぎ
    void AnimateBackground()
    {
        if (background)
        {
            float s = 1.08f + Mathf.Sin(Time.time * 0.05f) * 0.04f;
            background.localScale = new Vector3(s, s, 1f);
            background.anchoredPosition = new Vector2(Mathf.Sin(Time.time * 0.07f) * 25f, Mathf.Sin(Time.time * 0.05f) * 10f);
        }
        if (backgroundImage)
        {
            float flicker = 0.85f + 0.1f * Mathf.PerlinNoise(Time.time * 2.5f, 0f) + 0.05f * Mathf.PerlinNoise(Time.time * 9f, 3f);
            backgroundImage.color = bgBase * flicker;
        }
    }

    void Move(int d)
    {
        index = (index + d + menuItems.Length) % menuItems.Length;
        Refresh();
        Play(moveSound, 0.3f);
    }

    void Refresh()
    {
        for (int i = 0; i < menuItems.Length; i++)
        {
            string label = menuItems[i].text.TrimStart('▶', ' ').TrimEnd(' ', '◀');
            menuItems[i].text = i == index ? $"▶  {label}  ◀" : label;
            menuItems[i].color = i == index ? Selected : Normal;
        }
    }

    void Decide()
    {
        switch (index)
        {
            case 0: StartCoroutine(StartGame()); break;
            case 1: if (howToPanel) howToPanel.SetActive(true); break;
            case 2: Quit(); break;
        }
    }

    IEnumerator StartGame()
    {
        busy = true;
        Play(decideSound, 0.8f);
        titleText.color = Blood;
        yield return new WaitForSeconds(0.4f);
        if (ambient) StartCoroutine(FadeAudio(ambient, 0f, 1.2f));
        yield return Fade(1f, 1.2f);
        yield return new WaitForSeconds(0.3f);

        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else
        {
            Debug.LogError($"[Title] シーン「{gameSceneName}」が Build Settings に登録されていません。");
            busy = false;
            titleText.color = Selected;
            yield return Fade(0f, 0.5f);
        }
    }

    void Glitch()
    {
        if (Time.time < nextGlitch) return;
        nextGlitch = Time.time + Random.Range(3f, 7f);
        StartCoroutine(GlitchOnce());
    }

    IEnumerator GlitchOnce()
    {
        var pos = titleText.rectTransform.anchoredPosition;
        for (int i = 0; i < Random.Range(3, 7); i++)
        {
            titleText.rectTransform.anchoredPosition = pos + new Vector2(Random.Range(-14f, 14f), Random.Range(-4f, 4f));
            titleText.color = Random.value < 0.5f ? Blood : Selected;
            if (glitchTexts.Length > 0 && Random.value < 0.35f)
                titleText.text = glitchTexts[Random.Range(0, glitchTexts.Length)];
            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
        }
        titleText.rectTransform.anchoredPosition = pos;
        if (!busy) titleText.color = Selected;
        titleText.text = title;
    }

    IEnumerator Fade(float to, float dur)
    {
        if (!fader) yield break;
        float from = fader.alpha;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            fader.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        fader.alpha = to;
    }

    static IEnumerator FadeAudio(AudioSource s, float to, float dur)
    {
        float from = s.volume;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            s.volume = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        s.volume = to;
    }

    void Play(AudioClip c, float v)
    {
        if (sfx && c) sfx.PlayOneShot(c, v);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
