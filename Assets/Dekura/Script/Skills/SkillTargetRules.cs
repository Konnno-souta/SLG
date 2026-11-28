using System;
using System.Collections.Generic;
using UnityEngine;

// スキルごとの対象選択ルール。
// SkillData を変更せず、スキルIDをキーにして別管理する。
public sealed class SkillTargetRule
{
    // 通常攻撃型スキルの攻撃回数。
    // 1以上: 対象選択・プレビュー・演出を通常攻撃と同じ流れで行い、この回数だけ攻撃する(リオンなど)
    // 0: 通常のスキル(GetValidCells で選んだマスを確定して TryUse)
    public int AttackCount;

    // true: ResolveTargets が空のマスでは確定操作を受け付けない(エアなど)
    public bool RequireTarget;

    // 1以上: 対象確定(TryUse成功)後、続けて使用者の移動先を選ばせる(移動力はこの値)。
    // 通常の移動と同じ挙動(範囲表示→クリックで経路移動)。
    public int FollowUpMoveRange;

    // 移動フェーズ中にキャンセルされたときの巻き戻し処理(エアなら運搬の取り消し)。
    // 移動が完了した後はキャンセルできない。
    public Action<UnitView, SkillManager> OnFollowUpCancelled;

    // 移動完了後に続けて「配置マス」を選ばせる(エアの降ろす先)。
    // 引数: 使用者, 中心マス(使用者の位置), マネージャー → 選べるマス。
    // 移動先の絞り込みにも使う(配置マスが0件になる移動先は選べない)。
    public Func<UnitView, Vector2Int, SkillManager, HashSet<Vector2Int>> GetPlaceCells;

    // 配置マスが確定したときの処理。true で成功(スキル終了)、false なら選択を続ける
    public Func<UnitView, Vector2Int, SkillManager, bool> OnPlace;

    // 選択できるマス(通常攻撃型は攻撃可能範囲)
    public Func<UnitView, SkillManager, HashSet<Vector2Int>> GetValidCells;

    // 1回目クリックで表示する影響範囲(単体なら選択マスのみ)
    public Func<UnitView, Vector2Int, HashSet<Vector2Int>> GetAffectedCells;

    // 確定時に SkillContext.Targets へ入れるユニット
    public Func<UnitView, Vector2Int, SkillManager, List<UnitView>> ResolveTargets;
}

public static class SkillTargetRules
{
    private static readonly Dictionary<string, SkillTargetRule> rules =
        new Dictionary<string, SkillTargetRule>();

    public static void Register(string id, SkillTargetRule rule) =>
        rules[id] = rule;

    public static bool TryGet(string id, out SkillTargetRule rule) =>
        rules.TryGetValue(id, out rule);
}