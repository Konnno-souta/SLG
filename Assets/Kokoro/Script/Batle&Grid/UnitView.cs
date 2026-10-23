using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage5版のUnitViewを更新。自動配置と従来の手動配置の両方を保持。
/// UnitState・GridPathfinder・UnitSelectionControllerは既存のまま使用できる。
/// </summary>
public sealed class UnitView : MonoBehaviour
{
    [Header("手動配置用（CSV生成時には上書き）")]
    [SerializeField] private UnitTeam team = UnitTeam.Ally;
    [SerializeField] private Vector2Int initialPosition = new Vector2Int(1, 1);
    [SerializeField, Min(0)] private int moveRange = 3;

    [Header("移動演出")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3f;

    public Vector2Int InitialPosition => initialPosition;
    public UnitState State { get; private set; }
    public CharacterData MasterData { get; private set; }
    public string SpawnId { get; private set; }

    /// <summary>
    /// 自動生成されたPrefabをUnitSelectionControllerに登録する前に呼ぶ。
    /// ※tempMoveRangeはCSV未確定の暫定値。
    /// </summary>
    public bool Configure(CharacterData character, string spawnId,
        Vector2Int position, int tempMoveRange)
    {
        if (State != null || character == null || string.IsNullOrWhiteSpace(spawnId))
            return false;
        if (character.Team != "Player" && character.Team != "Enemy")
        {
            Debug.LogError($"{name}: CSVのTeamが不明: {character.Team}", this);
            return false;
        }

        MasterData = character;
        SpawnId = spawnId;
        team = character.Team == "Player" ? UnitTeam.Ally : UnitTeam.Enemy;
        initialPosition = position;
        moveRange = Mathf.Max(0, tempMoveRange);
        gameObject.name = $"{spawnId}_{character.Name}";
        return true;
    }

    public bool Initialize(BattleGrid grid)
    {
        if (grid == null || !grid.IsWalkable(initialPosition) || State != null)
        {
            Debug.LogError($"{name}: 初期化失敗。座標や重複初期化を確認。", this);
            return false;
        }
        State = new UnitState(team, initialPosition, moveRange);
        SyncPosition(grid);
        return true;
    }

    public void SyncPosition(BattleGrid grid)
    {
        if (grid == null || State == null) return;
        transform.position = grid.CellToWorld(State.Position);
    }

    public IEnumerator MoveAlongPath(BattleGrid grid, IReadOnlyList<Vector2Int> path)
    {
        if (grid == null || path == null) yield break;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 target = grid.CellToWorld(path[i]);
            while ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
        }
    }
}
