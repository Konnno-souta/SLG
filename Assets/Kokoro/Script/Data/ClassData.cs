
using System;

/// <summary>
/// Class.csvのクラス情報。
///
/// ・クラスID
/// ・ステータス倍率
/// ・攻撃射程
/// ・移動範囲
///
/// 新CSV対応：
/// ・各Rate列に対応
/// ・CRT_Rateを追加
/// ・MovementRangeから移動力を取得
/// </summary>
public sealed class ClassData
{
    // ========================================
    // 基本情報
    // ========================================

    public string Id { get; private set; }
    public string Name { get; private set; }

    // 新CSVでは1=朝、2=夜、3=両方。
    // 既存コードとの互換性のため文字列で保持。
    public string Time { get; private set; }

    public string IconId { get; private set; }
    public string AttackModelId { get; private set; }
    public string EffectId { get; private set; }

    // ========================================
    // ステータス倍率
    // ========================================

    public float AttackRatio { get; private set; }
    public float MagicRatio { get; private set; }

    public float AttackDefenseRatio
    {
        get;
        private set;
    }

    public float MagicDefenseRatio
    {
        get;
        private set;
    }

    public float SpeedRatio { get; private set; }
    public float HitRatio { get; private set; }
    public float EvasionRatio { get; private set; }

    // 第18段階で追加
    public float CriticalRatio { get; private set; }

    // ========================================
    // 攻撃範囲
    // ========================================

    public int MinAttackRange
    {
        get;
        private set;
    }

    public int MaxAttackRange
    {
        get;
        private set;
    }

    public string RangeType
    {
        get;
        private set;
    }

    // ========================================
    // 移動力
    // ========================================

    // 既存コードの参照名を維持する。
    // 新CSVではMovementRange列から取得。
    public int MaxMovementRange
    {
        get;
        private set;
    }

    // ========================================
    // CSV読み込み
    // ========================================

    public static ClassData FromCsv(CsvRow row)
    {
        if (row == null)
        {
            throw new ArgumentNullException(
                nameof(row)
            );
        }

        ClassData data = new ClassData
        {
            Id = row.Get("CL_ID"),
            Name = row.Get("Name"),
            Time = row.Get("Time"),

            IconId = row.Get("CL_IC"),

            AttackModelId =
                row.Get("3DModel_Attack"),

            EffectId = row.Get("Effect_ID"),

            // 新CSVの列名
            AttackRatio =
                row.GetFloat("ATK_Rate"),

            MagicRatio =
                row.GetFloat("MGC_Rate"),

            AttackDefenseRatio =
                row.GetFloat("ATKDEF_Rate"),

            MagicDefenseRatio =
                row.GetFloat("MGCDEF_Rate"),

            SpeedRatio =
                row.GetFloat("SPD_Rate"),

            HitRatio =
                row.GetFloat("HIT_Rate"),

            EvasionRatio =
                row.GetFloat("EVA_Rate"),

            CriticalRatio =
                row.GetFloat("CRT_Rate"),

            MinAttackRange =
                row.GetInt("MinRange_Attack"),

            MaxAttackRange =
                row.GetInt("MaxRange_Attack"),

            RangeType =
                row.Get("RangeType"),

            MaxMovementRange =
                row.GetInt("MovementRange")
        };

        // ====================================
        // データ検証
        // ====================================

        if (string.IsNullOrWhiteSpace(data.Id) ||
            string.IsNullOrWhiteSpace(data.Name))
        {
            throw new FormatException(
                "CL_IDまたはNameが空欄です。"
            );
        }

        if (!int.TryParse(data.Id, out int id) ||
            id <= 0)
        {
            throw new FormatException(
                $"CL_ID={data.Id} は" +
                "1以上の整数にしてください。"
            );
        }

        if (data.Time != "1" &&
            data.Time != "2" &&
            data.Time != "3")
        {
            throw new FormatException(
                $"{data.Id}: Timeは1、2、3の" +
                "いずれかにしてください。"
            );
        }

        if (data.MinAttackRange < 0 ||
            data.MaxAttackRange <
                data.MinAttackRange)
        {
            throw new FormatException(
                $"{data.Id}: 攻撃射程が不正です。"
            );
        }

        if (data.MaxMovementRange < 0)
        {
            throw new FormatException(
                $"{data.Id}: MovementRangeは" +
                "0以上にしてください。"
            );
        }

        if (data.CriticalRatio < 0)
        {
            throw new FormatException(
                $"{data.Id}: CRT_Rateは" +
                "0以上にしてください。"
            );
        }

        return data;
    }
}
