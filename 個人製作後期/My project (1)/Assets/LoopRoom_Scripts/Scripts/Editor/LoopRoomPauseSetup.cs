using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using H = LoopRoomTitleSetup;

/// <summary>
/// 「LoopRoom > 一時停止メニュー（ESC）を追加」
/// </summary>
public static class LoopRoomPauseSetup
{
    [MenuItem("LoopRoom/一時停止メニュー（ESC）を追加", false, 5)]
    static void Add()
    {
        var canvasGo = GameObject.Find("[LoopRoom]/Canvas");
        if (!canvasGo)
        {
            EditorUtility.DisplayDialog("LoopRoom", "ゲームシーン（[LoopRoom] があるシーン）を開いてから実行してください。", "OK");
            return;
        }
        var old = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (old)
        {
            if (!EditorUtility.DisplayDialog("LoopRoom", "一時停止メニューはすでにあります。作り直しますか？", "作り直す", "やめる")) return;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var canvas = canvasGo.transform;
        var font = H.Font();

        var root = new GameObject("PauseMenu", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        root.transform.SetAsLastSibling(); // 一番手前に表示
        H.Stretch(root.GetComponent<RectTransform>());

        var panel = H.Img("Panel", root.transform, new Color(0, 0, 0, 0.8f));
        H.Stretch(panel.rectTransform);

        var title = H.Txt("Title", panel.transform, "一時停止", 64, font, FontStyle.Bold);
        H.Anchor(title.rectTransform, new Vector2(0, 0.7f), new Vector2(1, 0.85f));

        var items = new Text[5];
        for (int i = 0; i < items.Length; i++)
        {
            var t = H.Txt("Item" + i, panel.transform, "", 38, font, FontStyle.Normal);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.58f);
            t.rectTransform.sizeDelta = new Vector2(760, 60);
            t.rectTransform.anchoredPosition = new Vector2(0, -i * 70);
            items[i] = t;
        }

        var hint = H.Txt("Hint", panel.transform,
            "W / S ・ ↑ ↓ で選択　　A / D ・ ← → で調整　　Enter ・ クリックで決定　　ESC で閉じる", 20, font, FontStyle.Normal);
        hint.color = new Color(0.5f, 0.48f, 0.45f);
        H.Anchor(hint.rectTransform, new Vector2(0, 0.04f), new Vector2(1, 0.1f));

        var menu = root.AddComponent<PauseMenu>();
        var so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        var p = so.FindProperty("items");
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);

        Undo.RegisterCreatedObjectUndo(root, "Pause Menu");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("LoopRoom",
            "一時停止メニューを追加しました。\n\nゲーム中に ESC で開きます。\nCtrl+S で保存して ▶ 再生してください。", "OK");
    }
}
