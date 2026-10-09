using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 「LoopRoom > 音素材を割り当て」：Audio フォルダの WAV をファイル名で探して、ゲームの各所に自動で設定する。
/// </summary>
public static class LoopRoomAudioSetup
{
    [MenuItem("LoopRoom/音素材を割り当て", false, 2)]
    static void Assign()
    {
        if (!Object.FindFirstObjectByType<LoopManager>())
        {
            EditorUtility.DisplayDialog("LoopRoom", "先に「おまかせでゲーム化」を実行してください。", "OK");
            return;
        }

        var log = new List<string>();
        var missing = new List<string>();
        AudioClip Clip(string name)
        {
            var guid = AssetDatabase.FindAssets(name + " t:AudioClip")
                .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
            var c = guid != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)) : null;
            if (!c) missing.Add(name);
            return c;
        }

        SetLoopImport("ambient_drone_loop", "ambient_inn_night_loop", "tension_rise");

        // ---- ScareFX
        var fx = Object.FindFirstObjectByType<ScareFX>();
        if (!fx)
        {
            fx = new GameObject("ScareFX").AddComponent<ScareFX>();
            var root = GameObject.Find("[LoopRoom]");
            if (root) fx.transform.SetParent(root.transform);
            Undo.RegisterCreatedObjectUndo(fx.gameObject, "ScareFX");
        }
        Set(fx, so =>
        {
            so.FindProperty("stinger").objectReferenceValue = Clip("stinger_hit");
            so.FindProperty("scream").objectReferenceValue = Clip("scream");
            so.FindProperty("bang").objectReferenceValue = Clip("door_bang");
            so.FindProperty("heartbeat").objectReferenceValue = Clip("heartbeat_loop");
            so.FindProperty("whisper").objectReferenceValue = Clip("whisper");
            so.FindProperty("tension").objectReferenceValue = Clip("tension_rise");
            var steps = Enumerable.Range(1, 4).Select(i => Clip($"footstep_wood_{i:00}")).Where(c => c).ToArray();
            var p = so.FindProperty("footstepVariations");
            p.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = steps[i];
        });
        log.Add("・びっくり演出の音（衝撃音／叫び声／ドア／足音／鼓動／ささやき）");

        // ---- 環境音
        var amb = Object.FindFirstObjectByType<AmbientAudio>();
        if (!amb)
        {
            var go = new GameObject("Ambient");
            var root = GameObject.Find("[LoopRoom]");
            if (root) go.transform.SetParent(root.transform);
            amb = go.AddComponent<AmbientAudio>();
            Undo.RegisterCreatedObjectUndo(go, "Ambient");
        }
        Set(amb, so =>
        {
            var layers = so.FindProperty("layers");
            layers.arraySize = 2;
            var l0 = layers.GetArrayElementAtIndex(0);
            l0.FindPropertyRelative("clip").objectReferenceValue = Clip("ambient_drone_loop");
            l0.FindPropertyRelative("volume").floatValue = 0.45f;
            var l1 = layers.GetArrayElementAtIndex(1);
            l1.FindPropertyRelative("clip").objectReferenceValue = Clip("ambient_inn_night_loop");
            l1.FindPropertyRelative("volume").floatValue = 0.6f;
        });
        log.Add("・環境音（低いうなり＋夜風ときしみ）");

        // ---- ドアとベッド
        foreach (var choice in Object.FindObjectsByType<LoopChoice>(FindObjectsSortMode.None))
        {
            var clip = Clip(choice.MeansAnomaly ? "bed_rustle" : "door_open_creak");
            var src = choice.GetComponent<AudioSource>();
            if (!src) src = Undo.AddComponent<AudioSource>(choice.gameObject);
            src.playOnAwake = false;
            src.clip = clip;
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.volume = 0.9f;
            Set(choice, so => so.FindProperty("sfx").objectReferenceValue = src);
        }
        log.Add("・ドアのきしみ音／ベッドの衣ずれの音");

        // ---- 時計（あれば）
        var clock = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("clock") && t.GetComponentInChildren<Renderer>());
        AudioSource clockSrc = null;
        if (clock)
        {
            clockSrc = clock.GetComponent<AudioSource>();
            if (!clockSrc) clockSrc = Undo.AddComponent<AudioSource>(clock.gameObject);
            clockSrc.clip = Clip("clock_tick_loop");
            clockSrc.loop = true;
            clockSrc.playOnAwake = true;
            clockSrc.spatialBlend = 1f;
            clockSrc.minDistance = 0.8f;
            clockSrc.maxDistance = 8f;
            clockSrc.volume = 0.8f;
            log.Add($"・時計の秒針の音（{clock.name}）");
        }

        // ---- 間違い／クリア
        var lm = Object.FindFirstObjectByType<LoopManager>();
        Set(lm, so => so.FindProperty("wrongSound").objectReferenceValue = Clip("wrong_answer"));
        var cs = Object.FindFirstObjectByType<ClearScreen>();
        if (cs) Set(cs, so => so.FindProperty("clearSound").objectReferenceValue = Clip("clear_morning"));
        log.Add("・間違えたときの音／クリア時の朝の音");

        // ---- 音の異変を追加
        var parent = GameObject.Find("[LoopRoom]/Anomalies");
        int added = 0;
        if (!Object.FindObjectsByType<SilenceAnomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any())
        {
            AddAnomaly<SilenceAnomaly>(parent, "環境音が消えた", 2, null);
            if (clockSrc)
                AddAnomaly<SilenceAnomaly>(parent, "時計の音が止まった", 3,
                    so => { var p = so.FindProperty("targets"); p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = clockSrc; });
            added += clockSrc ? 2 : 1;
        }
        if (!Object.FindObjectsByType<JumpScareAnomaly>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                   .Any(a => new SerializedObject(a).FindProperty("kind").enumValueIndex == (int)JumpScareAnomaly.Kind.FakeOut))
        {
            AddAnomaly<JumpScareAnomaly>(parent, "何かが来そうで来ない", 2, so =>
            {
                so.FindProperty("kind").enumValueIndex = (int)JumpScareAnomaly.Kind.FakeOut;
                so.FindProperty("delayRange").vector2Value = new Vector2(4f, 9f);
            });
            added++;
        }
        if (added > 0) log.Add($"・音の異変を {added} 個追加（環境音が消える／時計が止まる／フェイント）");

        EditorSceneManager.MarkAllScenesDirty();
        string msg = "音を割り当てました。\n\n" + string.Join("\n", log);
        if (missing.Count > 0)
            msg += "\n\n⚠ 見つからなかった音：\n" + string.Join(", ", missing.Distinct()) + "\n（Audio フォルダを Assets に入れたか確認してください）";
        msg += "\n\nCtrl+S で保存して ▶ 再生してください。";
        Debug.Log("[LoopRoom] " + msg);
        EditorUtility.DisplayDialog("LoopRoom", msg, "OK");
    }

    static void AddAnomaly<T>(GameObject parent, string name, int minLoop, System.Action<SerializedObject> setup) where T : Anomaly
    {
        var go = new GameObject("異変_" + name);
        if (parent) go.transform.SetParent(parent.transform);
        Undo.RegisterCreatedObjectUndo(go, "Audio Anomaly");
        var a = go.AddComponent<T>();
        a.anomalyName = name;
        a.minLoop = minLoop;
        if (setup != null) Set(a, setup);
    }

    static void SetLoopImport(params string[] names)
    {
        // 長い環境音はストリーミング再生にしてメモリを節約
        foreach (var n in names)
        foreach (var g in AssetDatabase.FindAssets(n + " t:AudioClip"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
            var s = imp.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.Streaming) continue;
            s.loadType = AudioClipLoadType.Streaming;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedProperties();
    }
}
