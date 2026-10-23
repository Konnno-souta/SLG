using System.Collections;
using UnityEngine;

/// <summary>
/// 攻撃 → (当たるタイミングで) 被弾 の流れを試すデモ。画面右側のボタンで再生。
/// 「ヒット」「クリティカル」「ミス」(攻撃エフェクトだけ) を選べる。
/// </summary>
public class FEBattleDemo : MonoBehaviour
{
    [Tooltip("攻撃側の足元")] public Vector3 attackerPos = Vector3.zero;
    [Tooltip("被弾側の足元")] public Vector3 targetPos = new Vector3(0f, 0f, 2.4f);
    [Tooltip("被弾側の当たる高さ")] public float hitHeight = 1.1f;
    [Tooltip("矢の速さ (m/秒)")] public float arrowSpeed = 9f;

    static readonly string[] ResultNames = { "ヒット", "クリティカル", "ミス" };

    Vector3 Dir
    {
        get { var d = targetPos - attackerPos; d.y = 0f; return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward; }
    }

    Vector3 HitPoint { get { return targetPos + Vector3.up * hitHeight - Dir * 0.3f; } }

    /// result: 0 = ヒット, 1 = クリティカル, 2 = ミス
    public void PlayAttackAndHit(FEAttackType type, int result)
    {
        StartCoroutine(Run(type, result));
    }

    IEnumerator Run(FEAttackType type, int result)
    {
        var rot = Quaternion.LookRotation(Dir);
        float wait;
        if (type == FEAttackType.Arrow)
        {
            var start = attackerPos + Vector3.up * 1.3f + Dir * 0.2f;
            var atk = FEBattleFX.PlayAttack(type, start, rot);
            if (atk != null) atk.gameObject.AddComponent<FEMover>().velocity = Dir * arrowSpeed;
            wait = Vector3.Distance(start, HitPoint) / Mathf.Max(arrowSpeed, 0.01f);
        }
        else
        {
            var atk = FEBattleFX.PlayAttack(type, attackerPos, rot);
            wait = atk != null ? atk.hitTime : 0.2f;
        }

        if (result == 2) yield break;   // ミス: 被弾エフェクトなし
        yield return new WaitForSeconds(wait);
        FEBattleFX.PlayHit(FEBattleFX.HitTypeFor(type), HitPoint, Dir, result == 1, FEBattleFX.SlashRoll(type));
    }

    public void PlayBlunt(bool critical)
    {
        FEBattleFX.PlayHit(FEHitType.Blunt, HitPoint, Dir, critical);
    }

    void OnGUI()
    {
        const float w = 300f;
        GUILayout.BeginArea(new Rect(Screen.width - w - 10f, 10f, w, 260f), GUI.skin.box);
        GUILayout.Label("攻撃 → 被弾 (別々のエフェクト)");
        for (int t = 0; t < FEEffectFactory.AttackNames.Length; t++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(FEEffectFactory.AttackNames[t], GUILayout.Width(70f));
            for (int r = 0; r < 3; r++)
                if (GUILayout.Button(ResultNames[r])) PlayAttackAndHit((FEAttackType)t, r);
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("打撃のみ", GUILayout.Width(70f));
        if (GUILayout.Button("ヒット")) PlayBlunt(false);
        if (GUILayout.Button("クリティカル")) PlayBlunt(true);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
