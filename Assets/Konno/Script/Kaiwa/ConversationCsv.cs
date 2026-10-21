using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Conversation
{
    public enum ScriptSource
    {
        Text, // テキスト形式（Script Asset）
        Csv   // CSV（Csv Asset + Story Id）
    }

    /// <summary>CSVの見出し名（1行目）。表に合わせてインスペクターで変更できる</summary>
    [Serializable]
    public class CsvColumns
    {
        public string storyId = "story_id";
        public string textId = "text_id";
        public string characterId = "character_id";
        [Tooltip("キャラクタービジュアル（Database の表情名）")]
        public string visual = "visual";
        [Tooltip("表示するキャラクター名（空なら Database の表示名）")]
        public string characterName = "name";
        public string text = "text";
        [Tooltip("BGM列（例: town / town fade=2 vol=0.8 / stop）。その行の表示前に流れる")]
        public string bgm = "bgm";
        [Tooltip("SE列（例: door / door vol=0.5 delay=0.2）。その行の表示前に鳴る")]
        public string se = "se";
        [Tooltip("区切り文字。CSVなら , / TSVなら \\t")]
        public string delimiter = ",";
    }

    /// <summary>CSVの1行分</summary>
    public class CsvRow
    {
        public int rowNumber;         // CSV上の行番号（見出し=1）
        public string storyId;
        public string textId;
        public string characterId;
        public string visual;
        public string characterName;
        public string text;
        public string bgm;
        public string se;
        /// <summary>全列（見出し名 → 値）。追加の列を使いたい時用</summary>
        public Dictionary<string, string> columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Get(string header) => columns.TryGetValue(header, out var v) ? v : null;
    }

    /// <summary>
    /// 会話CSVの読み込み。
    /// ・story_id が空の行は、上の行と同じストーリーとして扱う
    /// ・text が @ / * / - で始まる行はコマンド行（@bg, @show, @choice, *ラベル など）
    /// </summary>
    public class ConversationCsv
    {
        public readonly List<CsvRow> rows = new List<CsvRow>();
        public readonly List<string> headers = new List<string>();

        // ================= 読み込み =================

        public static ConversationCsv Parse(string source, CsvColumns columns = null)
        {
            columns = columns ?? new CsvColumns();
            var csv = new ConversationCsv();
            char delim = string.IsNullOrEmpty(columns.delimiter) ? ','
                       : columns.delimiter == "\\t" ? '\t' : columns.delimiter[0];

            // 見出し行を探す（上にタイトル行などがあっても、見出しのある行から読む）
            var table = SplitCsv(source, delim);
            int headerRow = FindHeaderRow(table, columns);

            // 見つからなければ、区切り文字を変えて試す（タブ区切り・セミコロン区切り）
            if (headerRow < 0)
            {
                foreach (var alt in new[] { '\t', ';', ',' })
                {
                    if (alt == delim) continue;
                    var t2 = SplitCsv(source, alt);
                    int h2 = FindHeaderRow(t2, columns);
                    if (h2 < 0) continue;
                    Debug.Log($"[ConversationCsv] 区切り文字を「{(alt == '\t' ? "タブ" : alt.ToString())}」として読み込みました");
                    table = t2; headerRow = h2; delim = alt;
                    break;
                }
            }

            if (table.Count == 0)
            {
                Debug.LogWarning("[ConversationCsv] CSV が空です");
                return csv;
            }

            if (headerRow < 0)
            {
                // どの列も見つからない → 実際の1行目を表示して原因を分かりやすくする
                string first = "";
                foreach (var row in table) if (!IsEmptyRow(row)) { first = string.Join(" | ", row); break; }
                if (first.Length > 120) first = first.Substring(0, 120) + "…";
                Debug.LogWarning(
                    $"[ConversationCsv] 見出し行が見つかりません。見出しに「{columns.storyId}」と「{columns.text}」が必要です。\n" +
                    $"実際の1行目: [{first}]\n" +
                    "・会話テキスト（.txt）を Csv Asset に入れていないか\n" +
                    "・見出しを日本語などに変えた場合は、ConversationPlayer の Csv Columns も同じ文字にする");
                headerRow = 0;
            }

            foreach (var h in table[headerRow]) csv.headers.Add(h.Trim());

            int iStory = csv.FindColumn(columns.storyId);
            int iTextId = csv.FindColumn(columns.textId);
            int iChara = csv.FindColumn(columns.characterId);
            int iVisual = csv.FindColumn(columns.visual);
            int iName = csv.FindColumn(columns.characterName);
            int iText = csv.FindColumn(columns.text);
            int iBgm = csv.FindColumn(columns.bgm);
            int iSe = csv.FindColumn(columns.se);

            string found = $"（見つかった見出し: {string.Join(", ", csv.headers)}）";
            if (iText < 0) Debug.LogWarning($"[ConversationCsv] テキスト列 '{columns.text}' が見つかりません{found}");
            if (iStory < 0) Debug.LogWarning($"[ConversationCsv] ストーリーID列 '{columns.storyId}' が見つかりません{found}");

            string lastStory = "";
            for (int r = headerRow + 1; r < table.Count; r++)
            {
                var cells = table[r];
                if (IsEmptyRow(cells)) continue;

                var row = new CsvRow
                {
                    rowNumber = r + 1,
                    storyId = Cell(cells, iStory),
                    textId = Cell(cells, iTextId),
                    characterId = Cell(cells, iChara),
                    visual = Cell(cells, iVisual),
                    characterName = Cell(cells, iName),
                    text = Cell(cells, iText, trim: false),
                    bgm = Cell(cells, iBgm),
                    se = Cell(cells, iSe)
                };

                // ストーリーIDが空なら上の行を引き継ぐ
                if (string.IsNullOrEmpty(row.storyId)) row.storyId = lastStory;
                else lastStory = row.storyId;

                for (int c = 0; c < csv.headers.Count; c++)
                    if (!string.IsNullOrEmpty(csv.headers[c]))
                        row.columns[csv.headers[c]] = c < cells.Count ? cells[c] : "";

                csv.rows.Add(row);
            }
            return csv;
        }

        /// <summary>上から20行以内で、ストーリーID列かテキスト列の見出しがある行を探す</summary>
        static int FindHeaderRow(List<List<string>> table, CsvColumns columns)
        {
            int limit = Mathf.Min(table.Count, 20);
            for (int r = 0; r < limit; r++)
                foreach (var cell in table[r])
                {
                    string c = cell.Trim();
                    if (string.Equals(c, columns.text?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c, columns.storyId?.Trim(), StringComparison.OrdinalIgnoreCase))
                        return r;
                }
            return -1;
        }

        int FindColumn(string header)
        {
            if (string.IsNullOrEmpty(header)) return -1;
            for (int i = 0; i < headers.Count; i++)
                if (string.Equals(headers[i], header.Trim(), StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        // ================= 取得 =================

        /// <summary>CSVに含まれるストーリーIDの一覧（登場順）</summary>
        public List<string> GetStoryIds()
        {
            var list = new List<string>();
            foreach (var r in rows)
                if (!string.IsNullOrEmpty(r.storyId) && !list.Contains(r.storyId))
                    list.Add(r.storyId);
            return list;
        }

        /// <summary>指定ストーリーの行</summary>
        public List<CsvRow> GetRows(string storyId)
        {
            var list = new List<CsvRow>();
            foreach (var r in rows)
                if (string.Equals(r.storyId, storyId, StringComparison.OrdinalIgnoreCase))
                    list.Add(r);
            return list;
        }

        /// <summary>テキストIDから行を取得</summary>
        public CsvRow FindByTextId(string textId)
        {
            foreach (var r in rows)
                if (string.Equals(r.textId, textId, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }

        /// <summary>指定ストーリーに登場するキャラクターIDの一覧</summary>
        public List<string> GetCharacterIds(string storyId = null)
        {
            var list = new List<string>();
            foreach (var r in storyId == null ? rows : GetRows(storyId))
                if (!string.IsNullOrEmpty(r.characterId) && !list.Contains(r.characterId))
                    list.Add(r.characterId);
            return list;
        }

        // ================= 会話スクリプトへ変換 =================

        public ConversationScript BuildScript(string storyId)
        {
            var target = GetRows(storyId);
            if (target.Count == 0)
            {
                var ids = GetStoryIds();
                Debug.LogWarning($"[ConversationCsv] ストーリーID '{storyId}' の行がありません" +
                    (ids.Count > 0 ? $"（CSV にあるストーリーID: {string.Join(", ", ids)}）" : ""));
                return null;
            }

            var sb = new StringBuilder();
            var meta = new Dictionary<int, CsvRow>();
            int line = 1;

            foreach (var row in target)
            {
                // BGM・SE列 → その行の前にコマンドとして入れる
                if (!string.IsNullOrEmpty(row.bgm)) { sb.Append("@bgm ").Append(row.bgm).Append('\n'); line++; }
                if (!string.IsNullOrEmpty(row.se))
                {
                    // 「door / bell delay=0.5」のように / 区切りで複数鳴らせる
                    foreach (var se in row.se.Split('/'))
                    {
                        if (se.Trim().Length == 0) continue;
                        sb.Append("@se ").Append(se.Trim()).Append('\n');
                        line++;
                    }
                }

                string text = (row.text ?? "").Replace("\r", "");
                string trimmed = text.Trim();
                if (trimmed.Length == 0 && string.IsNullOrEmpty(row.characterId)) continue;

                // コマンド行（@bg, @show, @choice, - 選択肢, *ラベル, // コメント）
                if (trimmed.StartsWith("@") || trimmed.StartsWith("*") || trimmed.StartsWith("-") || trimmed.StartsWith("//"))
                {
                    foreach (var l in trimmed.Split('\n'))
                    {
                        sb.Append(l.Trim()).Append('\n');
                        line++;
                    }
                    continue;
                }

                // セリフ行 → 「キャラID(ビジュアル): テキスト」に変換
                string speaker = row.characterId ?? "";
                if (!string.IsNullOrEmpty(row.visual) && speaker.Length > 0) speaker += $"({row.visual})";

                var lines = text.Split('\n');
                meta[line] = row;
                sb.Append(speaker).Append(": ").Append(lines[0].Trim()).Append('\n');
                line++;
                for (int k = 1; k < lines.Length; k++)
                {
                    sb.Append("  ").Append(lines[k].Trim()).Append('\n');
                    line++;
                }
            }

            var script = ConversationScript.Parse(sb.ToString());

            // テキストID・表示名を付け直す
            foreach (var cmd in script.commands)
            {
                if (cmd.type != CommandType.Line) continue;
                if (!meta.TryGetValue(cmd.lineNumber, out var row)) continue;
                cmd.textId = row.textId;
                cmd.displayName = row.characterName;
                cmd.storyId = row.storyId;
            }
            return script;
        }

        // ================= CSV分解 =================

        static List<List<string>> SplitCsv(string s, char delim)
        {
            var table = new List<List<string>>();
            if (string.IsNullOrEmpty(s)) return table;
            if (s[0] == '\uFEFF') s = s.Substring(1); // BOM除去

            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < s.Length && s[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == delim) { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    row.Add(cell.ToString()); cell.Clear();
                    table.Add(row);
                    row = new List<string>();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                table.Add(row);
            }
            return table;
        }

        static string Cell(List<string> cells, int index, bool trim = true)
        {
            if (index < 0 || index >= cells.Count) return "";
            return trim ? cells[index].Trim() : cells[index];
        }

        static bool IsEmptyRow(List<string> cells)
        {
            foreach (var c in cells) if (!string.IsNullOrWhiteSpace(c)) return false;
            return true;
        }
    }
}