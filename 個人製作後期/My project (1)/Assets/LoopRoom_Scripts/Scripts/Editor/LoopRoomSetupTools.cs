using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自分で作った部屋（アセットの部屋など）にゲームの仕組みを組み込むためのメニュー集。
///
/// ★ おまかせ：今のシーンからベッド・ドア・家具を名前で探して、全部自動で組み込む
///
/// 手動で調整したいとき：
///  1. 部屋全体を選択 → 「当たり判定を付ける」
///  2. ドアを選択 → 「選択中をドアにする」／ベッドを選択 → 「選択中をベッドにする」
///  3. シーンビューで目覚める位置を映す → 「スポーン地点をここに置く」
///  4. 「ゲームの仕組みを追加」
///  5. 好きな家具を選択 → 「選択中を異変にする > …」
/// </summary>
public static class LoopRoomSetupTools
{
    const string Menu = "LoopRoom/自分の部屋で作る/";
    const string AMenu = Menu + "5. 選択中を異変にする/";

    // ==================================================================
    // ★ おまかせ
    // ==================================================================
    [MenuItem("LoopRoom/★ 今のシーンをおまかせでゲーム化", false, 0)]
    static void AutoSetup()
    {
        if (Object.FindFirstObjectByType<LoopManager>())
        {
            Done("このシーンはすでにゲーム化されています（[LoopRoom] があります）。\nやり直す場合は Hierarchy の [LoopRoom] を削除してから実行してください。");
            return;
        }
        if (!EditorUtility.DisplayDialog("LoopRoom",
                "今開いているシーンをゲーム化します。\n\n" +
                "・すべての家具に当たり判定を付ける\n" +
                "・ベッドとドアを名前から探して設定\n" +
                "・プレイヤー／UI／ループ管理を追加\n" +
                "・家具から異変を自動で作る\n\n" +
                "元のシーンを残したい場合は、先に File > Save As で別名保存してください。",
                "ゲーム化する", "やめる"))
            return;

        var log = new List<string>();
        var all = SceneTransforms();

        // 1. 当たり判定
        int added = AddCollidersTo(all.Select(t => t.gameObject));
        log.Add($"当たり判定：{added} 個追加");

        // 2. ベッドとドア
        var used = new HashSet<Transform>();
        var bed = FindBed(all);
        var door = FindDoor(all);
        if (bed) { MakeChoice(bed.gameObject, true, "眠る（異変あり）"); used.Add(bed); log.Add($"ベッド：{bed.name}"); }
        else log.Add("⚠ ベッドが見つかりません（手動で「選択中をベッドにする」）");
        if (door) { MakeChoice(door.gameObject, false, "ドアを開ける（異変なし）"); used.Add(door); log.Add($"ドア：{door.name}"); }
        else log.Add("⚠ ドアが見つかりません（手動で「選択中をドアにする」）");

        // 3. スポーン地点（ベッドの横、部屋の中央を向く）
        var roomBounds = RoomBounds(all);
        Vector3 spawnPos = roomBounds.center;
        if (bed)
        {
            var bb = WorldBounds(bed.gameObject);
            Vector3 toCenter = roomBounds.center - bb.center; toCenter.y = 0;
            spawnPos = bb.center + toCenter.normalized * (Mathf.Max(bb.extents.x, bb.extents.z) + 0.6f);
        }
        spawnPos = GroundAt(spawnPos, roomBounds);
        Vector3 look = roomBounds.center - spawnPos; look.y = 0;
        var spawn = CreateOrMoveSpawn(spawnPos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        EnsureFloor(spawn.position, roomBounds);

        // 4. ゲームの仕組み
        AddGameSystemsCore(spawn);

        // 5. 異変
        int n = AutoAnomalies(all, used, bed, door);
        n += AddScaresCore();
        log.Add($"異変：{n} 個作成（びっくり系4種を含む）");

        // 雰囲気
        NightMoodCore();

        EditorSceneManager.MarkSceneDirty(spawn.gameObject.scene);
        Debug.Log("[LoopRoom] おまかせ完了\n" + string.Join("\n", log));
        Done("ゲーム化しました！\n\n" + string.Join("\n", log) +
             "\n\n▶ 再生して遊んでみてください。\n（Ctrl+S でシーンを保存するのを忘れずに）");
    }

    static Transform FindBed(List<Transform> all)
    {
        return all.Where(t => Has(t, "bed") && !Has(t, "bedroll") && HasRenderer(t))
                  .Select(PrefabRoot).Distinct()
                  .OrderByDescending(t => WorldBounds(t.gameObject).size.sqrMagnitude)
                  .FirstOrDefault();
    }

    static Transform FindDoor(List<Transform> all)
    {
        // まずは「door」を含み「wall」を含まない、実際の扉パーツを探す
        var leaf = all.Where(t => Has(t, "door") && !Has(t, "wall") && !Has(t, "frame") && HasRenderer(t))
                      .OrderByDescending(t => WorldBounds(t.gameObject).size.y)
                      .FirstOrDefault();
        if (leaf) return leaf;
        // なければ「ドア付きの壁」ごと
        return all.Where(t => Has(t, "door") && HasRenderer(t)).Select(PrefabRoot).FirstOrDefault();
    }

    /// <summary>家具の名前から、それっぽい異変を自動で作る。</summary>
    static int AutoAnomalies(List<Transform> all, HashSet<Transform> used, Transform bed, Transform door)
    {
        int count = 0;
        var roots = all.Where(HasRenderer).Select(PrefabRoot).Distinct()
                       .Where(t => !IsInside(t, bed) && !IsInside(t, door) && !t.name.StartsWith("[LoopRoom]"))
                       .ToList();

        // ルール：キーワード → 異変の種類（同じキーワードは最大 max 個まで）
        void Rule(string[] keys, int max, System.Func<Transform, bool> make)
        {
            int made = 0;
            foreach (var t in roots)
            {
                if (made >= max) break;
                if (used.Contains(t) || !keys.Any(k => Has(t, k))) continue;
                if (make(t)) { used.Add(t); made++; count++; }
            }
        }

        Rule(new[] { "candle", "lantern", "torch", "fireplace" }, 2,
             t => t.GetComponentInChildren<Light>(true) && MakeAnomaly<CandleOutAnomaly>(t, "の火が消えた", 0,
                 (a, x) => a.FindProperty("target").objectReferenceValue = x) != null);
        Rule(new[] { "chest", "barrel", "crate" }, 1, t => MakeDuplicate(t, 0) != null);
        Rule(new[] { "chest", "barrel", "crate" }, 1, t => Turn(t, 2) != null);
        Rule(new[] { "flag", "banner", "painting", "picture", "shield" }, 1,
             t => MakeAnomaly<TransformAnomaly>(t, "が逆さま", 0, (a, x) =>
             {
                 a.FindProperty("target").objectReferenceValue = x;
                 a.FindProperty("rotationOffset").vector3Value = new Vector3(0, 0, 180);
             }) != null);
        Rule(new[] { "flag", "banner", "painting", "picture" }, 1, t => Vanish(t, 1) != null);
        Rule(new[] { "carpet", "rug" }, 1,
             t => MakeAnomaly<TransformAnomaly>(t, "の位置がずれている", 3, (a, x) =>
             {
                 a.FindProperty("target").objectReferenceValue = x;
                 a.FindProperty("positionOffset").vector3Value = new Vector3(0.5f, 0, 0.3f);
             }) != null);
        Rule(new[] { "crystal", "book", "bottle", "cup", "mug", "jug", "skull", "vase" }, 2, t => Vanish(t, 1) != null);
        Rule(new[] { "chair", "stool" }, 1, t => Turn(t, 2) != null);
        Rule(new[] { "wardrobe", "cabinet", "shelf", "closet", "table" }, 1,
             t => MakeAnomaly<TransformAnomaly>(t, "が大きい", 3, (a, x) =>
             {
                 a.FindProperty("target").objectReferenceValue = x;
                 a.FindProperty("scaleMultiplier").vector3Value = Vector3.one * 1.5f;
             }) != null);
        Rule(new[] { "armor", "armour", "statue", "doll", "mannequin" }, 1,
             t => MakeAnomaly<MoveWhenUnseenAnomaly>(t, "が近づいてくる", 4, (a, x) =>
             {
                 a.FindProperty("target").objectReferenceValue = x;
                 a.FindProperty("visibilityCheck").objectReferenceValue = x.GetComponentInChildren<Renderer>();
             }) != null);

        // 照明（燭台とは別のLightがあれば）
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                           .Where(l => l.type != LightType.Directional && !used.Any(u => l.transform.IsChildOf(u)))
                           .ToList();
        if (lights.Count > 0)
        {
            MakeAnomaly<LightAnomaly>(lights[0].transform, "が赤い", 0, (a, x) =>
            {
                a.FindProperty("targetLight").objectReferenceValue = x.GetComponent<Light>();
                a.FindProperty("changeColor").boolValue = true;
                a.FindProperty("flicker").boolValue = false;
            }, "照明が赤い");
            count++;
        }
        if (lights.Count > 1)
        {
            MakeAnomaly<LightAnomaly>(lights[1].transform, "がちらつく", 1, (a, x) =>
            {
                a.FindProperty("targetLight").objectReferenceValue = x.GetComponent<Light>();
                a.FindProperty("changeColor").boolValue = false;
                a.FindProperty("flicker").boolValue = true;
            }, "照明がちらつく");
            count++;
        }

        // ドアの向こうからノック
        if (door)
        {
            var knock = LoopRoomSceneBuilder.CreateKnockClip();
            var go = new GameObject("異変_ドアの向こうからノック");
            go.transform.SetParent(AnomalyParent());
            var db = WorldBounds(door.gameObject);
            go.transform.position = db.center;
            var src = go.AddComponent<AudioSource>();
            src.maxDistance = 15f;
            var s = go.AddComponent<SoundAnomaly>();
            s.anomalyName = "ドアの向こうからノック";
            s.minLoop = 1;
            Set(s, so =>
            {
                Refs(so, "clips", knock);
                so.FindProperty("minInterval").floatValue = 3f;
                so.FindProperty("maxInterval").floatValue = 7f;
            });
            count++;
        }

        return count;
    }

    static Anomaly Vanish(Transform t, int minLoop) =>
        MakeAnomaly<ObjectSwapAnomaly>(t, "が消えた", minLoop, (a, x) => Refs(a, "normalObjects", x.gameObject));

    static Anomaly Turn(Transform t, int minLoop) =>
        MakeAnomaly<TransformAnomaly>(t, "の向きが逆", minLoop, (a, x) =>
        {
            a.FindProperty("target").objectReferenceValue = x;
            a.FindProperty("rotationOffset").vector3Value = new Vector3(0, 180, 0);
        });

    // ==================================================================
    // 1. 当たり判定
    // ==================================================================
    [MenuItem(Menu + "1. 選択中に当たり判定を付ける（部屋全体を選んで実行）", false, 1)]
    static void AddColliders()
    {
        int count = AddCollidersTo(Selection.gameObjects);
        Done(count > 0
            ? $"{count} 個のオブジェクトに当たり判定（MeshCollider）を付けました。"
            : "新しく付ける必要のあるオブジェクトはありませんでした（すでに当たり判定が付いています）。");
    }

    [MenuItem(Menu + "1. 選択中に当たり判定を付ける（部屋全体を選んで実行）", true)]
    static bool HasSelection() => Selection.gameObjects.Length > 0;

    static int AddCollidersTo(IEnumerable<GameObject> roots)
    {
        int count = 0;
        var done = new HashSet<MeshFilter>();
        foreach (var go in roots)
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!done.Add(mf)) continue;
            if (mf.GetComponent<Collider>() || !mf.sharedMesh || !mf.GetComponent<MeshRenderer>()) continue;
            if (mf.GetComponentInParent<Collider>() is Collider pc && !pc.isTrigger) continue; // 親にすでに判定がある
            Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh;
            count++;
        }
        return count;
    }

    // ==================================================================
    // 2. ドアとベッド
    // ==================================================================
    [MenuItem(Menu + "2a. 選択中をドアにする（異変なし）", false, 20)]
    static void MakeDoor()
    {
        MakeChoice(Selection.activeGameObject, false, "ドアを開ける（異変なし）");
        Done($"「{Selection.activeGameObject.name}」をドアに設定しました。");
    }

    [MenuItem(Menu + "2b. 選択中をベッドにする（異変あり）", false, 21)]
    static void MakeBed()
    {
        MakeChoice(Selection.activeGameObject, true, "眠る（異変あり）");
        Done($"「{Selection.activeGameObject.name}」をベッドに設定しました。");
    }

    [MenuItem(Menu + "2a. 選択中をドアにする（異変なし）", true)]
    [MenuItem(Menu + "2b. 選択中をベッドにする（異変あり）", true)]
    static bool OneSelected() => Selection.activeGameObject != null;

    static void MakeChoice(GameObject go, bool meansAnomaly, string prompt)
    {
        foreach (var old in go.GetComponents<LoopChoice>()) Undo.DestroyObjectImmediate(old);

        // 見た目全体を覆う「調べる用」トリガー
        if (!go.GetComponents<BoxCollider>().Any(c => c.isTrigger))
        {
            var b = WorldBounds(go);
            var box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.lossyScale;
            box.size = new Vector3(b.size.x / Mathf.Abs(s.x), b.size.y / Mathf.Abs(s.y), b.size.z / Mathf.Abs(s.z));
        }

        var choice = Undo.AddComponent<LoopChoice>(go);
        Set(choice, so =>
        {
            so.FindProperty("meansAnomaly").boolValue = meansAnomaly;
            so.FindProperty("prompt").stringValue = prompt;
        });
    }

    // ==================================================================
    // 3. スポーン地点
    // ==================================================================
    [MenuItem(Menu + "3. スポーン地点をここに置く（シーンビューの中心）", false, 40)]
    static void PlaceSpawn()
    {
        var sv = SceneView.lastActiveSceneView;
        if (!sv) { Done("シーンビューを開いてから実行してください。"); return; }

        var fwd = sv.camera.transform.forward; fwd.y = 0;
        var spawn = CreateOrMoveSpawn(GroundAt(sv.pivot, null),
            fwd.sqrMagnitude > 0.01f ? Quaternion.LookRotation(fwd) : Quaternion.identity);
        Selection.activeGameObject = spawn.gameObject;
        Done("スポーン地点を置きました。青い矢印（Z）の向きが目覚めたときの正面です。");
    }

    static Transform CreateOrMoveSpawn(Vector3 pos, Quaternion rot)
    {
        var spawn = GameObject.Find("SpawnPoint");
        if (!spawn) { spawn = new GameObject("SpawnPoint"); Undo.RegisterCreatedObjectUndo(spawn, "SpawnPoint"); }
        Undo.RecordObject(spawn.transform, "Move SpawnPoint");
        spawn.transform.SetPositionAndRotation(pos, rot);

        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc)
        {
            Undo.RecordObject(pc.transform, "Move Player");
            pc.transform.SetPositionAndRotation(pos, rot);
        }
        return spawn.transform;
    }

    static Vector3 GroundAt(Vector3 p, Bounds? room)
    {
        Physics.SyncTransforms();
        float top = room.HasValue ? room.Value.max.y : p.y + 1.5f;
        // 天井に当たらないよう、少し下から何回か探す
        foreach (float startY in new[] { p.y + 1.5f, room.HasValue ? room.Value.center.y : p.y + 0.5f, p.y + 0.3f })
        {
            var from = new Vector3(p.x, Mathf.Min(startY, top - 0.2f), p.z);
            if (Physics.Raycast(from, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.7f)
                return hit.point;
        }
        return room.HasValue ? new Vector3(p.x, room.Value.min.y, p.z) : p;
    }

    /// <summary>足元に床の判定がなければ、見えない床を敷く（落下防止）。</summary>
    static void EnsureFloor(Vector3 spawnPos, Bounds room)
    {
        Physics.SyncTransforms();
        if (Physics.Raycast(spawnPos + Vector3.up * 0.5f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)) return;

        var floor = new GameObject("[LoopRoom] SafetyFloor");
        Undo.RegisterCreatedObjectUndo(floor, "SafetyFloor");
        var box = floor.AddComponent<BoxCollider>();
        floor.transform.position = new Vector3(room.center.x, spawnPos.y - 0.05f, room.center.z);
        box.size = new Vector3(room.size.x + 2, 0.1f, room.size.z + 2);
        Debug.LogWarning("[LoopRoom] 床の当たり判定が見つからなかったので、見えない床を追加しました。");
    }

    // ==================================================================
    // 4. ゲームの仕組み一式
    // ==================================================================
    [MenuItem(Menu + "4. ゲームの仕組みを追加", false, 60)]
    static void AddGameSystems()
    {
        if (Object.FindFirstObjectByType<LoopManager>()) { Done("このシーンにはすでに LoopManager があります。"); return; }
        if (Object.FindObjectsByType<LoopChoice>(FindObjectsSortMode.None).Length < 2)
            Debug.LogWarning("[LoopRoom] ドアとベッドがまだ設定されていません（手順2）。後からでも設定できます。");

        var spawnGo = GameObject.Find("SpawnPoint");
        if (!spawnGo) { PlaceSpawn(); spawnGo = GameObject.Find("SpawnPoint"); }
        AddGameSystemsCore(spawnGo.transform);
        Done("ゲームの仕組みを追加しました。\n\n次は家具を選んで「選択中を異変にする」から異変を作りましょう。");
    }

    static void AddGameSystemsCore(Transform spawn)
    {
        // 既存のカメラはプレイヤーのカメラと競合するので無効化
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(c.gameObject, "Disable Camera");
            c.gameObject.SetActive(false);
        }

        var root = new GameObject("[LoopRoom]");
        Undo.RegisterCreatedObjectUndo(root, "LoopRoom Systems");

        // プレイヤー
        var player = new GameObject("Player");
        player.transform.SetParent(root.transform);
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.7f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.88f, 0);
        var pc = player.AddComponent<PlayerController>();

        var camRoot = new GameObject("CameraRoot").transform;
        camRoot.SetParent(player.transform, false);
        camRoot.localPosition = new Vector3(0, 1.6f, 0);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(camRoot, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 70f;
        camGo.AddComponent<AudioListener>();
        var interactor = camGo.AddComponent<PlayerInteractor>();
        Set(pc, so => so.FindProperty("cameraRoot").objectReferenceValue = camRoot);
        LoopRoomFixTools.AddPlayerGlow(camGo.transform);

        // URPならポストプロセスをON
        var urpData = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpData != null)
        {
            var data = camGo.GetComponent(urpData);
            if (!data) data = camGo.AddComponent(urpData);
            urpData.GetProperty("renderPostProcessing")?.SetValue(data, true);
        }

        // UI
        var font = BuiltinFont();
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var canvas = canvasGo.transform;

        var cross = UIImage("Crosshair", canvas, new Color(1, 1, 1, 0.5f));
        cross.rectTransform.sizeDelta = new Vector2(6, 6);

        var prompt = UIText("Prompt", canvas, "", 30, font);
        prompt.rectTransform.anchoredPosition = new Vector2(0, -90);
        prompt.rectTransform.sizeDelta = new Vector2(900, 60);
        Set(interactor, so => so.FindProperty("promptText").objectReferenceValue = prompt);

        var nights = UIText("Nights", canvas, "1 泊目", 34, font);
        nights.alignment = TextAnchor.UpperLeft;
        nights.rectTransform.anchorMin = nights.rectTransform.anchorMax = new Vector2(0, 1);
        nights.rectTransform.pivot = new Vector2(0, 1);
        nights.rectTransform.anchoredPosition = new Vector2(40, -30);
        nights.rectTransform.sizeDelta = new Vector2(400, 60);
        var counter = nights.gameObject.AddComponent<WallCounter>();
        Set(counter, so =>
        {
            so.FindProperty("uiText").objectReferenceValue = nights;
            so.FindProperty("format").stringValue = "{0} 泊目";
            so.FindProperty("offset").intValue = 1;
        });

        var faderImg = UIImage("Fader", canvas, Color.black);
        Stretch(faderImg.rectTransform);
        faderImg.gameObject.AddComponent<CanvasGroup>().alpha = 0f; // 編集中は開けておく（再生時に自動で暗転→明転）
        var fader = faderImg.gameObject.AddComponent<ScreenFader>();

        var clearPanel = UIImage("ClearPanel", canvas, Color.black);
        Stretch(clearPanel.rectTransform);
        var clearText = UIText("Text", clearPanel.transform,
            "扉を開けると、朝の光が差し込んでいた。\n\nCLEAR\n\n<size=24>R キーでもう一度</size>", 44, font);
        Stretch(clearText.rectTransform);
        var clearScreen = canvasGo.AddComponent<ClearScreen>();
        Set(clearScreen, so => so.FindProperty("panel").objectReferenceValue = clearPanel.gameObject);
        clearPanel.gameObject.SetActive(false);

        // 進行役
        var lmGo = new GameObject("LoopManager");
        lmGo.transform.SetParent(root.transform);
        var lm = lmGo.AddComponent<LoopManager>();
        Set(lm, so =>
        {
            so.FindProperty("player").objectReferenceValue = pc;
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
            so.FindProperty("fader").objectReferenceValue = fader;
        });

        var anomRoot = new GameObject("Anomalies");
        anomRoot.transform.SetParent(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    // ==================================================================
    // 5. 異変を作る（手動）
    // ==================================================================
    [MenuItem(AMenu + "消える", false, 80)]
    static void AnomVanish() => Picked(t => Vanish(t, 0));

    [MenuItem(AMenu + "傾く", false, 81)]
    static void AnomTilt() => Picked(t => MakeAnomaly<TransformAnomaly>(t, "が傾いている", 0, (a, x) =>
    {
        a.FindProperty("target").objectReferenceValue = x;
        a.FindProperty("rotationOffset").vector3Value = new Vector3(0, 0, 25);
    }));

    [MenuItem(AMenu + "向きが変わる（180度）", false, 82)]
    static void AnomTurn() => Picked(t => Turn(t, 0));

    [MenuItem(AMenu + "少しずれる", false, 83)]
    static void AnomShift() => Picked(t => MakeAnomaly<TransformAnomaly>(t, "の位置がずれている", 0, (a, x) =>
    {
        a.FindProperty("target").objectReferenceValue = x;
        a.FindProperty("positionOffset").vector3Value = new Vector3(0.4f, 0, 0);
    }));

    [MenuItem(AMenu + "大きくなる", false, 84)]
    static void AnomBig() => Picked(t => MakeAnomaly<TransformAnomaly>(t, "が大きい", 0, (a, x) =>
    {
        a.FindProperty("target").objectReferenceValue = x;
        a.FindProperty("scaleMultiplier").vector3Value = Vector3.one * 1.5f;
    }));

    [MenuItem(AMenu + "逆さまになる", false, 85)]
    static void AnomUpsideDown() => Picked(t => MakeAnomaly<TransformAnomaly>(t, "が逆さま", 0, (a, x) =>
    {
        a.FindProperty("target").objectReferenceValue = x;
        a.FindProperty("rotationOffset").vector3Value = new Vector3(0, 0, 180);
    }));

    [MenuItem(AMenu + "火が消える（ろうそく・暖炉・ランタン）", false, 100)]
    static void AnomCandle() => Picked(t => MakeAnomaly<CandleOutAnomaly>(t, "の火が消えた", 0,
        (a, x) => a.FindProperty("target").objectReferenceValue = x));

    [MenuItem(AMenu + "見ていない間に近づく（人形・鎧など）", false, 101)]
    static void AnomCreep() => Picked(t => MakeAnomaly<MoveWhenUnseenAnomaly>(t, "が近づいてくる", 4, (a, x) =>
    {
        a.FindProperty("target").objectReferenceValue = x;
        a.FindProperty("visibilityCheck").objectReferenceValue = x.GetComponentInChildren<Renderer>();
    }));

    [MenuItem(AMenu + "コピーが増える（隣にもう1つ）", false, 102)]
    static void AnomDuplicate() => Picked(t => MakeDuplicate(t, 0));

    [MenuItem(AMenu + "照明が赤くなる（Lightを選択）", false, 120)]
    static void AnomRedLight() => Picked(t => !LightCheck(t) ? null : MakeAnomaly<LightAnomaly>(t, "が赤い", 0, (a, x) =>
    {
        a.FindProperty("targetLight").objectReferenceValue = x.GetComponentInChildren<Light>(true);
        a.FindProperty("changeColor").boolValue = true;
        a.FindProperty("flicker").boolValue = false;
    }));

    [MenuItem(AMenu + "照明がちらつく（Lightを選択）", false, 121)]
    static void AnomFlicker() => Picked(t => !LightCheck(t) ? null : MakeAnomaly<LightAnomaly>(t, "がちらつく", 1, (a, x) =>
    {
        a.FindProperty("targetLight").objectReferenceValue = x.GetComponentInChildren<Light>(true);
        a.FindProperty("changeColor").boolValue = false;
        a.FindProperty("flicker").boolValue = true;
    }));

    [MenuItem(AMenu + "消える", true)]
    [MenuItem(AMenu + "傾く", true)]
    [MenuItem(AMenu + "向きが変わる（180度）", true)]
    [MenuItem(AMenu + "少しずれる", true)]
    [MenuItem(AMenu + "大きくなる", true)]
    [MenuItem(AMenu + "逆さまになる", true)]
    [MenuItem(AMenu + "火が消える（ろうそく・暖炉・ランタン）", true)]
    [MenuItem(AMenu + "見ていない間に近づく（人形・鎧など）", true)]
    [MenuItem(AMenu + "コピーが増える（隣にもう1つ）", true)]
    [MenuItem(AMenu + "照明が赤くなる（Lightを選択）", true)]
    [MenuItem(AMenu + "照明がちらつく（Lightを選択）", true)]
    static bool CanMakeAnomaly() => Selection.activeTransform != null;

    static void Picked(System.Func<Transform, Anomaly> make)
    {
        var a = make(Selection.activeTransform);
        if (!a) return;
        Selection.activeGameObject = a.gameObject;
        EditorGUIUtility.PingObject(a.gameObject);
        Debug.Log($"[LoopRoom] 異変「{a.anomalyName}」を作りました。");
    }

    static bool LightCheck(Transform t)
    {
        if (t.GetComponentInChildren<Light>(true)) return true;
        Done($"「{t.name}」には Light が見つかりません。Light の付いたオブジェクトを選んでください。");
        return false;
    }

    static Anomaly MakeDuplicate(Transform src, int minLoop)
    {
        var copy = Object.Instantiate(src.gameObject, src.parent);
        copy.name = src.name + "_異変コピー";
        var b = WorldBounds(src.gameObject);
        copy.transform.position = src.position + src.right * Mathf.Max(b.size.x, 0.5f) * 1.1f;
        copy.SetActive(false);
        Undo.RegisterCreatedObjectUndo(copy, "Duplicate Anomaly");
        return MakeAnomaly<ObjectSwapAnomaly>(src, "が2つある", minLoop, (a, x) => Refs(a, "anomalyObjects", copy));
    }

    static T MakeAnomaly<T>(Transform target, string suffix, int minLoop,
                            System.Action<SerializedObject, Transform> setup, string displayName = null) where T : Anomaly
    {
        string label = displayName ?? (CleanName(target.name) + suffix);
        var go = new GameObject("異変_" + label);
        go.transform.SetParent(AnomalyParent());
        Undo.RegisterCreatedObjectUndo(go, "Create Anomaly");

        var a = go.AddComponent<T>();
        a.anomalyName = label;
        a.minLoop = minLoop;
        Set(a, so => setup(so, target));
        return a;
    }

    static Transform AnomalyParent()
    {
        var p = GameObject.Find("[LoopRoom]/Anomalies");
        return p ? p.transform : null;
    }

    // ==================================================================
    // 6. びっくり要素
    // ==================================================================
    [MenuItem("LoopRoom/びっくり要素を追加（急に何か来る）", false, 1)]
    [MenuItem(Menu + "6. びっくり要素を追加", false, 150)]
    static void AddScares()
    {
        if (!Object.FindFirstObjectByType<LoopManager>())
        {
            Done("先に「おまかせでゲーム化」か「4. ゲームの仕組みを追加」を実行してください。");
            return;
        }
        if (Object.FindObjectsByType<JumpScareAnomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0
            && !EditorUtility.DisplayDialog("LoopRoom", "びっくり要素はすでにあります。もう一組追加しますか？", "追加する", "やめる"))
            return;

        int n = AddScaresCore();
        Done($"びっくり系の異変を {n} 種追加しました。\n\n" +
             "・ドアを激しく叩かれる（1泊目〜）\n" +
             "・振り向くと後ろにいる（2泊目〜）\n" +
             "・停電して暗闇から近づいてくる（3泊目〜）\n" +
             "・正面から突っ込んでくる（4泊目〜）\n" +
             "・間違えると、暗転前に「何か」が襲ってくる\n\n" +
             "異変は [LoopRoom] > Anomalies にあります。");
    }

    static int AddScaresCore()
    {
        if (!Object.FindFirstObjectByType<ScareFX>())
        {
            var fx = new GameObject("ScareFX").AddComponent<ScareFX>();
            var root = GameObject.Find("[LoopRoom]");
            if (root) fx.transform.SetParent(root.transform);
            Undo.RegisterCreatedObjectUndo(fx.gameObject, "ScareFX");
        }

        var list = new (JumpScareAnomaly.Kind kind, string name, int minLoop, Vector2 delay)[]
        {
            (JumpScareAnomaly.Kind.DoorBang,  "ドアを激しく叩かれる",         1, new Vector2(3f, 8f)),
            (JumpScareAnomaly.Kind.BehindYou, "振り向くと後ろにいる",         2, new Vector2(6f, 12f)),
            (JumpScareAnomaly.Kind.Blackout,  "停電して暗闇から近づいてくる", 3, new Vector2(5f, 10f)),
            (JumpScareAnomaly.Kind.Rush,      "正面から突っ込んでくる",       4, new Vector2(6f, 14f)),
        };
        foreach (var s in list)
        {
            var go = new GameObject("びっくり_" + s.name);
            go.transform.SetParent(AnomalyParent());
            Undo.RegisterCreatedObjectUndo(go, "Jump Scare");
            var a = go.AddComponent<JumpScareAnomaly>();
            a.anomalyName = s.name;
            a.minLoop = s.minLoop;
            Set(a, so =>
            {
                so.FindProperty("kind").enumValueIndex = (int)s.kind;
                so.FindProperty("delayRange").vector2Value = s.delay;
            });
        }
        EditorSceneManager.MarkAllScenesDirty();
        return list.Length;
    }

    // ==================================================================
    // 雰囲気
    // ==================================================================
    [MenuItem(Menu + "おまけ：夜の暗さにする", false, 200)]
    static void NightMood()
    {
        NightMoodCore();
        Done("夜の雰囲気にしました。ろうそくやランタンの灯りがメインの明かりになります。");
    }

    static void NightMoodCore()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.13f, 0.16f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.015f);
        RenderSettings.fogDensity = 0.02f;

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            Undo.RecordObject(l, "Dim Sun");
            l.intensity = 0.05f;
            l.color = new Color(0.4f, 0.45f, 0.7f);
        }
        EditorSceneManager.MarkAllScenesDirty();
    }

    // ==================================================================
    // ヘルパー
    // ==================================================================
    static List<Transform> SceneTransforms()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var list = new List<Transform>();
        foreach (var r in scene.GetRootGameObjects())
            list.AddRange(r.GetComponentsInChildren<Transform>(false));
        return list;
    }

    static bool Has(Transform t, string key) => t.name.ToLowerInvariant().Contains(key);
    static bool HasRenderer(Transform t) => t.GetComponentInChildren<Renderer>() != null;
    static bool IsInside(Transform t, Transform container) => container && (t == container || t.IsChildOf(container) || container.IsChildOf(t));

    /// <summary>家具1つ分のまとまり（いちばん外側のプレハブ）を返す。</summary>
    static Transform PrefabRoot(Transform t)
    {
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
        return root ? root.transform : t;
    }

    static string CleanName(string n)
    {
        // "dfk_chest_02_closed (1)" → "chest_02_closed"
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*\(\d+\)$", "");
        int i = n.IndexOf('_');
        if (i > 0 && i <= 4) n = n.Substring(i + 1);
        return n;
    }

    static Bounds RoomBounds(List<Transform> all)
    {
        var floor = all.FirstOrDefault(t => t.name.ToUpperInvariant() == "FLOOR");
        if (floor && HasRenderer(floor))
        {
            var fb = WorldBounds(floor.gameObject);
            fb.Encapsulate(fb.center + Vector3.up * 3f);
            return fb;
        }
        var rs = all.Select(t => t.GetComponent<Renderer>()).Where(r => r && !(r is ParticleSystemRenderer)).ToList();
        if (rs.Count == 0) return new Bounds(Vector3.zero, Vector3.one * 5);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Done(string msg)
    {
        Debug.Log("[LoopRoom] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("LoopRoom", msg, "OK");
    }

    static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedProperties();
    }

    static void Refs(SerializedObject so, string prop, params Object[] values)
    {
        var p = so.FindProperty(prop);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static Font BuiltinFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (!f) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return f;
    }

    static Image UIImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text UIText(string name, Transform parent, string text, int size, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.9f, 0.88f, 0.8f);
        t.supportRichText = true;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
