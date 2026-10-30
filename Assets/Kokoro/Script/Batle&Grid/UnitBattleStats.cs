
using System;

/// <summary>
/// 戦闘中の朝夜クラス。
/// </summary>
public enum BattleClassMode
{
    Light,
    Dark
}

/// <summary>
/// キャラクターの戦闘ステータス。
///
/// Character.csv の基礎値と
/// Class.csv の倍率から能力値を生成する。
///
/// 朝夜のクラス変更に対応。
/// 第16段階ではHP復元機能を追加。
/// </summary>
public sealed class UnitBattleStats
{
    // ========================================
    // CSVマスターデータ
    // ========================================

    public CharacterData Character { get; }

    public ClassData LightClass { get; }

    public ClassData DarkClass { get; }

    public ClassData CurrentClass
    {
        get;
        private set;
    }

    public BattleClassMode ClassMode
    {
        get;
        private set;
    }

    // ========================================
    // HP
    // ========================================

    public int MaxHP
    {
        get;
        private set;
    }

    public int CurrentHP
    {
        get;
        private set;
    }

    public bool IsDead => CurrentHP <= 0;

    // ========================================
    // 能力値
    // ========================================

    public float Attack { get; private set; }

    public float Magic { get; private set; }

    public float AttackDefense { get; private set; }

    public float MagicDefense { get; private set; }

    public float Speed { get; private set; }

    public float Hit { get; private set; }

    public float Evasion { get; private set; }

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

    public int MoveRange
    {
        get;
        private set;
    }

    // ========================================
    // 表示用
    // ========================================

    public string CharacterId => Character.Id;

    public string CharacterName => Character.Name;

    public string CurrentClassId => CurrentClass.Id;

    public string CurrentClassName => CurrentClass.Name;

    // ========================================
    // 生成
    // ========================================

    public UnitBattleStats(
        CharacterData character,
        ClassData lightClass,
        ClassData darkClass,
        BattleClassMode initialMode)
    {
        Character =
            character ??
            throw new ArgumentNullException(
                nameof(character)
            );

        LightClass =
            lightClass ??
            throw new ArgumentNullException(
                nameof(lightClass)
            );

        DarkClass =
            darkClass ??
            throw new ArgumentNullException(
                nameof(darkClass)
            );

        // 正式CSVにHP倍率はないため
        // Character.csvの基礎HPを使用する。
        MaxHP = Character.BaseHP;
        CurrentHP = MaxHP;

        SetClassMode(initialMode);
    }

    // ========================================
    // 昼夜クラス変更
    // ========================================

    /// <summary>
    /// 使用クラスと能力値を更新。
    ///
    /// 現在HPは変更しない。
    /// </summary>
    public void SetClassMode(
        BattleClassMode mode)
    {
        ClassMode = mode;

        CurrentClass =
            mode == BattleClassMode.Light
                ? LightClass
                : DarkClass;

        RecalculateStats();
    }

    // ========================================
    // 能力値の計算
    // ========================================

    private void RecalculateStats()
    {
        Attack =
            Character.BaseAttack *
            CurrentClass.AttackRatio;

        Magic =
            Character.BaseMagic *
            CurrentClass.MagicRatio;

        AttackDefense =
            Character.BaseAttackDefense *
            CurrentClass.AttackDefenseRatio;

        MagicDefense =
            Character.BaseMagicDefense *
            CurrentClass.MagicDefenseRatio;

        Speed =
            Character.BaseSpeed *
            CurrentClass.SpeedRatio;

        Hit =
            Character.BaseHit *
            CurrentClass.HitRatio;

        Evasion =
            Character.BaseEvasion *
            CurrentClass.EvasionRatio;

        MinAttackRange =
            CurrentClass.MinAttackRange;

        MaxAttackRange =
            CurrentClass.MaxAttackRange;

        RangeType =
            CurrentClass.RangeType;

        MoveRange =
            CurrentClass.MaxMovementRange;
    }

    /// <summary>
    /// JSONに保存された現在HPを復元。
    ///
    /// MaxHPは正式CSVの値を維持する。
    ///
    /// 不正なHPなら変更しない。
    /// </summary>
    public bool TryRestoreCurrentHP(int hp)
    {
        if (hp < 0 || hp > MaxHP)
        {
            return false;
        }

        CurrentHP = hp;

        return true;
    }


    /// <summary>
    /// 指定されたダメージで現在HPを減らす。
    ///
    /// ・ダメージ量の計算は行わない
    /// ・HPは0未満にならない
    /// ・実際に減ったHPを返す
    ///
    /// 今後、攻撃・敵AI・スキルなどから
    /// 共通して呼び出す。
    /// </summary>
    public bool TryTakeDamage(
        int requestedDamage,
        out int appliedDamage)
    {
        appliedDamage = 0;

        if (requestedDamage < 0)
        {
            return false;
        }

        if (CurrentHP <= 0)
        {
            return false;
        }

        int previousHP = CurrentHP;

        CurrentHP = Math.Max(
            0,
            CurrentHP - requestedDamage
        );

        appliedDamage =
            previousHP - CurrentHP;

        return true;
    }

}
