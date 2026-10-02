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

    [Header("演出")]
    [SerializeField] float fadeOutTime = 1.2f;
    [SerializeField] float blackHoldTime = 0.8f;
    [SerializeField] float fadeInTime = 1.5f;

    [Header("イベント（UIや音の接続用）")]
    public UnityEvent<int> onStreakChanged;  // 現在の連続正解数
    public UnityEvent onWrongAnswer;
    public UnityEvent onGameCleared;

    public int Streak { get; private set; }
    public int TotalLoops { get; private set; }
    public Anomaly CurrentAnomaly { get; private set; }
    public bool IsTransitioning { get; private set; }

    readonly List<Anomaly> anomalies = new();
    readonly Queue<Anomaly> recent = new();

    void Awake()
    {
        Instance = this;
        // シーン内の異変を自動収集（非アクティブなオブジェクトも含む）
        anomalies.AddRange(FindObjectsByType<Anomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        Debug.Log($"[Loop] 異変を {anomalies.Count} 個検出");
    }

    void Start()
    {
        // 最初の1周は必ず"普通"の部屋を見せる
        BeginLoop(forceNormal: true);
        if (fader) fader.SetAlpha(1f);
        if (fader) StartCoroutine(fader.Fade(0f, fadeInTime));
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
            onWrongAnswer?.Invoke();
        }
        onStreakChanged?.Invoke(Streak);

        if (Streak >= streakToClear)
        {
            Debug.Log("[Loop] クリア！");
            onGameCleared?.Invoke();
            IsTransitioning = false;
            yield break; // クリア演出はイベント側で
        }

        yield return new WaitForSeconds(blackHoldTime);

        BeginLoop(forceNormal: !correct); // 間違えた直後は"普通"を見せ直す
        player.Teleport(spawnPoint.position, spawnPoint.rotation);

        if (fader) yield return fader.Fade(0f, fadeInTime);

        player.CanControl = true;
        IsTransitioning = false;
    }

    void BeginLoop(bool forceNormal)
    {
        TotalLoops++;

        if (CurrentAnomaly) CurrentAnomaly.Deactivate();
        CurrentAnomaly = null;

        if (!forceNormal && Random.value < anomalyChance)
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
    }
#endif
}
