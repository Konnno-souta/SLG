#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SRPG風の草原ステージ用にTerrainを自動で仕上げるエディタ拡張 (v2)。
/// メニュー: Tools > Terrain Finisher
/// ・サイズ合わせ / 盤面は平ら、余白にだけ丘
/// ・明るい草(2トーン)と明るい土のレイヤーを自動作成(元のレイヤーは書き換えない)
/// ・蛇行する道、草の色ムラ、土ムラ
/// ・森マス(木のかたまり)と、余白の木・岩の配置
/// このファイルは「Editor」という名前のフォルダの中に置くこと。
/// </summary>
public sealed class TerrainFinisher : EditorWindow
{
    private const string PropsName = "TF_Props";
    private const string Suffix = "_TF";

    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainLayer grassLayer;
    [SerializeField] private TerrainLayer soilLayer;

    // BattleGrid / StageConfig.csv に合わせる値
    [SerializeField] private int gridWidth = 8;
    [SerializeField] private int gridHeight = 8;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private float margin = 10f;

    // 形
    [SerializeField] private bool resize = true;
    [SerializeField] private bool shapeHeights = true;
    [SerializeField] private float hillHeight = 3f;
    [SerializeField] private bool reposition = false;

    // 色
    [SerializeField] private bool brighten = true;
    [SerializeField] private string outputFolder = "Assets/Konno/TerrainLayers";
    [SerializeField] private Color grassTint = new Color(1.5f, 1.9f, 0.9f);
    [SerializeField] private Color grassLightTint = new Color(1.9f, 2.2f, 1.05f);
    [SerializeField] private Color soilTint = new Color(1.5f, 1.35f, 1.1f);
    [SerializeField] private float normalScaleMul = 0.4f;

    // 塗り
    [SerializeField] private bool drawRoad = true;
    [SerializeField] private float roadWidthCells = 0.9f;
    [SerializeField] private float roadMeander = 0.15f;
    [SerializeField] private float lightPatchStrength = 0.6f;
    [SerializeField] private float soilPatchStrength = 0.15f;
    [SerializeField] private float patchScale = 6f;
    [SerializeField] private int seed = 1;

    // 小物
    [SerializeField] private List<GameObject> treePrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> rockPrefabs = new List<GameObject>();
    [SerializeField] private int forestCells = 5;
    [SerializeField] private int marginProps = 40;
    [SerializeField] private float propScale = 1f;

    private Vector2 scroll;

    [MenuItem("Tools/Terrain Finisher")]
    private static void Open() => GetWindow<TerrainFinisher>("Terrain Finisher");

    private void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("対象", EditorStyles.boldLabel);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        if (GUILayout.Button("選択中のTerrainとそのレイヤーを取り込む")) PickFromSelection();
        grassLayer = (TerrainLayer)EditorGUILayout.ObjectField("草レイヤー(元)", grassLayer, typeof(TerrainLayer), false);
        soilLayer = (TerrainLayer)EditorGUILayout.ObjectField("土レイヤー(元)", soilLayer, typeof(TerrainLayer), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("グリッド(StageConfig / BattleGrid と同じ値)", EditorStyles.boldLabel);
        gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Width(マス)", gridWidth));
        gridHeight = Mathf.Max(1, EditorGUILayout.IntField("Height(マス)", gridHeight));
        cellSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("Cell Size", cellSize));
        margin = Mathf.Max(0f, EditorGUILayout.FloatField("余白(m)", margin));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("形", EditorStyles.boldLabel);
        resize = EditorGUILayout.Toggle("サイズを合わせる", resize);
        shapeHeights = EditorGUILayout.Toggle("盤面を平らに/余白に丘", shapeHeights);
        using (new EditorGUI.DisabledScope(!shapeHeights))
            hillHeight = EditorGUILayout.Slider("丘の高さ(m) 0で完全に平ら", hillHeight, 0f, 15f);
        reposition = EditorGUILayout.Toggle("位置を(-余白,0,-余白)に", reposition);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("色(明るいレイヤーを別ファイルで作成)", EditorStyles.boldLabel);
        brighten = EditorGUILayout.Toggle("明るくする", brighten);
        using (new EditorGUI.DisabledScope(!brighten))
        {
            outputFolder = EditorGUILayout.TextField("保存先フォルダ", outputFolder);
            grassTint = EditorGUILayout.ColorField(new GUIContent("草の色(乗算)"), grassTint, true, false, true);
            grassLightTint = EditorGUILayout.ColorField(new GUIContent("明るい草の色(乗算)"), grassLightTint, true, false, true);
            soilTint = EditorGUILayout.ColorField(new GUIContent("土の色(乗算)"), soilTint, true, false, true);
            normalScaleMul = EditorGUILayout.Slider("凹凸の強さ(倍率)", normalScaleMul, 0f, 1f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("塗り", EditorStyles.boldLabel);
        drawRoad = EditorGUILayout.Toggle("道を描く", drawRoad);
        using (new EditorGUI.DisabledScope(!drawRoad))
        {
            roadWidthCells = EditorGUILayout.Slider("道幅(マス)", roadWidthCells, 0.5f, 4f);
            roadMeander = EditorGUILayout.Slider("道の蛇行", roadMeander, 0f, 0.4f);
        }
        lightPatchStrength = EditorGUILayout.Slider("明るい草ムラの濃さ", lightPatchStrength, 0f, 1f);
        soilPatchStrength = EditorGUILayout.Slider("土ムラの濃さ", soilPatchStrength, 0f, 1f);
        patchScale = EditorGUILayout.Slider("ムラの大きさ(m)", patchScale, 1f, 30f);
        seed = EditorGUILayout.IntField("シード(変えると模様が変わる)", seed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("小物(空なら配置しない)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("treePrefabs"), new GUIContent("木のプレハブ"), true);
        EditorGUILayout.PropertyField(so.FindProperty("rockPrefabs"), new GUIContent("岩のプレハブ"), true);
        so.ApplyModifiedProperties();
        forestCells = Mathf.Max(0, EditorGUILayout.IntField("森マスの数", forestCells));
        marginProps = Mathf.Max(0, EditorGUILayout.IntField("余白に置く数", marginProps));
        propScale = Mathf.Max(0.01f, EditorGUILayout.FloatField("小物の大きさ(倍率)", propScale));

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "実行すると今の塗り・高さ・前回配置した小物は上書きされます。先にシーンを保存してください。",
            MessageType.Warning);

        using (new EditorGUI.DisabledScope(terrain == null || grassLayer == null || soilLayer == null))
        {
            if (GUILayout.Button("生成", GUILayout.Height(32))) Generate();
        }
        EditorGUILayout.EndScrollView();
    }

    private void PickFromSelection()
    {
        GameObject go = Selection.activeGameObject;
        Terrain picked = go != null ? go.GetComponent<Terrain>() : null;
        if (picked == null)
        {
            EditorUtility.DisplayDialog("Terrain Finisher", "HierarchyでTerrainを選択してください。", "OK");
            return;
        }
        terrain = picked;
        TerrainLayer[] layers = terrain.terrainData.terrainLayers;
        if (layers.Length > 0) grassLayer = layers[0];
        if (layers.Length > 1) soilLayer = layers[layers.Length > 2 ? 2 : 1];
        // 前回の生成後は [草, 明るい草, 土] の順なので、3枚あるときは3枚目を土として扱う。
        // 違っていたら手で入れ直すこと。
    }

    // ------------------------------------------------------------------ 生成

    private void Generate()
    {
        TerrainData td = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Terrain Finisher");

        if (resize)
        {
            td.size = new Vector3(
                gridWidth * cellSize + margin * 2f,
                Mathf.Max(td.size.y, hillHeight * 2f, 1f),
                gridHeight * cellSize + margin * 2f);
        }
        Vector3 size = td.size;
        float offset = seed * 13.37f;

        if (shapeHeights) BuildHeights(td, size, offset);

        if (reposition)
        {
            Undo.RecordObject(terrain.transform, "Terrain Finisher");
            terrain.transform.localPosition = new Vector3(-margin, 0f, -margin);
        }

        // レイヤー: 0=草, 1=明るい草(明るくする場合のみ), 最後=土
        TerrainLayer grassA = grassLayer;
        TerrainLayer grassB = null;
        TerrainLayer soil = soilLayer;
        if (brighten)
        {
            EnsureFolder(outputFolder);
            grassA = MakeTinted(grassLayer, "Grass", grassTint);
            grassB = MakeTinted(grassLayer, "GrassLight", grassLightTint);
            soil = MakeTinted(soilLayer, "Soil", soilTint);
            AssetDatabase.SaveAssets();
        }
        bool twoTone = grassB != null;
        td.terrainLayers = twoTone ? new[] { grassA, grassB, soil } : new[] { grassA, soil };
        int soilIndex = twoTone ? 2 : 1;
        Undo.RegisterCompleteObjectUndo(td.alphamapTextures, "Terrain Finisher");

        PaintSplat(td, size, offset, twoTone, soilIndex);

        terrain.Flush();
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(terrain);

        string forestLog = ScatterProps(size);

        Debug.Log($"Terrain Finisher: 生成完了 size=({size.x}, {size.z}) " +
                  $"盤面={gridWidth}x{gridHeight} cell={cellSize} 余白={margin}{forestLog}", terrain);
    }

    // 盤面とその周囲2mは高さ0。そこから外へ向かってなだらかな丘にする。
    private void BuildHeights(TerrainData td, Vector3 size, float offset)
    {
        int res = td.heightmapResolution;
        float[,] heights = new float[res, res];
        if (hillHeight > 0f && margin > 2f)
        {
            for (int iz = 0; iz < res; iz++)
            {
                for (int ix = 0; ix < res; ix++)
                {
                    float x = ix / (res - 1f) * size.x;
                    float z = iz / (res - 1f) * size.z;
                    float dOut = DistanceOutsideBoard(x, z, size);
                    float ramp = Smooth(2f, margin, dOut);
                    float n = Mathf.PerlinNoise(x / 12f + offset, z / 12f - offset);
                    float h = ramp * (0.35f + 0.65f * n) * hillHeight;
                    heights[iz, ix] = Mathf.Clamp01(h / size.y);
                }
            }
        }
        td.SetHeights(0, 0, heights);
    }

    private void PaintSplat(TerrainData td, Vector3 size, float offset, bool twoTone, int soilIndex)
    {
        int aw = td.alphamapWidth;
        int ah = td.alphamapHeight;
        float[,,] map = new float[ah, aw, td.alphamapLayers];

        for (int iz = 0; iz < ah; iz++)
        {
            for (int ix = 0; ix < aw; ix++)
            {
                float x = (ix + 0.5f) / aw * size.x;
                float z = (iz + 0.5f) / ah * size.z;
                float gx = (x - margin) / cellSize;
                float gz = (z - margin) / cellSize;

                float road = 0f;
                if (drawRoad)
                {
                    float ragged = (Mathf.PerlinNoise(x * 0.5f + offset, z * 0.5f + offset) - 0.5f) * 0.3f;
                    float d = Mathf.Abs(gz - RoadCenterZ(gx)) + ragged;
                    float half = roadWidthCells * 0.5f;
                    road = 1f - Smooth(half - 0.2f, half + 0.2f, d);
                }

                float n1 = Mathf.PerlinNoise(x / patchScale + offset, z / patchScale + offset);
                float soilPatch = Smooth(0.6f, 0.85f, n1) * soilPatchStrength;
                float soil = Mathf.Max(road, soilPatch);

                float light = 0f;
                if (twoTone)
                {
                    float s = patchScale * 1.8f;
                    float n2 = Mathf.PerlinNoise(x / s - offset, z / s + offset * 0.5f);
                    light = Smooth(0.4f, 0.7f, n2) * lightPatchStrength * (1f - soil);
                }

                float grass = Mathf.Clamp01(1f - soil - light);
                float sum = grass + soil + light;
                map[iz, ix, 0] = grass / sum;
                if (twoTone) map[iz, ix, 1] = light / sum;
                map[iz, ix, soilIndex] = soil / sum;
            }
        }
        td.SetAlphamaps(0, 0, map);
    }

    // 森マス(盤面内の木のかたまり)と、余白の木・岩を置く。戻り値はログ用の文字列。
    private string ScatterProps(Vector3 size)
    {
        Transform old = terrain.transform.Find(PropsName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        treePrefabs.RemoveAll(p => p == null);
        rockPrefabs.RemoveAll(p => p == null);
        if (treePrefabs.Count == 0 && rockPrefabs.Count == 0) return "";

        GameObject parent = new GameObject(PropsName);
        Undo.RegisterCreatedObjectUndo(parent, "Terrain Finisher");
        parent.transform.SetParent(terrain.transform, false);

        System.Random rng = new System.Random(seed);
        StringBuilder log = new StringBuilder();

        // 森マス
        if (treePrefabs.Count > 0 && forestCells > 0)
        {
            HashSet<Vector2Int> used = new HashSet<Vector2Int>();
            int target = Mathf.Min(forestCells, gridWidth * gridHeight);
            for (int attempt = 0; attempt < target * 30 && used.Count < target; attempt++)
            {
                Vector2Int cell = new Vector2Int(rng.Next(gridWidth), rng.Next(gridHeight));
                if (used.Contains(cell)) continue;
                if (drawRoad &&
                    Mathf.Abs(cell.y + 0.5f - RoadCenterZ(cell.x + 0.5f)) < roadWidthCells * 0.5f + 0.6f)
                    continue;
                used.Add(cell);

                int count = 2 + rng.Next(2);
                for (int i = 0; i < count; i++)
                {
                    float lx = margin + (cell.x + 0.2f + 0.6f * Rand(rng)) * cellSize;
                    float lz = margin + (cell.y + 0.2f + 0.6f * Rand(rng)) * cellSize;
                    Place(treePrefabs[rng.Next(treePrefabs.Count)], lx, lz, parent.transform, rng);
                }
            }
            if (used.Count > 0)
            {
                log.Append(" 森マス(グリッド座標)=");
                foreach (Vector2Int c in used) log.Append($"({c.x},{c.y}) ");
            }
        }

        // 余白(盤面の外1m以遠)
        if (margin > 1.5f)
        {
            int placed = 0;
            for (int attempt = 0; attempt < marginProps * 20 && placed < marginProps; attempt++)
            {
                float lx = Rand(rng) * size.x;
                float lz = Rand(rng) * size.z;
                if (DistanceOutsideBoard(lx, lz, size) < 1f) continue;

                bool useRock = rockPrefabs.Count > 0 && (treePrefabs.Count == 0 || Rand(rng) < 0.25f);
                List<GameObject> list = useRock ? rockPrefabs : treePrefabs;
                Place(list[rng.Next(list.Count)], lx, lz, parent.transform, rng);
                placed++;
            }
        }
        return log.ToString();
    }

    private void Place(GameObject prefab, float localX, float localZ, Transform parent, System.Random rng)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 world = origin + new Vector3(localX, 0f, localZ);
        world.y = origin.y + terrain.SampleHeight(world);

        GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (go == null) go = Instantiate(prefab);
        go.transform.SetParent(parent, true);
        go.transform.position = world;
        go.transform.rotation = Quaternion.Euler(0f, Rand(rng) * 360f, 0f);
        go.transform.localScale = prefab.transform.localScale * propScale * (0.85f + 0.3f * Rand(rng));
    }

    // ------------------------------------------------------------------ レイヤー

    // 元のレイヤーをコピーし、色を乗算した別ファイルを作る(元は書き換えない)。
    private TerrainLayer MakeTinted(TerrainLayer src, string kind, Color tint)
    {
        if (src.name.Contains(Suffix))
        {
            FindOriginal(src);
            return src; // 生成済みレイヤーはそのまま使う(二重に色を掛けない)
        }

        string path = $"{outputFolder}/{src.name}{Suffix}{kind}.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = src.diffuseTexture;
        layer.normalMapTexture = src.normalMapTexture;
        layer.maskMapTexture = src.maskMapTexture;
        layer.tileSize = src.tileSize;
        layer.tileOffset = src.tileOffset;
        layer.specular = src.specular;
        layer.metallic = src.metallic;
        layer.smoothness = src.smoothness;
        layer.diffuseRemapMin = src.diffuseRemapMin;
        layer.maskMapRemapMin = src.maskMapRemapMin;
        layer.maskMapRemapMax = src.maskMapRemapMax;
        // Color Tint は diffuseRemapMax の rgb。w は元の設定を保つ。
        layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, src.diffuseRemapMax.w);
        layer.normalScale = src.normalScale * normalScaleMul;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    // 生成済みレイヤーが入力に入っていた場合は使えないので、警告だけ出す。
    private TerrainLayer FindOriginal(TerrainLayer generated)
    {
        Debug.LogWarning(
            $"Terrain Finisher: 「{generated.name}」は生成済みレイヤーです。" +
            "色を正しく付け直すには、草・土の欄に元のレイヤー(パック内のもの)を入れてください。", generated);
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }

    // ------------------------------------------------------------------ 補助

    // 道の中心(マス単位のZ)。盤面をX方向に横断し、ゆるく蛇行する。
    private float RoadCenterZ(float gx) =>
        gridHeight * 0.5f + Mathf.Sin(gx / gridWidth * Mathf.PI * 2f + seed) * gridHeight * roadMeander;

    // 盤面の矩形から外側へ何m離れているか(盤面内は0)。x,zはTerrainローカル座標。
    private float DistanceOutsideBoard(float x, float z, Vector3 size)
    {
        float dx = Mathf.Max(margin - x, x - (size.x - margin), 0f);
        float dz = Mathf.Max(margin - z, z - (size.z - margin), 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float Rand(System.Random rng) => (float)rng.NextDouble();

    // GLSLのsmoothstep相当(a以下で0、b以上で1)
    private static float Smooth(float a, float b, float v)
    {
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}
#endif