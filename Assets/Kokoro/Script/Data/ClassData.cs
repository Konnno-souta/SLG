using System;

/// <summary>
/// Class.csv の基礎ステータス倍率と攻撃射程情報。
/// 0.9 = 基礎値の90%、1.1 = 基礎値の110%。
/// 小数の丸め方が未決定のため倍率計算はfloatのまま扱う。
/// </summary>
public sealed class ClassData
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Time { get; private set; }
    public string IconId { get; private set; }
    public string AttackModelId { get; private set; }
    public string EffectId { get; private set; }
    public float AttackRatio { get; private set; }
    public float MagicRatio { get; private set; }
    public float AttackDefenseRatio { get; private set; }
    public float MagicDefenseRatio { get; private set; }
    public float SpeedRatio { get; private set; }
    public float HitRatio { get; private set; }
    public float EvasionRatio { get; private set; }
    public int MinAttackRange { get; private set; }
    public int MaxAttackRange { get; private set; }
    public string RangeType { get; private set; }

    public static ClassData FromCsv(CsvRow row)
    {
        var data = new ClassData
        {
            Id = row.Get("CL_ID"),
            Name = row.Get("Name"),
            Time = row.Get("Time"),
            IconId = row.Get("CL_IC"),
            AttackModelId = row.Get("3DModel_Attack"),
            EffectId = row.Get("Effect_ID"),
            AttackRatio = row.GetFloat("ATK_Ratio"),
            MagicRatio = row.GetFloat("MAGIC_Rate"),
            AttackDefenseRatio = row.GetFloat("ATK_DEF_Rate"),
            MagicDefenseRatio = row.GetFloat("MAGIC_DEF_Rate"),
            SpeedRatio = row.GetFloat("SPD_Rate"),
            HitRatio = row.GetFloat("HIT_Rate"),
            EvasionRatio = row.GetFloat("EVASION_Rate"),
            MinAttackRange = row.GetInt("MinRange_Attack"),
            MaxAttackRange = row.GetInt("MaxRange_Attack"),
            RangeType = row.Get("RangeType")
        };
        if (string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.Name))
            throw new FormatException("CL_ID と Name は必須です。");
        if (data.MinAttackRange < 0 || data.MaxAttackRange < data.MinAttackRange)
            throw new FormatException("攻撃射程の MinRange_Attack / MaxRange_Attack が不正です。");
        return data;
    }
}
