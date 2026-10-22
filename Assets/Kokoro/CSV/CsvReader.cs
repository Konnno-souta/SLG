using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// CSVの1行。列名を使って値を読み取れる。
/// </summary>
public sealed class CsvRow
{
    private readonly Dictionary<string, int> columns;
    private readonly string[] values;

    public string FileName { get; }
    public int LineNumber { get; }

    internal CsvRow(string fileName, int lineNumber,
        Dictionary<string, int> columns, string[] values)
    {
        FileName = fileName;
        LineNumber = lineNumber;
        this.columns = columns;
        this.values = values;
    }

    public string Get(string columnName)
    {
        if (!columns.TryGetValue(columnName, out int index))
        {
            throw new FormatException(
                $"{FileName} {LineNumber}行目: 列「{columnName}」がありません。");
        }

        return values[index].Trim();
    }

    public int GetInt(string columnName)
    {
        string value = Get(columnName);
        if (!int.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int result))
        {
            throw new FormatException(
                $"{FileName} {LineNumber}行目: {columnName} は整数が必要です。値=「{value}」");
        }
        return result;
    }

    public float GetFloat(string columnName)
    {
        string value = Get(columnName);
        if (!float.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out float result) ||
            float.IsNaN(result) || float.IsInfinity(result))
        {
            throw new FormatException(
                $"{FileName} {LineNumber}行目: {columnName} は数値が必要です。値=「{value}」");
        }
        return result;
    }
}

/// <summary>
/// UTF-8 CSVを解析する共通クラス。
/// ダブルクォート内のカンマ・改行・""を処理する。
/// 先頭のメモ行は、指定した主キー列のヘッダーまでスキップする。
/// </summary>
public static class CsvReader
{
    private sealed class RawRecord
    {
        public int Line;
        public string[] Fields;
    }

    public static List<CsvRow> Read(TextAsset asset, string primaryKeyColumn)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));

        List<RawRecord> records = Parse(asset.text, asset.name);
        int headerIndex = -1;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].Fields.Length > 0 &&
                records[i].Fields[0].Trim().TrimStart('\uFEFF') == primaryKeyColumn)
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
            throw new FormatException(
                $"{asset.name}: ヘッダー「{primaryKeyColumn}」が見つかりません。");

        string[] header = records[headerIndex].Fields;
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < header.Length; i++)
        {
            string name = header[i].Trim().TrimStart('\uFEFF');
            if (string.IsNullOrEmpty(name))
                continue;

            if (columns.ContainsKey(name))
                throw new FormatException($"{asset.name}: 列名「{name}」が重複しています。");
            columns.Add(name, i);
        }

        var result = new List<CsvRow>();
        for (int i = headerIndex + 1; i < records.Count; i++)
        {
            RawRecord record = records[i];
            bool empty = true;
            foreach (string value in record.Fields)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    empty = false;
                    break;
                }
            }
            if (empty)
                continue;

            if (record.Fields.Length != header.Length)
            {
                throw new FormatException(
                    $"{asset.name} {record.Line}行目: 列数が違います。" +
                    $"ヘッダー={header.Length}列、データ={record.Fields.Length}列");
            }

            result.Add(new CsvRow(asset.name, record.Line, columns, record.Fields));
        }
        return result;
    }

    private static List<RawRecord> Parse(string text, string fileName)
    {
        var records = new List<RawRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int line = 1;
        int recordStartLine = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (inQuotes)
                {
                    inQuotes = false;
                }
                else if (field.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    throw new FormatException(
                        $"{fileName} {line}行目: ダブルクォートの位置が不正です。");
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (inQuotes)
                {
                    field.Append('\n');
                }
                else
                {
                    fields.Add(field.ToString());
                    records.Add(new RawRecord
                    {
                        Line = recordStartLine,
                        Fields = fields.ToArray()
                    });
                    field.Clear();
                    fields.Clear();
                }

                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                line++;
                if (!inQuotes)
                    recordStartLine = line;
            }
            else
            {
                field.Append(c);
            }
        }

        if (inQuotes)
            throw new FormatException($"{fileName}: 閉じていないダブルクォートがあります。");

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new RawRecord
            {
                Line = recordStartLine,
                Fields = fields.ToArray()
            });
        }

        return records;
    }
}
