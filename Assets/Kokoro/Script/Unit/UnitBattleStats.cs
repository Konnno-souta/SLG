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
    // スキル状態
    // ========================================

    public SkillRuntimeState Skills { get; } =
        new SkillRuntimeState();

    // クリティカルの基本計算は未実装。
    // スキルによる補正値のみ取得する。
    public float CriticalBonus =>
        Skills.GetModifier(SkillStat.Critical);

    // ========================================
    // 能力値
    // ========================================

    private float baseAttack;

    public float Attack =>
        baseAttack +
        Skills.GetModifier(SkillStat.Attack);

    private float baseMagic;

    public float Magic =>
        baseMagic +
        Skills.GetModifier(SkillStat.Magic);

    private float baseAttackDefense;

    public float AttackDefense =>
        baseAttackDefense +
        Skills.GetModifier(
            SkillStat.AttackDefense);

    private float baseMagicDefense;

    public float MagicDefense =>
        baseMagicDefense +
        Skills.GetModifier(
            SkillStat.MagicDefense);

    private float baseSpeed;

    public float Speed =>
        baseSpeed +
        Skills.GetModifier(SkillStat.Speed);

    private float baseHit;

    public float Hit =>
        baseHit +
        Skills.GetModifier(SkillStat.Hit);

    private float baseEvasion;

    public float Evasion =>
        baseEvasion +
        Skills.GetModifier(SkillStat.Evasion);

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

    // 補正前の移動力。
    // スキル補正後はUnitState.MoveRangeを使用。
    public int MoveRange
    {
        get;
        private set;
    }

    // ========================================
    // 表示用
    // ========================================

    public string CharacterId =>
        Character.Id;

    public string CharacterName =>
        Character.Name;

    public string CurrentClassId =>
        CurrentClass.Id;

    public string CurrentClassName =>
        CurrentClass.Name;

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
    /// 現在HPとスキル状態は変更しない。
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
        baseAttack =
            Character.BaseAttack *
            CurrentClass.AttackRatio;

        baseMagic =
            Character.BaseMagic *
            CurrentClass.MagicRatio;

        baseAttackDefense =
            Character.BaseAttackDefense *
            CurrentClass.AttackDefenseRatio;

        baseMagicDefense =
            Character.BaseMagicDefense *
            CurrentClass.MagicDefenseRatio;

        baseSpeed =
            Character.BaseSpeed *
            CurrentClass.SpeedRatio;

        baseHit =
            Character.BaseHit *
            CurrentClass.HitRatio;

        baseEvasion =
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

    // ========================================
    // 現在HPの復元
    // ========================================

    /// <summary>
    /// JSONに保存された現在HPを復元。
    ///
    /// MaxHPは正式CSVの値を維持する。
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

    // ========================================
    // ダメージ適用
    // ========================================

    /// <summary>
    /// 指定されたダメージで現在HPを減らす。
    ///
    /// ・ダメージ量の計算は行わない
    /// ・HPは0未満にならない
    /// ・実際に減ったHPを返す
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