using UnityEngine;

public enum FEAttackType { Kesa, Thrust, Sweep, Rising, Arrow }
public enum FEHitType { Slash, Pierce, Blunt }

/// <summary>
/// 攻撃エフェクトと被弾エフェクトを別々に出す窓口。シーンに1つ置く (Setup Demo で自動配置)。
///
/// 使い方:
///   // 振り始め (アニメーションイベントなど)
///   var atk = FEBattleFX.PlayAttack(FEAttackType.Kesa, attacker.transform);
///   // ダメージが入った瞬間 (当たった点・攻撃方向・クリティカルか)
///   FEBattleFX.PlayHit(FEHitType.Slash, hitPoint, attacker.transform.forward, isCritical, FEBattleFX.SlashRoll(FEAttackType.Kesa));
///   // 外れた (ミス) ときは PlayHit を呼ばなければ攻撃エフェクトだけになる
///
/// atk.hitTime に「当たるタイミングの目安 (秒)」が入っているので、ダメージ処理の時刻合わせにも使える。
/// </summary>
public class FEBattleFX : MonoBehaviour
{
    public static FEBattleFX Instance { get; private set; }

    [Tooltip("FEAttackType の順: 袈裟斬り, 刺突, 横薙ぎ, 斬り上げ, 弓")]
    public GameObject[] attackPrefabs = new GameObject[5];
    [Tooltip("斬撃, 斬撃クリティカル, 刺突, 刺突クリティカル, 打撃, 打撃クリティカル")]
    public GameObject[] hitPrefabs = new GameObject[6];

    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    // ---------------------------------------------------------------- 攻撃
    public static FEEffect PlayAttack(FEAttackType type, Transform attacker)
    {
        return PlayAttack(type, attacker.position, attacker.rotation);
    }

    /// pos = 攻撃者の足元 (弓は矢の先端), rot = 攻撃方向が +Z
    public static FEEffect PlayAttack(FEAttackType type, Vector3 pos, Quaternion rot)
    {
        int i = (int)type;
        var prefab = Instance != null && i < Instance.attackPrefabs.Length ? Instance.attackPrefabs[i] : null;
        return Spawn(prefab, FEEffectFactory.AttackIds[i], pos, rot);
    }

    // ---------------------------------------------------------------- 被弾
    /// hitPoint = 当たった点, attackDir = 攻撃が向かう方向, slashRollDeg = 斬線の傾き (斬撃のみ。SlashRoll() 参照)
    public static FEEffect PlayHit(FEHitType type, Vector3 hitPoint, Vector3 attackDir, bool critical, float slashRollDeg = 0f)
    {
        int i = (int)type * 2 + (critical ? 1 : 0);
        if (attackDir.sqrMagnitude < 1e-6f) attackDir = Vector3.forward;
        var rot = Quaternion.LookRotation(attackDir.normalized) * Quaternion.AngleAxis(slashRollDeg, Vector3.forward);
        var prefab = Instance != null && i < Instance.hitPrefabs.Length ? Instance.hitPrefabs[i] : null;
        return Spawn(prefab, FEEffectFactory.HitIds[i], hitPoint, rot);
    }

    /// 攻撃の種類に合う被弾の種類
    public static FEHitType HitTypeFor(FEAttackType type)
    {
        return type == FEAttackType.Thrust || type == FEAttackType.Arrow ? FEHitType.Pierce : FEHitType.Slash;
    }

    /// 斬線の傾き (度)。袈裟=右上→左下, 横薙ぎ=水平, 斬り上げ=左下→右上
    public static float SlashRoll(FEAttackType type)
    {
        switch (type)
        {
            case FEAttackType.Kesa: return -35f;
            case FEAttackType.Rising: return 35f;
            default: return 0f;
        }
    }

    static FEEffect Spawn(GameObject prefab, string id, Vector3 pos, Quaternion rot)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, pos, rot);
        }
        else
        {
            // プレハブ未設定 (エディタ再生時) はその場で組み立てる
            go = FEEffectFactory.BuildBattle(id);
            if (go == null) return null;
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
        }
        return go.GetComponent<FEEffect>();
    }
}
