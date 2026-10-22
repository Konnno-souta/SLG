/// <summary>
/// Class.csvの1クラス分のマスターデータ。
/// Ratio/Rate列がどんな計算式を意味するかは未確定なので、
/// このクラスでは数値を保持するだけにする。
/// </summary>
public sealed class ClassData
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public int IconId { get; private set; }
    public int AttackModelId { get; private set; }
    public int EffectId { get; private set; }

    public float AttackRatio { get; private set; }
    public float MagicRatio { get; private set; }
    public float AttackDefenseRatio { get; private set; }
    public float MagicDefenseRatio { get; private set; }
    public float SpeedRatio { get; private set; }
    public float HitRatio { get; private set; }
    public float EvasionRatio { get; private set; }
    public float ActionRangeRatio { get; private set; }

    public static ClassData FromCsv(CsvRow row)
    {
        return new ClassData
        {
            Id = row.GetInt("Class_ID"),
            Name = row.Get("Name"),
            IconId = row.GetInt("Class_Icon"),
            AttackModelId = row.GetInt("3DModel_Attack"),
            EffectId = row.GetInt("Effect_ID"),
            AttackRatio = row.GetFloat("ATK_Ratio"),
            MagicRatio = row.GetFloat("MAGIC_Rate"),
            AttackDefenseRatio = row.GetFloat("ATK_DEF_Rate"),
            MagicDefenseRatio = row.GetFloat("MAGIC_DEF_Rate"),
            SpeedRatio = row.GetFloat("SPD_Rate"),
            HitRatio = row.GetFloat("HIT_Rate"),
            EvasionRatio = row.GetFloat("EVASION_Rate"),
            ActionRangeRatio = row.GetFloat("Action_Range_Rate")
        };
    }
}
