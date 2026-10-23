using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC で開く一時停止メニュー（設定画面）。
///  再開する / マウス感度 / 音量 / タイトルへ戻る / ゲームを終了
/// ・W/S か ↑↓ で選択、Enter / Space / クリックで決定
/// ・感度と音量は A/D か ←→（またはクリックした位置の左右）で調整。設定は保存される
/// ・開いている間はゲームの時間と音が止まる
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] string titleSceneName = "Title";

    [Header("参照（自動設定）")]
    [SerializeField] GameObject panel;
    [SerializeField] Text[] items;   // 0:再開 1:感度 2:音量 3:タイトルへ 4:終了

    const string KeySens = "LoopRoom.MouseSensitivity";
    const string KeyVol = "LoopRoom.Volume";

    public static bool IsPaused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsPaused = false;

    static readonly Color Normal = new(0.62f, 0.6f, 0.56f);
    static readonly Color Selected = new(0.95f, 0.92f, 0.85f);
    static readonly Color Warn = new(0.85f, 0.25f, 0.2f);

    int index;
    bool confirmTitle;
    bool prevControl;
    CursorLockMode prevLock;
    PlayerController player;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        if (panel) panel.SetActive(false);
        IsPaused = false;

        // 保存してある設定を反映
        if (player && PlayerPrefs.HasKey(KeySens)) player.MouseSensitivity = PlayerPrefs.GetFloat(KeySens);
        AudioListener.volume = PlayerPrefs.GetFloat(KeyVol, 1f);
    }

    void OnDestroy()
    {
        if (IsPaused) { Time.timeScale = 1f; AudioListener.pause = false; IsPaused = false; }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else if (CanPause()) Open();
            return;
        }
        if (!IsPaused) return;

        var mouse = Mouse.current;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Move(-1);
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Move(1);
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Adjust(-1);
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Adjust(1);
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Decide();

        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            for (int i = 0; i < items.Length; i++)
            {
                var rt = items[i].rectTransform;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null)) continue;
                if (i != index) { index = i; confirmTitle = false; Refresh(); }
                if (!mouse.leftButton.wasPressedThisFrame) continue;

                if (i == 1 || i == 2)
                {
                    // 項目の左半分クリックで下げる、右半分で上げる
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mp, null, out var local);
                    Adjust(local.x < 0 ? -1 : 1);
                }
                else Decide();
            }
        }
    }

    bool CanPause()
    {
        if (TitleScreen.IsOpen) return false;
        var lm = LoopManager.Instance;
        if (lm && (lm.Cleared || lm.IsTransitioning)) return false;
        return true;
    }

    void Open()
    {
        IsPaused = true;
        index = 0;
        confirmTitle = false;
        if (player) { prevControl = player.CanControl; player.CanControl = false; }
        prevLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        panel.SetActive(true);
        Refresh();
    }

    void Resume()
    {
        IsPaused = false;
        panel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = prevLock == CursorLockMode.None ? CursorLockMode.Locked : prevLock;
        Cursor.visible = false;
        if (player) player.CanControl = prevControl;
        PlayerPrefs.Save();
    }

    void Move(int d)
    {
        index = (index + d + items.Length) % items.Length;
        confirmTitle = false;
        Refresh();
    }

    void Adjust(int d)
    {
        if (index == 1 && player)
        {
            player.MouseSensitivity = Mathf.Clamp(Mathf.Round((player.MouseSensitivity + d * 0.01f) * 100f) / 100f, 0.01f, 0.5f);
            PlayerPrefs.SetFloat(KeySens, player.MouseSensitivity);
        }
        else if (index == 2)
        {
            AudioListener.volume = Mathf.Clamp01(Mathf.Round((AudioListener.volume + d * 0.1f) * 10f) / 10f);
            PlayerPrefs.SetFloat(KeyVol, AudioListener.volume);
        }
        Refresh();
    }

    void Decide()
    {
        switch (index)
        {
            case 0: Resume(); break;
            case 3:
                if (!confirmTitle) { confirmTitle = true; Refresh(); }
                else GoToTitle();
                break;
            case 4: Quit(); break;
        }
    }

    void Refresh()
    {
        string sens = player ? player.MouseSensitivity.ToString("0.00") : "-";
        string vol = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
        string[] labels =
        {
            "再開する",
            $"マウス感度　◀ {sens} ▶",
            $"音量　◀ {vol} ▶",
            confirmTitle ? "本当に戻る？（もう一度押す）" : "タイトルへ戻る",
            "ゲームを終了",
        };
        for (int i = 0; i < items.Length && i < labels.Length; i++)
        {
            items[i].text = i == index ? $"▶  {labels[i]}  ◀" : labels[i];
            items[i].color = i == 3 && confirmTitle ? Warn : i == index ? Selected : Normal;
        }
    }

    void GoToTitle()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
        if (Application.CanStreamedLevelBeLoaded(titleSceneName)) SceneManager.LoadScene(titleSceneName);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
