
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
        if (!BattleLaunchSession.HasContinuePending ||
            BattleLaunchSession.CurrentSlot != slotNumber)
        {
            Debug.LogError(
                "CONTINUEの引き継ぎ情報がありません。",
                this
            );
            return;
        }

        // 現在の戦闘Sceneはステージ1だけ。
        if (BattleLaunchSession.StageId != "1")
        {
            Debug.LogWarning(
                "現在はステージ1のCONTINUEのみ対応。" +
                $" 保存StageID={BattleLaunchSession.StageId}",
                this
            );
            return;
        }

        Debug.Log(
            $"【CONTINUE】DATA{slotNumber}" +
            " → ステージ1",
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
