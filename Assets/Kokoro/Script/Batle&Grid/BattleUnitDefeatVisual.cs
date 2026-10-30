
using UnityEngine;

/// <summary>
///
/// HPが0になったら：
/// ・Rendererを非表示
/// ・Colliderを無効化
///
/// HPが1以上に復元されたら：
/// ・元のRenderer状態へ戻す
/// ・元のCollider状態へ戻す
///
/// GameObjectとUnitViewは残す。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnitView))]
public sealed class BattleUnitDefeatVisual : MonoBehaviour
{
    private UnitView unit;

    private Renderer[] renderers;
    private bool[] originalRendererStates;

    private Collider[] colliders;
    private bool[] originalColliderStates;

    private bool hasLastState;
    private bool lastDefeated;

    // ========================================
    // 初期化
    // ========================================

    private void Awake()
    {
        unit = GetComponent<UnitView>();

        renderers =
            GetComponentsInChildren<Renderer>(true);

        originalRendererStates =
            new bool[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalRendererStates[i] =
                renderers[i].enabled;
        }

        colliders =
            GetComponentsInChildren<Collider>(true);

        originalColliderStates =
            new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            originalColliderStates[i] =
                colliders[i].enabled;
        }
    }

    // ========================================
    // HP状態を監視
    // ========================================

    private void Update()
    {
        Refresh();
    }

    /// <summary>
    /// HPの変化に合わせて表示を更新する。
    /// セーブ・ロード後にも自動反映される。
    /// </summary>
    public void Refresh()
    {
        if (unit == null ||
            unit.State == null ||
            unit.State.CombatStats == null)
        {
            return;
        }

        bool defeated =
            unit.State.CombatStats.CurrentHP <= 0;

        // 状態に変化がない場合は処理しない。
        if (hasLastState &&
            lastDefeated == defeated)
        {
            return;
        }

        hasLastState = true;
        lastDefeated = defeated;

        // ====================================
        // モデル表示
        // ====================================

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].enabled =
                !defeated &&
                originalRendererStates[i];
        }

        // ====================================
        // Collider
        // ====================================

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            colliders[i].enabled =
                !defeated &&
                originalColliderStates[i];
        }

        if (defeated)
        {
            Debug.Log(
                $"【戦闘不能】{unit.name} " +
                "HPが0になりました。",
                this
            );
        }
        else
        {
            Debug.Log(
                $"【戦闘可能】{unit.name} " +
                "HPが1以上です。",
                this
            );
        }
    }
}
