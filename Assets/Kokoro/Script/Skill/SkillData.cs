using System;

// スキルの共通定義
// CSVを使用せず、コードから登録する。
public sealed class SkillData
{
    public string Id { get; }

    public string Name { get; }

    public Func<UnitView, bool> IsAvailable { get; }

    public Func<SkillContext, bool> CanUse { get; }

    public Func<SkillContext, bool> Execute { get; }

    public bool IsActive => Execute != null;

    public SkillData(
        string id,
        string name,
        Func<UnitView, bool> isAvailable,
        Func<SkillContext, bool> canUse = null,
        Func<SkillContext, bool> execute = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(nameof(id));
        }

        Id = id;

        Name = name ?? id;

        IsAvailable = isAvailable ??
            throw new ArgumentNullException(
                nameof(isAvailable));

        CanUse = canUse;

        Execute = execute;
    }
}