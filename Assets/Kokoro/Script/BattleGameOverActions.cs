using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameOverMenuから呼ぶ敗北後の操作。
/// 既存の戦闘ロード・マップ進行を再利用する。
/// </summary>
public sealed class BattleGameOverActions : MonoBehaviour
{
    [Header("システム参照")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleSaveManager saveManager;
    [SerializeField] private BattleStageLoader stageLoader;

    [Header("コンテニュー")]
    [Tooltip("ON: 同じステージの有効なセーブから再開。OFF: ステージの最初から再開")]
    [SerializeField] private bool resumeSavedBattle = true;

    [Header("戻るマップ")]
    [SerializeField] private string mapSceneName = "StageSelectScene";

    private bool transitioning;

    // GameOverMenu.On Continue から呼ぶ。
    public void ContinueBattle()
    {
        if (!CanLeaveGameOver())
            return;

        Scene currentScene = SceneManager.GetActiveScene();

        if (currentScene.buildIndex < 0 ||
            !Application.CanStreamedLevelBeLoaded(currentScene.buildIndex))
        {
            Debug.LogError(
                "現在の戦闘シーンをBuild Profilesに登録してください。",
                this
            );
            return;
        }

        // 以前の復元要求を消し、必要な場合だけ新しい要求を作る。
        BattleLaunchSession.NotifyContinueRestored();

        bool restoreRequested = TryPrepareSavedBattle();

        Debug.Log(
            restoreRequested
                ? "【ゲームオーバー】保存地点から再開します。"
                : "【ゲームオーバー】ステージの最初から再開します。",
            this
        );

        transitioning = true;
        Time.timeScale = 1f;

        // 戦闘終了済みのTurnManagerを直接復元せず、
        // シーンを再生成して既存のBattleContinueStartupに任せる。
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    // GameOverMenu.On Return To Map から呼ぶ。
    public void ReturnToMap()
    {
        if (!CanLeaveGameOver())
            return;

        string sceneName = (mapSceneName ?? "").Trim();

        if (string.IsNullOrEmpty(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"マップシーン「{sceneName}」がBuild Profilesにありません。",
                this
            );
            return;
        }

        // 敗北ではクリアフラグを追加しない。
        // 未処理の復元要求もマップへ持ち込まない。
        BattleLaunchSession.NotifyContinueRestored();

        // Direct起動はファイル保存せず、既存のマップ進行だけを維持。
        if (BattleLaunchSession.Mode == BattleLaunchMode.Direct)
            BattleStageClearProgress.MarkDirectMapReturn();

        Debug.Log("【ゲームオーバー】マップへ戻ります。", this);

        transitioning = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private bool TryPrepareSavedBattle()
    {
        // Direct起動では本番DATAの古い戦闘セーブを誤って読まない。
        // NEW GAME直後の旧セーブもCanDebugLoadで保護する。
        if (!resumeSavedBattle ||
            BattleLaunchSession.Mode == BattleLaunchMode.Direct ||
            !BattleLaunchSession.CanDebugLoad)
        {
            return false;
        }

        if (saveManager == null || stageLoader == null ||
            stageLoader.CurrentStage == null)
        {
            Debug.LogWarning("戦闘セーブの参照が不足しています。", this);
            return false;
        }

        int slot = BattleLaunchSession.CurrentSlot;

        if (!saveManager.TryReadSlot(slot, out BattleSaveData data) ||
            data == null)
        {
            return false;
        }

        // 別ステージのセーブに切り替えない。
        if (data.stageId != stageLoader.CurrentStage.StageId ||
            data.battlePhase != BattlePhase.Player.ToString())
        {
            Debug.LogWarning("現在のステージの有効な戦闘セーブがありません。", this);
            return false;
        }

        // 既存のタイトルCONTINUEと同じ復元要求。
        return BattleLaunchSession.BeginContinue(slot, data.stageId);
    }

    private bool CanLeaveGameOver()
    {
        if (transitioning)
            return false;

        if (turnManager == null || !turnManager.IsBattleFinished)
        {
            Debug.LogWarning("実際のゲームオーバー後に実行してください。", this);
            return false;
        }

        // 勝利画面の確認ボタン等から誤って呼ばないようにする。
        foreach (UnitView unit in turnManager.BattleUnits)
        {
            if (unit == null || unit.State == null || unit.MasterData == null)
                continue;

            if (unit.State.Team == UnitTeam.Ally &&
                unit.MasterData.Id == "1" &&
                BattleUnitLife.IsAlive(unit))
            {
                Debug.LogWarning("勝利時にはゲームオーバー操作を実行できません。", this);
                return false;
            }
        }

        // TurnManagerと同じく、主人公が存在しない場合も敗北扱い。
        return true;
    }
}
