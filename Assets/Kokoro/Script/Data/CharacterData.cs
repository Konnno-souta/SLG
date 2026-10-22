/// <summary>
/// Character.csvの1キャラクター分のマスターデータ。
/// 戦闘中の現在HP・位置などはUnitState側で管理する。
/// </summary>
public sealed class CharacterData
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public int PositionId { get; private set; }  // 1=仲間、2=敵（現在のCSV）

    public string DesignNormal { get; private set; }
    public string DesignPositive { get; private set; }
    public string DesignNegative { get; private set; }
    public int ModelId { get; private set; }

    public int LightClassId { get; private set; }
    public int DarkClassId { get; private set; }

    public int BaseHP { get; private set; }
    public int BaseAttack { get; private set; }
    public int BaseMagic { get; private set; }
    public int BaseAttackDefense { get; private set; }
    public int BaseMagicDefense { get; private set; }
    public int BaseSpeed { get; private set; }
    public int BaseHit { get; private set; }
    public int BaseEvasion { get; private set; }
    public int BaseActionRange { get; private set; }

    public static CharacterData FromCsv(CsvRow row)
    {
        return new CharacterData
        {
            Id = row.GetInt("Character_ID"),
            Name = row.Get("Name"),
            PositionId = row.GetInt("Position"),
            DesignNormal = row.Get("Design_Normal"),
            DesignPositive = row.Get("Design_Positive"),
            DesignNegative = row.Get("Design_Negative"),
            ModelId = row.GetInt("3DModel"),
            LightClassId = row.GetInt("Class_Light"),
            DarkClassId = row.GetInt("Class_Dark"),
            BaseHP = row.GetInt("HP_Basic"),
            BaseAttack = row.GetInt("ATK_Basic"),
            BaseMagic = row.GetInt("MAGIC_Basic"),
            BaseAttackDefense = row.GetInt("ATK_DEF_Basic"),
            BaseMagicDefense = row.GetInt("MAGIC_DEF_Basic"),
            BaseSpeed = row.GetInt("SPD_Basic"),
            BaseHit = row.GetInt("HIT_Basic"),
            BaseEvasion = row.GetInt("EVASION_Basic"),
            BaseActionRange = row.GetInt("Action_Range_Basic")
        };
    }
}
