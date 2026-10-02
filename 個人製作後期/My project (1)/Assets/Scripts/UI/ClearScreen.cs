using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// クリア時に表示するエンディング画面。Rキーで最初から。
/// </summary>
public class ClearScreen : MonoBehaviour
{
    [SerializeField] GameObject panel;

    bool shown;

    void Start()
    {
        if (panel) panel.SetActive(false);
        if (LoopManager.Instance) LoopManager.Instance.onGameCleared.AddListener(Show);
    }

    void Show()
    {
        shown = true;
        if (panel) panel.SetActive(true);
        var player = FindFirstObjectByType<PlayerController>();
        if (player) player.CanControl = false;
    }

    void Update()
    {
        if (!shown) return;
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
