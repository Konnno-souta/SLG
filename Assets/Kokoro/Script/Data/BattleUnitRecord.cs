using UnityEngine;

/// <summary>
/// BattleUnit.csv の1行。Character_IDはCharacter.csvを参照する。
/// MoveRangeは移動力仕様が未確定のため、一時的に配置CSVから与える。
/// </summary>
public sealed class BattleUnitRecord
{
    public string BattleId { get; private set; }
    public string SpawnId { get; private set; }
    public int CharacterId { get; private set; }
    public Vector2Int Position { get; private set; }
    public int MoveRange { get; private set; }

    public static BattleUnitRecord FromCsv(CsvRow row)
    {
        var record = new BattleUnitRecord
        {
            BattleId = row.Get("BattleID"),
            SpawnId = row.Get("SpawnID"),
            CharacterId = row.GetInt("Character_ID"),
            Position = new Vector2Int(row.GetInt("GridX"), row.GetInt("GridY")),
            MoveRange = row.GetInt("MoveRange")
        };

        if (string.IsNullOrWhiteSpace(record.BattleId) ||
            string.IsNullOrWhiteSpace(record.SpawnId))
            throw new System.FormatException("BattleID / SpawnID は必須です。");

        if (record.CharacterId <= 0 || record.MoveRange < 0)
            throw new System.FormatException("Character_ID または MoveRange が不正です。");

        return record;
    }
}
