#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 城ステージ用の「両開きの城門」をプログラムで作るエディタ拡張。
/// メニュー: Tools > Castle Door Builder
/// ・石の枠(左右の柱+尖りアーチ+要石)
/// ・木の扉2枚(板目テクスチャ自動生成)+鉄の帯・鋲・輪の取っ手
/// ・左右の扉はそれぞれ蝶番で開閉できる(CastleDoubleDoorコンポーネント)
/// 生成物はメッシュ・マテリアル・テクスチャ込みでプレハブとして保存される。
/// 原点 = 扉の下端中央、正面(+Z) = 部屋の内側。
/// このファイルは「Editor」フォルダに、CastleDoubleDoor.cs は Editor 以外のフォルダに置くこと。
/// </summary>
public sealed class CastleDoorBuilder : EditorWindow
{
    [SerializeField] private string prefabName = "CastleDoubleDoor";
    [SerializeField] private string outputFolder = "Assets/Konno/CastleDoor";

    [Header("大きさ(m)")]
    [SerializeField] private float doorWidth = 2.2f;
    [SerializeField] private float straightHeight = 2.6f;
    [SerializeField] private float archRadiusFactor = 0.8f;
    [SerializeField] private float leafThickness = 0.12f;
    [SerializeField] private float centerGap = 0.012f;
    [SerializeField] private int archSegments = 16;

    [Header("石の枠")]
    [SerializeField] private bool buildFrame = true;
    [SerializeField] private float frameWidth = 0.35f;
    [SerializeField] private float frameDepth = 0.5f;
    [SerializeField] private bool keystone = true;
    [SerializeField] private bool darkBacking = true;
    [SerializeField] private Material frameMaterialOverride;
    [SerializeField] private Color stoneColor = new Color(0.62f, 0.6f, 0.57f);

    [Header("木の扉")]
    [SerializeField] private Color woodColor = new Color(0.45f, 0.28f, 0.15f);
    [SerializeField] private int plankCount = 8;
    [SerializeField] private Color ironColor = new Color(0.16f, 0.16f, 0.17f);
    [SerializeField] private int bandCount = 3;
    [SerializeField] private bool studs = true;
    [SerializeField] private bool ringHandles = true;

    [Header("開閉")]
    [SerializeField, Range(0f, 120f)] private float openAngle = 95f;
    [SerializeField] private bool openInward = true;

    [SerializeField] private int seed = 3;

    private Vector2 scroll;

    [MenuItem("Tools/Castle Door Builder")]
    public static void OpenWindow() => GetWindow<CastleDoorBuilder>("Castle Door Builder");

    private void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("保存", EditorStyles.boldLabel);
        Prop(so, "prefabName", "プレハブ名");
        Prop(so, "outputFolder", "保存先フォルダ");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("大きさ(m)", EditorStyles.boldLabel);
        Prop(so, "doorWidth", "扉2枚分の横幅");
        Prop(so, "straightHeight", "アーチが始まる高さ");
        Prop(so, "archRadiusFactor", "アーチの尖り具合(0.5=半円 1=鋭い)");
        Prop(so, "leafThickness", "扉の厚み");
        Prop(so, "centerGap", "扉の合わせ目のすき間");
        Prop(so, "archSegments", "アーチのなめらかさ");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("石の枠", EditorStyles.boldLabel);
        Prop(so, "buildFrame", "枠を作る");
        Prop(so, "frameWidth", "枠の太さ");
        Prop(so, "frameDepth", "枠の奥行き");
        Prop(so, "keystone", "アーチの頂上に要石");
        Prop(so, "darkBacking", "扉の奥に暗い背板(開けたとき奥が空っぽに見えない)");
        Prop(so, "frameMaterialOverride", "枠のマテリアル(任意。パックの壁と同じにすると馴染む)");
        Prop(so, "stoneColor", "石の色(マテリアル未指定時)");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("木の扉", EditorStyles.boldLabel);
        Prop(so, "woodColor", "木の色");
        Prop(so, "plankCount", "板の枚数(2枚分)");
        Prop(so, "ironColor", "鉄の色");
        Prop(so, "bandCount", "鉄の帯の本数");
        Prop(so, "studs", "鋲を打つ");
        Prop(so, "ringHandles", "輪の取っ手");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("開閉", EditorStyles.boldLabel);
        Prop(so, "openAngle", "開く角度");
        Prop(so, "openInward", "部屋の内側に開く");
        Prop(so, "seed", "シード(木目が変わる)");

        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("生成してプレハブ保存", GUILayout.Height(32))) Build();
        EditorGUILayout.HelpBox(
            "保存すると、開いている Castle Stage Generator の「扉」欄に自動で入ります。\n" +
            "シーンに残る見本は、CastleDoubleDoor の「開始時の開き具合」を動かすと開いた形を確認できます。",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private static void Prop(SerializedObject so, string name, string label) =>
        EditorGUILayout.PropertyField(so.FindProperty(name), new GUIContent(label), true);

    // ================================================================= 生成

    private void Build()
    {
        doorWidth = Mathf.Max(0.3f, doorWidth);
        straightHeight = Mathf.Max(0.3f, straightHeight);
        archRadiusFactor = Mathf.Clamp(archRadiusFactor, 0.5f, 2f);
        leafThickness = Mathf.Max(0.01f, leafThickness);
        archSegments = Mathf.Clamp(archSegments, 4, 64);
        plankCount = Mathf.Max(1, plankCount);

        EnsureFolder(outputFolder);
        string meshFolder = outputFolder + "/Meshes";
        EnsureFolder(meshFolder);

        // ---- マテリアル
        Texture2D woodTex = SaveTexture(MakeWoodTexture(), $"{outputFolder}/{prefabName}_Wood.png");
        Material wood = SaveMaterial($"{outputFolder}/{prefabName}_Wood.mat", Color.white, woodTex, 0.15f, 0f);
        Material iron = SaveMaterial($"{outputFolder}/{prefabName}_Iron.mat", ironColor, null, 0.45f, 0.7f);
        Material stone = frameMaterialOverride;
        if (stone == null)
        {
            Texture2D stoneTex = SaveTexture(MakeStoneTexture(), $"{outputFolder}/{prefabName}_Stone.png");
            stone = SaveMaterial($"{outputFolder}/{prefabName}_Stone.mat", Color.white, stoneTex, 0.1f, 0f);
        }

        // ---- ルート
        GameObject old = GameObject.Find(prefabName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject root = new GameObject(prefabName);
        Undo.RegisterCreatedObjectUndo(root, "Castle Door Builder");

        // ---- 扉2枚(蝶番の位置にピボット)
        // 扉板は開く側の面に寄せる(開いたときに石の枠へめり込まないように)
        float half = doorWidth * 0.5f;
        float side = openInward ? 1f : -1f;
        float hingeZ = buildFrame ? side * Mathf.Max(0f, frameDepth * 0.5f - leafThickness * 0.5f - 0.01f) : 0f;
        Transform leftHinge = NewChild(root.transform, "LeftLeaf", new Vector3(-half, 0f, hingeZ));
        Transform rightHinge = NewChild(root.transform, "RightLeaf", new Vector3(half, 0f, hingeZ));

        Mesh leftMesh = SaveMesh(BuildLeafMesh(true), $"{meshFolder}/{prefabName}_LeafL.asset");
        Mesh rightMesh = SaveMesh(BuildLeafMesh(false), $"{meshFolder}/{prefabName}_LeafR.asset");
        AddMesh(leftHinge, "Wood", leftMesh, wood);
        AddMesh(rightHinge, "Wood", rightMesh, wood);

        Mesh torus = ringHandles
            ? SaveMesh(BuildTorus(0.09f, 0.012f, 20, 8), $"{meshFolder}/{prefabName}_Ring.asset")
            : null;
        DecorateLeaf(leftHinge, true, iron, torus);
        DecorateLeaf(rightHinge, false, iron, torus);

        // ---- 石の枠
        if (buildFrame)
        {
            Transform frame = NewChild(root.transform, "Frame", Vector3.zero);
            float jx = half + frameWidth * 0.5f;
            Box(frame, "Jamb_L", new Vector3(-jx, straightHeight * 0.5f, 0f), new Vector3(frameWidth, straightHeight, frameDepth), stone);
            Box(frame, "Jamb_R", new Vector3(jx, straightHeight * 0.5f, 0f), new Vector3(frameWidth, straightHeight, frameDepth), stone);
            // 柱頭と柱の台
            Vector3 cap = new Vector3(frameWidth * 1.3f, 0.12f, frameDepth * 1.12f);
            Box(frame, "Capital_L", new Vector3(-jx, straightHeight - 0.06f, 0f), cap, stone);
            Box(frame, "Capital_R", new Vector3(jx, straightHeight - 0.06f, 0f), cap, stone);
            Box(frame, "Base_L", new Vector3(-jx, 0.08f, 0f), new Vector3(cap.x, 0.16f, cap.z), stone);
            Box(frame, "Base_R", new Vector3(jx, 0.08f, 0f), new Vector3(cap.x, 0.16f, cap.z), stone);

            Mesh arch = SaveMesh(BuildArchRing(), $"{meshFolder}/{prefabName}_Arch.asset");
            AddMesh(frame, "Arch", arch, stone);

            if (keystone)
            {
                float apex = ArchApexY(ArchRadius() + frameWidth, 0f);
                Box(frame, "Keystone", new Vector3(0f, apex - frameWidth * 0.45f, 0f),
                    new Vector3(frameWidth * 0.75f, frameWidth * 1.2f, frameDepth * 1.1f), stone);
            }
            if (darkBacking)
            {
                Material dark = SaveMaterial($"{outputFolder}/{prefabName}_Dark.mat",
                    new Color(0.05f, 0.04f, 0.035f), null, 0f, 0f);
                Mesh back = SaveMesh(BuildBackingMesh(), $"{meshFolder}/{prefabName}_Backing.asset");
                Transform b = AddMesh(frame, "DarkBacking", back, dark);
                b.localPosition = new Vector3(0f, 0f, -side * (frameDepth * 0.5f - 0.01f));
            }
            // 敷居
            Box(frame, "Threshold", new Vector3(0f, 0.015f, 0f),
                new Vector3(doorWidth + frameWidth * 2f, 0.03f, frameDepth), stone);
        }

        // ---- 開閉コンポーネント(既存の CastleDoubleDoor を使う)
        CastleDoubleDoor door = root.AddComponent<CastleDoubleDoor>();
        door.Setup(leftHinge, rightHinge);
        SerializedObject dso = new SerializedObject(door);
        dso.FindProperty("openAngle").floatValue = Mathf.Clamp(openAngle, 0f, 120f);
        dso.FindProperty("openInward").boolValue = openInward;
        dso.ApplyModifiedPropertiesWithoutUndo();

        // ---- 保存
        AssetDatabase.SaveAssets();
        string prefabPath = $"{outputFolder}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
        Selection.activeGameObject = root;
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUIUtility.PingObject(saved);

        // 開いている Castle Stage Generator の「扉」欄に入れる
        foreach (CastleStageGenerator gen in Resources.FindObjectsOfTypeAll<CastleStageGenerator>())
            gen.SetDoorPrefab(saved);
        Debug.Log($"Castle Door Builder: 保存しました {prefabPath}(外枠の幅 {doorWidth + (buildFrame ? frameWidth * 2f : 0f):0.00}m)", root);
    }

    // ================================================================= 形の計算

    // アーチの半径。尖りアーチは「反対側の起点」付近を中心とする2つの円弧でできている。
    private float ArchRadius() => doorWidth * archRadiusFactor;

    // 半径rの左円弧上で、x の位置の高さ
    private float ArchApexY(float r, float x)
    {
        float cx = -doorWidth * 0.5f + ArchRadius();
        float dx = x - cx;
        return straightHeight + Mathf.Sqrt(Mathf.Max(0f, r * r - dx * dx));
    }

    // 左円弧(半径r)を、起点側(左端)から x=endX まで点列にする
    private List<Vector2> LeftArc(float r, float endX)
    {
        float cx = -doorWidth * 0.5f + ArchRadius();
        float start = Mathf.PI;                                            // 左端
        float end = Mathf.Acos(Mathf.Clamp((endX - cx) / r, -1f, 1f));   // x=endX の角度
        List<Vector2> pts = new List<Vector2>();
        for (int i = 0; i <= archSegments; i++)
        {
            float a = Mathf.Lerp(start, end, i / (float)archSegments);
            pts.Add(new Vector2(cx + Mathf.Cos(a) * r, straightHeight + Mathf.Sin(a) * r));
        }
        return pts;
    }

    // ================================================================= メッシュ

    // 扉1枚分。左扉は蝶番(-half)を原点、右扉は(+half)を原点にした座標で作る。
    private Mesh BuildLeafMesh(bool left)
    {
        float half = doorWidth * 0.5f;
        float inner = -centerGap * 0.5f; // 左扉の合わせ目側のX
        List<Vector2> arc = LeftArc(ArchRadius(), inner);

        // 左扉の外形(反時計回り): 下辺 → 合わせ目 → 頂上 → 円弧を下って起点 → 左辺
        List<Vector2> poly = new List<Vector2>
        {
            new Vector2(-half, 0f),
            new Vector2(inner, 0f),
        };
        for (int i = arc.Count - 1; i >= 0; i--) poly.Add(arc[i]);

        if (!left)
            for (int i = 0; i < poly.Count; i++) poly[i] = new Vector2(-poly[i].x, poly[i].y);

        float pivotX = left ? -half : half;
        MeshBuilder mb = new MeshBuilder();
        mb.Extrude(poly, leafThickness, new Vector2(-pivotX, 0f),
            p => new Vector2((p.x + half) / doorWidth, p.y / (doorWidth * 2f)));
        return mb.ToMesh(left ? "LeafL" : "LeafR");
    }

    // 扉2枚分の外形(尖りアーチ)の薄い板。開けたときの奥の暗がりに使う
    private Mesh BuildBackingMesh()
    {
        float half = doorWidth * 0.5f;
        List<Vector2> arc = LeftArc(ArchRadius(), 0f); // 左端 → 頂上
        List<Vector2> poly = new List<Vector2> { new Vector2(-half, 0f), new Vector2(half, 0f) };
        for (int i = 0; i < arc.Count; i++) poly.Add(new Vector2(-arc[i].x, arc[i].y));      // 右端 → 頂上
        for (int i = arc.Count - 2; i >= 0; i--) poly.Add(arc[i]);                          // 頂上 → 左端
        MeshBuilder mb = new MeshBuilder();
        mb.Extrude(poly, 0.01f, Vector2.zero, p => p * 0.5f);
        return mb.ToMesh("Backing");
    }

    // 石のアーチ(内側の円弧と外側の円弧の間を、奥行き方向に押し出したもの)
    private Mesh BuildArchRing()
    {
        float r = ArchRadius();
        float R = r + frameWidth;
        float d = frameDepth * 0.5f;
        MeshBuilder mb = new MeshBuilder();

        for (int side = 0; side < 2; side++)
        {
            float mirror = side == 0 ? 1f : -1f;
            List<Vector2> innerArc = LeftArc(r, 0f);
            List<Vector2> outerArc = LeftArc(R, 0f);
            // 外側の弧は柱の外側(-half - frameWidth)から始まるよう、中心は内側と共通

            for (int i = 0; i < archSegments; i++)
            {
                Vector3 a0 = P(innerArc[i], mirror), a1 = P(innerArc[i + 1], mirror);
                Vector3 b0 = P(outerArc[i], mirror), b1 = P(outerArc[i + 1], mirror);
                Vector3 f = Vector3.forward * d;
                Vector3 mid = (a0 + a1 + b0 + b1) * 0.25f;
                Vector3 center = new Vector3((-doorWidth * 0.5f + r) * mirror, straightHeight, 0f);

                // 正面・背面
                mb.Quad(a0 + f, a1 + f, b1 + f, b0 + f, Vector3.forward, UV(a0), UV(a1), UV(b1), UV(b0));
                mb.Quad(a0 - f, a1 - f, b1 - f, b0 - f, Vector3.back, UV(a0), UV(a1), UV(b1), UV(b0));
                // 内側(扉側)の面・外側の面
                Vector3 inN = (center - (a0 + a1) * 0.5f); inN.z = 0f;
                Vector3 outN = ((b0 + b1) * 0.5f - center); outN.z = 0f;
                mb.Quad(a0 - f, a1 - f, a1 + f, a0 + f, inN.normalized, new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(0.2f, 0.3f), new Vector2(0, 0.3f));
                mb.Quad(b0 - f, b1 - f, b1 + f, b0 + f, outN.normalized, new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(0.2f, 0.3f), new Vector2(0, 0.3f));
            }
        }
        return mb.ToMesh("ArchRing");

        Vector3 P(Vector2 v, float m) => new Vector3(v.x * m, v.y, 0f);
        Vector2 UV(Vector3 v) => new Vector2(v.x * 0.5f, v.y * 0.5f);
    }

    // 輪の取っ手(XY平面の輪、Z方向が厚み)
    private static Mesh BuildTorus(float major, float minor, int segU, int segV)
    {
        MeshBuilder mb = new MeshBuilder();
        for (int i = 0; i < segU; i++)
        {
            for (int j = 0; j < segV; j++)
            {
                Vector3 p00 = T(i, j), p10 = T(i + 1, j), p11 = T(i + 1, j + 1), p01 = T(i, j + 1);
                Vector3 c = TubeCenter(i + 0.5f);
                Vector3 n = ((p00 + p10 + p11 + p01) * 0.25f - c).normalized;
                mb.Quad(p00, p10, p11, p01, n, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }
        Mesh m = mb.ToMesh("Ring");
        m.RecalculateNormals();
        return m;

        Vector3 TubeCenter(float i)
        {
            float u = i / segU * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(u) * major, Mathf.Sin(u) * major, 0f);
        }
        Vector3 T(int i, int j)
        {
            float u = i / (float)segU * Mathf.PI * 2f;
            float v = j / (float)segV * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
            return dir * (major + Mathf.Cos(v) * minor) + Vector3.forward * (Mathf.Sin(v) * minor);
        }
    }

    // 鉄の帯・鋲・取っ手を扉の子として付ける(扉と一緒に回る)
    private void DecorateLeaf(Transform hinge, bool left, Material iron, Mesh torus)
    {
        float leafW = doorWidth * 0.5f - centerGap * 0.5f;
        float sign = left ? 1f : -1f;              // 蝶番から合わせ目へ向かう向き
        float centerX = leafW * 0.5f * sign;
        float frontZ = leafThickness * 0.5f;

        for (int b = 0; b < bandCount; b++)
        {
            float y = straightHeight * Mathf.Lerp(0.14f, 0.86f, bandCount == 1 ? 0.5f : b / (bandCount - 1f));
            Box(hinge, $"Band{b}", new Vector3(centerX, y, 0f),
                new Vector3(leafW * 0.96f, 0.07f, leafThickness + 0.02f), iron);

            if (!studs) continue;
            int n = Mathf.Max(2, Mathf.FloorToInt(leafW / 0.16f));
            for (int s = 0; s < n; s++)
            {
                float x = Mathf.Lerp(0.08f, leafW - 0.08f, s / (n - 1f)) * sign;
                Sphere(hinge, "Stud", new Vector3(x, y, frontZ + 0.012f), 0.035f, iron);
            }
        }

        if (torus != null)
        {
            float hx = (leafW - 0.2f) * sign;
            float hy = straightHeight * 0.45f;
            // 座金(丸い板)と輪
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Prepare(plate, hinge, "HandlePlate", iron);
            plate.transform.localPosition = new Vector3(hx, hy + 0.08f, frontZ + 0.012f);
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.localScale = new Vector3(0.11f, 0.012f, 0.11f);

            Transform ring = AddMesh(hinge, "HandleRing", torus, iron);
            ring.localPosition = new Vector3(hx, hy, frontZ + 0.03f);
            ring.localRotation = Quaternion.Euler(15f, 0f, 0f);
        }
    }

    // ================================================================= テクスチャ

    private Texture2D MakeWoodTexture()
    {
        const int w = 512, h = 1024;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        System.Random rng = new System.Random(seed);
        float[] plankTone = new float[plankCount];
        float[] plankOffset = new float[plankCount];
        for (int i = 0; i < plankCount; i++)
        {
            plankTone[i] = 0.85f + (float)rng.NextDouble() * 0.3f;
            plankOffset[i] = (float)rng.NextDouble() * 100f;
        }

        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)w * plankCount;
                int p = Mathf.Min(plankCount - 1, Mathf.FloorToInt(fx));
                float local = fx - p;
                // 木目: 縦に長いノイズ + 細かい筋
                float grain = Mathf.PerlinNoise(local * 6f + plankOffset[p], y * 0.006f + plankOffset[p]);
                float fine = Mathf.PerlinNoise(x * 0.35f, y * 0.01f + plankOffset[p]) * 0.35f;
                float tone = plankTone[p] * (0.78f + grain * 0.32f + fine * 0.2f);
                // 板の継ぎ目(暗い溝)
                float edge = Mathf.Min(local, 1f - local);
                if (edge < 0.025f) tone *= 0.35f;
                else if (edge < 0.05f) tone *= 0.75f;
                Color c = woodColor * tone;
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private Texture2D MakeStoneTexture()
    {
        const int w = 512, h = 512;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        Color[] px = new Color[w * h];
        const int rows = 4;
        for (int y = 0; y < h; y++)
        {
            int row = y * rows / h;
            float ly = (y * rows / (float)h) - row;
            for (int x = 0; x < w; x++)
            {
                // 段ごとに半分ずらした石積み
                float fx = x / (float)w * 2f + (row % 2) * 0.5f;
                float lx = fx - Mathf.Floor(fx);
                int block = Mathf.FloorToInt(fx) + row * 7;
                float n = Mathf.PerlinNoise(x * 0.04f + block, y * 0.04f);
                float tone = 0.82f + n * 0.25f + (block * 37 % 10) * 0.012f;
                if (ly < 0.035f || ly > 0.965f || lx < 0.02f || lx > 0.98f) tone *= 0.55f;
                Color c = stoneColor * tone;
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ================================================================= 保存・補助

    private static Texture2D SaveTexture(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material SaveMaterial(string path, Color color, Texture2D tex, float smoothness, float metallic)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Transform NewChild(Transform parent, string name, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    private static Transform AddMesh(Transform parent, string name, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    private static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Prepare(go, parent, name, mat);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
    }

    private static void Sphere(Transform parent, string name, Vector3 pos, float size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Prepare(go, parent, name, mat);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size, size, size * 0.6f);
    }

    private static void Prepare(GameObject go, Transform parent, string name, Material mat)
    {
        go.name = name;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }

    // ================================================================= メッシュ組み立て用

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();

        // 面の向き(normal)に合わせて三角形の巡回順を自動で決める
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            for (int k = 0; k < 4; k++) normals.Add(normal);
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
            if (flip) tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        /// <summary>凸多角形をZ方向に厚みtで押し出す。offsetは全頂点に足す移動量。</summary>
        public void Extrude(List<Vector2> poly, float t, Vector2 offset, System.Func<Vector2, Vector2> uvOf)
        {
            float hz = t * 0.5f;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in poly) centroid += p;
            centroid /= poly.Count;

            // 正面・背面(重心から扇形に分割)
            foreach (float z in new[] { hz, -hz })
            {
                Vector3 n = z > 0 ? Vector3.forward : Vector3.back;
                int baseIndex = verts.Count;
                verts.Add(V(centroid, z)); uvs.Add(uvOf(centroid)); normals.Add(n);
                foreach (Vector2 p in poly) { verts.Add(V(p, z)); uvs.Add(uvOf(p)); normals.Add(n); }
                for (int i = 0; i < poly.Count; i++)
                {
                    int a = baseIndex, b = baseIndex + 1 + i, c = baseIndex + 1 + (i + 1) % poly.Count;
                    bool flip = Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), n) < 0f;
                    if (flip) tris.AddRange(new[] { a, c, b }); else tris.AddRange(new[] { a, b, c });
                }
            }

            // 側面
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                Vector2 e = q - p;
                Vector2 n2 = new Vector2(e.y, -e.x).normalized;
                if (Vector2.Dot(n2, (p + q) * 0.5f - centroid) < 0f) n2 = -n2;
                float len = e.magnitude;
                Quad(V(p, hz), V(q, hz), V(q, -hz), V(p, -hz), new Vector3(n2.x, n2.y, 0f),
                    new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 0.1f), new Vector2(0, 0.1f));
            }

            Vector3 V(Vector2 v, float z) => new Vector3(v.x + offset.x, v.y + offset.y, z);
        }

        public Mesh ToMesh(string name)
        {
            Mesh m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
#endif
