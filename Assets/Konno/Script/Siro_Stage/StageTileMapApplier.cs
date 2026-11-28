using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プランナーの「StageN_TileLayout.csv」(0=通行可、1=通行不可、2=障害物)を読み、
/// ・BattleGrid の通行可否を設定する
/// ・フィールドに合ったマップチップ(壁・障害物の見た目)を並べる
/// BattleStageLoader から呼ばれる(Awake で Apply、Start のユニット配置前に ResolveUnitConflicts)。
/// 床・部屋の壁・絨毯などステージ共通の部分は地形Prefab(Resources/Stages/3D10 など)のまま。
/// ※このファイルは Editor フォルダに入れないこと。
/// </summary>
public sealed class StageTileMapApplier : MonoBehaviour
{
    [Serializable]
    public sealed class StageTileEntry
    {
        [Tooltip("StageConfig.csv の StageID(例: 1、61)")]
        public string stageId;
        public TextAsset csv;
    }

    [Header("ステージごとのタイル配置CSV(右上の︙ >「CSVを名前から自動で割り当て」)")]
    [SerializeField] private List<StageTileEntry> layouts = new List<StageTileEntry>();

    [Header("フィールドごとの見た目(マップチップセット)")]
    [SerializeField] private List<TileChipSet> chipSets = new List<TileChipSet>();

    [Header("CSVの読み方(プランナーと要確認)")]
    [Tooltip("オン: CSVの1行目 = 手前(y=0・味方側)  オフ: 1行目 = 奥")]
    [SerializeField] private bool firstRowIsFront = true;

    [Tooltip("CSVの(列,行)から内部座標を求めるときに引く値。(0,0)=CSVの左上のマスが内部(0,0)")]
    [SerializeField] private Vector2Int csvOffset = Vector2Int.zero;

    [Tooltip("通行できる値。これ以外は通行不可")]
    [SerializeField] private List<int> walkableValues = new List<int> { 0 };

    [Header("見た目")]
    [SerializeField] private bool buildVisuals = true;

    [Tooltip("通行不可マスのグリッド表示(GridDebugView)を隠す")]
    [SerializeField] private bool hideGridTilesOnBlocked = true;

    [Tooltip("チップをまとめて描画して軽くする(Static Batching)")]
    [SerializeField] private bool combineMeshes = true;

    [Header("ユニット配置CSVとの食い違い")]
    [Tooltip("オン: 通行不可マスにユニットが置かれていたら、警告を出してそのマスだけ通行可にする(暫定)\n" +
             "オフ: そのままにする(BattleUnitSpawner がエラーで配置を中止する)")]
    [SerializeField] private bool openBlockedSpawnCells = true;

    private BattleGrid grid;
    private Transform chipRoot;
    private readonly Dictionary<Vector2Int, GameObject> chipObjects = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<int, Material> fallbackMaterials = new Dictionary<int, Material>();
    private readonly HashSet<Vector2Int> unitConflictCells = new HashSet<Vector2Int>();

    /// <summary>ユニット配置CSVで、通行不可マスに置かれていたマスか(ResolveUnitConflicts の結果)。</summary>
    public bool WasBlockedForUnit(Vector2Int p) => unitConflictCells.Contains(p);

    /// <summary>今のステージのタイル値 [x, y](内部座標)。CSVが無ければ null。</summary>
    public int[,] Tiles { get; private set; }

    /// <summary>マスのタイル値。範囲外やCSVなしは -1。</summary>
    public int GetTile(Vector2Int p) =>
        Tiles != null && p.x >= 0 && p.y >= 0 && p.x < Tiles.GetLength(0) && p.y < Tiles.GetLength(1)
            ? Tiles[p.x, p.y] : -1;

    // ================================================================ 適用

    /// <summary>
    /// タイル配置を読み込んで通行可否と見た目を設定する。BattleStageLoader.Awake から呼ぶ。
    /// </summary>
    public bool Apply(string stageId, string fieldName, BattleGrid targetGrid, Transform fieldRoot)
    {
        grid = targetGrid;
        Tiles = null;
        unitConflictCells.Clear();
        Clear();
        if (grid == null) return false;

        TextAsset csv = FindCsv(stageId);
        if (csv == null)
        {
            Debug.LogWarning($"StageTileMapApplier: StageID={stageId} のタイル配置CSVが未設定です(全マス通行可のまま)。", this);
            return false;
        }

        int[,] raw;
        try
        {
            raw = Parse(csv.text);
        }
        catch (FormatException ex)
        {
            Debug.LogError($"StageTileMapApplier: {csv.name} の読込エラー: {ex.Message}", this);
            return false;
        }

        int rows = raw.GetLength(0);
        int cols = raw.GetLength(1);
        if (cols != grid.Width || rows != grid.Height)
            Debug.LogWarning($"StageTileMapApplier: {csv.name} は {cols}×{rows} マス、ステージは {grid.Width}×{grid.Height} マスです。", this);

        Tiles = new int[grid.Width, grid.Height]; // データの無いマスは 0(通行可)
        int blocked = 0;
        for (int r = 0; r < rows; r++)
        {
            int csvY = firstRowIsFront ? r : rows - 1 - r;
            for (int c = 0; c < cols; c++)
            {
                Vector2Int p = new Vector2Int(c - csvOffset.x, csvY - csvOffset.y);
                if (!grid.IsInside(p)) continue;
                int v = raw[r, c];
                Tiles[p.x, p.y] = v;
                bool walk = walkableValues.Contains(v);
                grid.SetWalkable(p, walk);
                if (!walk) blocked++;
            }
        }
        Debug.Log($"タイル配置: {csv.name} を適用(通行不可 {blocked} マス)", this);

        if (buildVisuals) BuildChips(fieldName, fieldRoot);
        if (hideGridTilesOnBlocked && isActiveAndEnabled) StartCoroutine(HideGridTilesNextFrame());
        return true;
    }

    /// <summary>
    /// ユニット配置CSVの座標が通行不可マスと重なっていないか調べる。BattleStageLoader.Start の配置前に呼ぶ。
    /// </summary>
    public void ResolveUnitConflicts(string stageId, TextAsset charaLayoutCsv)
    {
        if (Tiles == null || grid == null || charaLayoutCsv == null) return;

        List<CsvRow> rows;
        try
        {
            rows = CsvReader.Read(charaLayoutCsv, "StageID");
        }
        catch (Exception)
        {
            return; // 配置CSVのエラーは BattleUnitSpawner 側で報告される
        }

        int conflicts = 0;
        foreach (CsvRow row in rows)
        {
            if (row.Get("StageID") != stageId) continue;
            BattleUnitRecord rec;
            try { rec = BattleUnitRecord.FromCsv(row); }
            catch (Exception) { continue; }

            if (!grid.IsInside(rec.Position) || grid.IsWalkable(rec.Position)) continue;
            conflicts++;
            unitConflictCells.Add(rec.Position);
            string msg = $"タイル配置と配置CSVの食い違い: {rec.Memo} CSV({rec.SourceX},{rec.SourceY}) → 内部{rec.Position} " +
                         $"はタイル値 {GetTile(rec.Position)}(通行不可)です。";
            if (openBlockedSpawnCells)
            {
                grid.SetWalkable(rec.Position, true);
                RemoveChip(rec.Position);
                Debug.LogWarning(msg + " 暫定でこのマスを通行可にしました。", this);
            }
            else
            {
                Debug.LogError(msg, this);
            }
        }
        if (conflicts > 0)
            Debug.LogWarning($"StageID={stageId}: {conflicts}体が通行不可マスに配置されています。" +
                             "タイル配置CSVの向き・ずれの設定か、配置CSVの座標を確認してください。", this);
    }

    // ================================================================ 見た目

    private void BuildChips(string fieldName, Transform fieldRoot)
    {
        TileChipSet set = chipSets.Find(s => s != null && s.FieldName == fieldName);
        if (set == null)
            Debug.LogWarning($"StageTileMapApplier: フィールド {fieldName} のマップチップセットがありません。仮の箱で表示します。", this);

        GameObject rootGo = new GameObject("TileChips");
        chipRoot = rootGo.transform;
        if (fieldRoot != null) chipRoot.SetParent(fieldRoot, true);
        chipRoot.SetPositionAndRotation(grid.transform.position, grid.transform.rotation);

        float scale = set != null ? grid.CellSize / set.ChipCellSize : 1f;
        System.Random rng = new System.Random((fieldName ?? "").GetHashCode());
        var culled = new List<Vector2Int>();

        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                int v = Tiles[x, y];
                if (walkableValues.Contains(v)) continue; // 通行できる床は地形Prefabの床をそのまま使う

                Vector2Int p = new Vector2Int(x, y);
                TileChipSet.Chip chip = set != null ? set.Find(v) : null;
                GameObject go;
                if (chip != null && chip.prefab != null)
                {
                    go = Instantiate(chip.prefab, chipRoot);
                    go.transform.localScale = chip.prefab.transform.localScale * scale;
                    if (chip.randomRotate)
                        go.transform.rotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
                    else
                        culled.Add(p);
                }
                else
                {
                    go = MakeFallback(v);
                    go.transform.SetParent(chipRoot, false);
                }
                go.name = $"Chip_{x}_{y}_{v}";
                go.transform.position = grid.CellToWorld(p);
                chipObjects[p] = go;
            }

        // 同じ値のチップが隣り合う面は見えないので消す(子の Face_N / Face_S / Face_E / Face_W)
        foreach (Vector2Int p in culled)
        {
            Transform t = chipObjects[p].transform;
            int v = Tiles[p.x, p.y];
            HideFaceIfSame(t, p + Vector2Int.up, v, "Face_N");
            HideFaceIfSame(t, p + Vector2Int.down, v, "Face_S");
            HideFaceIfSame(t, p + Vector2Int.right, v, "Face_E");
            HideFaceIfSame(t, p + Vector2Int.left, v, "Face_W");
        }

        if (combineMeshes) StaticBatchingUtility.Combine(rootGo);
    }

    private void HideFaceIfSame(Transform chip, Vector2Int neighbor, int value, string faceName)
    {
        if (GetTile(neighbor) != value || !chipObjects.ContainsKey(neighbor)) return;
        Transform face = FindDeep(chip, faceName);
        if (face != null) face.gameObject.SetActive(false);
    }

    // 子孫の中から名前で探す(Face_N などが孫の階層にあっても見つける)
    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private GameObject MakeFallback(int value)
    {
        float cs = grid.CellSize;
        float h = value == 1 ? 1.6f * cs : 0.7f * cs;
        GameObject holder = new GameObject();
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(holder.transform, false);
        box.transform.localPosition = new Vector3(0f, h * 0.5f - 0.03f, 0f);
        box.transform.localScale = new Vector3(cs * 0.98f, h, cs * 0.98f);
        box.GetComponent<MeshRenderer>().sharedMaterial = FallbackMaterial(value);
        return holder;
    }

    private Material FallbackMaterial(int value)
    {
        if (fallbackMaterials.TryGetValue(value, out Material m)) return m;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Color c = value == 1 ? new Color(0.35f, 0.34f, 0.33f) : new Color(0.45f, 0.33f, 0.22f);
        m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        fallbackMaterials[value] = m;
        return m;
    }

    private void RemoveChip(Vector2Int p)
    {
        if (!chipObjects.TryGetValue(p, out GameObject go)) return;
        chipObjects.Remove(p);
        if (go != null) Destroy(go);
        if (Tiles != null) Tiles[p.x, p.y] = walkableValues.Count > 0 ? walkableValues[0] : 0;
    }

    private IEnumerator HideGridTilesNextFrame()
    {
        yield return null; // GridDebugView.Start がマスを作り終わるのを待つ
        if (grid == null) yield break;
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                if (grid.IsWalkable(new Vector2Int(x, y))) continue;
                Transform tile = grid.transform.Find($"GridCell_{x}_{y}");
                if (tile != null) tile.gameObject.SetActive(false);
            }
    }

    private void Clear()
    {
        chipObjects.Clear();
        if (chipRoot != null) Destroy(chipRoot.gameObject);
        chipRoot = null;
    }

    private void OnDestroy()
    {
        foreach (Material m in fallbackMaterials.Values)
            if (m != null) Destroy(m);
    }

    // ================================================================ CSV

    private TextAsset FindCsv(string stageId)
    {
        string id = (stageId ?? "").Trim();
        StageTileEntry e = layouts.Find(l => l != null && l.csv != null && (l.stageId ?? "").Trim() == id);
        return e?.csv;
    }

    /// <summary>ヘッダーなしの数字だけのCSVを [行, 列] で読む。空行は無視、短い行は 0 で埋める。</summary>
    public static int[,] Parse(string text)
    {
        var lines = new List<string[]>();
        foreach (string rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.Replace(",", "").Trim().Length == 0) continue;
            lines.Add(line.Split(','));
        }
        if (lines.Count == 0) throw new FormatException("中身がありません。");

        int cols = 0;
        foreach (string[] l in lines) cols = Mathf.Max(cols, l.Length);
        int[,] result = new int[lines.Count, cols];
        for (int r = 0; r < lines.Count; r++)
            for (int c = 0; c < lines[r].Length; c++)
            {
                string s = lines[r][c].Trim();
                if (s.Length == 0) continue;
                if (!int.TryParse(s, out int v))
                    throw new FormatException($"{r + 1}行{c + 1}列目「{s}」は数字ではありません。");
                result[r, c] = v;
            }
        return result;
    }

#if UNITY_EDITOR
    /// <summary>
    /// プロジェクト内の「StageN_TileLayout」を探して StageID N に割り当てる。
    /// 「Stage6_2_TileLayout」のように名前と StageID が一致しないものは、下の対応表で補正する。
    /// </summary>
    [ContextMenu("CSVを名前から自動で割り当て")]
    private void AutoAssignLayouts()
    {
        // 名前の数字と StageID が一致しないものの対応表(StageConfig.csv に合わせる)
        var special = new Dictionary<string, string> { { "Stage6_2_TileLayout", "61" } };

        string[] guids = UnityEditor.AssetDatabase.FindAssets("_TileLayout t:TextAsset");
        int added = 0;
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null || !asset.name.StartsWith("Stage") || !asset.name.EndsWith("_TileLayout")) continue;

            string id = special.TryGetValue(asset.name, out string s)
                ? s
                : asset.name.Substring(5, asset.name.Length - 5 - "_TileLayout".Length);
            if (!int.TryParse(id, out _)) continue;

            StageTileEntry entry = layouts.Find(l => l != null && l.stageId == id);
            if (entry == null) { entry = new StageTileEntry { stageId = id }; layouts.Add(entry); }
            entry.csv = asset;
            added++;
        }
        layouts.Sort((a, b) => string.CompareOrdinal((a?.stageId ?? "").PadLeft(4, '0'), (b?.stageId ?? "").PadLeft(4, '0')));
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"StageTileMapApplier: {added}件のタイル配置CSVを割り当てました。", this);
    }
#endif
}