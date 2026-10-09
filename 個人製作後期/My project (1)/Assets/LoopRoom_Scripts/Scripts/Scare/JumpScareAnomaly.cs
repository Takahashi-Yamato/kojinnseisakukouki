using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 「急に何かが来る」びっくり系の異変。
/// これが起きたループは"異変あり"なので、正解はベッドで眠ること。
///
///  Rush      … 照明がちらついた直後、正面の暗がりから「何か」が猛スピードで突っ込んでくる
///  BehindYou … 背後でささやき声。振り向くと、すぐ後ろに立っている
///  DoorBang  … 廊下から足音が近づき、ドアの前で止まる。静寂のあと、ドアを激しく叩かれる
///  Blackout  … 突然停電。鼓動の中、暗闇に赤い目が浮かび、近づいてくる。明かりが戻ると目の前に
/// </summary>
public class JumpScareAnomaly : Anomaly
{
    public enum Kind { Rush, BehindYou, DoorBang, Blackout, FakeOut }

    [SerializeField] Kind kind = Kind.Rush;
    [Tooltip("ループ開始から何秒後に起きるか（ランダム）")]
    [SerializeField] Vector2 delayRange = new Vector2(5f, 12f);

    Coroutine routine;
    AudioSource loopSound;

    protected override void OnActivate()
    {
        routine = StartCoroutine(Run());
    }

    protected override void OnDeactivate()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (loopSound) Destroy(loopSound.gameObject);
        var fx = ScareFX.Instance;
        fx.HideFigure();
        fx.SetLightsOff(false);
        foreach (var a in AmbientAudio.All) a.SetMuted(false);
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(Random.Range(delayRange.x, delayRange.y));
        while (LoopManager.Instance && LoopManager.Instance.IsTransitioning) yield return null;

        switch (kind)
        {
            case Kind.Rush: yield return Rush(); break;
            case Kind.BehindYou: yield return BehindYou(); break;
            case Kind.DoorBang: yield return DoorBang(); break;
            case Kind.Blackout: yield return Blackout(); break;
            case Kind.FakeOut: yield return FakeOut(); break;
        }
        routine = null;
    }

    // ------------------------------------------------------------------
    IEnumerator Rush()
    {
        var fx = ScareFX.Instance;
        var cam = fx.CamT;
        var player = Player();
        if (!cam || !player) yield break;

        // 前ぶれ：ささやき＋ちらつき
        fx.Play2D(fx.Whisper, 0.5f);
        yield return fx.Flicker(0.9f);

        // 正面の奥（壁の手前）に出現
        Vector3 dir = ScareFX.Flat(cam.forward);
        float dist = 7f;
        if (Physics.Raycast(cam.position, dir, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            dist = hit.distance - 0.5f;
        dist = Mathf.Max(dist, 2.2f);
        Vector3 feet = player.position + dir * dist;

        fx.SetLightsOff(true);
        yield return new WaitForSeconds(0.3f);
        fx.ShowFigure(feet, player.position);
        fx.SetLightsOff(false);
        fx.Play2D(fx.Stinger, 1f);
        fx.Shake(0.3f, 0.02f);

        // 一直線に突っ込んでくる
        var fig = fx.Figure.transform;
        const float speed = 11f;
        float timeout = 2f;
        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;
            Vector3 target = player.position; target.y = fig.position.y;
            Vector3 to = target - fig.position;
            if (to.magnitude < 0.7f) break;
            fig.position += to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude);
            fig.rotation = Quaternion.LookRotation(to);
            yield return null;
        }

        fx.Play2D(fx.Scream, 1f);
        fx.Shake(0.6f, 0.07f);
        fx.FovPunch(20f);
        yield return new WaitForSeconds(0.12f);
        fx.HideFigure();
        yield return fx.CutToBlack(0.5f);
    }

    // ------------------------------------------------------------------
    IEnumerator BehindYou()
    {
        var fx = ScareFX.Instance;
        var cam = fx.CamT;
        var player = Player();
        if (!cam || !player) yield break;

        // 背後に壁がない瞬間を待って、すぐ後ろに出現
        Vector3 pos;
        while (true)
        {
            Vector3 back = -ScareFX.Flat(cam.forward);
            float d = 1.2f;
            if (Physics.Raycast(cam.position, back, out var h, 2f, ~0, QueryTriggerInteraction.Ignore))
                d = Mathf.Min(d, h.distance - 0.35f);
            if (d > 0.6f) { pos = player.position + back * d; break; }
            yield return new WaitForSeconds(0.4f);
        }
        fx.ShowFigure(pos, player.position);
        Vector3 head = pos + Vector3.up * 1.9f;
        fx.PlayAt(fx.Whisper, head, 1f);

        // 振り向いて「見る」まで待つ（ときどきささやいてヒントを出す）
        float hint = 0f;
        while (!fx.IsLookingAt(head, 28f))
        {
            hint += Time.deltaTime;
            if (hint > 7f) { fx.PlayAt(fx.Whisper, head, 1f); hint = 0f; }
            yield return null;
        }

        fx.Play2D(fx.Stinger, 1f);
        fx.Shake(0.45f, 0.05f);
        fx.FovPunch(14f);
        yield return new WaitForSeconds(0.7f);
        fx.SetLightsOff(true);
        fx.HideFigure();
        yield return new WaitForSeconds(0.35f);
        fx.SetLightsOff(false);
    }

    // ------------------------------------------------------------------
    IEnumerator DoorBang()
    {
        var fx = ScareFX.Instance;
        var player = Player();
        if (!player) yield break;

        var door = FindObjectsByType<LoopChoice>(FindObjectsSortMode.None)
                   .FirstOrDefault(c => !c.MeansAnomaly);
        Vector3 doorPos = door ? Center(door.gameObject) : player.position + player.forward * 3f;
        Vector3 outward = ScareFX.Flat(doorPos - player.position);

        // 廊下から足音が近づいてくる
        int steps = 9;
        for (int i = 0; i < steps; i++)
        {
            float k = i / (float)(steps - 1);
            Vector3 p = doorPos + outward * Mathf.Lerp(7f, 0.6f, k);
            fx.PlayAt(fx.Footstep, p, Mathf.Lerp(0.4f, 1f, k));
            yield return new WaitForSeconds(Mathf.Lerp(0.75f, 0.5f, k));
        }

        // ドアの前で止まる……
        yield return new WaitForSeconds(Random.Range(2f, 3.5f));

        // ドンドンドン！！
        for (int i = 0; i < 4; i++)
        {
            fx.PlayAt(fx.Bang, doorPos, 1f);
            fx.Shake(0.25f, 0.035f);
            if (i == 0) StartCoroutine(fx.Flicker(1.2f));
            yield return new WaitForSeconds(i < 2 ? 0.28f : 0.18f);
        }
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// フェイント：不穏な音がどんどん高まって……ブツッと切れる。何も起きない。
    /// （「来る、来る…」と身構えさせるだけで十分怖い）
    /// </summary>
    IEnumerator FakeOut()
    {
        var fx = ScareFX.Instance;
        var clip = fx.Tension;
        if (!clip) { yield return Blackout(); yield break; }

        foreach (var a in AmbientAudio.All) a.SetMuted(true);
        loopSound = fx.PlayLoop2D(clip, 1f);
        if (loopSound) loopSound.loop = false;
        yield return new WaitForSeconds(clip.length - 0.05f);

        // ブツッ。一瞬だけ照明が落ちて、静寂
        if (loopSound) Destroy(loopSound.gameObject);
        fx.SetLightsOff(true);
        yield return new WaitForSeconds(0.25f);
        fx.SetLightsOff(false);
        yield return new WaitForSeconds(2.5f);
        foreach (var a in AmbientAudio.All) a.SetMuted(false);
    }

    IEnumerator Blackout()
    {
        var fx = ScareFX.Instance;
        var cam = fx.CamT;
        var player = Player();
        if (!cam || !player) yield break;

        fx.PlayAt(fx.Footstep, cam.position + Vector3.up, 0.6f); // カチッ（スイッチが落ちる音の代わり）
        fx.SetLightsOff(true);
        loopSound = fx.PlayLoop2D(fx.Heartbeat, 0.9f);
        yield return new WaitForSeconds(1.5f);

        // 暗闇の奥に、赤い目だけが浮かぶ
        Vector3 dir = ScareFX.Flat(cam.forward);
        float dist = 4f;
        if (Physics.Raycast(cam.position, dir, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            dist = Mathf.Max(1.5f, hit.distance - 0.4f);
        Vector3 start = player.position + dir * dist;
        fx.ShowFigure(start, player.position);

        // ゆっくり近づいてくる
        var fig = fx.Figure.transform;
        Vector3 end = player.position + dir * 1.1f;
        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            fig.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / 2.5f));
            if (loopSound) loopSound.pitch = Mathf.Lerp(1f, 1.6f, t / 2.5f); // 鼓動が速くなる
            yield return null;
        }

        // 明かりが戻ると目の前に
        fx.SetLightsOff(false);
        fx.Play2D(fx.Stinger, 1f);
        fx.Shake(0.5f, 0.05f);
        fx.FovPunch(15f);
        if (loopSound) Destroy(loopSound.gameObject);
        yield return new WaitForSeconds(0.9f);

        yield return fx.Flicker(0.5f);
        fx.HideFigure();
    }

    // ------------------------------------------------------------------
    static Transform Player()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        return pc ? pc.transform : null;
    }

    static Vector3 Center(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }
}
