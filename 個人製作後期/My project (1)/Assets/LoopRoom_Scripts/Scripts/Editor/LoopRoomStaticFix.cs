using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 動かす・大きさを変える異変の対象から「Batching Static」を自動で外す。
///
/// アセットの家具は Static（動かない物）になっていることが多く、
/// Static の物はゲーム開始時に見た目が固定されるため、スクリプトで動かしても見た目が変わらない。
/// ▶ 再生を押した瞬間にチェックして直すので、普段は何もしなくてよい。
/// </summary>
[InitializeOnLoad]
public static class LoopRoomStaticFix
{
    static LoopRoomStaticFix()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Fix(false);
        };
    }

    [MenuItem("LoopRoom/困ったとき/動く異変が動かない・大きくならないを直す", false, 302)]
    static void FixMenu() => Fix(true);

    static void Fix(bool showDialog)
    {
        var targets = new HashSet<GameObject>();

        foreach (var a in Object.FindObjectsByType<TransformAnomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddTarget(targets, new SerializedObject(a).FindProperty("target").objectReferenceValue as Transform, a.transform);

        foreach (var a in Object.FindObjectsByType<MoveWhenUnseenAnomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddTarget(targets, new SerializedObject(a).FindProperty("target").objectReferenceValue as Transform, a.transform);

        int fixedCount = 0;
        var names = new List<string>();
        foreach (var root in targets)
        {
            bool changed = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                Undo.RecordObject(t.gameObject, "Remove Batching Static");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                changed = true;
            }
            if (changed) { fixedCount++; names.Add(root.name); }
        }

        if (fixedCount > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[LoopRoom] 動く異変の対象 {fixedCount} 個から Static を外しました：{string.Join(", ", names)}");
        }

        if (showDialog)
        {
            EditorUtility.DisplayDialog("LoopRoom", fixedCount > 0
                ? $"{fixedCount} 個の家具から Static を外しました。\n\n{string.Join("\n", names)}\n\nCtrl+S で保存してください。"
                : "Static が付いた対象は見つかりませんでした。", "OK");
        }
    }

    static void AddTarget(HashSet<GameObject> set, Transform target, Transform fallback)
    {
        var t = target ? target : fallback;
        if (t) set.Add(t.gameObject);
    }
}
