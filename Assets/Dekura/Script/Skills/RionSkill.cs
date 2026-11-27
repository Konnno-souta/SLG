using UnityEngine;

// リオン 連撃: 攻撃回数+1(パッシブ)
public static class RionSkill
{
    public const string Id = "rion.rengeki";

    private const float FollowUpRate = 1f;   // TODO: 2撃目の倍率(プランナー確認)

    // 2撃目の被弾エフェクト用(SkillBootstrapper が設定。未設定でも動作する)
    public static BattleCombatEffectPlayer EffectPlayer;

    public static void Register(SkillManager m)
    {
        // パッシブ: execute を渡さない(IsActive = false)
        m.Register(new SkillData(
            Id,
            "連撃",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Rion)));
    }

    public static void OnAttackResolved(
        SkillAttackContext ctx, BattleAttackPreview r)
    {
        // 追撃の連鎖防止(BattleAttackExecutor の resolvingDepth と二重の安全策)
        if (ctx.IsPreview || ctx.IsFollowUp) return;
        if (!SkillIds.IsCharacter(ctx.Attacker, SkillIds.Rion)) return;

        // 庇う等で対象が変わった場合に対応
        UnitView target = r.ActualDefender ?? ctx.Defender;

        if (!SkillBattleAPI.IsAlive(ctx.Attacker) ||
            !SkillBattleAPI.IsAlive(target))
        {
            return;
        }

        if (BattleAttackExecutor.TryExecute(
                ctx.Attacker,
                target,
                out _,
                FollowUpRate,
                useSkillHooks: true,
                isFollowUp: true))
        {
            // 追撃は同期処理のため、演出は1撃目と同時になる
            if (EffectPlayer != null)
            {
                EffectPlayer.PlayHit(ctx.Attacker, target);
            }

            SkillBattleAPI.RefreshDefeatVisual(target);
        }
    }
}
