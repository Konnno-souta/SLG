
using System;

/// <summary>
/// 現在使用している昼夜クラス。
/// </summary>
public enum BattleClassMode
{
    Light, // 朝
    Dark   // 夜
}

/// <summary>
/// ユニットの戦闘用ステータス。
///
/// Character.csvの基礎ステータスと
/// Class.csvの倍率から能力を計算する。
///
/// 朝夜を切り替えると再計算する。
/// </summary>
public sealed class UnitBattleStats
{
    // ================================
    // 元のCSVデータ
    // ================================

    public CharacterData Character { get; }

    public ClassData LightClass { get; }
    public ClassData DarkClass { get; }

    public ClassData CurrentClass { get; private set; }

    public BattleClassMode ClassMode { get; private set; }

    // ================================
    // HP
    // ================================

    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }

    public bool IsDead => CurrentHP <= 0;

    // ================================
    // 戦闘ステータス
    // ================================

    public float Attack { get; private set; }
    public float Magic { get; private set; }

    public float AttackDefense { get; private set; }
    public float MagicDefense { get; private set; }

    public float Speed { get; private set; }
    public float Hit { get; private set; }
    public float Evasion { get; private set; }

    // ================================
    // 攻撃範囲
    // ================================

    public int MinAttackRange { get; private set; }
    public int MaxAttackRange { get; private set; }
    public string RangeType { get; private set; }

    // ================================
    // 移動範囲（今回追加）
    // ================================

    public int MoveRange { get; private set; }

    // ================================
    // 取得用
    // ================================

    public string CharacterId => Character.Id;
    public string CharacterName => Character.Name;

    public string CurrentClassId => CurrentClass.Id;
    public string CurrentClassName => CurrentClass.Name;

    // ================================
    // コンストラクタ
    // ================================

    public UnitBattleStats(
        CharacterData character,
        ClassData lightClass,
        ClassData darkClass,
        BattleClassMode initialMode)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        if (lightClass == null)
            throw new ArgumentNullException(nameof(lightClass));

        if (darkClass == null)
            throw new ArgumentNullException(nameof(darkClass));

        Character = character;
        LightClass = lightClass;
        DarkClass = darkClass;

        // 現在のCSVにHP倍率は存在しないため、
        // HPはCharacter.csvの基礎値を使用する。
        MaxHP = character.BaseHP;
        CurrentHP = MaxHP;

        SetClassMode(initialMode);
    }

    // ================================
    // 昼夜クラスの変更
    // ================================

    /// <summary>
    /// 朝夜に応じたクラスを選び直し、
    /// 戦闘ステータスを再計算する。
    ///
    /// HPはリセットしない。
    /// </summary>
    public void SetClassMode(BattleClassMode mode)
    {
        ClassMode = mode;

        CurrentClass =
            mode == BattleClassMode.Light
                ? LightClass
                : DarkClass;

        RecalculateStats();
    }

    // ================================
    // 能力値の計算
    // ================================

    private void RecalculateStats()
    {
        // 基礎ステータス × クラス倍率
        Attack =
            Character.BaseAttack * CurrentClass.AttackRatio;

        Magic =
            Character.BaseMagic * CurrentClass.MagicRatio;

        AttackDefense =
            Character.BaseAttackDefense *
            CurrentClass.AttackDefenseRatio;

        MagicDefense =
            Character.BaseMagicDefense *
            CurrentClass.MagicDefenseRatio;

        Speed =
            Character.BaseSpeed * CurrentClass.SpeedRatio;

        Hit =
            Character.BaseHit * CurrentClass.HitRatio;

        Evasion =
            Character.BaseEvasion * CurrentClass.EvasionRatio;

        // 攻撃射程
        MinAttackRange = CurrentClass.MinAttackRange;
        MaxAttackRange = CurrentClass.MaxAttackRange;
        RangeType = CurrentClass.RangeType;

        // 移動力は倍率ではなく、クラス側の直接指定値
        MoveRange = CurrentClass.MaxMovementRange;
    }
}
