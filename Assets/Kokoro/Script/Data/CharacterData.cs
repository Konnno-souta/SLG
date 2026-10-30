
using System;

/// <summary>
/// Character.csv の基礎データ。
/// 現在HPや配置座標は保持しない。
///
/// 新CSV対応：
/// ・Character_IDは数値文字列
/// ・Teamの1/2をPlayer/Enemyへ変換
/// ・新しい基礎ステータス列名に対応
/// ・クリティカル率を追加
///
/// 既存の公開プロパティ名は維持する。
/// </summary>
public sealed class CharacterData
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Team { get; private set; }

    public string ModelId { get; private set; }

    public string DesignNormal { get; private set; }
    public string DesignPositive { get; private set; }
    public string DesignNegative { get; private set; }

    public string LightClassId { get; private set; }
    public string DarkClassId { get; private set; }

    public int BaseHP { get; private set; }
    public int BaseAttack { get; private set; }
    public int BaseMagic { get; private set; }
    public int BaseAttackDefense { get; private set; }
    public int BaseMagicDefense { get; private set; }
    public int BaseSpeed { get; private set; }
    public int BaseHit { get; private set; }
    public int BaseEvasion { get; private set; }

    // 第18段階で追加
    public int BaseCritical { get; private set; }

    // ========================================
    // CSV読み込み
    // ========================================

    public static CharacterData FromCsv(CsvRow row)
    {
        if (row == null)
        {
            throw new ArgumentNullException(
                nameof(row)
            );
        }

        var data = new CharacterData
        {
            Id = row.Get("Character_ID"),
            Name = row.Get("Name"),

            // 新CSV：
            // 1 = Player
            // 2 = Enemy
            //
            // 既存のUnitViewなどを変更しないため、
            // 内部では従来の文字列に変換する。
            Team = ConvertTeam(
                row.Get("Team")
            ),

            ModelId = row.Get("3DModel"),

            DesignNormal =
                row.Get("Design_Normal"),

            DesignPositive =
                row.Get("Design_Positive"),

            DesignNegative =
                row.Get("Design_Negative"),

            LightClassId =
                row.Get("CL_Light"),

            DarkClassId =
                row.Get("CL_Dark"),

            BaseHP =
                row.GetInt("HP_Basic"),

            BaseAttack =
                row.GetInt("ATK_Basic"),

            BaseMagic =
                row.GetInt("MGC_Basic"),

            BaseAttackDefense =
                row.GetInt("ATKDEF_Basic"),

            BaseMagicDefense =
                row.GetInt("MGCDEF_Basic"),

            BaseSpeed =
                row.GetInt("SPD_Basic"),

            BaseHit =
                row.GetInt("HIT_Basic"),

            BaseEvasion =
                row.GetInt("EVA_Basic"),

            // CSVの正式な列名をそのまま使用。
            BaseCritical =
                row.GetInt("CRT_Bssic")
        };

        // ====================================
        // データ検証
        // ====================================

        if (string.IsNullOrWhiteSpace(data.Id) ||
            string.IsNullOrWhiteSpace(data.Name))
        {
            throw new FormatException(
                "Character_IDとNameは必須です。"
            );
        }

        if (!int.TryParse(data.Id, out int id) ||
            id <= 0)
        {
            throw new FormatException(
                $"Character_ID={data.Id} は" +
                "1以上の整数にしてください。"
            );
        }

        if (string.IsNullOrWhiteSpace(
                data.LightClassId) ||
            string.IsNullOrWhiteSpace(
                data.DarkClassId))
        {
            throw new FormatException(
                $"{data.Id}: CL_Lightと" +
                "CL_Darkは必須です。"
            );
        }

        if (data.BaseHP <= 0)
        {
            throw new FormatException(
                $"{data.Id}: HP_Basicは" +
                "1以上にしてください。"
            );
        }

        if (data.BaseCritical < 0)
        {
            throw new FormatException(
                $"{data.Id}: CRT_Bssicは" +
                "0以上にしてください。"
            );
        }

        return data;
    }

    // ========================================
    // Teamの形式変換
    // ========================================

    private static string ConvertTeam(
        string csvTeam)
    {
        switch (csvTeam)
        {
            case "1":
            case "Player":
                return "Player";

            case "2":
            case "Enemy":
                return "Enemy";

            default:
                throw new FormatException(
                    $"Team={csvTeam} は不正です。" +
                    "1または2を指定してください。"
                );
        }
    }
}
