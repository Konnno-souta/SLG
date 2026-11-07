using System.Collections.Generic;
using System.Text;
using UnityEngine;


public class StageSynopsisTable
{
    readonly Dictionary<string, string> table = new Dictionary<string, string>();

    public int Count => table.Count;

    public StageSynopsisTable(TextAsset csv, string idColumn = "id", string textColumn = "synopsis")
    {
        if (csv == null) return;

        List<List<string>> rows = Parse(csv.text);
        if (rows.Count == 0) return;

        // 見出し行から列の位置を探す（見つからなければ 0列目 = id、1列目 = 本文）
        List<string> header = rows[0];
        int idCol = header.FindIndex(h => h.Trim() == idColumn);
        int textCol = header.FindIndex(h => h.Trim() == textColumn);
        if (idCol < 0) idCol = 0;
        if (textCol < 0) textCol = 1;

        for (int i = 1; i < rows.Count; i++)
        {
            List<string> row = rows[i];
            if (row.Count <= Mathf.Max(idCol, textCol)) continue;

            string id = row[idCol].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            string text = row[textCol].Replace("\\n", "\n");
            if (table.ContainsKey(id))
                Debug.LogWarning($"[StageSynopsisTable] id「{id}」が重複しています（{csv.name} の {i + 1} 行目）");
            table[id] = text;
        }
    }

    public bool TryGet(string id, out string synopsis) => table.TryGetValue(id ?? "", out synopsis);

    // CSV を行・列に分ける（"" で囲んだ中のカンマや改行にも対応）
    static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        // 先頭の BOM を取り除く
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } // "" は " 1文字
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* 無視（\r\n 対策） */ }
                else if (c == '\n')
                {
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else field.Append(c);
            }
        }

        // 最後の行（末尾に改行が無い場合）
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}