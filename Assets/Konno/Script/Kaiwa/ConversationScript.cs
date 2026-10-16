using System;
using System.Collections.Generic;
using UnityEngine;

namespace Conversation
{
    public enum CommandType
    {
        Line, Background, Show, Hide, Face, Jump, Shake, Emote, Wait, Fade, Choice, Goto, Clear, End
    }

    public class ChoiceOption
    {
        public string text;
        public string label;
    }

    public class Command
    {
        public CommandType type;
        public string[] args = Array.Empty<string>();
        public string speaker;
        public string expression;
        public string text;
        public List<ChoiceOption> options;
        public int lineNumber;

        public string Arg(int i, string def = null) => i < args.Length ? args[i] : def;
    }

    /// <summary>
    /// 会話スクリプトのパーサー。書式:
    ///   id: セリフ            キャラのセリフ（idはDatabaseのID。未登録ならそのまま名前として表示）
    ///   id(表情): セリフ      表情を変えつつセリフ
    ///   : ナレーション        名前なし
    ///     続きの行            インデントすると直前のセリフに改行して追加
    ///   @bg 名前 [秒]         背景（none で黒）
    ///   @show id [表情] [left|center|right|0〜1]   登場（既に居れば移動・表情変更）
    ///   @hide id / @clear     退場 / 全員退場
    ///   @face id 表情         表情だけ変える
    ///   @jump id [回数]       ぴょんと跳ねる
    ///   @shake id             横に揺れる
    ///   @emote id 記号        頭の上に記号（!  ?  !?  … など）
    ///   @wait 秒
    ///   @fade out|in [秒]     画面暗転
    ///   @choice               選択肢（次の行から「- 文 -> ラベル」）
    ///   *ラベル / @goto ラベル
    ///   @end
    ///   // コメント
    /// </summary>
    public class ConversationScript
    {
        public readonly List<Command> commands = new List<Command>();
        public readonly Dictionary<string, int> labels = new Dictionary<string, int>();

        static readonly char[] Spaces = { ' ', '\t', '　' };

        public static ConversationScript Parse(string source)
        {
            var s = new ConversationScript();
            var lines = source.Replace("\r", "").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;

                bool indented = raw[0] == ' ' || raw[0] == '\t' || raw[0] == '　';
                var last = s.commands.Count > 0 ? s.commands[s.commands.Count - 1] : null;

                // インデント行 → 直前のセリフの続き
                if (indented && last != null && last.type == CommandType.Line)
                {
                    last.text += "\n" + line;
                    continue;
                }

                // ラベル
                if (line.StartsWith("*"))
                {
                    s.labels[line.Substring(1).Trim()] = s.commands.Count;
                    continue;
                }

                // コマンド
                if (line.StartsWith("@"))
                {
                    var parts = line.Substring(1).Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    var args = new string[parts.Length - 1];
                    Array.Copy(parts, 1, args, 0, args.Length);
                    var cmd = new Command { args = args, lineNumber = i + 1 };

                    switch (parts[0].ToLowerInvariant())
                    {
                        case "bg": cmd.type = CommandType.Background; break;
                        case "show": cmd.type = CommandType.Show; break;
                        case "hide": cmd.type = CommandType.Hide; break;
                        case "face": cmd.type = CommandType.Face; break;
                        case "jump": cmd.type = CommandType.Jump; break;
                        case "shake": cmd.type = CommandType.Shake; break;
                        case "emote": cmd.type = CommandType.Emote; break;
                        case "wait": cmd.type = CommandType.Wait; break;
                        case "fade": cmd.type = CommandType.Fade; break;
                        case "goto": cmd.type = CommandType.Goto; break;
                        case "clear": cmd.type = CommandType.Clear; break;
                        case "end": cmd.type = CommandType.End; break;
                        case "choice":
                            cmd.type = CommandType.Choice;
                            cmd.options = new List<ChoiceOption>();
                            while (i + 1 < lines.Length)
                            {
                                var next = lines[i + 1].Trim();
                                if (next.Length == 0) { i++; continue; }
                                if (!next.StartsWith("-")) break;
                                i++;
                                var body = next.Substring(1).Trim();
                                int arrow = body.IndexOf("->", StringComparison.Ordinal);
                                var opt = new ChoiceOption();
                                if (arrow >= 0)
                                {
                                    opt.text = body.Substring(0, arrow).Trim();
                                    opt.label = body.Substring(arrow + 2).Trim();
                                }
                                else opt.text = body;
                                cmd.options.Add(opt);
                            }
                            break;
                        default:
                            Debug.LogWarning($"[Conversation] {i + 1}行目: 不明なコマンド @{parts[0]}");
                            continue;
                    }
                    s.commands.Add(cmd);
                    continue;
                }

                // セリフ / ナレーション
                var lc = new Command { type = CommandType.Line, lineNumber = i + 1 };
                int colon = line.IndexOf(':');
                int colonJa = line.IndexOf('：');
                if (colon < 0 || (colonJa >= 0 && colonJa < colon)) colon = colonJa;

                if (colon >= 0)
                {
                    string sp = line.Substring(0, colon).Trim();
                    lc.text = line.Substring(colon + 1).Trim();

                    int p = sp.IndexOfAny(new[] { '(', '（' });
                    if (p >= 0)
                    {
                        int q = sp.IndexOfAny(new[] { ')', '）' }, p);
                        int end = q > p ? q : sp.Length;
                        lc.expression = sp.Substring(p + 1, end - p - 1).Trim();
                        sp = sp.Substring(0, p).Trim();
                    }
                    lc.speaker = sp;
                }
                else
                {
                    lc.text = line;
                }
                s.commands.Add(lc);
            }
            return s;
        }
    }
}
