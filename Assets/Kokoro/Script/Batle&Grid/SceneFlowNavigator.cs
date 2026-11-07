
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 第24段階：シーン統合。
///
/// NEW GAME:
/// タイトル → マップセレクト → 戦闘
///
/// CONTINUE:
/// タイトル → 保存済みステージの戦闘
///
/// クリア・ゲームオーバーによる
/// 自動シーン遷移は行わない。
/// </summary>
public sealed class SceneFlowNavigator : MonoBehaviour
{
    [Header("シーン名")]
    [SerializeField]
    private string titleSceneName = "";

    [SerializeField]
    private string mapSelectSceneName = "";

    [SerializeField]
    private string stage01SceneName = "";

    [Header("戦闘前会話（任意）")]
    [SerializeField]
    private bool useConversationBeforeBattle = false;

    [SerializeField]
    private string conversationSceneName = "";

    public void OpenTitle()
    {
        LoadScene(titleSceneName);
    }

    public void OpenMapSelect()
    {
        LoadScene(mapSelectSceneName);
    }

    /// <summary>
    /// タイトルでNEW GAMEのDATAを選択後に呼ぶ。
    /// </summary>
    public void OpenMapSelectFromSlot(int slotNumber)
    {
        if (!BattleLaunchSession.BeginNewGame(
            slotNumber))
        {
            return;
        }

        Debug.Log(
            $"【NEW GAME】DATA{slotNumber}" +
            " → マップセレクト",
            this
        );

        OpenMapSelect();
    }

    /// <summary>
    /// CONTINUEのDATAを選択後に呼ぶ。
    ///
    /// StageIdはTitleSaveSlotSelectionControllerが
    /// BattleLaunchSessionへ登録済み。
    /// </summary>

    public void OpenContinueFromSlot(int slotNumber)
    {
        Debug.Log(
            "【CONTINUE受信確認】" +
            $"\nイベントDATA：{slotNumber}" +
            $"\n保持DATA：{BattleLaunchSession.CurrentSlot}" +
            $"\nモード：{BattleLaunchSession.Mode}" +
            $"\nPending：{BattleLaunchSession.HasContinuePending}" +
            $"\nStageID：{BattleLaunchSession.StageId}",
            this
        );

        if (!BattleLaunchSession.HasContinuePending)
        {
            Debug.LogError(
                "CONTINUEの引き継ぎ情報がありません。" +
                "\nBeginContinueが実行されたか確認してください。",
                this
            );
            return;
        }

        if (BattleLaunchSession.CurrentSlot != slotNumber)
        {
            Debug.LogError(
                "CONTINUEのDATA番号が一致しません。" +
                "\nUnityEventのDynamic int設定を確認してください。",
                this
            );
            return;
        }

        string stageId =
            BattleLaunchSession.StageId.Trim();

        // 現段階はステージ1のみ対応
        bool isStage01 =
            stageId == "1" ||
            stageId.Equals(
                "ST01",
                System.StringComparison.OrdinalIgnoreCase
            );

        if (!isStage01)
        {
            Debug.LogWarning(
                $"未対応のStageID：{stageId}" +
                "\n現在はステージ1のみ読み込めます。",
                this
            );
            return;
        }

        Debug.Log(
            $"【CONTINUE開始】DATA{slotNumber}" +
            $" / StageID={stageId}",
            this
        );

        OpenStage01();
    }


    /// <summary>
    /// マップセレクトでステージ1を選択。
    /// </summary>
    public void OpenSelectedStage01()
    {
        if (useConversationBeforeBattle)
        {
            LoadScene(conversationSceneName);
            return;
        }

        OpenStage01();
    }

    /// <summary>
    /// ステージ1へ。
    /// 会話終了後にも使用する。
    /// </summary>
    public void OpenStage01()
    {
        Debug.Log(
            "【シーン統合】ステージ1へ移動",
            this
        );

        LoadScene(stage01SceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning(
                "移動先Scene名が未設定です。",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
            sceneName))
        {
            Debug.LogError(
                $"Scene「{sceneName}」を読み込めません。" +
                " Build Profiles / Build Settingsを" +
                "確認してください。",
                this
            );
            return;
        }

        Time.timeScale = 1f;

        Debug.Log(
            $"【シーン移動】{sceneName}",
            this
        );

        SceneManager.LoadScene(sceneName);
    }
}
