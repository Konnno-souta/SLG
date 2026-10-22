using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ユニットの3D表示と移動演出。初期配置は手動／CSVの両方式に対応。
/// </summary>
public sealed class UnitView : MonoBehaviour
{
    [Header("手動配置用（CSV自動生成時には上書きされます）")]
    [SerializeField] private UnitTeam team = UnitTeam.Ally;
    [SerializeField] private Vector2Int initialPosition = new Vector2Int(1, 1);
    [SerializeField, Min(0)] private int moveRange = 3;

    [Header("移動演出")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3f;

    public Vector2Int InitialPosition => initialPosition;
    public UnitState State { get; private set; }

    // CSV読み込み後にセットされるマスター情報。
    public CharacterData MasterData { get; private set; }
    public string SpawnId { get; private set; }

    /// <summary>自動生成直後、Initialize前に呼び出す。</summary>
    public bool Configure(
        CharacterData character, string spawnId,
        Vector2Int position, int temporaryMoveRange)
    {
        if (State != null || character == null || string.IsNullOrWhiteSpace(spawnId))
            return false;

        if (character.PositionId != 1 && character.PositionId != 2)
        {
            Debug.LogError($"{name}: 所属ID={character.PositionId}は未対応です。", this);
            return false;
        }

        MasterData = character;
        SpawnId = spawnId;
        team = character.PositionId == 1 ? UnitTeam.Ally : UnitTeam.Enemy;
        initialPosition = position;
        moveRange = Mathf.Max(0, temporaryMoveRange);
        gameObject.name = $"{spawnId}_{character.Name}";
        return true;
    }

    public bool Initialize(BattleGrid grid)
    {
        if (grid == null || !grid.IsWalkable(initialPosition) || State != null)
        {
            Debug.LogError($"{name}: 初期化できません。配置先または状態を確認してください。", this);
            return false;
        }

        State = new UnitState(team, initialPosition, moveRange);
        SyncPosition(grid);
        return true;
    }

    public void SyncPosition(BattleGrid grid)
    {
        if (grid == null || State == null)
            return;

        transform.position = grid.CellToWorld(State.Position);
    }

    public IEnumerator MoveAlongPath(BattleGrid grid, IReadOnlyList<Vector2Int> path)
    {
        if (grid == null || path == null)
            yield break;

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
