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
        if (!cameraRoot)
        {
            var cam = GetComponentInChildren<Camera>(true);
            cameraRoot = cam ? (cam.transform.parent != transform && cam.transform.parent ? cam.transform.parent : cam.transform) : transform;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (!cc.enabled) cc.enabled = true; // 念のため（無効のままだと一切動けない）
        if (!CanControl) { ApplyGravityOnly(); return; }

        // 視点（マウスのdeltaはフレーム単位の値なのでdeltaTimeは掛けない）
        Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up * look.x);
        pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
        cameraRoot.localEulerAngles = new Vector3(pitch, 0f, 0f);

        // 移動（WASD と 矢印キーのどちらでも）
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;

        Vector3 move = (transform.right * input.x + transform.forward * input.y).normalized * walkSpeed;
        move.y = UpdateVerticalVelocity();
        Vector3 before = transform.position;
        lastFlags = cc.Move(move * Time.deltaTime);
        lastInput = input;

        // 診断：キーは押しているのに進めていないなら、引っかかっている相手を調べる
        Vector3 moved = transform.position - before; moved.y = 0f;
        lastSpeed = Time.deltaTime > 0f ? moved.magnitude / Time.deltaTime : 0f;
        if (input != Vector2.zero && lastSpeed < walkSpeed * 0.1f)
        {
            stuckTime += Time.deltaTime;
            if (stuckTime > 0.6f && !reported) ReportBlockers();
        }
        else stuckTime = 0f;
    }

    // ---------------- 診断用 ----------------
    Vector2 lastInput;
    float lastSpeed, stuckTime;
    CollisionFlags lastFlags;
    bool reported;

    void ReportBlockers()
    {
        reported = true;
        float r = cc.radius;
        Vector3 c = transform.TransformPoint(cc.center);
        float half = Mathf.Max(0f, cc.height * 0.5f - r);
        Vector3 p1 = c + Vector3.up * half, p2 = c - Vector3.up * (half - 0.05f);
        var hits = Physics.OverlapCapsule(p1, p2, r + 0.02f, ~0, QueryTriggerInteraction.Ignore);
        var names = new System.Collections.Generic.List<string>();
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            names.Add(Path(h.transform) + $"（{h.GetType().Name}）");
        }
        if (names.Count > 0)
            Debug.LogWarning("[Player] 動けません。プレイヤーの体が次の物にめり込んでいます：\n・" + string.Join("\n・", names) +
                             "\n→ その物を選んで当たり判定（Collider）をオフにするか、スポーン地点を空いている場所に置き直してください。");
        else
            Debug.LogWarning($"[Player] キーは押されていますが進めていません（ぶつかり方：{lastFlags}）。周りに見えない壁がないか確認してください。");
    }

    static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 40) };
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, Screen.height - style.fontSize * 2.2f, Screen.width, style.fontSize * 2f),
            $"入力 {lastInput}   速さ {lastSpeed:0.00} m/s   ぶつかり {lastFlags}   接地 {cc.isGrounded}", style);
    }
#endif

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
        reported = false;
    }
}
