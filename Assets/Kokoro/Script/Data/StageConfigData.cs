using System;

/// <summary>
/// StageConfig.csv の1戦闘マップ分。
/// StageIdは戦闘マップ、StoryIdはストーリーの章番号。
/// </summary>
public sealed class StageConfigData
{
    public string StageId { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string FieldName { get; private set; }
    public string Memo { get; private set; }

    // CSVのSTORYory_IDを読み込む。
    public int StoryId { get; private set; }

    public static StageConfigData FromCsv(CsvRow row)
    {
        if (row == null)
            throw new ArgumentNullException(nameof(row));

        var data = new StageConfigData
        {
            StageId = row.Get("StageID"),
            Width = row.GetInt("Width"),
            Height = row.GetInt("Height"),
            FieldName = row.Get("3D_Name"),
            Memo = row.Get("Memo"),
            StoryId = row.GetInt("STORYory_ID")
        };

        if (string.IsNullOrWhiteSpace(data.StageId) ||
            data.Width <= 0 || data.Height <= 0)
            throw new FormatException("StageID、Width、Heightの指定が不正です。");

        if (data.StoryId <= 0)
            throw new FormatException("STORYory_IDは1以上にしてください。");

        return data;
    }
}