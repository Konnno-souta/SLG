using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【デバッグ用】Konno のシーンだけで、ステージの地形+タイル配置CSV(マップチップ)を確認する。
/// Kokoro 側のスクリプト(BattleStageLoader など)は一切変更しない。
/// 読むだけ使うもの: BattleGrid / CsvReader / StageConfigData / BattleUnitRecord。
///
/// 使い方:
///  1. シーンに空の GameObject を作り、このコンポーネントと StageTileMapApplier を付ける
///  2. Stage Config Csv と Chara Layout Csv に Kokoro/CSV の CSV を入れる
///  3. 再生 → 左上のボタンでステージを切り替え
/// ※このファイルは Editor フォルダに入れないこと。メインのバトルシーンには置かないこと。
/// </summary>
public sealed class StageTileDebugLoader : MonoBehaviour
{
    [Header("CSV(Kokoro/CSV のものを入れる。読むだけ)")]
    [SerializeField] private TextAsset stageConfigCsv;
    [SerializeField] private TextAsset charaLayoutCsv;

    [Header("最初に表示するステージ")]
    [SerializeField] private string stageId = "1";

    [Header("参照(空なら自動で作る/探す)")]
    [SerializeField] private StageTileMapApplier tileMap;
    [SerializeField] private BattleGrid grid;

    [Header("表示")]
    [SerializeField] private bool showUnits = true;
    [SerializeField] private bool showGridLines = true;
    [SerializeField] private bool showGui = true;
    [SerializeField] private bool fitCamera = true;
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.25f);

    private readonly List<string> stageIds = new List<string>();
    private readonly Dictionary<string, StageConfigData> configs = new Dictionary<string, StageConfigData>();
    private GameObject fieldInstance;
    private GameObject debugRoot;
    private Material lineMaterial;
    private readonly Dictionary<string, Material> unitMaterials = new Dictionary<string, Material>();
    private string status = "";
    private Vector2 guiScroll;

    private void Start()
    {
        if (tileMap == null) tileMap = GetComponent<StageTileMapApplier>();
        if (tileMap == null) tileMap = gameObject.AddComponent<StageTileMapApplier>();
        if (grid == null)
        {
            // デバッグ専用のグリッド(メインのシーンの BattleGrid とは別物)
            GameObject g = new GameObject("DebugBattleGrid");
            grid = g.AddComponent<BattleGrid>();
        }

        ReadStageConfig();
        Load(stageId);
    }

    // ================================================================ 読み込み

    private void ReadStageConfig()
    {
        stageIds.Clear();
        configs.Clear();
        if (stageConfigCsv == null)
        {
            status = "Stage Config Csv が未設定です";
            return;
        }
        try
        {
            foreach (CsvRow row in CsvReader.Read(stageConfigCsv, "StageID"))
            {
                string id = row.Get("StageID");
                if (string.IsNullOrWhiteSpace(id) || configs.ContainsKey(id)) continue;
                try
                {
                    configs[id] = StageConfigData.FromCsv(row);
                    stageIds.Add(id);
                }
                catch (Exception) { /* 空行など */ }
            }
        }
        catch (Exception ex)
        {
            status = $"StageConfig 読込エラー: {ex.Message}";
            Debug.LogError(status, this);
        }
    }

    /// <summary>指定したステージを表示し直す。</summary>
    public void Load(string id)
    {
        if (!configs.TryGetValue(id, out StageConfigData cfg))
        {
            status = $"StageID={id} が StageConfig にありません";
            Debug.LogWarning(status, this);
            return;
        }
        stageId = id;

        if (fieldInstance != null) Destroy(fieldInstance);
        if (debugRoot != null) Destroy(debugRoot);
        debugRoot = new GameObject("TileDebug");

        // グリッドを作り直す(全マス通行可に戻る)
        grid.Initialize(cfg.Width, cfg.Height);

        // 地形Prefab(Resources/Stages/3D10 など)
        GameObject prefab = Resources.Load<GameObject>($"Stages/{cfg.FieldName}");
        if (prefab != null)
        {
            fieldInstance = Instantiate(prefab, grid.transform.position, Quaternion.identity);
            fieldInstance.name = cfg.FieldName;
        }
        else
        {
            Debug.LogWarning($"地形Prefabが見つかりません: Resources/Stages/{cfg.FieldName}", this);
        }

        // タイル配置(通行可否+マップチップ)
        tileMap.Apply(id, cfg.FieldName, grid, fieldInstance != null ? fieldInstance.transform : debugRoot.transform);
        tileMap.ResolveUnitConflicts(id, charaLayoutCsv);

        int units = showUnits ? PlaceUnitMarkers(id) : 0;
        if (showGridLines) BuildGridLines();
        if (fitCamera) FitCamera();

        status = $"StageID {id} / {cfg.FieldName} {cfg.Memo} / {cfg.Width}×{cfg.Height} / ユニット{units}体";
        Debug.Log("タイルデバッグ: " + status, this);
    }

    // ================================================================ 表示

    // 配置CSVのユニットを色付きのカプセルで置く(味方=青、敵=赤、通行不可マス上=黄)
    private int PlaceUnitMarkers(string id)
    {
        if (charaLayoutCsv == null) return 0;
        List<CsvRow> rows;
        try { rows = CsvReader.Read(charaLayoutCsv, "StageID"); }
        catch (Exception ex) { Debug.LogError($"配置CSV読込エラー: {ex.Message}", this); return 0; }

        int count = 0;
        float cs = grid.CellSize;
        foreach (CsvRow row in rows)
        {
            if (row.Get("StageID") != id) continue;
            BattleUnitRecord rec;
            try { rec = BattleUnitRecord.FromCsv(row); }
            catch (Exception) { continue; }

            bool inside = grid.IsInside(rec.Position);
            // ResolveUnitConflicts で開けたマスは通行可になっているので、元のタイル値で判定する
            bool wasBlocked = inside && tileMap.WasBlockedForUnit(rec.Position);
            string kind = !inside || wasBlocked ? "Warn" : rec.Team;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cap.GetComponent<Collider>());
            cap.name = $"{rec.Team}_{rec.Memo}_({rec.SourceX},{rec.SourceY})";
            cap.transform.SetParent(debugRoot.transform, false);
            Vector3 pos = inside
                ? grid.CellToWorld(rec.Position)
                : grid.transform.TransformPoint(new Vector3((rec.Position.x + 0.5f) * cs, 0f, (rec.Position.y + 0.5f) * cs));
            cap.transform.position = pos + Vector3.up * 0.5f * cs;
            cap.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f) * cs;
            cap.GetComponent<MeshRenderer>().sharedMaterial = UnitMaterial(kind);
            count++;
        }
        return count;
    }

    private Material UnitMaterial(string kind)
    {
        if (unitMaterials.TryGetValue(kind, out Material m)) return m;
        Color c = kind == "Player" ? new Color(0.2f, 0.45f, 1f)
                : kind == "Enemy" ? new Color(1f, 0.25f, 0.2f)
                : new Color(1f, 0.85f, 0.1f);
        m = NewMaterial("Universal Render Pipeline/Lit", c);
        unitMaterials[kind] = m;
        return m;
    }

    // ゲーム画面でも見えるマス目の線
    private void BuildGridLines()
    {
        float cs = grid.CellSize;
        var verts = new List<Vector3>();
        var idx = new List<int>();
        const float y = 0.02f;
        for (int x = 0; x <= grid.Width; x++)
        {
            verts.Add(new Vector3(x * cs, y, 0f)); verts.Add(new Vector3(x * cs, y, grid.Height * cs));
        }
        for (int z = 0; z <= grid.Height; z++)
        {
            verts.Add(new Vector3(0f, y, z * cs)); verts.Add(new Vector3(grid.Width * cs, y, z * cs));
        }
        for (int i = 0; i < verts.Count; i++) idx.Add(i);

        Mesh mesh = new Mesh { name = "DebugGridLines" };
        mesh.SetVertices(verts);
        mesh.SetIndices(idx, MeshTopology.Lines, 0);

        GameObject go = new GameObject("GridLines");
        go.transform.SetParent(debugRoot.transform, false);
        go.transform.SetPositionAndRotation(grid.transform.position, grid.transform.rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        if (lineMaterial == null) lineMaterial = NewMaterial("Universal Render Pipeline/Unlit", gridLineColor, true);
        go.AddComponent<MeshRenderer>().sharedMaterial = lineMaterial;
    }

    private void FitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float cs = grid.CellSize;
        Vector3 center = grid.transform.TransformPoint(new Vector3(grid.Width * cs * 0.5f, 0f, grid.Height * cs * 0.42f));
        float size = Mathf.Max(grid.Width, grid.Height) * cs;
        cam.transform.position = center + new Vector3(0f, size * 0.95f, -size * 0.55f);
        cam.transform.LookAt(center);
    }

    private static Material NewMaterial(string shaderName, Color color, bool transparent = false)
    {
        Shader shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Material m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (transparent && m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return m;
    }

    // ================================================================ 画面のボタン

    private void OnGUI()
    {
        if (!showGui) return;
        GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20), GUI.skin.box);
        GUILayout.Label("タイル配置デバッグ");
        GUILayout.Label(status);

        guiScroll = GUILayout.BeginScrollView(guiScroll, GUILayout.MaxHeight(260));
        foreach (string id in stageIds)
        {
            StageConfigData c = configs[id];
            string label = $"{(id == stageId ? "▶ " : "")}{id}: {c.FieldName} {Short(c.Memo)}";
            if (GUILayout.Button(label)) Load(id);
        }
        GUILayout.EndScrollView();

        bool u = GUILayout.Toggle(showUnits, "ユニット配置を表示");
        bool l = GUILayout.Toggle(showGridLines, "マス目の線を表示");
        if (u != showUnits || l != showGridLines)
        {
            showUnits = u;
            showGridLines = l;
            Load(stageId);
        }
        GUILayout.Label("青=味方 赤=敵 黄=通行不可マスや盤面外に置かれている");
        GUILayout.EndArea();
    }

    private static string Short(string s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length > 10 ? s.Substring(0, 10) + "…" : s);

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
        foreach (Material m in unitMaterials.Values) if (m != null) Destroy(m);
    }
}
