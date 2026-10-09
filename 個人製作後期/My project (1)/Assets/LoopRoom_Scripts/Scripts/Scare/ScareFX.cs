using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// びっくり演出の道具箱。「何か（影の人影）」の出し入れ、画面の揺れ、照明の消灯、効果音、暗転をまとめて扱う。
/// シーンに無ければ自動で作られる。
/// 人影を好きなモデル（モンスターなど）に変えたいときは Figure Prefab に設定する。
/// </summary>
public class ScareFX : MonoBehaviour
{
    static ScareFX instance;
    public static ScareFX Instance
    {
        get
        {
            if (!instance) instance = FindFirstObjectByType<ScareFX>();
            if (!instance) instance = new GameObject("ScareFX").AddComponent<ScareFX>();
            return instance;
        }
    }

    [Header("「何か」の見た目（空なら影の人影を自動生成）")]
    [SerializeField] GameObject figurePrefab;

    [Header("音（空なら自動生成の音を使う）")]
    [SerializeField] AudioClip stinger;
    [SerializeField] AudioClip scream;
    [SerializeField] AudioClip bang;
    [SerializeField] AudioClip footstep;
    [Tooltip("足音のバリエーション（入っていれば毎歩ランダムに選ぶ）")]
    [SerializeField] AudioClip[] footstepVariations;
    [Tooltip("緊張が高まっていく音（フェイント演出で使用）")]
    [SerializeField] AudioClip tension;
    [SerializeField] AudioClip heartbeat;
    [SerializeField] AudioClip whisper;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    public AudioClip Stinger => stinger ? stinger : ScareSounds.Stinger;
    public AudioClip Scream => scream ? scream : ScareSounds.Scream;
    public AudioClip Bang => bang ? bang : ScareSounds.Bang;
    public AudioClip Footstep => footstepVariations != null && footstepVariations.Length > 0
        ? footstepVariations[Random.Range(0, footstepVariations.Length)]
        : footstep ? footstep : ScareSounds.Footstep;
    public AudioClip Tension => tension;
    public AudioClip Heartbeat => heartbeat ? heartbeat : ScareSounds.Heartbeat;
    public AudioClip Whisper => whisper ? whisper : ScareSounds.Whisper;

    AudioSource source2D;
    GameObject figure;
    Camera cam;
    Vector3 camBasePos;
    Quaternion camBaseRot;
    float baseFov, shakeTime, shakeMag, fovPunch;
    readonly Dictionary<Light, bool> savedLights = new();
    bool lightsOff;

    void Awake()
    {
        instance = this;
        source2D = gameObject.AddComponent<AudioSource>();
        source2D.playOnAwake = false;
        source2D.spatialBlend = 0f;
    }

    // ------------------------------------------------------------------
    // カメラ
    // ------------------------------------------------------------------
    public Camera Cam
    {
        get
        {
            if (!cam)
            {
                cam = Camera.main;
                if (cam)
                {
                    camBasePos = cam.transform.localPosition;
                    camBaseRot = cam.transform.localRotation;
                    baseFov = cam.fieldOfView;
                }
            }
            return cam;
        }
    }

    public Transform CamT => Cam ? cam.transform : null;

    /// <summary>画面を揺らす。</summary>
    public void Shake(float duration, float magnitude)
    {
        shakeTime = Mathf.Max(shakeTime, duration);
        shakeMag = Mathf.Max(shakeMag, magnitude);
    }

    /// <summary>一瞬ズームして「迫ってくる」感じを出す。</summary>
    public void FovPunch(float degrees) => fovPunch = Mathf.Max(fovPunch, degrees);

    void LateUpdate()
    {
        if (!Cam) return;
        var t = cam.transform;

        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            float m = shakeMag * Mathf.Clamp01(shakeTime * 4f);
            t.localPosition = camBasePos + Random.insideUnitSphere * m;
            t.localRotation = camBaseRot * Quaternion.Euler(Random.Range(-1f, 1f) * m * 50f, Random.Range(-1f, 1f) * m * 50f, Random.Range(-1f, 1f) * m * 30f);
            if (shakeTime <= 0f)
            {
                shakeMag = 0f;
                t.localPosition = camBasePos;
                t.localRotation = camBaseRot;
            }
        }

        if (fovPunch > 0.05f)
        {
            cam.fieldOfView = baseFov - fovPunch;
            fovPunch = Mathf.Lerp(fovPunch, 0f, Time.deltaTime * 5f);
        }
        else if (fovPunch != 0f)
        {
            fovPunch = 0f;
            cam.fieldOfView = baseFov;
        }
    }

    /// <summary>その点がカメラの正面あたりに見えていて、間に壁がないか。</summary>
    public bool IsLookingAt(Vector3 point, float maxAngle)
    {
        if (!Cam) return false;
        var t = cam.transform;
        Vector3 to = point - t.position;
        if (Vector3.Angle(t.forward, to) > maxAngle) return false;
        if (Physics.Linecast(t.position, point, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.distance > to.magnitude - 0.4f;
        return true;
    }

    // ------------------------------------------------------------------
    // 音
    // ------------------------------------------------------------------
    public void Play2D(AudioClip clip, float vol = 1f)
    {
        if (clip) source2D.PlayOneShot(clip, vol * volume);
    }

    public AudioSource PlayAt(AudioClip clip, Vector3 pos, float vol = 1f, bool loop = false)
    {
        if (!clip) return null;
        var go = new GameObject("ScareSound_" + clip.name);
        go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1f;
        s.maxDistance = 18f;
        s.volume = vol * volume;
        s.Play();
        if (!loop) Destroy(go, clip.length + 0.2f);
        return s;
    }

    public AudioSource PlayLoop2D(AudioClip clip, float vol = 1f)
    {
        var s = PlayAt(clip, Vector3.zero, vol, true);
        if (s) s.spatialBlend = 0f;
        return s;
    }

    // ------------------------------------------------------------------
    // 照明
    // ------------------------------------------------------------------
    public void SetLightsOff(bool off)
    {
        if (off)
        {
            if (lightsOff) return;
            lightsOff = true;
            savedLights.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) continue;
                if (figure && l.transform.IsChildOf(figure.transform)) continue; // 目の光は残す
                savedLights[l] = l.enabled;
                l.enabled = false;
            }
        }
        else
        {
            if (!lightsOff) return;
            lightsOff = false;
            foreach (var kv in savedLights) if (kv.Key) kv.Key.enabled = kv.Value;
            savedLights.Clear();
        }
    }

    /// <summary>照明をバチバチと不規則に点滅させる。</summary>
    public IEnumerator Flicker(float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end)
        {
            SetLightsOff(true);
            yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
            SetLightsOff(false);
            yield return new WaitForSeconds(Random.Range(0.04f, 0.18f));
        }
        SetLightsOff(false);
    }

    /// <summary>画面を一瞬で真っ暗にして、少ししたら戻す。</summary>
    public IEnumerator CutToBlack(float hold, bool fadeBack = true)
    {
        var fader = LoopManager.Instance ? LoopManager.Instance.Fader : null;
        if (fader) fader.SetAlpha(1f);
        yield return new WaitForSeconds(hold);
        if (fader && fadeBack) yield return fader.Fade(0f, 0.25f);
    }

    // ------------------------------------------------------------------
    // 「何か」
    // ------------------------------------------------------------------
    public GameObject Figure
    {
        get
        {
            if (!figure)
            {
                figure = figurePrefab ? Instantiate(figurePrefab) : BuildShadowFigure();
                foreach (var c in figure.GetComponentsInChildren<Collider>()) Destroy(c); // プレイヤーの邪魔をしない
                figure.SetActive(false);
            }
            return figure;
        }
    }

    public void ShowFigure(Vector3 feetPos, Vector3 lookAt)
    {
        var f = Figure;
        f.transform.position = feetPos;
        Vector3 d = lookAt - feetPos; d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) f.transform.rotation = Quaternion.LookRotation(d);
        f.SetActive(true);
    }

    public void HideFigure()
    {
        if (figure) figure.SetActive(false);
    }

    /// <summary>間違えたときの罰：真っ暗→目の前に「何か」→叫び声→暗転。</summary>
    public IEnumerator PenaltyScare()
    {
        var t = CamT;
        if (!t) yield break;

        SetLightsOff(true);
        Play2D(Heartbeat, 0.8f);
        yield return new WaitForSeconds(0.6f);

        Vector3 fwd = Flat(t.forward);
        Vector3 feet = t.position + fwd * 0.55f;
        feet.y = t.position.y - 2.0f; // 顔の高さを目線に合わせる
        ShowFigure(feet, t.position);
        SetLightsOff(false);

        Play2D(Scream, 1f);
        Play2D(Stinger, 0.8f);
        Shake(0.7f, 0.06f);
        FovPunch(18f);
        yield return new WaitForSeconds(0.55f);

        var fader = LoopManager.Instance ? LoopManager.Instance.Fader : null;
        if (fader) fader.SetAlpha(1f);
        HideFigure();
    }

    public static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // 素材がなくても使える、背の高い真っ黒な人影（赤く光る目つき）
    GameObject BuildShadowFigure()
    {
        var root = new GameObject("ShadowFigure");
        var black = MakeMat(new Color(0.008f, 0.008f, 0.01f));
        var eye = MakeMat(new Color(1f, 0.12f, 0.05f));

        Part(PrimitiveType.Capsule, root.transform, new Vector3(0, 1.0f, 0), new Vector3(0.42f, 0.95f, 0.28f), black, Quaternion.identity);
        Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 2.0f, 0.03f), new Vector3(0.28f, 0.36f, 0.3f), black, Quaternion.identity);
        Part(PrimitiveType.Capsule, root.transform, new Vector3(-0.3f, 1.05f, 0.08f), new Vector3(0.09f, 0.62f, 0.09f), black, Quaternion.Euler(10, 0, 6));
        Part(PrimitiveType.Capsule, root.transform, new Vector3(0.3f, 1.05f, 0.08f), new Vector3(0.09f, 0.62f, 0.09f), black, Quaternion.Euler(10, 0, -6));
        Part(PrimitiveType.Sphere, root.transform, new Vector3(-0.065f, 2.03f, 0.16f), new Vector3(0.05f, 0.03f, 0.03f), eye, Quaternion.identity);
        Part(PrimitiveType.Sphere, root.transform, new Vector3(0.065f, 2.03f, 0.16f), new Vector3(0.05f, 0.03f, 0.03f), eye, Quaternion.identity);

        var glow = new GameObject("EyeGlow").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0, 2.03f, 0.45f);
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.15f, 0.08f);
        glow.range = 1.6f;
        glow.intensity = 2.5f;
        return root;
    }

    static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material MakeMat(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        return m;
    }
}
