using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleUnit.csvから、指定した戦闘のユニットを配置する。
/// ステージサイズ／障害物は引き続きBattleGrid（開発用）で管理。
/// </summary>
public sealed class BattleUnitSpawner : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private GameDataRepository repository;
    [SerializeField] private BattleGrid grid;
    [SerializeField] private UnitSelectionController selectionController;
    [SerializeField] private TextAsset battleUnitCsv;
    [SerializeField] private UnitView unitPrefab;

    [Header("ステージ指定")]
    [SerializeField] private string battleId = "B001";
    [SerializeField] private Transform unitContainer;

    [Header("仮モデルの所属色（未設定ならPrefabの色を使う）")]
    [SerializeField] private Material allyMaterial;
    [SerializeField] private Material enemyMaterial;

    private void Start()
    {
        SpawnBattleUnits();
    }

    public void SpawnBattleUnits()
    {
        if (repository == null || !repository.IsLoaded ||
            grid == null || selectionController == null ||
            battleUnitCsv == null || unitPrefab == null)
        {
            Debug.LogError("BattleUnitSpawner: Inspector参照またはCSV読み込み状態を確認してください。", this);
            return;
        }

        List<CsvRow> rows;
        try
        {
            rows = CsvReader.Read(battleUnitCsv, "BattleID");
        }
        catch (Exception ex)
        {
            Debug.LogError($"BattleUnit.csvの解析に失敗: {ex.Message}", this);
            return;
        }

        var usedSpawns = new HashSet<string>(StringComparer.Ordinal);
        var usedPositions = new HashSet<Vector2Int>();
        int spawned = 0;
        int errors = 0;
        int foundRows = 0;

        foreach (CsvRow row in rows)
        {
            // BattleIDが異なる行は別の戦闘に属する。
            if (row.Get("BattleID") != battleId) continue;
            foundRows++;
            try
            {
                BattleUnitRecord data = BattleUnitRecord.FromCsv(row);

                if (!usedSpawns.Add(data.SpawnId))
                    throw new FormatException($"SpawnID={data.SpawnId} が重複しています。");
                if (!usedPositions.Add(data.Position))
                    throw new FormatException($"初期位置={data.Position} が重複しています。");
                if (!grid.IsWalkable(data.Position))
                    throw new FormatException($"初期位置={data.Position} は範囲外または通行不可です。");
                if (!repository.TryGetCharacter(data.CharacterId, out CharacterData character))
                    throw new FormatException($"Character_ID={data.CharacterId} がCharacter.csvにありません。");
                if (character.PositionId != 1 && character.PositionId != 2)
                    throw new FormatException($"Character_ID={character.Id} の所属ID={character.PositionId} が不正です。");

                UnitView instance = Instantiate(unitPrefab, unitContainer);
                if (!instance.Configure(character, data.SpawnId, data.Position, data.MoveRange) ||
                    !selectionController.RegisterSpawnedUnit(instance))
                {
                    Destroy(instance.gameObject);
                    throw new InvalidOperationException("ユニットの初期化・登録に失敗しました。");
                }

                // 仮のCapsuleだけを対象とした着色。正式モデル導入時は別方式へ移す。
                Material material = character.PositionId == 1 ? allyMaterial : enemyMaterial;
                Renderer visual = instance.GetComponentInChildren<Renderer>();
                if (visual != null && material != null)
                    visual.sharedMaterial = material;

                spawned++;
                Debug.Log($"生成: {data.SpawnId} / {character.Name} / " +
                          $"HP={character.BaseHP} / {data.Position}", instance);
            }
            catch (Exception ex)
            {
                errors++;
                Debug.LogError($"{battleUnitCsv.name} {row.LineNumber}行目: {ex.Message}", this);
            }
        }

        if (foundRows == 0)
            Debug.LogWarning($"BattleID={battleId} の配置データが見つかりません。", this);

        Debug.Log($"BattleID={battleId}: {spawned}体生成、エラー{errors}件", this);
    }
}
