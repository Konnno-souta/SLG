using System;
using System.Collections.Generic;

// 補正できるステータス
public enum SkillStat
{
    Attack,
    Magic,
    AttackDefense,
    MagicDefense,
    Speed,
    Hit,
    Evasion,
    Critical,
    MoveRange,
    MoveBlocked
}

// ユニットごとのスキル状態
public sealed class SkillRuntimeState
{
    private sealed class Modifier
    {
        public SkillStat Stat;

        public float Amount;

        public int ExpiresAtTurn;
    }

    private readonly Dictionary<string, Modifier>
        modifiers =
            new Dictionary<string, Modifier>();

    private readonly Dictionary<string, int>
        values =
            new Dictionary<string, int>();

    // ========================================
    // ステータス補正登録
    // ========================================

    // 同一キーは上書き。
    // 異なるキーは加算する。
    // expiresAtTurnは効果が解除されるターン。
    public void SetModifier(
        string key,
        SkillStat stat,
        float amount,
        int expiresAtTurn = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(nameof(key));
        }

        if (float.IsNaN(amount) ||
            float.IsInfinity(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount));
        }

        if (expiresAtTurn < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtTurn));
        }

        modifiers[key] = new Modifier
        {
            Stat = stat,

            Amount = amount,

            ExpiresAtTurn = expiresAtTurn
        };
    }

    // ========================================
    // ステータス補正取得
    // ========================================

    public float GetModifier(SkillStat stat)
    {
        float result = 0f;

        foreach (Modifier modifier in modifiers.Values)
        {
            if (modifier.Stat == stat)
            {
                result += modifier.Amount;
            }
        }

        return result;
    }

    // ========================================
    // 補正解除
    // ========================================

    public bool RemoveModifier(string key)
    {
        return modifiers.Remove(key);
    }

    // ========================================
    // 効果時間終了
    // ========================================

    public void ExpireAtTurn(int turn)
    {
        List<string> keys = new List<string>();

        foreach (var pair in modifiers)
        {
            if (pair.Value.ExpiresAtTurn != int.MaxValue &&
                pair.Value.ExpiresAtTurn <= turn)
            {
                keys.Add(pair.Key);
            }
        }

        foreach (string key in keys)
        {
            modifiers.Remove(key);
        }
    }

    // ========================================
    // 使用回数・クールタイム管理
    // ========================================

    public int GetValue(
        string key,
        int defaultValue = 0)
    {
        return values.TryGetValue(
            key,
            out int value)
            ? value
            : defaultValue;
    }

    public void SetValue(
        string key,
        int value)
    {
        values[key] = value;
    }

    public bool RemoveValue(string key)
    {
        return values.Remove(key);
    }

    // ========================================
    // 状態確認
    // ========================================

    public bool HasState =>
        modifiers.Count != 0 ||
        values.Count != 0;

    // ========================================
    // 全状態リセット
    // ========================================

    public void Clear()
    {
        modifiers.Clear();

        values.Clear();
    }
}