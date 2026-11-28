using System;
using UnityEngine;

/// <summary>
/// 実際に勝利したステージのクリア情報を、現在のDATAに保存する。
/// StageClearPanel の On Next から RecordClear を呼ぶ。
/// シーン遷移と会話再生は StageClearPanel に任せる。
/// </summary>
public sealed class BattleStageClearProgress : MonoBehaviour
{
    [Header("システム参照")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleStageLoader stageLoader;

    private bool recorded;

    // Direct起動で戦闘からマップ選択へ戻る場合だけ進行を引き継ぐ。
    // シーン移動を越えて保持し、マップ側で一度だけ消費する。
    private static bool directClearReturnPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDirectClearReturn()
    {
        directClearReturnPending = false;
    }

    // 勝利・敗北どちらの帰還でも使用。クリア記録は変更しない。
    public static void MarkDirectMapReturn()
    {
        if (BattleLaunchSession.Mode == BattleLaunchMode.Direct)
            directClearReturnPending = true;
    }

    public static bool ConsumeDirectClearReturn()
    {
        bool pending = directClearReturnPending;
        directClearReturnPending = false;
        return pending;
    }

    public void RecordClear()
    {
        if (recorded)
            return;

        if (turnManager == null || stageLoader == null)
        {
            Debug.LogError("クリア記録：参照が不足しています。", this);
            return;
        }

        // 確認ボタンだけでステージを解放しない。
        if (!IsActualVictory())
        {
            Debug.LogWarning("戦闘勝利時のみクリアを記録します。", this);
            return;
        }

        if (stageLoader.CurrentStage == null)
        {
            Debug.LogError("クリア記録：ステージが取得できません。", this);
            return;
        }

        // CSVの「1」と「ST01」の両形式に対応。
        string id = (Convert.ToString(stageLoader.CurrentStage.StageId) ?? "").Trim();

        if (id.StartsWith("ST", StringComparison.OrdinalIgnoreCase))
            id = id.Substring(2);

        if (!int.TryParse(id, out int stageId) ||
            stageId < 1 || stageId > 6)
        {
            Debug.LogError("クリア記録：ステージIDが不正です。", this);
            return;
        }

        // Unityから直接シーンを起動した場合は、
        // 本番のセーブスロットを変更せずメモリ上だけ記録する。
        if (BattleLaunchSession.Mode.ToString() == "Direct")
        {
            if (StageProgress.Data == null)
                StageProgress.ResetAll();

            StageProgress.CurrentStageId = stageId;
            StageProgress.SetCleared(stageId);

            MarkDirectMapReturn();
            recorded = true;

            Debug.Log($"【デバッグクリア記録】Stage{stageId}（ファイル保存なし）", this);
            return;
        }

        int slot = BattleLaunchSession.CurrentSlot;

        if (slot < 1 || slot > 3)
        {
            Debug.LogError("クリア記録：セーブスロットが未選択です。", this);
            return;
        }

        // 既存のマップ進行セーブを再利用する。
        if (!StageProgressSaveSystem.RecordStageClear(slot, stageId))
        {
            Debug.LogError("ステージクリアの保存に失敗しました。", this);
            return;
        }

        recorded = true;

        Debug.Log($"【ステージクリア記録成功】DATA{slot} / Stage{stageId}", this);
    }

    private bool IsActualVictory()
    {
        if (!turnManager.IsBattleFinished)
            return false;

        bool protagonistAlive = false;

        foreach (UnitView unit in turnManager.BattleUnits)
        {
            if (!BattleUnitLife.IsAlive(unit))
                continue;

            if (unit.State.Team == UnitTeam.Enemy)
                return false;

            if (unit.State.Team == UnitTeam.Ally &&
                unit.MasterData != null &&
                unit.MasterData.Id == "1")
            {
                protagonistAlive = true;
            }
        }

        return protagonistAlive;
    }
}
