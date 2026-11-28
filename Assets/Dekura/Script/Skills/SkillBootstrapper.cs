using UnityEngine;

// 全スキルの登録とイベント購読。SkillManager と同じシーンに配置する。
[DefaultExecutionOrder(100)]
public sealed class SkillBootstrapper : MonoBehaviour
{
    private SkillManager mgr;

    private void Start()
    {
        // SkillManager.Instance は Awake で設定されるので Start で取得する
        mgr = SkillManager.Instance;

        if (mgr == null)
        {
            Debug.LogError("SkillManagerがありません", this);
            enabled = false;
            return;
        }

        // スキル登録(新しいスキルはここに1行足す)
        ClanSkill.Register(mgr);
        MillardSkill.Register(mgr);
        BitSkill.Register(mgr);
        RionSkill.Register(mgr);
        RaySkill.Register(mgr);
        KarmaSkill.Register(mgr);
        AirSkill.Register(mgr);

        // イベント購読
        mgr.AttackPreparing += MillardSkill.OnAttackPreparing;
        mgr.AttackResolved += MillardSkill.OnAttackResolved;
        mgr.BattleTurnStarted += MillardSkill.OnTurnStarted;
    }

    private void OnDestroy()
    {
        if (mgr == null) return;

        // シーン再読込での二重購読を防ぐ
        mgr.AttackPreparing -= MillardSkill.OnAttackPreparing;
        mgr.AttackResolved -= MillardSkill.OnAttackResolved;
        mgr.BattleTurnStarted -= MillardSkill.OnTurnStarted;
    }
}