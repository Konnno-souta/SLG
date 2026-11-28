using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage_CharaLayout.csvからユニットを生成する。
/// Startでの自動実行はしない。
/// BattleStageLoader.Startから呼ぶ。
/// </summary>
public sealed class BattleUnitSpawner : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private GameDataRepository repository;
    [SerializeField] private BattleGrid grid;
    [SerializeField] private UnitSelectionController selectionController;
    [SerializeField] private UnitView unitPrefab;
    [SerializeField] private Transform unitContainer;

    [Header("正式CSVに移動力列がない間だけ使う仮値")]
    [SerializeField, Min(0)]
    private int temporaryMoveRange = 3;

    [Header("Capsuleテスト用の色分け（未設定でも動く）")]
    [SerializeField] private Material allyMaterial;
    [SerializeField] private Material enemyMaterial;

    /// <summary>
    /// ステージを指定してCSVの配置を行う。
    /// 検証でエラーがあれば生成しない。
    /// </summary>
    public bool SpawnStage(string stageId, TextAsset layoutCsv)
    {
        if (repository == null || !repository.IsLoaded ||
            grid == null || selectionController == null ||
            unitPrefab == null || layoutCsv == null)
        {
            Debug.LogError(
                "BattleUnitSpawner: 参照かGameDataRepositoryの読み込みに失敗しました。",
                this
            );

            return false;
        }

        List<CsvRow> rows;

        try
        {
            rows = CsvReader.Read(layoutCsv, "StageID");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"{layoutCsv.name}: CSV解析失敗: {ex.Message}",
                this
            );

            return false;
        }

        // 先に全行検証し、
        // 問題があれば中途半端な生成を避ける。
        var records =
            new List<(BattleUnitRecord Record, string SpawnId)>();

        var positions = new HashSet<Vector2Int>();

        int errors = 0;

        for (int rowIndex = 0;
             rowIndex < rows.Count;
             rowIndex++)
        {
            CsvRow row = rows[rowIndex];

            if (row.Get("StageID") != stageId)
                continue;

            try
            {
                BattleUnitRecord record =
                    BattleUnitRecord.FromCsv(row);

                if (!repository.TryGetCharacter(
                    record.CharacterId,
                    out CharacterData character))
                {
                    throw new FormatException(
                        $"CharacterID={record.CharacterId} が" +
                        "Character.csvにありません。"
                    );
                }

                if (record.Team != character.Team)
                {
                    throw new FormatException(
                        $"Team={record.Team} と" +
                        $"Character.csvのTeam={character.Team}が違います。"
                    );
                }

                if (!grid.IsWalkable(record.Position))
                {
                    throw new FormatException(
                        $"CSV座標=({record.SourceX},{record.SourceY}) → " +
                        $"Unity座標={record.Position} は" +
                        "グリッド外または通行不可です。"
                    );
                }

                if (!positions.Add(record.Position))
                {
                    throw new FormatException(
                        $"座標({record.SourceX},{record.SourceY})に" +
                        "配置が重複しています。"
                    );
                }

                // =================================
                // セーブ用の個体ID生成
                // =================================

                // CSVの物理行番号ではなく、
                // ヘッダーを1行目とみなした行番号を使用する。
                // これにより冒頭の説明行の影響を受けない。
                //
                // ただし、データ行自体の追加・削除・
                // 並べ替えには非対応。

                int logicalRowNumber = rowIndex + 2;

                string spawnId =
                    $"{record.StageId}_Row{logicalRowNumber:D3}";

                records.Add((record, spawnId));
            }
            catch (Exception ex)
            {
                errors++;

                Debug.LogError(
                    $"{layoutCsv.name} {row.LineNumber}行目: " +
                    ex.Message,
                    this
                );
            }
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"{stageId}: 配置CSVに{errors}件のエラー。" +
                "生成を中止しました。",
                this
            );

            return false;
        }

        if (records.Count == 0)
        {
            Debug.LogWarning(
                $"{layoutCsv.name}: StageID={stageId} の配置がありません。",
                this
            );

            return false;
        }

        int generated = 0;

        foreach (var pending in records)
        {
            BattleUnitRecord record = pending.Record;

            repository.TryGetCharacter(
                record.CharacterId,
                out CharacterData character
            );

            UnitView instance = Instantiate(
                unitPrefab,
                unitContainer
            );

            if (!instance.Configure(
                    character,
                    pending.SpawnId,
                    record.Position,
                    temporaryMoveRange) ||
                !selectionController.RegisterSpawnedUnit(instance))
            {
                Destroy(instance.gameObject);

                Debug.LogError(
                    $"{stageId}: {record.CharacterId} の" +
                    "生成・登録に失敗。",
                    this
                );

                return false;
            }

            Material material =
                character.Team == "Player"
                    ? allyMaterial
                    : enemyMaterial;

            Renderer visual =
                instance.GetComponentInChildren<Renderer>();

            if (visual != null && material != null)
            {
                visual.sharedMaterial = material;
            }

            generated++;

            Debug.Log(
                $"生成: {character.Id} {character.Name} " +
                $"({record.SourceX},{record.SourceY}) " +
                $"→ 内部{record.Position} " +
                $"[{pending.SpawnId}]",
                instance
            );
        }

        Debug.Log(
            $"{stageId}: {generated}体生成成功",
            this
        );

        return true;
    }
}