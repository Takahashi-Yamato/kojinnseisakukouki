using UnityEngine;

/// <summary>
/// プレイヤーが見ていない間にだけ、オブジェクトが少しずつ近づいてくる。
/// 例：人形、マネキン、部屋の隅の影
/// </summary>
public class MoveWhenUnseenAnomaly : Anomaly
{
    [SerializeField] Transform target;          // 動かすオブジェクト
    [SerializeField] Renderer visibilityCheck;  // 見えているか判定に使うRenderer
    [SerializeField] float stepDistance = 0.4f; // 1回の移動量
    [SerializeField] float stepCooldown = 2.5f; // 見えなくなってから動くまでの最短時間
    [SerializeField] float minDistanceToPlayer = 1.2f;

    Transform player;
    Camera cam;
    Vector3 basePos;
    Quaternion baseRot;
    float unseenTimer;

    void Awake()
    {
        if (!target) target = transform;
        if (!visibilityCheck) visibilityCheck = target.GetComponentInChildren<Renderer>();
        basePos = target.position;
        baseRot = target.rotation;
    }

    protected override void OnActivate()
    {
        cam = Camera.main;
        var pc = FindFirstObjectByType<PlayerController>();
        player = pc ? pc.transform : null;
        unseenTimer = 0f;
    }

    protected override void OnDeactivate()
    {
        target.SetPositionAndRotation(basePos, baseRot);
    }

    void Update()
    {
        if (!IsActive || !player || !cam) return;

        if (IsVisible())
        {
            unseenTimer = 0f;
            return;
        }

        unseenTimer += Time.deltaTime;
        if (unseenTimer < stepCooldown) return;
        unseenTimer = 0f;

        Vector3 toPlayer = player.position - target.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude <= minDistanceToPlayer) return;

        target.position += toPlayer.normalized * stepDistance;
        target.rotation = Quaternion.LookRotation(toPlayer.normalized); // 常にこちらを向く
    }

    bool IsVisible()
    {
        // カメラの視錐台に入っていて、かつ間に壁がないか
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        if (!GeometryUtility.TestPlanesAABB(planes, visibilityCheck.bounds)) return false;

        Vector3 from = cam.transform.position;
        Vector3 to = visibilityCheck.bounds.center;
        if (Physics.Linecast(from, to, out var hit))
            return hit.transform == target || hit.transform.IsChildOf(target);
        return true;
    }
}
