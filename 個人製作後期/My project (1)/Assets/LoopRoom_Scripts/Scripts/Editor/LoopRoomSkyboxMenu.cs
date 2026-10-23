using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 「LoopRoom > 自分の部屋で作る > 7. 空（Skybox）の異変」
/// </summary>
public static class LoopRoomSkyboxMenu
{
    const string M = "LoopRoom/自分の部屋で作る/7. 空（Skybox）の異変/";

    [MenuItem(M + "選択中のマテリアルに差し替える", false, 170)]
    static void Swap()
    {
        var mat = Selection.activeObject as Material;
        if (!mat)
        {
            EditorUtility.DisplayDialog("LoopRoom",
                "Project ウィンドウで、異変用の skybox マテリアルを選んでから実行してください。\n\n" +
                "（例：昼の空、赤い空、星のない夜空 など）", "OK");
            return;
        }
        Create($"空が違う（{mat.name}）", 0, so => so.FindProperty("anomalySkybox").objectReferenceValue = mat);
    }

    [MenuItem(M + "空が赤い", false, 180)]
    static void Red() => Create("空が赤い", 0, so =>
        so.FindProperty("tint").colorValue = new Color(1f, 0.3f, 0.25f));

    [MenuItem(M + "空の向きが違う（月や雲の位置がずれる）", false, 181)]
    static void Rotate() => Create("空の向きが違う", 3, so =>
    {
        so.FindProperty("tint").colorValue = Color.white;
        so.FindProperty("rotation").floatValue = 150f;
    });

    [MenuItem(M + "空が真っ暗", false, 182)]
    static void Dark() => Create("窓の外が真っ暗", 1, so =>
    {
        so.FindProperty("tint").colorValue = Color.white;
        so.FindProperty("exposure").floatValue = 0.03f;
    });

    [MenuItem(M + "空が明るすぎる（夜なのに昼）", false, 183)]
    static void Bright() => Create("夜なのに外が明るい", 0, so =>
    {
        so.FindProperty("tint").colorValue = new Color(1f, 0.97f, 0.9f);
        so.FindProperty("exposure").floatValue = 2.8f;
    });

    static void Create(string name, int minLoop, System.Action<SerializedObject> setup)
    {
        if (!RenderSettings.skybox)
            Debug.LogWarning("[LoopRoom] このシーンには skybox が設定されていません（Window > Rendering > Lighting > Environment）");

        var go = new GameObject("異変_" + name);
        var parent = GameObject.Find("[LoopRoom]/Anomalies");
        if (parent) go.transform.SetParent(parent.transform);
        Undo.RegisterCreatedObjectUndo(go, "Skybox Anomaly");

        var a = go.AddComponent<SkyboxAnomaly>();
        a.anomalyName = name;
        a.minLoop = minLoop;
        var so = new SerializedObject(a);
        setup(so);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log($"[LoopRoom] 異変「{name}」を作りました。");
    }
}
