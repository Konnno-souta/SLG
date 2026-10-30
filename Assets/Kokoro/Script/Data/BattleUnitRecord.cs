using System;
using UnityEngine;

/// <summary>
/// プランナーのStage_CharaLayout.csv の1行。
/// X/Yは1始まりなので、読込時に一度だけ-1する。
/// 現CSVにはSpawnIDがないので、セーブ用の安定個体IDはまだ未対応。
/// </summary>
public sealed class BattleUnitRecord
{
    public string StageId { get; private set; }
    public string CharacterId { get; private set; }
    public string Team { get; private set; }
    public string Memo { get; private set; }
    public int SourceLine { get; private set; }
    public int SourceX { get; private set; }
    public int SourceY { get; private set; }
    public Vector2Int Position { get; private set; }

    public static BattleUnitRecord FromCsv(CsvRow row)
    {
        int csvX = row.GetInt("X");
        int csvY = row.GetInt("Y");
        if (csvX < 1 || csvY < 1)
            throw new FormatException($"X/Yは1始まりです。受信値=({csvX},{csvY})");

        var data = new BattleUnitRecord
        {
            StageId = row.Get("StageID"),
            CharacterId = row.Get("CharacterID"),
            Team = row.Get("Team"),
            Memo = row.Get("Name"),
            SourceLine = row.LineNumber,
            SourceX = csvX,
            SourceY = csvY,
            Position = new Vector2Int(csvX - 1, csvY - 1)
        };
        if (string.IsNullOrWhiteSpace(data.StageId) ||
            string.IsNullOrWhiteSpace(data.CharacterId))
            throw new FormatException("StageIDとCharacterIDは必須です。");
        if (data.Team != "Player" && data.Team != "Enemy")
            throw new FormatException($"Team={data.Team} は Player または Enemy にしてください。");
        return data;
    }
}
