using System;
using System.Collections.Generic;
using UnityEngine;

namespace StaffRoll
{
    public enum EntryType
    {
        RoleAndNames, // 役職 + 名前
        Text,         // 1行テキスト
        Image,        // ロゴなどの画像
        Space         // 空白
    }

    [Serializable]
    public class StaffRollEntry
    {
        public EntryType type = EntryType.RoleAndNames;

        [Tooltip("役職名（RoleAndNames用）")]
        public string role;

        [Tooltip("名前やテキスト。複数名は改行で区切る")]
        [TextArea(1, 8)] public string names;

        [Tooltip("Image用")]
        public Sprite image;
        public Vector2 imageSize = new Vector2(400, 200);

        [Tooltip("Space用の高さ(px)")]
        public float spaceHeight = 60f;

        [Tooltip("0以下ならプレイヤー側のデフォルトサイズ")]
        public float fontSizeOverride = 0f;
    }

    [Serializable]
    public class StaffRollSection
    {
        [Tooltip("セクション見出し（空なら非表示）")]
        public string title;

        public List<StaffRollEntry> entries = new List<StaffRollEntry>();

        [Tooltip("このセクション後の余白。負の値ならプレイヤー側のデフォルト")]
        public float spacingAfter = -1f;
    }

    [CreateAssetMenu(menuName = "StaffRoll/Staff Roll Data", fileName = "StaffRollData")]
    public class StaffRollData : ScriptableObject
    {
        public List<StaffRollSection> sections = new List<StaffRollSection>();

        [Header("最後に表示するもの（空なら無し）")]
        public Sprite finalImage;
        public Vector2 finalImageSize = new Vector2(600, 300);
        public string finalText = "Thank you for playing!";

        /// <summary>
        /// テキスト形式からデータを生成する。
        ///   # 見出し          … セクション開始
        ///   役職: 名前A, 名前B … 役職と名前（, や 、 で複数名）
        ///   役職:             … 名前を次行以降にインデントして列挙
        ///     名前A
        ///   ただの行          … テキスト
        ///   ---  / --- 120    … 空白（数値で高さ指定）
        ///   ! 最後のテキスト  … ラストに中央表示する文字
        ///   // コメント
        /// </summary>
        public static StaffRollData FromText(string source)
        {
            var data = CreateInstance<StaffRollData>();
            data.finalText = "";
            StaffRollSection current = null;
            StaffRollEntry lastRoleEntry = null;

            var lines = source.Replace("\r", "").Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;

                bool indented = raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t' || raw[0] == '　');

                // インデント行 → 直前の役職に名前を追加
                if (indented && lastRoleEntry != null)
                {
                    lastRoleEntry.names = string.IsNullOrEmpty(lastRoleEntry.names)
                        ? line
                        : lastRoleEntry.names + "\n" + line;
                    continue;
                }
                lastRoleEntry = null;

                if (line.StartsWith("#"))
                {
                    current = new StaffRollSection { title = line.Substring(1).Trim() };
                    data.sections.Add(current);
                    continue;
                }

                if (line.StartsWith("!"))
                {
                    data.finalText = line.Substring(1).Trim();
                    continue;
                }

                if (current == null)
                {
                    current = new StaffRollSection { title = "" };
                    data.sections.Add(current);
                }

                if (line.StartsWith("---"))
                {
                    var entry = new StaffRollEntry { type = EntryType.Space };
                    if (float.TryParse(line.Substring(3).Trim(), out var h)) entry.spaceHeight = h;
                    current.entries.Add(entry);
                    continue;
                }

                int idx = line.IndexOf(':');
                if (idx < 0) idx = line.IndexOf('：');

                if (idx > 0)
                {
                    var role = line.Substring(0, idx).Trim();
                    var rest = line.Substring(idx + 1);
                    var names = rest.Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < names.Length; i++) names[i] = names[i].Trim();

                    var entry = new StaffRollEntry
                    {
                        type = EntryType.RoleAndNames,
                        role = role,
                        names = string.Join("\n", names)
                    };
                    current.entries.Add(entry);
                    lastRoleEntry = entry;
                }
                else
                {
                    current.entries.Add(new StaffRollEntry { type = EntryType.Text, names = line });
                }
            }
            return data;
        }
    }
}
