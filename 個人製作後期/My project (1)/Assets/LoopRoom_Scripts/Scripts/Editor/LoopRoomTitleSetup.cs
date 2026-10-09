using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「LoopRoom > タイトル画面を追加」：今のゲームシーンにタイトル画面を組み込む。
/// </summary>
public static class LoopRoomTitleSetup
{
    [MenuItem("LoopRoom/タイトル/ゲームシーンの中にタイトルを重ねる", false, 4)]
    static void AddTitle()
    {
        var canvasGo = GameObject.Find("[LoopRoom]/Canvas");
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (!canvasGo || !pc)
        {
            EditorUtility.DisplayDialog("LoopRoom", "先に「おまかせでゲーム化」を実行してください。", "OK");
            return;
        }
        var old = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
        if (old)
        {
            if (!EditorUtility.DisplayDialog("LoopRoom", "タイトル画面はすでにあります。作り直しますか？", "作り直す", "やめる")) return;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var canvas = canvasGo.transform;
        var font = Font();

        // ---- パネル（部屋が透けて見える暗幕）
        var panel = Img("TitleScreen", canvas, new Color(0, 0, 0, 0.6f));
        Stretch(panel.rectTransform);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // 上下を暗くしてタイトル周りを締める
        var top = Img("ShadeTop", panel.transform, new Color(0, 0, 0, 0.55f));
        Anchor(top.rectTransform, new Vector2(0, 0.75f), new Vector2(1, 1));
        var bottom = Img("ShadeBottom", panel.transform, new Color(0, 0, 0, 0.55f));
        Anchor(bottom.rectTransform, new Vector2(0, 0), new Vector2(1, 0.3f));

        var title = Txt("Title", panel.transform, "八泊目", 150, font, FontStyle.Bold);
        Anchor(title.rectTransform, new Vector2(0, 0.55f), new Vector2(1, 0.85f));
        var shadow = title.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.5f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(4, -4);

        var sub = Txt("Subtitle", panel.transform, "― 異変に気づいたら、眠れ ―", 32, font, FontStyle.Normal);
        sub.color = new Color(0.7f, 0.66f, 0.6f);
        Anchor(sub.rectTransform, new Vector2(0, 0.48f), new Vector2(1, 0.56f));

        string[] labels = { "はじめる", "遊び方", "おわる" };
        var items = new Text[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var t = Txt("Menu_" + labels[i], panel.transform, labels[i], 40, font, FontStyle.Normal);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.32f);
            t.rectTransform.sizeDelta = new Vector2(600, 64);
            t.rectTransform.anchoredPosition = new Vector2(0, -i * 70);
            items[i] = t;
        }

        var hint = Txt("Hint", panel.transform, "W / S ・ ↑ ↓ で選択　　Enter ・ クリックで決定", 20, font, FontStyle.Normal);
        hint.color = new Color(0.5f, 0.48f, 0.45f);
        Anchor(hint.rectTransform, new Vector2(0, 0.02f), new Vector2(1, 0.07f));

        // ---- 遊び方
        var how = Img("HowTo", panel.transform, new Color(0.02f, 0.015f, 0.015f, 0.95f));
        Stretch(how.rectTransform);
        var howText = Txt("Text", how.transform,
            "<size=56>遊び方</size>\n\n" +
            "あなたは、ある宿の一室で目を覚ます。\n" +
            "何度部屋を出ても、また同じ部屋で目覚めてしまう。\n\n" +
            "部屋のどこかに <color=#c23a2a>異変</color> があると思ったら …… <b>ベッドで眠る</b>\n" +
            "異変がないと思ったら …… <b>ドアから出る</b>\n\n" +
            "正しい選択を <b>8回連続</b> で続ければ、朝を迎えられる。\n" +
            "間違えれば、また1泊目から。\n\n" +
            "<size=26><color=#8a8278>WASD：移動　　マウス：見回す　　E / クリック：調べる</color></size>\n\n" +
            "<size=22><color=#6a625a>（何かキーを押すと戻ります）</color></size>", 34, font, FontStyle.Normal);
        Stretch(howText.rectTransform);
        how.gameObject.SetActive(false);

        // ---- 暗転（Fader）より下に置いて、開始時の暗転で覆われるようにする
        var fader = canvas.Find("Fader");
        if (fader) panel.transform.SetSiblingIndex(fader.GetSiblingIndex());

        // ---- スクリプト
        var ts = panel.gameObject.AddComponent<TitleScreen>();
        var hide = new System.Collections.Generic.List<Object>();
        foreach (var n in new[] { "Crosshair", "Prompt", "Nights" })
        {
            var c = canvas.Find(n);
            if (c) hide.Add(c.gameObject);
        }
        var camRoot = new SerializedObject(pc).FindProperty("cameraRoot").objectReferenceValue;

        var so = new SerializedObject(ts);
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = sub;
        so.FindProperty("howToPanel").objectReferenceValue = how.gameObject;
        so.FindProperty("cameraRoot").objectReferenceValue = camRoot;
        var mi = so.FindProperty("menuItems");
        mi.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) mi.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        var hd = so.FindProperty("hideDuringTitle");
        hd.arraySize = hide.Count;
        for (int i = 0; i < hide.Count; i++) hd.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(panel.gameObject, "Title Screen");
        EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = panel.gameObject;
        EditorUtility.DisplayDialog("LoopRoom",
            "タイトル画面を追加しました。\n\n" +
            "タイトル名・サブタイトルは、選択中の TitleScreen の Inspector で変えられます。\n" +
            "Ctrl+S で保存して ▶ 再生してください。", "OK");
    }

    internal static Font Font()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (!f) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return f;
    }

    internal static Image Img(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    internal static Text Txt(string name, Transform parent, string s, int size, Font font, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = s;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.95f, 0.92f, 0.85f);
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    internal static void Stretch(RectTransform rt) => Anchor(rt, Vector2.zero, Vector2.one);

    internal static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
