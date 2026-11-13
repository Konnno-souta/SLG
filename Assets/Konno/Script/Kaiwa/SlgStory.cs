using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Conversation
{
    /// <summary>どちらの会話か</summary>
    public enum SlgStoryKind
    {
        Intro,   // ステージ前（IntroText）
        After    // ステージクリア後（AfterText）
    }

    /// <summary>
    /// 本文に書かれた「演出メモ」（画面暗転、〇〇エフェクト など）を、コマンドに置き換えるルール
    /// </summary>
    [Serializable]
    public class SlgDirectionRule
    {
        public enum MatchMode { Exact, Contains }

        [Tooltip("本文と比べる文字")]
        public string match = "";
        [Tooltip("Exact: 本文がこの文字と同じ / Contains: 本文にこの文字が入っている")]
        public MatchMode mode = MatchMode.Exact;
        [Tooltip("話者（Chara_ID）がこれの時だけ。空ならだれでも")]
        public string chara = "";
        [Tooltip("代わりに実行するコマンド。複数は | で区切る（例：@fade out 0.5 | @fade in 0.5）。空なら何もしない")]
        public string commands = "";
        [Tooltip("オンなら本文もセリフとして出す（オフなら本文は出さない）")]
        public bool showText = false;

        public SlgDirectionRule() { }
        public SlgDirectionRule(string match, MatchMode mode, string commands, string chara = "")
        {
            this.match = match; this.mode = mode; this.commands = commands; this.chara = chara;
        }

        public bool IsMatch(string text, string speaker)
        {
            if (string.IsNullOrEmpty(match) && string.IsNullOrEmpty(chara)) return false;
            if (!string.IsNullOrEmpty(chara) && chara.Trim() != speaker) return false;
            if (string.IsNullOrEmpty(match)) return true;
            return mode == MatchMode.Exact ? text == match.Trim() : text.Contains(match.Trim());
        }
    }

    /// <summary>リアクションID → 頭の上に出す記号</summary>
    [Serializable]
    public class SlgReactionEmote
    {
        public string reactionId = "Rea1";
        [Tooltip("頭の上に出す記号（! ? ♪ など）。空なら何も出さない")]
        public string emote = "!";
        public SlgReactionEmote() { }
        public SlgReactionEmote(string id, string emote) { reactionId = id; this.emote = emote; }
    }

    /// <summary>CSV のキャラ名 → Database のキャラID（名前が同じなら不要）</summary>
    [Serializable]
    public class SlgNameAlias
    {
        [Tooltip("CSV に書いてある名前（Chara_ID 列、2Dイラスト一覧の「クラン_通常」の「クラン」）")]
        public string name = "";
        [Tooltip("ConversationDatabase のキャラID")]
        public string characterId = "";
    }

    /// <summary>
    /// 「SLG_ストーリー会話テキスト」形式の CSV を、会話パート（ConversationPlayer）のスクリプトに変える。
    ///   Stage_ID / 〇〇Text_ID / Chara_ID / Text / 2D_ilust_ID ×3 / Reaction_ID ×3
    /// 2D_ilust_ID の3列は 左・真ん中・右 の立ち絵。空欄は「前の行のまま」、非表示のIDで退場。
    /// </summary>
    public class SlgStoryConverter
    {
        // ---- 設定（SlgStoryPlayer の Inspector から渡す） ----
        public string[] slotPositions = { "left", "center", "right" };
        public List<string> narratorNames = new List<string> { "ログ", "ナレーション" };
        public List<string> newlineMarks = new List<string> { "/n", "n/" };
        public List<SlgReactionEmote> reactionEmotes = new List<SlgReactionEmote>();
        public List<SlgNameAlias> nameAliases = new List<SlgNameAlias>();
        public List<SlgDirectionRule> directionRules = new List<SlgDirectionRule>();
        public bool clearCharactersAtStart = true;
        public bool placeCardAsNarration = true;

        static bool IsPlaceCard(string t)
        {
            t = t.Trim();
            if (t.Length < 3) return false;
            return IsWave(t[0]) && IsWave(t[t.Length - 1]);
        }

        static bool IsWave(char c) => c == '~' || c == '～' || c == '〜';

        // ---- 読み込んだ表 ----
        public class Illust { public string chara, expression; public bool hidden; }
        public class Row
        {
            public int rowNumber;
            public string stage, textId, chara, text;
            public string[] illusts = new string[3];
            public string[] reactions = new string[3];
        }

        readonly Dictionary<string, Illust> illusts = new Dictionary<string, Illust>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> reactionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>変換中に見つかった問題（コンソールに出す用）</summary>
        public readonly List<string> warnings = new List<string>();

        // =====================================================================
        // 一覧の読み込み
        // =====================================================================

        /// <summary>2Dイラスト一覧（2D01,クラン_通常 …）。会話の CSV の右側に貼ってある表でも読める</summary>
        public int LoadIllustList(string csvText)
        {
            int count = 0;
            foreach (var cells in Csv.Split(csvText))
            {
                // 行のどこにあっても「2D〇〇, 名前_表情」の組を拾う
                for (int c = 0; c + 1 < cells.Count; c++)
                {
                    string id = cells[c].Trim();
                    string use = cells[c + 1].Trim();
                    if (!IsIllustId(id) || use.Length == 0) continue;
                    var il = new Illust();
                    if (use.Contains("非表示")) il.hidden = true;
                    else
                    {
                        int u = use.LastIndexOfAny(new[] { '_', '＿' });
                        il.chara = u > 0 ? use.Substring(0, u).Trim() : use;
                        il.expression = u > 0 ? use.Substring(u + 1).Trim() : "";
                    }
                    illusts[id] = il;
                    count++;
                    break;
                }
            }
            return count;
        }

        /// <summary>リアクション一覧（Rea1,！マーク …）</summary>
        public int LoadReactionList(string csvText)
        {
            int count = 0;
            foreach (var cells in Csv.Split(csvText))
            {
                for (int c = 0; c + 1 < cells.Count; c++)
                {
                    string id = cells[c].Trim();
                    if (!id.StartsWith("Rea", StringComparison.OrdinalIgnoreCase) || id.Length < 4 || !char.IsDigit(id[3])) continue;
                    reactionLabels[id] = cells[c + 1].Trim();
                    count++;
                    break;
                }
            }
            return count;
        }

        static bool IsIllustId(string s) =>
            s.Length >= 3 && (s.StartsWith("2D", StringComparison.OrdinalIgnoreCase)) && char.IsDigit(s[2]);

        // =====================================================================
        // 会話の CSV の読み込み
        // =====================================================================

        /// <summary>会話の CSV を行のリストにする（見出しの行は「Stage_ID」がある行を自動で探す）</summary>
        public List<Row> ParseStory(string csvText, string label)
        {
            var rows = new List<Row>();
            var all = Csv.Split(csvText);

            int header = -1, iStage = -1, iTextId = -1, iChara = -1, iText = -1;
            var iIllust = new List<int>();
            var iReact = new List<int>();
            for (int r = 0; r < all.Count && header < 0; r++)
            {
                var cells = all[r];
                for (int c = 0; c < cells.Count; c++)
                    if (Norm(cells[c]) == "stage_id") { header = r; break; }
                if (header < 0) continue;

                for (int c = 0; c < cells.Count; c++)
                {
                    string n = Norm(cells[c]);
                    if (n == "stage_id") iStage = c;
                    else if (n == "chara_id") iChara = c;
                    else if (n == "text") iText = c;
                    else if (n.Contains("text") && n.EndsWith("_id") && iTextId < 0) iTextId = c;
                    else if (n == "2d_ilust_id" && iIllust.Count < 3) iIllust.Add(c);
                    else if (n == "reaction_id" && iReact.Count < 3) iReact.Add(c);
                }
            }

            if (header < 0) { warnings.Add($"{label}: 見出しの行（Stage_ID）が見つかりません"); return rows; }
            if (iText < 0) { warnings.Add($"{label}: Text の列が見つかりません"); return rows; }

            string stage = "";
            for (int r = header + 1; r < all.Count; r++)
            {
                var cells = all[r];
                string text = Cell(cells, iText);
                string st = Cell(cells, iStage);
                if (st.Length > 0) stage = st;
                if (text.Length == 0 && st.Length == 0) continue;

                var row = new Row
                {
                    rowNumber = r + 1,
                    stage = stage,
                    textId = Cell(cells, iTextId),
                    chara = Cell(cells, iChara),
                    text = text,
                };
                for (int k = 0; k < 3; k++)
                {
                    row.illusts[k] = k < iIllust.Count ? Cell(cells, iIllust[k]) : "";
                    row.reactions[k] = k < iReact.Count ? Cell(cells, iReact[k]) : "";
                }
                rows.Add(row);
            }
            return rows;
        }

        static string Cell(List<string> cells, int i) => i >= 0 && i < cells.Count ? cells[i].Trim() : "";

        static string Norm(string s) => s.Trim().Replace('＿', '_').Replace(' ', '_').ToLowerInvariant();

        // =====================================================================
        // スクリプトに変換
        // =====================================================================

        /// <summary>指定したステージの行を、会話パートのスクリプト（文字）に変える</summary>
        public string BuildScript(List<Row> rows, string stageId, string label)
        {
            var sb = new StringBuilder();
            var slots = new string[3];       // 今それぞれの位置に出ているキャラ
            var faces = new string[3];       // その表情
            int lines = 0;

            if (clearCharactersAtStart) sb.Append("@clear\n");

            foreach (var row in rows)
            {
                if (row.stage != stageId.Trim()) continue;

                // 1. 立ち絵（左・真ん中・右）
                ApplyIllusts(sb, row, slots, faces, label);

                // 2. リアクション（その位置のキャラの頭の上）
                for (int k = 0; k < 3; k++)
                {
                    string emote = EmoteFor(row.reactions[k]);
                    if (emote.Length == 0) continue;
                    if (slots[k] == null)
                    {
                        warnings.Add($"{label} {row.rowNumber}行目: リアクション {row.reactions[k]} を出す位置（{k + 1}番目）にキャラがいません");
                        continue;
                    }
                    sb.Append("@emote ").Append(slots[k]).Append(' ').Append(emote).Append('\n');
                }

                // 3. 演出メモ（画面暗転 など）
                string speaker = row.chara;
                string text = row.text;
                var rule = FindRule(text, speaker);
                if (rule != null)
                {
                    foreach (var cmd in rule.commands.Split('|'))
                    {
                        string c = cmd.Trim();
                        if (c.Length > 0) sb.Append(c.StartsWith("@") ? c : "@" + c).Append('\n');
                    }
                    if (!rule.showText) continue;
                }

                // 4. セリフ
                if (text.Length == 0) continue;
                foreach (var m in newlineMarks)
                    if (!string.IsNullOrEmpty(m)) text = text.Replace(m, "\n");
                var parts = text.Split('\n');

                // 「～城内～」のような場所の表示は、話者が書いてあってもナレーションにする
                bool place = placeCardAsNarration && IsPlaceCard(text);
                bool narration = place || speaker.Length == 0 || narratorNames.Contains(speaker);
                sb.Append(narration ? "" : Id(speaker)).Append(": ").Append(parts[0].Trim()).Append('\n');
                for (int p = 1; p < parts.Length; p++)
                    if (parts[p].Trim().Length > 0) sb.Append("  ").Append(parts[p].Trim()).Append('\n');
                lines++;
            }

            if (lines == 0) warnings.Add($"{label}: Stage_ID「{stageId}」のセリフがありません");
            return sb.ToString();
        }

        void ApplyIllusts(StringBuilder sb, Row row, string[] slots, string[] faces, string label)
        {
            var newSlots = (string[])slots.Clone();
            var newFaces = (string[])faces.Clone();

            for (int k = 0; k < 3; k++)
            {
                string id = row.illusts[k];
                if (id.Length == 0) continue;   // 空欄は前の行のまま
                if (!illusts.TryGetValue(id, out var il))
                {
                    warnings.Add($"{label} {row.rowNumber}行目: 2DイラストID「{id}」が一覧にありません");
                    continue;
                }
                if (il.hidden) { newSlots[k] = null; newFaces[k] = null; }
                else { newSlots[k] = Id(il.chara); newFaces[k] = il.expression; }
            }

            // いなくなったキャラを退場させる（別の位置に移るキャラはそのまま）
            for (int k = 0; k < 3; k++)
                if (slots[k] != null && Array.IndexOf(newSlots, slots[k]) < 0)
                    sb.Append("@hide ").Append(slots[k]).Append('\n');

            // 新しく出る・位置が変わる・表情が変わるキャラ
            for (int k = 0; k < 3; k++)
            {
                string ch = newSlots[k];
                if (ch == null) continue;
                bool same = slots[k] == ch && faces[k] == newFaces[k];
                if (same) continue;
                sb.Append("@show ").Append(ch);
                if (!string.IsNullOrEmpty(newFaces[k])) sb.Append(' ').Append(newFaces[k]);
                sb.Append(' ').Append(slotPositions[k]).Append('\n');
            }

            Array.Copy(newSlots, slots, 3);
            Array.Copy(newFaces, faces, 3);
        }

        string EmoteFor(string reactionId)
        {
            if (string.IsNullOrEmpty(reactionId)) return "";
            foreach (var r in reactionEmotes)
                if (string.Equals(r.reactionId?.Trim(), reactionId, StringComparison.OrdinalIgnoreCase))
                    return (r.emote ?? "").Trim();

            // 設定が無い時は、一覧の説明から決める
            if (reactionLabels.TryGetValue(reactionId, out var lb))
            {
                if (lb.Contains("非表示")) return "";
                if (lb.Contains("！") || lb.Contains("!")) return "!";
                if (lb.Contains("？") || lb.Contains("?")) return "?";
                if (lb.Contains("キラキラ")) return "☆";
                if (lb.Contains("快活")) return "♪";
                return lb;
            }
            warnings.Add($"リアクションID「{reactionId}」の記号が決まっていません（Reaction Emotes に追加してください）");
            return "";
        }

        SlgDirectionRule FindRule(string text, string speaker)
        {
            foreach (var r in directionRules)
                if (r != null && r.IsMatch(text, speaker)) return r;
            return null;
        }

        string Id(string name)
        {
            foreach (var a in nameAliases)
                if (a != null && a.name?.Trim() == name && !string.IsNullOrEmpty(a.characterId)) return a.characterId.Trim();
            // スクリプトの書式で困る文字は外す（空白・コロン・カッコ）
            return name.Replace(" ", "").Replace("　", "").Replace(":", "").Replace("：", "")
                       .Replace("(", "").Replace(")", "").Replace("（", "").Replace("）", "");
        }

        // =====================================================================
        // CSV 分解（"" で囲まれたセル・改行・BOM に対応）
        // =====================================================================

        public static class Csv
        {
            public static List<List<string>> Split(string s)
            {
                var rows = new List<List<string>>();
                if (string.IsNullOrEmpty(s)) return rows;
                if (s[0] == '﻿') s = s.Substring(1);

                var row = new List<string>();
                var cell = new StringBuilder();
                bool quoted = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char ch = s[i];
                    if (quoted)
                    {
                        if (ch == '"')
                        {
                            if (i + 1 < s.Length && s[i + 1] == '"') { cell.Append('"'); i++; }
                            else quoted = false;
                        }
                        else cell.Append(ch);
                        continue;
                    }
                    if (ch == '"') quoted = true;
                    else if (ch == ',') { row.Add(cell.ToString()); cell.Length = 0; }
                    else if (ch == '\r') { }
                    else if (ch == '\n') { row.Add(cell.ToString()); cell.Length = 0; rows.Add(row); row = new List<string>(); }
                    else cell.Append(ch);
                }
                if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
                return rows;
            }
        }
    }
}
