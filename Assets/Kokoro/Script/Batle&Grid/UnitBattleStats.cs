
using System;

/// <summary>
/// 現在使用するクラスの区分。
///
/// Light : Character.csv の CL_Light
/// Dark  : Character.csv の CL_Dark
///
/// 昼夜の自動切り替えは今後実装する。
/// </summary>
public enum BattleClassMode
{
    Light,
    Dark
}

/// <summary>
/// ユニット1体分の戦闘ステータス。
///
/// Character.csvの基礎値と
/// Class.csvの倍率から最終能力値を計算する。
///
/// ScriptableObjectやMonoBehaviourではない
/// 通常のC#クラス。
/// </summary>
public sealed class UnitBattleStats
{
    // =====================================
    // 元データ
    // =====================================

    public CharacterData Character { get; }

    public ClassData LightClass { get; }

    public ClassData DarkClass { get; }

    // 現在のクラス
    public ClassData CurrentClass { get; private set; }

    // 現在の昼夜区分
    public BattleClassMode ClassMode { get; private set; }

    // =====================================
    // HP
    // =====================================

    // 現在の最大HP
    public int MaxHP { get; private set; }

    // 現在HP
    public int CurrentHP { get; private set; }

    // =====================================
    // 最終ステータス
    // 小数の丸め方は未確定のためfloat
    // =====================================

    // 物理攻撃力
    public float Attack { get; private set; }

    // 魔法攻撃力
    public float Magic { get; private set; }

    // 物理防御力
    public float AttackDefense { get; private set; }

    // 魔法防御力
    public float MagicDefense { get; private set; }

    // 素早さ
    public float Speed { get; private set; }

    // 命中
    public float Hit { get; private set; }

    // 回避
    public float Evasion { get; private set; }

    // =====================================
    // 攻撃射程
    // 移動力ではないので注意
    // =====================================

    public int MinAttackRange { get; private set; }

    public int MaxAttackRange { get; private set; }

    public string RangeType { get; private set; }

    // =====================================
    // 取得用プロパティ
    // =====================================

    public string CharacterId => Character.Id;

    public string CharacterName => Character.Name;

    public string CurrentClassId => CurrentClass.Id;

    public string CurrentClassName => CurrentClass.Name;

    public bool IsDead => CurrentHP <= 0;

    // =====================================
    // 初期化
    // =====================================

    /// <summary>
    /// CSVの情報を使って戦闘ステータスを作る。
    /// </summary>
    public UnitBattleStats(
        CharacterData character,
        ClassData lightClass,
        ClassData darkClass,
        BattleClassMode initialMode)
    {
        if (character == null)
        {
            throw new ArgumentNullException(
                nameof(character)
            );
        }

        if (lightClass == null)
        {
            throw new ArgumentNullException(
                nameof(lightClass)
            );
        }

        if (darkClass == null)
        {
            throw new ArgumentNullException(
                nameof(darkClass)
            );
        }

        Character = character;
        LightClass = lightClass;
        DarkClass = darkClass;

        // 現在のCSVにはHP倍率がない。
        // 基礎HPを最大HPにする。
        MaxHP = character.BaseHP;

        // 戦闘開始時はHP最大
        CurrentHP = MaxHP;

        // 初期クラスで能力値を計算する
        SetClassMode(initialMode);
    }

    // =====================================
    // クラスの適用
    // =====================================

    /// <summary>
    /// 指定されたクラスに切り替えて
    /// 最終ステータスを再計算する。
    ///
    /// 昼夜の自動切り替えタイミングは
    /// このクラスでは管理しない。
    /// </summary>
    public void SetClassMode(BattleClassMode mode)
    {
        ClassMode = mode;

        // Character.csvのIDから取得済みの
        // クラスを選択する。
        CurrentClass =
            mode == BattleClassMode.Light
                ? LightClass
                : DarkClass;

        RecalculateStats();
    }

    // =====================================
    // ステータス計算
    // =====================================

    /// <summary>
    /// 基礎ステータス × クラス倍率
    ///
    /// 例：
    /// ATK_Basic = 10
    /// ATK_Ratio = 1.1
    /// 最終Attack = 11
    ///
    /// 現段階では四捨五入・切り捨てはしない。
    /// </summary>
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

        // 攻撃射程は倍率計算しない。
        // Class.csvの値をそのまま取得。
        MinAttackRange =
            CurrentClass.MinAttackRange;

        MaxAttackRange =
            CurrentClass.MaxAttackRange;

        RangeType =
            CurrentClass.RangeType;
    }
}
