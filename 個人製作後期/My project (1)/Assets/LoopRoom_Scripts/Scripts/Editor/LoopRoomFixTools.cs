using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 「困ったとき」用メニュー。画面が真っ暗になる原因をまとめてチェックして直す。
/// </summary>
public static class LoopRoomFixTools
{
    [MenuItem("LoopRoom/困ったとき/画面が真っ暗を直す", false, 300)]
    static void FixBlackScreen()
    {
        var report = new List<string>();

        // 1. 暗転用の黒い幕（Fader）が閉じたままになっていないか
        foreach (var f in Object.FindObjectsByType<ScreenFader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var cg = f.GetComponent<CanvasGroup>();
            if (cg && cg.alpha > 0f)
            {
                Undo.RecordObject(cg, "Open Fader");
                cg.alpha = 0f;
                report.Add("・暗転用の黒い幕（Fader）を開けました");
            }
        }
        var clear = Object.FindObjectsByType<ClearScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in clear)
        {
            var so = new SerializedObject(c);
            var panel = so.FindProperty("panel").objectReferenceValue as GameObject;
            if (panel && panel.activeSelf)
            {
                Undo.RecordObject(panel, "Hide Clear Panel");
                panel.SetActive(false);
                report.Add("・クリア画面が出しっぱなしだったので隠しました");
            }
        }

        // 2. カメラ
        var pc = Object.FindFirstObjectByType<PlayerController>();
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var playerCam = pc ? pc.GetComponentInChildren<Camera>(true) : null;
        if (playerCam)
        {
            if (!playerCam.gameObject.activeInHierarchy || !playerCam.enabled)
            {
                Undo.RecordObject(playerCam.gameObject, "Enable Camera");
                playerCam.gameObject.SetActive(true);
                playerCam.enabled = true;
                report.Add("・プレイヤーのカメラを有効にしました");
            }
            if (!playerCam.CompareTag("MainCamera")) { playerCam.tag = "MainCamera"; report.Add("・プレイヤーのカメラに MainCamera タグを付けました"); }
            foreach (var c in cams)
            {
                if (c == playerCam || !c.gameObject.activeInHierarchy) continue;
                Undo.RecordObject(c.gameObject, "Disable Camera");
                c.gameObject.SetActive(false);
                report.Add($"・別のカメラ「{c.name}」をオフにしました");
            }
        }
        else report.Add("⚠ プレイヤーが見つかりません。「おまかせでゲーム化」を実行してください");

        // 3. 床をすり抜けて落ちていないか
        var spawn = GameObject.Find("SpawnPoint");
        if (pc && spawn)
        {
            Physics.SyncTransforms();
            var p = spawn.transform.position;
            bool ground = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore);
            if (!ground)
            {
                var floor = new GameObject("[LoopRoom] SafetyFloor");
                Undo.RegisterCreatedObjectUndo(floor, "SafetyFloor");
                floor.transform.position = p + Vector3.down * 0.05f;
                floor.AddComponent<BoxCollider>().size = new Vector3(30, 0.1f, 30);
                report.Add("・足元に床の当たり判定がなかったので、見えない床を追加しました（落下していた可能性）");
            }

            // 目線が家具や壁にめり込んでいないか
            Vector3 eye = p + Vector3.up * 1.6f;
            var inside = Physics.OverlapSphere(eye, 0.15f, ~0, QueryTriggerInteraction.Ignore);
            if (inside.Length > 0)
                report.Add($"⚠ 目覚める位置の目線が「{inside[0].name}」にめり込んでいます。\n   シーンビューで空いている場所を中央に映して「3. スポーン地点をここに置く」を実行してください");

            Undo.RecordObject(pc.transform, "Move Player");
            pc.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        }

        // 4. 部屋が暗すぎないか
        Brighten();
        report.Add("・環境光を明るくし、霧を薄くしました");

        // 5. プレイヤーの手元にほのかな灯り
        if (playerCam && !playerCam.GetComponentInChildren<Light>(true))
        {
            AddPlayerGlow(playerCam.transform);
            report.Add("・プレイヤーの周りにほのかな灯りを追加しました（停電演出では一緒に消えます）");
        }

        EditorSceneManager.MarkAllScenesDirty();
        string msg = string.Join("\n", report) +
                     "\n\nCtrl+S で保存して、もう一度 ▶ 再生してください。\n" +
                     "それでも真っ暗なら、Console に赤いエラーが出ていないか確認してください。";
        Debug.Log("[LoopRoom] 真っ暗チェック\n" + msg);
        EditorUtility.DisplayDialog("LoopRoom", msg, "OK");
    }

    [MenuItem("LoopRoom/困ったとき/もっと明るくする", false, 301)]
    static void BrightenMenu()
    {
        Brighten(0.3f);
        EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("LoopRoom", "環境光をさらに明るくしました。暗すぎ・明るすぎは\nWindow > Rendering > Lighting > Environment の Ambient Color で調整できます。", "OK");
    }

    public static void Brighten(float ambient = 0.18f)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(ambient, ambient * 0.9f, ambient * 1.05f);
        RenderSettings.fogDensity = Mathf.Min(RenderSettings.fogDensity, 0.02f);
    }

    public static void AddPlayerGlow(Transform cam)
    {
        var go = new GameObject("PlayerGlow");
        Undo.RegisterCreatedObjectUndo(go, "Player Glow");
        go.transform.SetParent(cam, false);
        go.transform.localPosition = new Vector3(0.2f, -0.3f, 0.3f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.8f, 0.55f);
        l.range = 4.5f;
        l.intensity = 0.8f;
        l.shadows = LightShadows.None;
    }
}
