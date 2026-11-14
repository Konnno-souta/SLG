
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 第27段階：シーン統合 + マップセーブ対応。
///
/// NEW GAME
/// タイトル → マップ
///
/// CONTINUE
/// マップセーブが最新 → マップ
/// 戦闘セーブが最新   → 保存された戦闘ステージ
///
/// 戦闘終了後の自動遷移は行わない。
/// </summary>
public sealed class SceneFlowNavigator : MonoBehaviour
{
    [Header("基本シーン")]
    [SerializeField]
    private string titleSceneName = "";

    [SerializeField]
    private string mapSelectSceneName = "";

    [Header("戦闘シーン")]
    [SerializeField]
    private string stage01SceneName = "";

    [SerializeField]
    private string stage02SceneName = "";

    [SerializeField]
    private string stage03SceneName = "";

    [SerializeField]
    private string stage04SceneName = "";

    [SerializeField]
    private string stage05SceneName = "";

    [SerializeField]
    private string stage06SceneName = "";

    [Header("戦闘前会話（任意）")]
    [SerializeField]
    private bool useConversationBeforeBattle = false;

    [SerializeField]
    private string conversationSceneName = "";

    private enum SaveLocation
    {
        None,
        Map,
        Battle
    }

    // ========================================
    // 基本シーン移動
    // ========================================

    public void OpenTitle()
    {
        LoadScene(titleSceneName);
    }

    public void OpenMapSelect()
    {
        LoadScene(mapSelectSceneName);
    }

    // ========================================
    // NEW GAME
    // ========================================

    /// <summary>
    /// タイトルでNEW GAMEのスロットを
    /// 確定したあとに呼ばれる。
    /// </summary>
    public void OpenMapSelectFromSlot(int slotNumber)
    {
        if (!CanLoadScene(mapSelectSceneName))
        {
            return;
        }

        if (!BattleLaunchSession.BeginNewGame(
            slotNumber))
        {
            return;
        }

        // 古い戦闘ロード要求を残さない。
        PendingBattleContinueRequest.Clear();

        // 新規ゲームなのでマップ進行を初期化。
        if (!StageProgressSaveSystem
            .ResetSlotForNewGame(slotNumber))
        {
            Debug.LogError(
                $"DATA{slotNumber}のマップ初期化に失敗。",
                this
            );
            return;
        }

        Debug.Log(
            $"【NEW GAME】DATA{slotNumber}" +
            "\nマップ進行情報を初期化しました。",
            this
        );

        OpenMapSelect();
    }

    // ========================================
    // CONTINUE
    // ========================================

    /// <summary>
    /// 最後に保存された場所に復帰する。
    ///
    /// マップ保存が最新ならマップへ。
    /// 戦闘保存が最新ならその戦闘ステージへ。
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

        if (slotNumber < 1 || slotNumber > 3)
        {
            Debug.LogError(
                $"不正なスロット番号：{slotNumber}",
                this
            );
            return;
        }

        SaveLocation location =
            GetLatestSaveLocation(slotNumber);

        // ====================================
        // セーブデータが存在しない
        // ====================================

        if (location == SaveLocation.None)
        {
            Debug.LogWarning(
                $"DATA{slotNumber}に" +
                "セーブファイルがありません。",
                this
            );
            return;
        }

        // ====================================
        // マップから再開
        // ====================================

        if (location == SaveLocation.Map)
        {
            if (!CanLoadScene(mapSelectSceneName))
            {
                return;
            }

            // マップロード時には
            // 戦闘ロード要求を使用しない。
            //
            // BeginNewGameはここでは
            // 起動モードの切り替えに使用する。
            // マップセーブの初期化はしない。
            if (!BattleLaunchSession.BeginNewGame(
                slotNumber))
            {
                Debug.LogError(
                    "マップ復帰用のセッション設定に失敗。",
                    this
                );
                return;
            }

            // 古い戦闘復帰要求を解除。
            PendingBattleContinueRequest.Clear();

            Debug.Log(
                $"【CONTINUE】DATA{slotNumber}" +
                "\nマップシーンから再開します。",
                this
            );

            // マップの進行情報は
            // StageSelectManager.Awake()で復元する。
            OpenMapSelect();

            return;
        }

        // ====================================
        // 戦闘から再開
        // ====================================

        if (!BattleLaunchSession.HasContinuePending)
        {
            Debug.LogError(
                "戦闘CONTINUEの引き継ぎ情報がありません。" +
                "\nタイトル側のBeginContinueを確認。",
                this
            );
            return;
        }

        if (BattleLaunchSession.CurrentSlot !=
            slotNumber)
        {
            Debug.LogError(
                "CONTINUEのDATA番号が一致しません。",
                this
            );
            return;
        }

        string stageId =
            (BattleLaunchSession.StageId ?? "").Trim();

        if (!TryGetStageSceneName(
            stageId,
            out string sceneName))
        {
            Debug.LogWarning(
                $"StageID「{stageId}」の" +
                "戦闘シーンが未設定です。" +
                "\nSceneFlowNavigatorのInspectorを" +
                "確認してください。",
                this
            );
            return;
        }

        if (!CanLoadScene(sceneName))
        {
            return;
        }

        Debug.Log(
            $"【CONTINUE開始】DATA{slotNumber}" +
            $"\nStageID：{stageId}" +
            $"\nScene：{sceneName}",
            this
        );

        // 既存のPendingを維持したまま移動。
        // 戦闘シーン側のBattleContinueStartupが
        // 保存状態を復元する。
        LoadScene(sceneName);
    }

    // ========================================
    // 最後に保存した場所を判定
    // ========================================

    private SaveLocation GetLatestSaveLocation(
        int slotNumber)
    {
        string mapPath =
            StageProgressSaveSystem.GetSavePath(
                slotNumber
            );

        string battlePath =
            Path.Combine(
                StageProgressSaveSystem.SaveDirectory,
                $"battle_slot_{slotNumber}.json"
            );

        bool hasMap = File.Exists(mapPath);
        bool hasBattle = File.Exists(battlePath);

        if (!hasMap && !hasBattle)
        {
            return SaveLocation.None;
        }

        if (hasMap && !hasBattle)
        {
            return SaveLocation.Map;
        }

        if (!hasMap && hasBattle)
        {
            return SaveLocation.Battle;
        }

        try
        {
            DateTime mapTime =
                File.GetLastWriteTimeUtc(mapPath);

            DateTime battleTime =
                File.GetLastWriteTimeUtc(battlePath);

            // 同時刻ならマップを優先。
            return mapTime >= battleTime
                ? SaveLocation.Map
                : SaveLocation.Battle;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "セーブ日時の取得に失敗：" +
                ex.Message,
                this
            );

            return SaveLocation.None;
        }
    }

    // ========================================
    // StageID → シーン名
    // ========================================

    private bool TryGetStageSceneName(
        string stageId,
        out string sceneName)
    {
        sceneName = "";

        if (string.IsNullOrWhiteSpace(stageId))
        {
            return false;
        }

        string value = stageId.Trim();

        // "ST03" または "3" の両方に対応。
        if (value.StartsWith(
            "ST",
            StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(2);
        }

        if (!int.TryParse(
            value,
            out int stageNumber))
        {
            return false;
        }

        switch (stageNumber)
        {
            case 1:
                sceneName = stage01SceneName;
                break;

            case 2:
                sceneName = stage02SceneName;
                break;

            case 3:
                sceneName = stage03SceneName;
                break;

            case 4:
                sceneName = stage04SceneName;
                break;

            case 5:
                sceneName = stage05SceneName;
                break;

            case 6:
                sceneName = stage06SceneName;
                break;

            default:
                return false;
        }

        return !string.IsNullOrWhiteSpace(sceneName);
    }

    // ========================================
    // マップから戦闘シーンへ
    // ========================================

    /// <summary>
    /// マップでST01を選んだとき。
    /// 既存のUnityEventとの互換性を維持。
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
    /// ST01へ移動。
    /// 会話終了後からも呼び出せる。
    /// </summary>
    public void OpenStage01()
    {
        Debug.Log(
            "【シーン統合】ステージ1へ移動",
            this
        );

        LoadScene(stage01SceneName);
    }

    // ========================================
    // シーン読み込み
    // ========================================

    private bool CanLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning(
                "移動先Scene名が未設定です。",
                this
            );
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(
            sceneName))
        {
            Debug.LogError(
                $"Scene「{sceneName}」を読み込めません。" +
                "\nBuild Profiles / Build Settingsを" +
                "確認してください。",
                this
            );
            return false;
        }

        return true;
    }

    private void LoadScene(string sceneName)
    {
        if (!CanLoadScene(sceneName))
        {
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
