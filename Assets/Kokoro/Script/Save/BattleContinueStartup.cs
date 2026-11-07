
using System.Collections;
using UnityEngine;

/// <summary>
/// 第24段階：タイトルCONTINUEの自動ロード。
///
/// 戦闘Sceneが準備できた後で
/// BattleLoadRestorer.RestoreSlot()を呼ぶ。
///
/// NEW GAMEや直接Play時は何もしない。
/// </summary>
public sealed class BattleContinueStartup
    : MonoBehaviour
{
    [Header("必須参照")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private UnitSelectionController selectionController;

    [SerializeField]
    private BattleLoadRestorer loadRestorer;

    private IEnumerator Start()
    {
        // 新規開始なら従来どおりTURN1。
        if (!BattleLaunchSession.HasContinuePending)
        {
            yield break;
        }

        int slot = BattleLaunchSession.CurrentSlot;

        Debug.Log(
            $"【CONTINUE準備開始】DATA{slot}",
            this
        );

        if (turnManager == null ||
            selectionController == null ||
            loadRestorer == null)
        {
            Debug.LogError(
                "BattleContinueStartupの" +
                "Inspector参照が不足しています。",
                this
            );

            yield break;
        }

        // CSV配置・ターン管理の初期化を待つ。
        // フレーム数に上限を設け、
        // 初期化失敗時の無限待機を防ぐ。
        const int MaxWaitFrames = 300;

        for (int i = 0; i < MaxWaitFrames; i++)
        {
            if (turnManager.CurrentPhase ==
                    BattlePhase.Player &&
                !turnManager.IsBattleFinished &&
                selectionController.CanRestoreBattleState)
            {
                // 既存のロード機能をそのまま使う。
                bool success =
                    loadRestorer.RestoreSlot(slot);

                if (success)
                {
                    BattleLaunchSession
                        .NotifyContinueRestored();

                    Debug.Log(
                        $"【CONTINUE復元成功】DATA{slot}" +
                        $"\nTURN {turnManager.TurnNumber}",
                        this
                    );
                }
                else
                {
                    Debug.LogError(
                        $"【CONTINUE復元失敗】DATA{slot}" +
                        "\nセーブデータのステージID・" +
                        "配置情報・ロード条件を" +
                        "確認してください。",
                        this
                    );
                }

                yield break;
            }

            yield return null;
        }

        Debug.LogError(
            "【CONTINUE復元失敗】" +
            "戦闘Sceneの初期化が完了しませんでした。",
            this
        );
    }
}
