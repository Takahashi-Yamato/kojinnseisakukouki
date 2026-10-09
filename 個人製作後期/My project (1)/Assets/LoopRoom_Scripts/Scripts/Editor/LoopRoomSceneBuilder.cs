using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メニュー「LoopRoom > シーンを自動作成」で、遊べる状態の部屋を丸ごと組み立てる。
/// 部屋・家具・プレイヤー・ドア/ベッド・UI・異変10種をすべて配置し、
/// Assets/LoopRoom/LoopRoom.unity として保存する。
/// </summary>
public static class LoopRoomSceneBuilder
{
    const string Root = "Assets/LoopRoom";
    const string GenDir = Root + "/Generated";
    const string ScenePath = Root + "/LoopRoom.unity";

    static Transform roomRoot, anomRoot;

    [MenuItem("LoopRoom/シーンを自動作成")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("LoopRoom", "LoopRoom.unity はすでにあります。作り直しますか？", "作り直す", "やめる"))
            return;

        Directory.CreateDirectory(GenDir + "/Materials");
        Directory.CreateDirectory(GenDir + "/Audio");
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting();

        roomRoot = new GameObject("Room").transform;
        anomRoot = new GameObject("Anomalies").transform;

        // ---------------- 部屋 ----------------
        var wall = Mat("Wall", new Color(0.55f, 0.52f, 0.47f));
        Box("Floor", roomRoot, new Vector3(0, -0.05f, 0), new Vector3(6, 0.1f, 5), Mat("Floor", new Color(0.30f, 0.20f, 0.13f)));
        Box("Ceiling", roomRoot, new Vector3(0, 3.05f, 0), new Vector3(6, 0.1f, 5), Mat("Ceiling", new Color(0.6f, 0.6f, 0.58f)));
        Box("Wall_E", roomRoot, new Vector3(3, 1.5f, 0), new Vector3(0.2f, 3, 5), wall);
        Box("Wall_W", roomRoot, new Vector3(-3, 1.5f, 0), new Vector3(0.2f, 3, 5), wall);
        Box("Wall_N", roomRoot, new Vector3(0, 1.5f, 2.5f), new Vector3(6, 3, 0.2f), wall);
        Box("Wall_S", roomRoot, new Vector3(0, 1.5f, -2.5f), new Vector3(6, 3, 0.2f), wall);

        var lightGo = new GameObject("CeilingLight");
        lightGo.transform.SetParent(roomRoot);
        lightGo.transform.position = new Vector3(0, 2.85f, 0);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 9f;
        light.intensity = 1.6f;
        light.color = new Color(1f, 0.9f, 0.75f);
        light.shadows = LightShadows.Soft;

        // ---------------- 家具 ----------------
        var wood = Mat("Wood", new Color(0.40f, 0.27f, 0.17f));
        var dark = Mat("Dark", new Color(0.05f, 0.05f, 0.05f));
        var metal = Mat("Metal", new Color(0.65f, 0.6f, 0.5f));

        // ドア（東の壁）
        var door = Group("Door", roomRoot, new Vector3(2.86f, 0, -1.0f));
        Box("Panel", door, new Vector3(0, 1.0f, 0), new Vector3(0.08f, 2.0f, 0.9f), Mat("Door", new Color(0.35f, 0.22f, 0.15f)), true);
        Sphere("Knob", door, new Vector3(-0.06f, 1.0f, 0.32f), 0.07f, metal, true);
        var doorChoice = door.gameObject.AddComponent<LoopChoice>();
        Props(doorChoice, so => { so.FindProperty("meansAnomaly").boolValue = false; so.FindProperty("prompt").stringValue = "ドアを開ける（異変なし）"; });

        // ベッド（西の壁）
        var bed = Group("Bed", roomRoot, new Vector3(-2.4f, 0, 0.6f));
        Box("Frame", bed, new Vector3(0, 0.25f, 0), new Vector3(1.0f, 0.5f, 2.0f), Mat("BedFrame", new Color(0.25f, 0.2f, 0.17f)), true);
        Box("Sheet", bed, new Vector3(0.05f, 0.53f, -0.15f), new Vector3(0.95f, 0.08f, 1.6f), Mat("Sheet", new Color(0.32f, 0.36f, 0.45f)), true);
        var pillow = Box("Pillow", bed, new Vector3(0, 0.6f, 0.75f), new Vector3(0.7f, 0.15f, 0.35f), Mat("Pillow", new Color(0.85f, 0.85f, 0.82f)), true);
        var bedChoice = bed.gameObject.AddComponent<LoopChoice>();
        Props(bedChoice, so => { so.FindProperty("meansAnomaly").boolValue = true; so.FindProperty("prompt").stringValue = "眠る（異変あり）"; });

        // 机と椅子（北の壁）
        var desk = Group("Desk", roomRoot, new Vector3(0.8f, 0, 2.05f));
        Box("Top", desk, new Vector3(0, 0.72f, 0), new Vector3(1.4f, 0.06f, 0.65f), wood, true);
        foreach (var x in new[] { -0.65f, 0.65f })
        foreach (var z in new[] { -0.28f, 0.28f })
            Box("Leg", desk, new Vector3(x, 0.35f, z), new Vector3(0.05f, 0.7f, 0.05f), wood, true);

        var chair = Group("Chair", roomRoot, new Vector3(0.8f, 0, 1.35f));
        Box("Seat", chair, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), wood, true);
        Box("Back", chair, new Vector3(0, 0.75f, -0.21f), new Vector3(0.45f, 0.55f, 0.04f), wood, true);
        foreach (var x in new[] { -0.2f, 0.2f })
        foreach (var z in new[] { -0.2f, 0.2f })
            Box("Leg", chair, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f), wood, true);

        // 時計（北の壁、机の上）
        var clock = Group("Clock", roomRoot, new Vector3(0.8f, 2.1f, 2.37f));
        var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        face.name = "Face";
        face.transform.SetParent(clock, false);
        face.transform.localRotation = Quaternion.Euler(90, 0, 0);
        face.transform.localScale = new Vector3(0.4f, 0.015f, 0.4f);
        face.GetComponent<Renderer>().sharedMaterial = Mat("ClockFace", new Color(0.9f, 0.88f, 0.82f));
        Box("HourHand", clock, new Vector3(0, 0.05f, -0.02f), new Vector3(0.02f, 0.1f, 0.005f), dark, true);
        Box("MinuteHand", clock, new Vector3(0.07f, 0, -0.025f), new Vector3(0.14f, 0.015f, 0.005f), dark, true);

        // 絵（南の壁）
        var painting = Group("Painting", roomRoot, new Vector3(-1.0f, 1.6f, -2.37f));
        Box("Frame", painting, Vector3.zero, new Vector3(0.9f, 0.7f, 0.04f), Mat("Frame", new Color(0.45f, 0.35f, 0.15f)), true);
        Box("Canvas", painting, new Vector3(0, 0, 0.022f), new Vector3(0.8f, 0.6f, 0.01f), Mat("CanvasArt", new Color(0.45f, 0.55f, 0.6f)), true);
        var figure = Box("Figure", painting, new Vector3(0.12f, -0.08f, 0.03f), new Vector3(0.1f, 0.3f, 0.005f), dark, true);

        // 人形（南西の角）
        var doll = Group("Doll", roomRoot, new Vector3(-2.6f, 0, -2.05f));
        var dollBody = Capsule("Body", doll, new Vector3(0, 0.2f, 0), new Vector3(0.25f, 0.2f, 0.25f), Mat("Dress", new Color(0.45f, 0.08f, 0.08f)));
        Sphere("Head", doll, new Vector3(0, 0.5f, 0), 0.2f, Mat("Skin", new Color(0.88f, 0.8f, 0.72f)), true);
        Sphere("EyeL", doll, new Vector3(-0.045f, 0.52f, 0.09f), 0.03f, dark, true);
        Sphere("EyeR", doll, new Vector3(0.045f, 0.52f, 0.09f), 0.03f, dark, true);
        doll.rotation = Quaternion.Euler(0, 45, 0);

        // 異変用の2つ目のドア（南の壁、普段は非表示）
        var fakeDoor = Group("FakeDoor", roomRoot, new Vector3(1.4f, 0, -2.36f));
        Box("Panel", fakeDoor, new Vector3(0, 1.0f, 0), new Vector3(0.9f, 2.0f, 0.08f), Mat("Door", Color.white), true);
        Sphere("Knob", fakeDoor, new Vector3(-0.32f, 1.0f, 0.06f), 0.07f, metal, true);
        fakeDoor.gameObject.SetActive(false);

        // 連続正解数を表示する紙（東の壁）
        var paper = Group("Counter", roomRoot, new Vector3(2.88f, 1.65f, 0.4f));
        paper.rotation = Quaternion.Euler(0, 90, 0);
        Box("Paper", paper, new Vector3(0, 0, 0.01f), new Vector3(0.3f, 0.4f, 0.01f), Mat("Paper", new Color(0.9f, 0.88f, 0.8f)), true);
        var counterText = new GameObject("Number").AddComponent<TextMesh>();
        counterText.transform.SetParent(paper, false);
        counterText.transform.localPosition = new Vector3(0, 0, -0.001f);
        var font = BuiltinFont();
        counterText.font = font;
        counterText.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        counterText.text = "0";
        counterText.fontSize = 120;
        counterText.characterSize = 0.02f;
        counterText.anchor = TextAnchor.MiddleCenter;
        counterText.alignment = TextAlignment.Center;
        counterText.color = new Color(0.1f, 0.05f, 0.05f);
        counterText.gameObject.AddComponent<WallCounter>();

        // ---------------- プレイヤー ----------------
        var spawn = new GameObject("SpawnPoint").transform;
        spawn.SetPositionAndRotation(new Vector3(-1.5f, 0, -0.2f), Quaternion.Euler(0, 90, 0));

        var player = new GameObject("Player");
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.7f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0, 0.85f, 0);
        var pc = player.AddComponent<PlayerController>();

        var camRoot = new GameObject("CameraRoot").transform;
        camRoot.SetParent(player.transform, false);
        camRoot.localPosition = new Vector3(0, 1.6f, 0);
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(camRoot, false);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 70f;
        camGo.AddComponent<AudioListener>();
        var interactor = camGo.AddComponent<PlayerInteractor>();
        Props(pc, so => so.FindProperty("cameraRoot").objectReferenceValue = camRoot);

        // ---------------- UI ----------------
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
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
        Props(interactor, so => so.FindProperty("promptText").objectReferenceValue = prompt);

        var faderImg = UIImage("Fader", canvas, Color.black);
        Stretch(faderImg.rectTransform);
        faderImg.gameObject.AddComponent<CanvasGroup>();
        var fader = faderImg.gameObject.AddComponent<ScreenFader>();

        var clearPanel = UIImage("ClearPanel", canvas, Color.black);
        Stretch(clearPanel.rectTransform);
        var clearText = UIText("Text", clearPanel.transform,
            "目が覚めると、知らない朝だった。\n\nCLEAR\n\n<size=24>R キーでもう一度</size>", 44, font);
        Stretch(clearText.rectTransform);
        var clearScreen = canvasGo.AddComponent<ClearScreen>();
        Props(clearScreen, so => so.FindProperty("panel").objectReferenceValue = clearPanel.gameObject);
        clearPanel.gameObject.SetActive(false);

        // ---------------- 進行役 ----------------
        var lm = new GameObject("LoopManager").AddComponent<LoopManager>();
        Props(lm, so =>
        {
            so.FindProperty("player").objectReferenceValue = pc;
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
            so.FindProperty("fader").objectReferenceValue = fader;
        });

        // ---------------- 異変10種 ----------------
        var a1 = AddAnomaly<ObjectSwapAnomaly>("絵の人物が消えた", 0);
        Props(a1, so => Refs(so, "normalObjects", figure.gameObject));

        var a2 = AddAnomaly<TransformAnomaly>("時計が傾いている", 0);
        Props(a2, so => { so.FindProperty("target").objectReferenceValue = clock; so.FindProperty("rotationOffset").vector3Value = new Vector3(0, 0, 28); });

        var a3 = AddAnomaly<LightAnomaly>("照明が赤い", 0);
        Props(a3, so => { so.FindProperty("targetLight").objectReferenceValue = light; so.FindProperty("changeColor").boolValue = true; so.FindProperty("flicker").boolValue = false; });

        var a4 = AddAnomaly<ObjectSwapAnomaly>("ドアが2つある", 0);
        Props(a4, so => Refs(so, "anomalyObjects", fakeDoor.gameObject));

        var a5 = AddAnomaly<LightAnomaly>("照明がちらつく", 1);
        Props(a5, so => { so.FindProperty("targetLight").objectReferenceValue = light; so.FindProperty("changeColor").boolValue = false; so.FindProperty("flicker").boolValue = true; });

        var a6 = AddAnomaly<SoundAnomaly>("ドアの向こうからノック", 1);
        a6.transform.position = new Vector3(3.2f, 1.2f, -1.0f);
        a6.GetComponent<AudioSource>().maxDistance = 15f;
        var knock = CreateKnockClip();
        Props(a6, so => { Refs(so, "clips", knock); so.FindProperty("minInterval").floatValue = 3f; so.FindProperty("maxInterval").floatValue = 7f; });

        var a7 = AddAnomaly<ObjectSwapAnomaly>("枕がない", 2);
        Props(a7, so => Refs(so, "normalObjects", pillow.gameObject));

        var a8 = AddAnomaly<TransformAnomaly>("椅子の向きが違う", 2);
        Props(a8, so => { so.FindProperty("target").objectReferenceValue = chair; so.FindProperty("rotationOffset").vector3Value = new Vector3(0, 150, 0); });

        var a9 = AddAnomaly<TransformAnomaly>("机が壁から離れている", 3);
        Props(a9, so => { so.FindProperty("target").objectReferenceValue = desk; so.FindProperty("positionOffset").vector3Value = new Vector3(0, 0, -0.45f); });

        var a10 = AddAnomaly<MoveWhenUnseenAnomaly>("人形が近づいてくる", 4);
        Props(a10, so => { so.FindProperty("target").objectReferenceValue = doll; so.FindProperty("visibilityCheck").objectReferenceValue = dollBody.GetComponent<Renderer>(); });

        // ---------------- 保存 ----------------
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[LoopRoom] シーンを作成しました：" + ScenePath + "　▶ 再生ボタンで遊べます");
        EditorUtility.DisplayDialog("LoopRoom", "シーンを作成しました。\n再生ボタンを押して遊んでみてください。\n\nWASD：移動　マウス：視点　E：調べる", "OK");
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

    static void SetupLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.06f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogDensity = 0.08f;
    }

    static T AddAnomaly<T>(string name, int minLoop) where T : Anomaly
    {
        var go = new GameObject("異変_" + name);
        go.transform.SetParent(anomRoot);
        var a = go.AddComponent<T>();
        a.anomalyName = name;
        a.minLoop = minLoop;
        return a;
    }

    static Material Mat(string name, Color color)
    {
        string path = $"{GenDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) { if (color != Color.white) m.color = color; return m; }

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader) { color = color };
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Transform Group(string name, Transform parent, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        t.position = pos;
        return t;
    }

    static Transform Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool local = false)
        => Prim(PrimitiveType.Cube, name, parent, pos, scale, mat, local);

    static Transform Sphere(string name, Transform parent, Vector3 pos, float size, Material mat, bool local = false)
        => Prim(PrimitiveType.Sphere, name, parent, pos, Vector3.one * size, mat, local);

    static Transform Capsule(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        => Prim(PrimitiveType.Capsule, name, parent, pos, scale, mat, true);

    static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool local)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        if (local) go.transform.localPosition = pos; else go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Font BuiltinFont()
    {
        // Unity 2022.2以降は LegacyRuntime.ttf、それ以前は Arial.ttf
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
        t.color = new Color(0.9f, 0.9f, 0.9f);
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

    static void Props(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Refs(SerializedObject so, string prop, params Object[] values)
    {
        var p = so.FindProperty(prop);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void AddToBuildSettings(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>コン、コン、コン…というノック音をその場で合成してwavとして保存する。</summary>
    public static AudioClip CreateKnockClip()
    {
        string path = GenDir + "/Audio/knock.wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing) return existing;
        Directory.CreateDirectory(GenDir + "/Audio");
        const int rate = 44100;
        int length = (int)(rate * 1.0f);
        var samples = new float[length];
        var rnd = new System.Random(7);
        foreach (float start in new[] { 0f, 0.24f, 0.46f })
        {
            int s0 = (int)(start * rate);
            for (int i = 0; i < (int)(0.15f * rate) && s0 + i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 45f);
                float tone = Mathf.Sin(2 * Mathf.PI * 95f * t) * 0.8f + Mathf.Sin(2 * Mathf.PI * 180f * t) * 0.3f;
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-t * 120f);
                samples[s0 + i] += (tone + noise) * env * 0.8f;
            }
        }

        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataSize = length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataSize);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataSize);
            foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }

        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
