using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using H = LoopRoomTitleSetup;

/// <summary>
/// 「LoopRoom > タイトル > タイトルシーンを作成」
/// ゲームシーンを開いた状態で実行すると、
///  1. 目覚める位置から見た部屋を撮影して、タイトルの背景画像にする
///  2. Title シーンを作って保存する
///  3. Build Settings に「Title → ゲーム」の順で登録する
/// </summary>
public static class LoopRoomTitleSceneBuilder
{
    const string Dir = "Assets/LoopRoom";
    const string GenDir = Dir + "/Generated";
    const string TitlePath = Dir + "/Title.unity";
    const string BgPath = GenDir + "/TitleBackground.png";
    const string VignettePath = GenDir + "/Vignette.png";

    [MenuItem("LoopRoom/タイトル/タイトルシーンを作成", false, 3)]
    static void Build()
    {
        var gameScene = EditorSceneManager.GetActiveScene();
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (!pc)
        {
            EditorUtility.DisplayDialog("LoopRoom", "ゲームシーン（[LoopRoom] があるシーン）を開いてから実行してください。", "OK");
            return;
        }
        if (string.IsNullOrEmpty(gameScene.path))
        {
            EditorUtility.DisplayDialog("LoopRoom", "ゲームシーンがまだ保存されていません。\nFile > Save As で保存してから実行してください。", "OK");
            return;
        }
        if (File.Exists(TitlePath) &&
            !EditorUtility.DisplayDialog("LoopRoom", "Title シーンはすでにあります。作り直しますか？\n（背景の撮り直しにも使えます）", "作り直す", "やめる"))
            return;

        Directory.CreateDirectory(GenDir);

        // ---- ゲームシーン内のタイトル（重ねる版）があれば外す
        var inScene = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
        if (inScene) Object.DestroyImmediate(inScene.gameObject);

        // ---- 背景を撮影
        bool captured = CaptureBackground(pc);
        EditorSceneManager.SaveScene(gameScene);
        string gamePath = gameScene.path;
        string gameName = gameScene.name;

        // ---- 素材
        MakeVignette();
        AssetDatabase.Refresh();
        var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        var vignette = AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
        var ambientClip = FindClip("ambient_drone_loop") ?? FindClip("ambient_inn_night_loop");
        var moveClip = FindClip("footstep_wood_02");
        var decideClip = FindClip("door_bang");

        // ---- Title シーン
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;
        var font = H.Font();

        // 背景（画面いっぱいに敷く）
        var bgRoot = new GameObject("Background", typeof(RectTransform)).GetComponent<RectTransform>();
        bgRoot.SetParent(canvas, false);
        H.Stretch(bgRoot);
        var bg = H.Img("Image", bgRoot, bgSprite ? new Color(0.8f, 0.78f, 0.76f) : new Color(0.05f, 0.03f, 0.03f));
        bg.sprite = bgSprite;
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;

        var dark = H.Img("Darken", canvas, new Color(0, 0, 0, 0.45f));
        H.Stretch(dark.rectTransform);
        var vig = H.Img("Vignette", canvas, Color.white);
        vig.sprite = vignette;
        H.Stretch(vig.rectTransform);
        var top = H.Img("ShadeTop", canvas, new Color(0, 0, 0, 0.35f));
        H.Anchor(top.rectTransform, new Vector2(0, 0.78f), Vector2.one);
        var bottom = H.Img("ShadeBottom", canvas, new Color(0, 0, 0, 0.5f));
        H.Anchor(bottom.rectTransform, Vector2.zero, new Vector2(1, 0.3f));

        var title = H.Txt("Title", canvas, "八泊目", 160, font, FontStyle.Bold);
        H.Anchor(title.rectTransform, new Vector2(0, 0.56f), new Vector2(1, 0.86f));
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0.5f, 0f, 0f, 0.75f);
        sh.effectDistance = new Vector2(5, -5);

        var sub = H.Txt("Subtitle", canvas, "― 異変に気づいたら、眠れ ―", 34, font, FontStyle.Normal);
        sub.color = new Color(0.72f, 0.68f, 0.62f);
        H.Anchor(sub.rectTransform, new Vector2(0, 0.48f), new Vector2(1, 0.56f));

        string[] labels = { "はじめる", "遊び方", "おわる" };
        var items = new Text[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var t = H.Txt("Menu_" + labels[i], canvas, labels[i], 42, font, FontStyle.Normal);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.33f);
            t.rectTransform.sizeDelta = new Vector2(600, 66);
            t.rectTransform.anchoredPosition = new Vector2(0, -i * 72);
            items[i] = t;
        }
        var hint = H.Txt("Hint", canvas, "W / S ・ ↑ ↓ で選択　　Enter ・ クリックで決定", 20, font, FontStyle.Normal);
        hint.color = new Color(0.5f, 0.48f, 0.45f);
        H.Anchor(hint.rectTransform, new Vector2(0, 0.02f), new Vector2(1, 0.07f));

        var how = H.Img("HowTo", canvas, new Color(0.02f, 0.015f, 0.015f, 0.95f));
        H.Stretch(how.rectTransform);
        var howText = H.Txt("Text", how.transform,
            "<size=56>遊び方</size>\n\n" +
            "あなたは、ある宿の一室で目を覚ます。\n" +
            "何度部屋を出ても、また同じ部屋で目覚めてしまう。\n\n" +
            "部屋のどこかに <color=#c23a2a>異変</color> があると思ったら …… <b>ベッドで眠る</b>\n" +
            "異変がないと思ったら …… <b>ドアから出る</b>\n\n" +
            "正しい選択を <b>8回連続</b> で続ければ、朝を迎えられる。\n" +
            "間違えれば、また1泊目から。\n\n" +
            "<size=26><color=#8a8278>WASD：移動　　マウス：見回す　　E / クリック：調べる</color></size>\n\n" +
            "<size=22><color=#6a625a>（何かキーを押すと戻ります）</color></size>", 34, font, FontStyle.Normal);
        H.Stretch(howText.rectTransform);
        how.gameObject.SetActive(false);

        var faderImg = H.Img("Fader", canvas, Color.black);
        H.Stretch(faderImg.rectTransform);
        var fader = faderImg.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 0f; // 編集中は見えるように（再生時は黒から始まる）

        // 音
        var audioGo = new GameObject("Audio");
        var amb = audioGo.AddComponent<AudioSource>();
        amb.clip = ambientClip;
        amb.loop = true;
        amb.playOnAwake = false;
        var sfx = audioGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        // メニュー
        var menu = new GameObject("TitleMenu").AddComponent<TitleSceneMenu>();
        var so = new SerializedObject(menu);
        so.FindProperty("gameSceneName").stringValue = gameName;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subtitleText").objectReferenceValue = sub;
        so.FindProperty("howToPanel").objectReferenceValue = how.gameObject;
        so.FindProperty("background").objectReferenceValue = bgRoot;
        so.FindProperty("backgroundImage").objectReferenceValue = bg;
        so.FindProperty("fader").objectReferenceValue = fader;
        so.FindProperty("ambient").objectReferenceValue = amb;
        so.FindProperty("sfx").objectReferenceValue = sfx;
        so.FindProperty("moveSound").objectReferenceValue = moveClip;
        so.FindProperty("decideSound").objectReferenceValue = decideClip;
        var mi = so.FindProperty("menuItems");
        mi.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) mi.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, TitlePath);

        // ---- Build Settings：Title → ゲーム → その他
        var list = new List<EditorBuildSettingsScene>
        {
            new(TitlePath, true),
            new(gamePath, true),
        };
        list.AddRange(EditorBuildSettings.scenes.Where(s => s.path != TitlePath && s.path != gamePath));
        EditorBuildSettings.scenes = list.ToArray();

        Selection.activeGameObject = menu.gameObject;
        EditorUtility.DisplayDialog("LoopRoom",
            "タイトルシーンを作りました。\n\n" +
            $"・Assets/LoopRoom/Title.unity（いま開いています）\n" +
            $"・「はじめる」で「{gameName}」シーンへ\n" +
            "・クリア後の R キーでタイトルに戻ります\n" +
            (captured ? "・背景はゲームの部屋を撮影した画像です\n" : "⚠ 背景の撮影に失敗したので、暗い背景になっています\n") +
            "\nタイトル名は TitleMenu の Inspector で変えられます。\n▶ 再生して確認してください。", "OK");
    }

    // ------------------------------------------------------------------
    static bool CaptureBackground(PlayerController pc)
    {
        var cam = pc.GetComponentInChildren<Camera>(true);
        if (!cam) return false;
        const int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        bool wasActive = cam.gameObject.activeSelf;
        try
        {
            cam.gameObject.SetActive(true);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(BgPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LoopRoom] 背景の撮影に失敗しました: " + e.Message);
            return false;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            cam.gameObject.SetActive(wasActive);
            Object.DestroyImmediate(rt);
        }

        AssetDatabase.ImportAsset(BgPath);
        if (AssetImporter.GetAtPath(BgPath) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
        return true;
    }

    static void MakeVignette()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 1.1f);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.35f, r));
            px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 245));
        }
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(VignettePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(VignettePath);
        if (AssetImporter.GetAtPath(VignettePath) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
    }

    static AudioClip FindClip(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        }
        return null;
    }
}
