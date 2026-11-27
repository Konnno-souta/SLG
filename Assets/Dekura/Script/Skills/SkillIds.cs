// キャラ識別。所有判定を1か所にまとめる。
public static class SkillIds
{
    // TODO: Character.csv の Id
    public const string Clan = "1";
    public const string Millard = "2";
    public const string Bit = "3";
    public const string Rion = "5";
    public const string Air = "7";
    public const string Ray = "9";
    public const string Karma = "11";
    public const string Grace = "13";

    public static bool IsCharacter(UnitView u, string id) =>
        u != null &&
        u.State != null &&
        u.State.CombatStats != null &&
        u.State.CombatStats.CharacterId == id;
}
