using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage_CharaLayout.csvからユニットを生成する。
/// Startでの自動実行はしない。BattleStageLoader.Startから呼ぶ。
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
    [SerializeField, Min(0)] private int temporaryMoveRange = 3;

    [Header("Capsuleテスト用の色分け（未設定でも動く）")]
    [SerializeField] private Material allyMaterial;
    [SerializeField] private Material enemyMaterial;

    /// <summary>ステージを指定してCSVの配置を行う。検証でエラーがあれば生成しない。</summary>
    public bool SpawnStage(string stageId, TextAsset layoutCsv)
    {
        if (repository == null || !repository.IsLoaded ||
            grid == null || selectionController == null ||
            unitPrefab == null || layoutCsv == null)
        {
            Debug.LogError("BattleUnitSpawner: 参照かGameDataRepositoryの読み込みに失敗しました。", this);
            return false;
        }

        List<CsvRow> rows;
        try
        {
            rows = CsvReader.Read(layoutCsv, "StageID");
        }
        catch (Exception ex)
        {
            Debug.LogError($"{layoutCsv.name}: CSV解析失敗: {ex.Message}", this);
            return false;
        }

        // 先に全行検証し、問題があれば中途半端な生成を避ける。
        var records = new List<BattleUnitRecord>();
        var positions = new HashSet<Vector2Int>();
        int errors = 0;
        foreach (CsvRow row in rows)
        {
            if (row.Get("StageID") != stageId) continue;
            try
            {
                BattleUnitRecord record = BattleUnitRecord.FromCsv(row);
                if (!repository.TryGetCharacter(record.CharacterId, out CharacterData character))
                    throw new FormatException($"CharacterID={record.CharacterId} がCharacter.csvにありません。");
                if (record.Team != character.Team)
                    throw new FormatException($"Team={record.Team} とCharacter.csvのTeam={character.Team}が違います。");
                if (!grid.IsWalkable(record.Position))
                    throw new FormatException($"CSV座標=({record.SourceX},{record.SourceY}) → " +
                        $"Unity座標={record.Position} はグリッド外または通行不可です。");
                if (!positions.Add(record.Position))
                    throw new FormatException($"座標({record.SourceX},{record.SourceY})に配置が重複しています。");

                records.Add(record);
            }
            catch (Exception ex)
            {
                errors++;
                Debug.LogError($"{layoutCsv.name} {row.LineNumber}行目: {ex.Message}", this);
            }
        }
        if (errors > 0)
        {
            Debug.LogError($"{stageId}: 配置CSVに{errors}件のエラー。生成を中止しました。", this);
            return false;
        }
        if (records.Count == 0)
        {
            Debug.LogWarning($"{layoutCsv.name}: StageID={stageId} の配置がありません。", this);
            return false;
        }

        int generated = 0;
        foreach (BattleUnitRecord record in records)
        {
            repository.TryGetCharacter(record.CharacterId, out CharacterData character);
            UnitView instance = Instantiate(unitPrefab, unitContainer);

            // 暫定ID。CSV行の並べ替えに弱いのでセーブ用には使わないこと。
            string temporarySpawnId = $"{stageId}_Row{record.SourceLine:D3}";
            if (!instance.Configure(character, temporarySpawnId,
                    record.Position, temporaryMoveRange) ||
                !selectionController.RegisterSpawnedUnit(instance))
            {
                Destroy(instance.gameObject);
                Debug.LogError($"{stageId}: {record.CharacterId} の生成・登録に失敗。", this);
                return false;
            }

            Material material = character.Team == "Player" ? allyMaterial : enemyMaterial;
            Renderer visual = instance.GetComponentInChildren<Renderer>();
            if (visual != null && material != null) visual.sharedMaterial = material;
            generated++;
            Debug.Log($"生成: {character.Id} {character.Name} ({record.SourceX},{record.SourceY}) " +
                $"→ 内部{record.Position} [{character.Team}]", instance);
        }
        Debug.Log($"{stageId}: {generated}体生成成功", this);
        return true;
    }
}
