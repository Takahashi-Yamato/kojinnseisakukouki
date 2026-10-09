using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// カメラ正面にRayを飛ばし、IInteractableを見ていればヒントを出し、Eキー／左クリックで調べる。
/// カメラに付ける。
/// ※日本語がそのまま表示できるよう、TextMeshProではなく標準のUI Textを使用。
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float reach = 2.2f;
    [SerializeField] LayerMask mask = ~0;
    [SerializeField] Text promptText;   // 画面中央下あたりのテキスト（なくても動く）

    IInteractable current;

    void Update()
    {
        current = null;

        bool blocked = (LoopManager.Instance && LoopManager.Instance.IsTransitioning) || TitleScreen.IsOpen;
        if (!blocked &&
            Physics.Raycast(transform.position, transform.forward, out var hit, reach, mask, QueryTriggerInteraction.Collide))
        {
            current = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (promptText) promptText.text = current != null ? $"[E] {current.Prompt}" : "";

        if (current == null) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && kb.eKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame))
        {
            current.Interact();
        }
    }
}
