using UnityEngine;

/// <summary>
/// 盤面の上に置く障害物(壁のかたまりなど)。
/// ゲーム開始時に、覆っているマスを BattleGrid で「通行不可」にする。
/// Castle Stage Generator の「障害物の壁」で自動的に付く。手で付ける場合は cell と size を設定する。
/// ※このファイルは Editor フォルダに入れないこと(ゲーム中に使うため)。
/// </summary>
[DisallowMultipleComponent]
public sealed class GridObstacle : MonoBehaviour
{
    [Tooltip("障害物の左下(手前・左)のマス。グリッド座標は0始まり")]
    [SerializeField] private Vector2Int cell = new Vector2Int(2, 10);

    [Tooltip("横(X)×縦(Z)のマス数")]
    [SerializeField] private Vector2Int size = new Vector2Int(3, 2);

    [Tooltip("オフにすると見た目だけになり、通行できる")]
    [SerializeField] private bool blockMovement = true;

    private BattleGrid grid;

    public Vector2Int Cell => cell;
    public Vector2Int Size => size;

    /// <summary>このマスを障害物が覆っているか。</summary>
    public bool Covers(Vector2Int position) =>
        position.x >= cell.x && position.x < cell.x + size.x &&
        position.y >= cell.y && position.y < cell.y + size.y;

    private void Start()
    {
        if (!blockMovement) return;

        // BattleStageLoader.Awake でグリッドのサイズが決まった後に実行される
        grid = FindAnyObjectByType<BattleGrid>();
        if (grid == null)
        {
            Debug.LogWarning("GridObstacle: シーンに BattleGrid がありません。", this);
            return;
        }
        SetCells(false);
    }

    private void OnDestroy()
    {
        // ステージを片付けたときに通行不可を戻す
        if (grid != null && blockMovement) SetCells(true);
    }

    private void SetCells(bool walkable)
    {
        int outside = 0;
        for (int x = cell.x; x < cell.x + size.x; x++)
        for (int y = cell.y; y < cell.y + size.y; y++)
        {
            if (!grid.SetWalkable(new Vector2Int(x, y), walkable)) outside++;
        }
        if (outside > 0 && !walkable)
            Debug.LogWarning($"GridObstacle: {outside}マスが盤面の外です({name})。", this);
    }

    /// <summary>生成ツールから設定する。</summary>
    public void Configure(Vector2Int obstacleCell, Vector2Int obstacleSize)
    {
        cell = obstacleCell;
        size = obstacleSize;
    }

    // ---------------------------------------------------------------- エディタ表示

    private void OnDrawGizmosSelected()
    {
        // 覆っているマスを赤で表示(グリッドの原点=親ステージの原点と仮定)
        Transform origin = transform.parent != null ? transform.root : transform;
        float cs = 1f;
        BattleGrid g = FindAnyObjectByType<BattleGrid>();
        if (g != null) cs = g.CellSize;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = origin.localToWorldMatrix;
        Vector3 center = new Vector3((cell.x + size.x * 0.5f) * cs, 0.05f, (cell.y + size.y * 0.5f) * cs);
        Gizmos.DrawCube(center, new Vector3(size.x * cs, 0.1f, size.y * cs));
        Gizmos.matrix = old;
    }
}
