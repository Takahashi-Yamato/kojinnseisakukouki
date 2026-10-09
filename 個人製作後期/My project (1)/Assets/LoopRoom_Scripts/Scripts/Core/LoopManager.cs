using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ゲーム全体の進行役。
/// ・ループごとに「異変あり／なし」を決め、異変を1つ有効化する
/// ・プレイヤーの選択（寝る＝異変あり／ドア＝異変なし）を判定する
/// ・正解が規定回数続いたらクリア、間違えたら0からやり直し
/// </summary>
public class LoopManager : MonoBehaviour
{
    public static LoopManager Instance { get; private set; }

    [Header("参照")]
    [SerializeField] PlayerController player;
    [SerializeField] Transform spawnPoint;
    [SerializeField] ScreenFader fader;

    [Header("ルール")]
    [Tooltip("クリアに必要な連続正解数")]
    [SerializeField] int streakToClear = 8;
    [Tooltip("異変が起きる確率")]
    [SerializeField, Range(0f, 1f)] float anomalyChance = 0.6f;
    [Tooltip("直近何回分の異変を再抽選から除外するか")]
    [SerializeField] int recentExclusion = 3;

    [Header("テスト用")]
    [Tooltip("ここに異変を入れると、毎ループ必ずその異変が起きる（最初の1周目から）。確認が終わったら空に戻す")]
    [SerializeField] Anomaly debugForceAnomaly;

    [Header("演出")]
    [SerializeField] float fadeOutTime = 1.2f;
    [SerializeField] float blackHoldTime = 0.8f;
    [SerializeField] float fadeInTime = 1.5f;
    [Tooltip("間違えたとき、暗転の前に「何か」が襲ってくる")]
    [SerializeField] bool scareOnWrong = true;
    [Tooltip("間違えたあと、暗転中に鳴らす音")]
    [SerializeField] AudioClip wrongSound;

    [Header("イベント（UIや音の接続用）")]
    public UnityEvent<int> onStreakChanged;  // 現在の連続正解数
    public UnityEvent onWrongAnswer;
    public UnityEvent onGameCleared;

    public int Streak { get; private set; }
    public int TotalLoops { get; private set; }
    public Anomaly CurrentAnomaly { get; private set; }
    public bool IsTransitioning { get; private set; }
    public ScreenFader Fader => fader;

    readonly List<Anomaly> anomalies = new();
    readonly Queue<Anomaly> recent = new();

    void Awake()
    {
        Instance = this;

        // 参照が空なら自動で探す
        if (!player) player = FindFirstObjectByType<PlayerController>();
        if (!spawnPoint) { var sp = GameObject.Find("SpawnPoint"); if (sp) spawnPoint = sp.transform; }
        if (!spawnPoint && player) spawnPoint = player.transform;
        if (!fader) fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        LightingFix.Apply(); // タイトルから来たときに真っ暗にならないように
        // シーン内の異変を自動収集（非アクティブなオブジェクトも含む）
        anomalies.AddRange(FindObjectsByType<Anomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        Debug.Log($"[Loop] 異変を {anomalies.Count} 個検出");
    }

    void Start()
    {
        // 最初の1周は必ず"普通"の部屋を見せる
        if (fader) fader.SetAlpha(1f);
        try { BeginLoop(forceNormal: true); }
        catch (System.Exception e) { Debug.LogException(e); } // 何かあっても画面は明るくする
        if (fader) StartCoroutine(fader.Fade(0f, fadeInTime));
        if (!player) Debug.LogError("[Loop] LoopManager に Player が設定されていません");
        if (!spawnPoint) Debug.LogError("[Loop] LoopManager に Spawn Point が設定されていません");
    }

    /// <summary>
    /// プレイヤーの選択を受け取る。
    /// sayAnomaly = true … 「異変がある」と答えた（ベッドで寝る）
    /// sayAnomaly = false … 「異変はない」と答えた（ドアから出る）
    /// </summary>
    public void SubmitChoice(bool sayAnomaly)
    {
        if (IsTransitioning) return;
        bool correct = sayAnomaly == (CurrentAnomaly != null);
        StartCoroutine(Transition(correct));
    }

    IEnumerator Transition(bool correct)
    {
        IsTransitioning = true;
        player.CanControl = false;
        bool cleared = false;

        try
        {
            // 進行中のびっくり演出は止める
            if (CurrentAnomaly is JumpScareAnomaly) Safe(() => CurrentAnomaly.Deactivate());

            // 間違えたら、暗転の前に「何か」が来る
            if (!correct && scareOnWrong) yield return ScareFX.Instance.PenaltyScare();

            if (fader) yield return fader.Fade(1f, fadeOutTime);

            if (correct)
            {
                Streak++;
                Debug.Log($"[Loop] 正解！ 連続 {Streak}/{streakToClear}");
            }
            else
            {
                Debug.Log($"[Loop] 不正解… 異変は「{(CurrentAnomaly ? CurrentAnomaly.anomalyName : "なし")}」でした");
                Streak = 0;
                Safe(() => onWrongAnswer?.Invoke());
                if (wrongSound) ScareFX.Instance.Play2D(wrongSound, 0.9f);
            }
            Safe(() => onStreakChanged?.Invoke(Streak));

            if (Streak >= streakToClear)
            {
                Debug.Log("[Loop] クリア！");
                cleared = true;
                Cleared = true;
                Safe(() => onGameCleared?.Invoke());
                yield break; // クリア演出はイベント側で
            }

            yield return new WaitForSeconds(blackHoldTime);

            Safe(() => BeginLoop(forceNormal: !correct)); // 間違えた直後は"普通"を見せ直す
            Safe(() => player.Teleport(spawnPoint.position, spawnPoint.rotation));

            if (fader) yield return fader.Fade(0f, fadeInTime);
        }
        finally
        {
            // 途中で何があっても、必ず操作できる状態に戻す
            if (!cleared)
            {
                if (fader) fader.SetAlpha(0f);
                player.CanControl = true;
            }
            IsTransitioning = false;
        }
    }

    /// <summary>エラーが出ても止まらずに先へ進む（エラー内容は Console に出す）。</summary>
    static void Safe(System.Action a)
    {
        try { a(); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    public bool Cleared { get; private set; }

    // 保険：場面転換中でもタイトル中でもクリア後でもないのに操作できなくなっていたら戻す
    float stuckTimer;
    void Update()
    {
        if (!player) return;
        bool shouldControl = !IsTransitioning && !Cleared && !TitleScreen.IsOpen;
        if (shouldControl && !player.CanControl)
        {
            stuckTimer += Time.unscaledDeltaTime;
            if (stuckTimer > 1.5f)
            {
                Debug.LogWarning("[Loop] 操作できない状態が続いていたので、操作を戻しました");
                player.CanControl = true;
                stuckTimer = 0f;
            }
        }
        else stuckTimer = 0f;
    }

    void BeginLoop(bool forceNormal)
    {
        TotalLoops++;

        if (CurrentAnomaly) CurrentAnomaly.Deactivate();
        CurrentAnomaly = null;

        if (debugForceAnomaly)
        {
            CurrentAnomaly = debugForceAnomaly;
            CurrentAnomaly.Activate();
        }
        else if (!forceNormal && Random.value < anomalyChance)
        {
            CurrentAnomaly = PickAnomaly();
            if (CurrentAnomaly)
            {
                CurrentAnomaly.Activate();
                recent.Enqueue(CurrentAnomaly);
                while (recent.Count > recentExclusion) recent.Dequeue();
            }
        }

        Debug.Log($"[Loop] 周回 {TotalLoops}：{(CurrentAnomaly ? CurrentAnomaly.anomalyName : "異変なし")}");
    }

    Anomaly PickAnomaly()
    {
        // 出現条件を満たし、最近出ていないものから重み付き抽選
        var pool = new List<Anomaly>();
        float total = 0f;
        foreach (var a in anomalies)
        {
            if (a.minLoop > Streak) continue;
            if (recent.Contains(a)) continue;
            pool.Add(a);
            total += a.weight;
        }
        if (pool.Count == 0) return null;

        float r = Random.value * total;
        foreach (var a in pool)
        {
            r -= a.weight;
            if (r <= 0f) return a;
        }
        return pool[^1];
    }

#if UNITY_EDITOR
    // エディタ上で現在の状態を確認しやすくする
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 20), $"Streak {Streak}/{streakToClear}  Loop {TotalLoops}");
        GUI.Label(new Rect(10, 30, 400, 20), $"Anomaly: {(CurrentAnomaly ? CurrentAnomaly.anomalyName : "-")}");
        if (player)
        {
            var cc = player.GetComponent<CharacterController>();
            GUI.Label(new Rect(10, 50, 700, 20),
                $"操作:{(player.CanControl ? "ON" : "OFF")}  転換中:{IsTransitioning}  タイトル:{TitleScreen.IsOpen}  " +
                $"接地:{(cc && cc.isGrounded)}  CC:{(cc && cc.enabled)}  Time:{Time.timeScale}");
        }
    }
#endif
}
