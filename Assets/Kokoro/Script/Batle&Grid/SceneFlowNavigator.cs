
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 第24段階：シーン統合用。
///
/// タイトル・マップ選択・ステージ1の
/// シーン移動を管理する。
///
/// 勝敗判定による自動シーン移動は行わない。
/// </summary>
public sealed class SceneFlowNavigator : MonoBehaviour
{
    [Header("シーン名")]
    [Tooltip("タイトル画面のScene名")]
    [SerializeField]
    private string titleSceneName = "";

    [Tooltip("マップ選択画面のScene名")]
    [SerializeField]
    private string mapSelectSceneName = "";

    [Tooltip("今動かしているメインSceneの名前")]
    [SerializeField]
    private string stage01SceneName = "";

    // =====================================
    // タイトルへ
    // =====================================

    public void OpenTitle()
    {
        LoadScene(titleSceneName);
    }

    // =====================================
    // マップ選択へ
    // =====================================

    public void OpenMapSelect()
    {
        LoadScene(mapSelectSceneName);
    }

    // =====================================
    // ステージ1へ
    // =====================================

    public void OpenStage01()
    {
        Debug.Log(
            "【シーン統合】ステージ1へ移動"
        );

        LoadScene(stage01SceneName);
    }

    // =====================================
    // 共通シーン読み込み
    // =====================================

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning(
                "移動先のScene名が未設定です。",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
            sceneName))
        {
            Debug.LogError(
                $"Scene「{sceneName}」を読み込めません。" +
                "Build SettingsまたはBuild Profilesの" +
                "シーン登録を確認してください。",
                this
            );
            return;
        }

        // 演出でゲームを一時停止していた場合、
        // 次のシーンを正常に開始できるよう戻す。
        Time.timeScale = 1f;

        Debug.Log(
            $"【シーン移動】{sceneName}",
            this
        );

        SceneManager.LoadScene(sceneName);
    }
}
