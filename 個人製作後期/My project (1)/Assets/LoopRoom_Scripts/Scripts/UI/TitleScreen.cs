using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// タイトル画面。部屋の景色を背景に、タイトルとメニュー（はじめる／遊び方／おわる）を出す。
/// ・W/S か ↑↓ で選択、Enter / Space / E / クリックで決定（マウスを乗せても選べる）
/// ・タイトル文字はときどきノイズのように乱れる
/// </summary>
public class TitleScreen : MonoBehaviour
{
    [Header("表示する文字")]
    [SerializeField] string title = "八泊目";
    [SerializeField] string subtitle = "― 異変に気づいたら、眠れ ―";
    [Tooltip("一瞬だけ入れ替わる不気味な文字")]
    [SerializeField] string[] glitchTexts = { "にげられない", "八泊目", "また、この部屋", "ねむれ" };

    [Header("参照（自動設定）")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Text titleText;
    [SerializeField] Text subtitleText;
    [SerializeField] Text[] menuItems;      // 0:はじめる 1:遊び方 2:おわる
    [SerializeField] GameObject howToPanel;
    [SerializeField] GameObject[] hideDuringTitle; // 照準・泊数など
    [SerializeField] Transform cameraRoot;

    static readonly Color Normal = new(0.62f, 0.6f, 0.56f);
    static readonly Color Selected = new(0.95f, 0.92f, 0.85f);
    static readonly Color Blood = new(0.75f, 0.08f, 0.06f);

    public static bool IsOpen { get; private set; }

    // 「ドメインリロードなし」の設定でも、再生のたびにリセットされるように
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsOpen = false;

    int index;
    bool closing;
    PlayerController player;
    Quaternion camBase;
    float nextGlitch;

    void Start()
    {
        IsOpen = true;
        player = FindFirstObjectByType<PlayerController>();
        if (player) player.CanControl = false;
        if (cameraRoot) camBase = cameraRoot.localRotation;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        titleText.text = title;
        subtitleText.text = subtitle;
        if (howToPanel) howToPanel.SetActive(false);
        foreach (var go in hideDuringTitle) if (go) go.SetActive(false);
        group.alpha = 1f;
        group.gameObject.SetActive(true);
        nextGlitch = Time.time + Random.Range(2f, 5f);
        Refresh();
    }

    void Update()
    {
        if (!IsOpen || closing) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // 背景の部屋をゆっくり見回す（ぼんやり目覚めた感じ）
        if (cameraRoot)
            cameraRoot.localRotation = camBase * Quaternion.Euler(Mathf.Sin(Time.time * 0.21f) * 2f, Mathf.Sin(Time.time * 0.13f) * 6f, 0f);

        Glitch();

        // 遊び方を表示中は、何か押したら閉じる
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
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Decide();
        }

        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            for (int i = 0; i < menuItems.Length; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(menuItems[i].rectTransform, mp, null)) continue;
                if (i != index) { index = i; Refresh(); }
                if (mouse.leftButton.wasPressedThisFrame) Decide();
            }
        }
    }

    void Move(int d)
    {
        index = (index + d + menuItems.Length) % menuItems.Length;
        Refresh();
        ScareFX.Instance.Play2D(ScareFX.Instance.Footstep, 0.25f);
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
            case 0: StartCoroutine(BeginGame()); break;
            case 1: if (howToPanel) howToPanel.SetActive(true); break;
            case 2: Quit(); break;
        }
    }

    IEnumerator BeginGame()
    {
        closing = true;
        var fx = ScareFX.Instance;
        fx.Play2D(fx.Bang, 0.7f);

        // タイトル文字が赤く染まってから消える
        titleText.color = Blood;
        yield return new WaitForSeconds(0.35f);

        var fader = LoopManager.Instance ? LoopManager.Instance.Fader : null;
        if (fader) yield return fader.Fade(1f, 0.8f);
        group.alpha = 0f;
        group.gameObject.SetActive(false);
        foreach (var go in hideDuringTitle) if (go) go.SetActive(true);

        if (cameraRoot) cameraRoot.localRotation = camBase;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return new WaitForSeconds(0.6f);

        if (fader) yield return fader.Fade(0f, 1.5f);
        IsOpen = false;
        if (player) player.CanControl = true;
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
        titleText.color = Selected;
        titleText.text = title;
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
