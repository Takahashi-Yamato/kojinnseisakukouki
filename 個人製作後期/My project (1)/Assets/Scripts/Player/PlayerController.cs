using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// シンプルなFPS移動。WASDで歩き、マウスで視点操作。
/// ホラー向けに走り・ジャンプはあえて無し。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] Transform cameraRoot;          // 子にあるカメラ（またはその親）
    [SerializeField] float walkSpeed = 2.0f;
    [SerializeField] float mouseSensitivity = 0.08f;
    [SerializeField] float gravity = -9.81f;
    [SerializeField] float pitchLimit = 80f;

    public bool CanControl { get; set; } = true;

    CharacterController cc;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (!CanControl) { ApplyGravityOnly(); return; }

        // 視点（マウスのdeltaはフレーム単位の値なのでdeltaTimeは掛けない）
        Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up * look.x);
        pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
        cameraRoot.localEulerAngles = new Vector3(pitch, 0f, 0f);

        // 移動
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed) input.x -= 1;

        Vector3 move = (transform.right * input.x + transform.forward * input.y).normalized * walkSpeed;
        move.y = UpdateVerticalVelocity();
        cc.Move(move * Time.deltaTime);
    }

    void ApplyGravityOnly()
    {
        cc.Move(new Vector3(0f, UpdateVerticalVelocity(), 0f) * Time.deltaTime);
    }

    float UpdateVerticalVelocity()
    {
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        return verticalVelocity;
    }

    /// <summary>ループ時にスポーン地点へ戻す。</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        cc.enabled = false; // CharacterControllerが有効だと位置の直接変更が上書きされる
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        pitch = 0f;
        cameraRoot.localEulerAngles = Vector3.zero;
        verticalVelocity = 0f;
        cc.enabled = true;
    }
}
