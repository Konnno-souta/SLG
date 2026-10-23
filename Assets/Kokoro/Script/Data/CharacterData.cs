using System;

/// <summary>
/// Character.csv の不変の基礎データ。現在HPや配置座標は保持しない。
/// 2026/10/22 CSV: Character_ID, Team, CL_Light, CL_Dark などを使用。
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

    public static CharacterData FromCsv(CsvRow row)
    {
        var data = new CharacterData
        {
            Id = row.Get("Character_ID"),
            Name = row.Get("Name"),
            Team = row.Get("Team"),
            ModelId = row.Get("3DModel"),
            DesignNormal = row.Get("Design_Normal"),
            // プランナーが更新したCSVの現行列名をそのまま使用
            DesignPositive = row.Get("Design_TEitive"),
            DesignNegative = row.Get("Design_Negative"),
            LightClassId = row.Get("CL_Light"),
            DarkClassId = row.Get("CL_Dark"),
            BaseHP = row.GetInt("HP_Basic"),
            BaseAttack = row.GetInt("ATK_Basic"),
            BaseMagic = row.GetInt("MAGIC_Basic"),
            BaseAttackDefense = row.GetInt("ATK_DEF_Basic"),
            BaseMagicDefense = row.GetInt("MAGIC_DEF_Basic"),
            BaseSpeed = row.GetInt("SPD_Basic"),
            BaseHit = row.GetInt("HIT_Basic"),
            BaseEvasion = row.GetInt("EVASION_Basic")
        };

        if (string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.Name))
            throw new FormatException("Character_ID と Name は必須です。");
        if (data.Team != "Player" && data.Team != "Enemy")
            throw new FormatException($"Team={data.Team} は Player または Enemy を指定してください。");
        if (string.IsNullOrWhiteSpace(data.LightClassId) ||
            string.IsNullOrWhiteSpace(data.DarkClassId))
            throw new FormatException("CL_Light と CL_Dark は必須です。");
        if (data.BaseHP <= 0)
            throw new FormatException("HP_Basic は1以上にしてください。");
        return data;
    }
}
