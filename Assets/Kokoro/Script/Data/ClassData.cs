
using System;

/// <summary>
/// Class.csvのクラス情報。
///
/// ・クラスID
/// ・ステータス倍率
/// ・攻撃射程
/// ・最大移動範囲
///
/// を保持する。
///
/// 倍率1.1は基礎ステータスの110%。
/// </summary>
public sealed class ClassData
{
    // ================================
    // 基本情報
    // ================================

    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Time { get; private set; }

    public string IconId { get; private set; }
    public string AttackModelId { get; private set; }
    public string EffectId { get; private set; }

    // ================================
    // ステータス倍率
    // ================================

    public float AttackRatio { get; private set; }
    public float MagicRatio { get; private set; }
    public float AttackDefenseRatio { get; private set; }
    public float MagicDefenseRatio { get; private set; }
    public float SpeedRatio { get; private set; }
    public float HitRatio { get; private set; }
    public float EvasionRatio { get; private set; }

    // ================================
    // 攻撃範囲
    // ================================

    public int MinAttackRange { get; private set; }
    public int MaxAttackRange { get; private set; }
    public string RangeType { get; private set; }

    // ================================
    // 移動力（今回追加）
    // ================================

    /// <summary>
    /// Class.csvのMaxMovementRange。
    /// 移動可能な最大マス数。
    /// </summary>
    public int MaxMovementRange { get; private set; }

    // ================================
    // CSV読み込み
    // ================================

    public static ClassData FromCsv(CsvRow row)
    {
        ClassData data = new ClassData
        {
            Id = row.Get("CL_ID"),
            Name = row.Get("Name"),
            Time = row.Get("Time"),

            IconId = row.Get("CL_IC"),
            AttackModelId = row.Get("3DModel_Attack"),
            EffectId = row.Get("Effect_ID"),

            AttackRatio = row.GetFloat("ATK_Ratio"),
            MagicRatio = row.GetFloat("MAGIC_Rate"),

            AttackDefenseRatio =
                row.GetFloat("ATK_DEF_Rate"),

            MagicDefenseRatio =
                row.GetFloat("MAGIC_DEF_Rate"),

            SpeedRatio = row.GetFloat("SPD_Rate"),
            HitRatio = row.GetFloat("HIT_Rate"),
            EvasionRatio = row.GetFloat("EVASION_Rate"),

            MinAttackRange =
                row.GetInt("MinRange_Attack"),

            MaxAttackRange =
                row.GetInt("MaxRange_Attack"),

            RangeType = row.Get("RangeType"),

            // 今回の最新版CSVで追加された列
            MaxMovementRange =
                row.GetInt("MaxMovementRange")
        };

        // 必須情報の確認
        if (string.IsNullOrWhiteSpace(data.Id) ||
            string.IsNullOrWhiteSpace(data.Name))
        {
            throw new FormatException(
                "CL_IDまたはNameが空欄です。"
            );
        }

        // 攻撃射程の確認
        if (data.MinAttackRange < 0 ||
            data.MaxAttackRange < data.MinAttackRange)
        {
            throw new FormatException(
                $"{data.Id}: 攻撃射程が不正です。"
            );
        }

        // 移動力の確認
        if (data.MaxMovementRange < 0)
        {
            throw new FormatException(
                $"{data.Id}: MaxMovementRangeは" +
                "0以上にしてください。"
            );
        }

        return data;
    }
}
