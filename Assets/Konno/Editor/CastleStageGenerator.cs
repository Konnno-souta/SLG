#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SRPG風の城(屋内)ステージを、床・壁・絨毯・柱のプレハブから自動で組み立てるエディタ拡張。
/// メニュー: Tools > Castle Stage Generator
/// ・プレハブの大きさを自動で測って1マスに合わせる(パックの寸法が分からなくても使える)
/// ・床の明るさムラ(3段階)、中央の赤い絨毯、壁、四隅の柱、扉、壁際の小物
/// ・「プレハブとして保存」で Assets/Resources/Stages/{名前}.prefab を作る(BattleStageLoaderが読む)
/// このファイルは「Editor」という名前のフォルダの中に置くこと。
/// 生成の基準点(ルートの位置)= マス(0,0)の角。ルートは(0,0,0)に置いたまま使う。
/// </summary>
public sealed class CastleStageGenerator : EditorWindow
{
    private const string MatSuffix = "_CS";

    // ------------------------------------------------------------ 設定
    [SerializeField] private string rootName = "3D10";

    [SerializeField] private int gridWidth = 20;
    [SerializeField] private int gridHeight = 30;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int outerFloorCells = 2;
    [SerializeField] private float floorTop = -0.03f;

    [SerializeField] private List<GameObject> floorPrefabs = new List<GameObject>();
    [SerializeField] private bool autoFitFloor = true;
    [SerializeField] private bool randomRotateFloor = true;

    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private int wallSegmentCells = 2;
    [SerializeField] private float wallHeightMul = 1f;
    [SerializeField] private bool flipWall = false;
    [SerializeField] private bool wallNorth = true;
    [SerializeField] private bool wallSouth = false;
    [SerializeField] private bool wallEast = true;
    [SerializeField] private bool wallWest = true;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private float doorOpeningCells = 2f;
    [SerializeField] private float doorSideGap = 0f;
    [SerializeField] private bool doorFitWallHeight = true;
    [SerializeField] private float doorHeightMul = 1f;
    [SerializeField] private bool doorFitWallThickness = true;
    [SerializeField] private float doorThicknessMul = 1f;
    [SerializeField, Range(-1f, 1f)] private float doorDepthAlign = 0f;
    [SerializeField] private float doorOffset = 0f;


    [SerializeField] private GameObject pillarPrefab;
    [SerializeField] private float pillarSizeCells = 0.8f;
    [SerializeField] private bool cornerPillars = true;
    [SerializeField] private bool interiorPillars = false;
    [SerializeField] private int pillarSpacing = 4;
    [SerializeField] private int pillarDistanceFromCarpet = 2;

    [SerializeField] private bool carpet = true;
    [SerializeField] private int carpetWidthCells = 2;
    [SerializeField] private Color carpetColor = new Color(0.55f, 0.07f, 0.08f);
    [SerializeField] private Color carpetBorderColor = new Color(0.85f, 0.65f, 0.2f);

    [SerializeField] private List<GameObject> wallProps = new List<GameObject>();
    [SerializeField] private int propEverySegments = 2;
    [SerializeField] private float propHeight = 0f;
    [SerializeField] private float propScale = 1f;

    [SerializeField] private bool tint = true;
    [SerializeField] private Color floorTint = new Color(1.2f, 1.15f, 1.05f);
    [SerializeField] private Color wallTint = new Color(1.1f, 1.05f, 1f);
    [SerializeField] private float floorVariation = 0.12f;
    [SerializeField] private float variationScale = 5f;
    [SerializeField] private string outputFolder = "Assets/Konno/CastleMaterials";

    [SerializeField] private bool removeColliders = true;
    [SerializeField] private int seed = 1;

    private Vector2 scroll;
    private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    /// <summary>Castle Door Builder が、作った城門を「扉」欄に入れるために呼ぶ。</summary>
    public void SetDoorPrefab(GameObject prefab)
    {
        doorPrefab = prefab;
        Repaint();
    }

    [MenuItem("Tools/Castle Stage Generator")]
    private static void Open() => GetWindow<CastleStageGenerator>("Castle Stage Generator");

    // ------------------------------------------------------------ GUI

    private void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        Header("ステージ");
        Prop(so, "rootName", "プレハブ名(CSVの3D_Name)");
        Prop(so, "gridWidth", "Width(マス)");
        Prop(so, "gridHeight", "Height(マス)");
        Prop(so, "cellSize", "Cell Size");
        Prop(so, "outerFloorCells", "盤面の外の床(マス)");
        Prop(so, "floorTop", "床の上面の高さ(m)");

        Header("床");
        Prop(so, "floorPrefabs", "床タイルのプレハブ(複数可)");
        Prop(so, "autoFitFloor", "1マスの大きさに自動で合わせる");
        Prop(so, "randomRotateFloor", "90度単位でランダムに回す");

        Header("壁(南=カメラ側は初期値オフ)");
        Prop(so, "wallPrefab", "壁のプレハブ");
        Prop(so, "wallSegmentCells", "壁1枚の長さ(マス)");
        Prop(so, "wallHeightMul", "壁の高さ(倍率)");
        Prop(so, "flipWall", "壁の表裏を反転");
        Prop(so, "wallNorth", "北(奥)");
        Prop(so, "wallEast", "東(右)");
        Prop(so, "wallWest", "西(左)");
        Prop(so, "wallSouth", "南(手前)");
        Prop(so, "doorPrefab", "扉(北の壁の中央。任意)");
        if (GUILayout.Button("城門を作る(Castle Door Builder を開く)")) CastleDoorBuilder.OpenWindow();
        Prop(so, "doorOpeningCells", "扉の横幅(マス)。左右の壁はこの幅だけ空ける");
        Prop(so, "doorSideGap", "扉と左右の壁のすき間(m)");
        Prop(so, "doorFitWallHeight", "扉の高さを壁に合わせる");
        Prop(so, "doorHeightMul", "扉の高さ(倍率)");
        Prop(so, "doorFitWallThickness", "扉の厚みを壁に合わせる");
        Prop(so, "doorThicknessMul", "扉の厚み(倍率)");
        Prop(so, "doorDepthAlign", "扉の前後(-1=内側の面に揃える 1=外側の面に揃える)");
        Prop(so, "doorOffset", "扉の前後の微調整(m。+で奥へ)");

        Header("柱");
        Prop(so, "pillarPrefab", "柱のプレハブ");
        Prop(so, "pillarSizeCells", "柱の太さ(マス)");
        Prop(so, "cornerPillars", "四隅に置く");
        Prop(so, "interiorPillars", "絨毯の両側に並べる(盤面内)");
        Prop(so, "pillarSpacing", "柱の間隔(マス)");
        Prop(so, "pillarDistanceFromCarpet", "絨毯からの距離(マス)");

        Header("絨毯");
        Prop(so, "carpet", "中央に絨毯を敷く");
        Prop(so, "carpetWidthCells", "絨毯の幅(マス)");
        Prop(so, "carpetColor", "絨毯の色");
        Prop(so, "carpetBorderColor", "縁取りの色");

        Header("壁際の小物(たいまつ等。任意)");
        Prop(so, "wallProps", "小物のプレハブ");
        Prop(so, "propEverySegments", "何枚の壁ごとに置くか");
        Prop(so, "propHeight", "置く高さ(m)");
        Prop(so, "propScale", "大きさ(倍率)");

        Header("色(明るいマテリアルを別ファイルで作成)");
        Prop(so, "tint", "色を調整する");
        Prop(so, "floorTint", "床の色(乗算)");
        Prop(so, "wallTint", "壁・柱の色(乗算)");
        Prop(so, "floorVariation", "床の明るさムラ");
        Prop(so, "variationScale", "ムラの大きさ(マス)");
        Prop(so, "outputFolder", "保存先フォルダ");

        Header("その他");
        Prop(so, "removeColliders", "コライダーを外す(マスのクリック判定を邪魔しない)");
        Prop(so, "seed", "シード(変えると模様が変わる)");

        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "同じ名前のオブジェクトがシーンにあれば、作り直します(Ctrl+Zで戻せます)。\n" +
            "グリッドと位置を合わせるため、生成されたルートは(0,0,0)のまま動かさないでください。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(floorPrefabs.TrueForAll(p => p == null)))
        {
            if (GUILayout.Button("生成", GUILayout.Height(32))) Generate();
        }
        if (GUILayout.Button("プレハブとして保存(Resources/Stages)", GUILayout.Height(26))) SaveAsPrefab();

        EditorGUILayout.EndScrollView();
    }

    private static void Header(string label)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    private static void Prop(SerializedObject so, string name, string label) =>
        EditorGUILayout.PropertyField(so.FindProperty(name), new GUIContent(label), true);

    // ------------------------------------------------------------ 生成

    private void Generate()
    {
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);
        cellSize = Mathf.Max(0.1f, cellSize);
        outerFloorCells = Mathf.Max(0, outerFloorCells);
        wallSegmentCells = Mathf.Max(1, wallSegmentCells);
        pillarSpacing = Mathf.Max(1, pillarSpacing);
        propEverySegments = Mathf.Max(1, propEverySegments);
        carpetWidthCells = Mathf.Clamp(carpetWidthCells, 1, gridWidth);
        materialCache.Clear();

        GameObject old = GameObject.Find(rootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Castle Stage Generator");
        Transform floorParent = Child(root, "Floor");
        Transform carpetParent = Child(root, "Carpet");
        Transform wallParent = Child(root, "Walls");
        Transform pillarParent = Child(root, "Pillars");
        Transform propParent = Child(root, "Props");

        if (tint) EnsureFolder(outputFolder);

        System.Random rng = new System.Random(seed);
        StringBuilder log = new StringBuilder();

        BuildFloor(floorParent, rng);
        if (carpet) BuildCarpet(carpetParent);
        float wallThickness = BuildWalls(wallParent, propParent, rng);
        BuildPillars(pillarParent, wallThickness, log);

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log($"Castle Stage Generator: 生成完了 {rootName} 盤面={gridWidth}x{gridHeight} cell={cellSize}{log}", root);
    }

    private void BuildFloor(Transform parent, System.Random rng)
    {
        List<Piece> tiles = new List<Piece>();
        foreach (GameObject p in floorPrefabs)
            if (p != null) tiles.Add(Measure(p, false));
        if (tiles.Count == 0) return;

        float offset = seed * 7.31f;
        for (int x = -outerFloorCells; x < gridWidth + outerFloorCells; x++)
        {
            for (int z = -outerFloorCells; z < gridHeight + outerFloorCells; z++)
            {
                Piece piece = tiles[rng.Next(tiles.Count)];
                Vector3 scale = Vector3.one;
                if (autoFitFloor)
                {
                    float sx = cellSize / Mathf.Max(0.001f, piece.bounds.size.x);
                    float sz = cellSize / Mathf.Max(0.001f, piece.bounds.size.z);
                    scale = new Vector3(sx, Mathf.Min(sx, sz), sz);
                }
                float yaw = randomRotateFloor ? rng.Next(4) * 90f : 0f;
                Vector3 pos = new Vector3((x + 0.5f) * cellSize, floorTop, (z + 0.5f) * cellSize);

                // 3段階の明るさムラ(0=普通 1=暗い 2=明るい)
                int variant = 0;
                if (tint && floorVariation > 0f)
                {
                    float n = Mathf.PerlinNoise(x / variationScale + offset, z / variationScale - offset);
                    variant = n < 0.38f ? 1 : n > 0.66f ? 2 : 0;
                }
                Color c = floorTint * (variant == 1 ? 1f - floorVariation : variant == 2 ? 1f + floorVariation * 0.6f : 1f);
                c.a = 1f;

                Place(piece, parent, pos, yaw, scale, true, tint ? $"Floor{variant}" : null, c);
            }
        }
    }

    private void BuildCarpet(Transform parent)
    {
        Material red = GetFlatMaterial("Carpet", carpetColor);
        Material gold = GetFlatMaterial("CarpetBorder", carpetBorderColor);

        float centerX = gridWidth * 0.5f * cellSize;
        float width = carpetWidthCells * cellSize;
        float z0 = -outerFloorCells * cellSize;
        float z1 = (gridHeight + outerFloorCells) * cellSize;
        float length = z1 - z0;
        float centerZ = (z0 + z1) * 0.5f;
        const float thick = 0.01f;
        float y = floorTop + thick * 0.5f + 0.002f; // 床のすぐ上。グリッド(高さ0)よりは下
        float border = 0.08f * cellSize;

        Strip(parent, "Carpet", red, new Vector3(centerX, y, centerZ), new Vector3(width - border * 2f, thick, length));
        Strip(parent, "Border_L", gold, new Vector3(centerX - width * 0.5f + border * 0.5f, y, centerZ), new Vector3(border, thick, length));
        Strip(parent, "Border_R", gold, new Vector3(centerX + width * 0.5f - border * 0.5f, y, centerZ), new Vector3(border, thick, length));
    }

    private void Strip(Transform parent, string name, Material mat, Vector3 pos, Vector3 size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // 壁を建て、壁の厚み(m)を返す(柱の配置に使う)。
    private float BuildWalls(Transform wallParent, Transform propParent, System.Random rng)
    {
        if (wallPrefab == null) return 0.3f * cellSize;

        Piece wall = Measure(wallPrefab, true);
        Piece door = doorPrefab != null ? Measure(doorPrefab, true) : null;
        List<GameObject> props = wallProps.FindAll(p => p != null);

        float x0 = -outerFloorCells * cellSize;
        float x1 = (gridWidth + outerFloorCells) * cellSize;
        float z0 = -outerFloorCells * cellSize;
        float z1 = (gridHeight + outerFloorCells) * cellSize;
        float flip = flipWall ? 180f : 0f;

        float length = Mathf.Max(0.001f, wall.bounds.size.x);
        float thickRaw = Mathf.Max(0.001f, wall.bounds.size.z);
        float wantSeg = wallSegmentCells * cellSize;
        float s = wantSeg / length;
        float thick = thickRaw * s;

        // 辺ごと: 始点, 方向, 長さ, 壁の向き(内側を向く回転), 外側へのずらし
        // from〜to(辺の始点からの距離m)の区間に壁を並べる。propCounterは小物の間隔用
        int propCounter = 0;
        void WallRun(Vector3 start, Vector3 dir, float from, float to, float yaw, Vector3 outward)
        {
            float len = to - from;
            if (len <= 0.01f) return;
            int count = Mathf.Max(1, Mathf.RoundToInt(len / wantSeg));
            float seg = len / count;
            float segScale = seg / length;
            // 高さは辺ごとに変わらないよう、基準の長さ(wantSeg)の倍率で決める
            Vector3 scale = new Vector3(segScale, s * wallHeightMul, s);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = start + dir * (from + (i + 0.5f) * seg) + outward * (thick * 0.5f);
                pos.y = floorTop;
                Place(wall, wallParent, pos, yaw + flip, scale, false, tint ? "Wall" : null, wallTint);

                if (props.Count > 0 && propCounter++ % propEverySegments == propEverySegments / 2)
                {
                    GameObject prop = props[rng.Next(props.Count)];
                    Piece pp = Measure(prop, false);
                    Vector3 pPos = start + dir * (from + (i + 0.5f) * seg) - outward * 0.15f * cellSize;
                    pPos.y = floorTop + propHeight;
                    Place(pp, propParent, pPos, yaw, Vector3.one * propScale, false, null, Color.white);
                }
            }
        }

        void Side(bool on, Vector3 start, Vector3 dir, float len, float yaw, Vector3 outward, bool hasDoor)
        {
            if (!on) return;
            propCounter = 0;

            if (!hasDoor || door == null)
            {
                WallRun(start, dir, 0f, len, yaw, outward);
                return;
            }

            // 扉を辺の中央(=絨毯の正面)に置き、残りを左右の壁で埋める
            // 扉の外枠の幅 = 扉の横幅。左右の壁は「扉の端+すき間」で止めるので、扉にかぶらない
            float opening = Mathf.Clamp(doorOpeningCells * cellSize, 0.1f, len);
            float gap = Mathf.Max(0f, doorSideGap);
            float doorFrom = (len - opening) * 0.5f;
            float doorTo = doorFrom + opening;
            WallRun(start, dir, 0f, doorFrom - gap, yaw, outward);
            WallRun(start, dir, doorTo + gap, len, yaw, outward);

            // 扉の大きさ: 横=扉の横幅ちょうど、縦=(壁の高さに合わせる or 横と同じ比率)×倍率
            float sx = opening / Mathf.Max(0.001f, door.bounds.size.x);
            float sy = doorFitWallHeight
                ? wall.bounds.size.y * s * wallHeightMul / Mathf.Max(0.001f, door.bounds.size.y)
                : sx;
            bool movableDoor = door.prefab.GetComponentInChildren<CastleDoubleDoor>(true) != null;
            float sz = doorFitWallThickness && !movableDoor
                ? thick / Mathf.Max(0.001f, door.bounds.size.z)
                : sx;
            Vector3 dScale = new Vector3(sx, sy * doorHeightMul, sz * doorThicknessMul);
            // 開閉する扉(CastleDoubleDoor)は、縦横を別々に伸ばすと開くときに形が歪むので等倍で拡大する。
            // 向きも壁の表裏反転とは関係なく、正面(+Z)を部屋の内側に向ける。
            if (movableDoor) dScale = Vector3.one * sx;
            float doorYaw = movableDoor ? yaw : yaw + flip;

            float doorThick = door.bounds.size.z * dScale.z;
            Vector3 dPos = start + dir * (len * 0.5f)
                + outward * (thick * 0.5f)
                + outward * ((doorThick - thick) * 0.5f) * doorDepthAlign
                + outward * doorOffset;
            dPos.y = floorTop;
            Place(door, wallParent, dPos, doorYaw, dScale, false, tint ? "Wall" : null, wallTint);
        }

        // 西(左): Z方向に伸び、+X(内側)を向く
        Side(wallWest, new Vector3(x0, 0f, z0), Vector3.forward, z1 - z0, 90f, Vector3.left, false);
        // 東(右)
        Side(wallEast, new Vector3(x1, 0f, z0), Vector3.forward, z1 - z0, -90f, Vector3.right, false);
        // 北(奥): X方向に伸び、-Z(内側)を向く。扉はここ
        Side(wallNorth, new Vector3(x0, 0f, z1), Vector3.right, x1 - x0, 180f, Vector3.forward, true);
        // 南(手前)
        Side(wallSouth, new Vector3(x0, 0f, z0), Vector3.right, x1 - x0, 0f, Vector3.back, false);

        return thick;
    }

    private void BuildPillars(Transform parent, float wallThickness, StringBuilder log)
    {
        if (pillarPrefab == null) return;
        Piece pillar = Measure(pillarPrefab, false);
        float foot = Mathf.Max(0.001f, Mathf.Max(pillar.bounds.size.x, pillar.bounds.size.z));

        if (cornerPillars)
        {
            float size = Mathf.Max(pillarSizeCells * cellSize, wallThickness * 1.3f);
            float s = size / foot;
            float x0 = -outerFloorCells * cellSize - wallThickness * 0.5f;
            float x1 = (gridWidth + outerFloorCells) * cellSize + wallThickness * 0.5f;
            float z0 = -outerFloorCells * cellSize - wallThickness * 0.5f;
            float z1 = (gridHeight + outerFloorCells) * cellSize + wallThickness * 0.5f;

            if (wallNorth || wallWest) Place(pillar, parent, new Vector3(x0, floorTop, z1), 0f, Vector3.one * s, false, tint ? "Wall" : null, wallTint);
            if (wallNorth || wallEast) Place(pillar, parent, new Vector3(x1, floorTop, z1), 0f, Vector3.one * s, false, tint ? "Wall" : null, wallTint);
            if (wallSouth || wallWest) Place(pillar, parent, new Vector3(x0, floorTop, z0), 0f, Vector3.one * s, false, tint ? "Wall" : null, wallTint);
            if (wallSouth || wallEast) Place(pillar, parent, new Vector3(x1, floorTop, z0), 0f, Vector3.one * s, false, tint ? "Wall" : null, wallTint);
        }

        if (interiorPillars)
        {
            float s = pillarSizeCells * cellSize / foot;
            int carpetLeft = (gridWidth - carpetWidthCells) / 2;
            int leftX = carpetLeft - 1 - pillarDistanceFromCarpet;
            int rightX = carpetLeft + carpetWidthCells + pillarDistanceFromCarpet;
            List<Vector2Int> cells = new List<Vector2Int>();

            for (int z = pillarSpacing / 2; z < gridHeight; z += pillarSpacing)
            {
                if (leftX >= 0) cells.Add(new Vector2Int(leftX, z));
                if (rightX < gridWidth) cells.Add(new Vector2Int(rightX, z));
            }
            foreach (Vector2Int c in cells)
            {
                Vector3 pos = new Vector3((c.x + 0.5f) * cellSize, floorTop, (c.y + 0.5f) * cellSize);
                Place(pillar, parent, pos, 0f, Vector3.one * s, false, tint ? "Wall" : null, wallTint);
            }
            if (cells.Count > 0)
            {
                log.Append(" 柱のマス(グリッド座標・通行不可にする場合はCSV側で)=");
                foreach (Vector2Int c in cells) log.Append($"({c.x},{c.y}) ");
            }
        }
    }

    // ------------------------------------------------------------ 配置の共通処理

    private sealed class Piece
    {
        public GameObject prefab;
        public float alignYaw;   // 長い辺をX方向に揃えるための回転
        public Bounds bounds;    // alignYaw適用後、原点に置いたときの見た目の範囲
    }

    // プレハブを一時的に原点へ置き、見た目の大きさを測る。
    private static Piece Measure(GameObject prefab, bool alignLongSideToX)
    {
        float yaw = 0f;
        Bounds b = MeasureAt(prefab, 0f);
        if (alignLongSideToX && b.size.z > b.size.x)
        {
            yaw = 90f;
            b = MeasureAt(prefab, 90f);
        }
        return new Piece { prefab = prefab, alignYaw = yaw, bounds = b };
    }

    private static Bounds MeasureAt(GameObject prefab, float yaw)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(
            Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(Vector3.zero, Vector3.one);
        bool first = true;
        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        DestroyImmediate(go);
        return b;
    }

    /// <summary>
    /// 空の親(holder)を作って位置・向き・大きさを持たせ、その子にプレハブを置く。
    /// topAnchor=true なら上面の中心、false なら底面の中心が pos に来る。
    /// </summary>
    private void Place(Piece piece, Transform parent, Vector3 pos, float yaw, Vector3 scale,
        bool topAnchor, string tintKind, Color tintColor)
    {
        GameObject holder = new GameObject(piece.prefab.name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = pos;
        holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        holder.transform.localScale = scale;

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, holder.transform);
        Vector3 anchor = new Vector3(
            piece.bounds.center.x,
            topAnchor ? piece.bounds.max.y : piece.bounds.min.y,
            piece.bounds.center.z);
        inst.transform.localRotation = Quaternion.Euler(0f, piece.alignYaw, 0f) * piece.prefab.transform.rotation;
        inst.transform.localPosition = -anchor;
        inst.transform.localScale = piece.prefab.transform.localScale;

        if (removeColliders)
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(c);

        if (tintKind != null)
        {
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = GetTinted(mats[i], tintKind, tintColor);
                r.sharedMaterials = mats;
            }
        }

        // 開け閉めする扉はStaticにすると動かなくなるので外す
        if (inst.GetComponentInChildren<CastleDoubleDoor>(true) != null) return;
        GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic);
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    private static Transform Child(GameObject root, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        return go.transform;
    }

    // ------------------------------------------------------------ マテリアル

    // 元のマテリアルをコピーして色を掛けた別ファイルを作る(元は書き換えない)。
    private Material GetTinted(Material src, string kind, Color mul)
    {
        if (src.name.Contains(MatSuffix)) return src;
        string key = src.GetInstanceID() + kind;
        if (materialCache.TryGetValue(key, out Material cached)) return cached;

        string path = $"{outputFolder}/{Sanitize(src.name)}{MatSuffix}_{kind}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(src);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = src.shader;
            mat.CopyPropertiesFromMaterial(src);
        }

        string prop = mat.HasProperty("_BaseColor") ? "_BaseColor" : mat.HasProperty("_Color") ? "_Color" : null;
        if (prop != null)
        {
            Color c = src.GetColor(prop);
            mat.SetColor(prop, new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a));
        }
        EditorUtility.SetDirty(mat);
        materialCache[key] = mat;
        return mat;
    }

    // 絨毯用の単色マテリアル(URP Lit)。
    private Material GetFlatMaterial(string kind, Color color)
    {
        EnsureFolder(outputFolder);
        string path = $"{outputFolder}/{kind}{MatSuffix}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }


    // ------------------------------------------------------------ 保存

    private void SaveAsPrefab()
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Castle Stage Generator",
                $"シーンに「{rootName}」がありません。先に生成してください。", "OK");
            return;
        }
        if (root.transform.position != Vector3.zero)
        {
            if (!EditorUtility.DisplayDialog("Castle Stage Generator",
                "ルートの位置が(0,0,0)ではありません。グリッドとずれますが保存しますか?", "保存", "やめる"))
                return;
        }

        EnsureFolder("Assets/Resources/Stages");
        string path = $"Assets/Resources/Stages/{rootName}.prefab";
        if (File.Exists(path) &&
            !EditorUtility.DisplayDialog("Castle Stage Generator", $"{path} を上書きしますか?", "上書き", "やめる"))
            return;

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
        Debug.Log($"Castle Stage Generator: 保存しました {path}", root);
    }
}
#endif