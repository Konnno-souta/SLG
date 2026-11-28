using System;
using UnityEngine;

/// <summary>
/// StageConfigをAwakeで読み込み、
/// GridDebugView.Startより前にサイズを適用。
/// StartでBattleUnitSpawnerを呼んで配置する。
/// 3D_Nameの地形PrefabはResources/Stages/から読み込む。
/// </summary>
public sealed class BattleStageLoader : MonoBehaviour
{
    [Header("プランナーの正式CSV")]
    [SerializeField] private TextAsset stageConfigCsv;
    [SerializeField] private TextAsset stageCharaLayoutCsv;

    [Header("ステージ指定")]
    [Tooltip("StageConfig.csvのStageIDを指定。例：1、6、61、7、8、9")]
    [SerializeField] private string stageId = "1";

    [Header("参照")]
    [SerializeField] private BattleGrid grid;
    [SerializeField] private BattleUnitSpawner spawner;

    private bool initialized;

    public StageConfigData CurrentStage
    {
        get;
        private set;
    }

    private void Awake()
    {
        if (grid == null || stageConfigCsv == null)
        {
            Debug.LogError(
                "BattleStageLoader: GridまたはStageConfig.csvが未設定です。",
                this
            );
            return;
        }

        try
        {
            int matches = 0;

            // ストーリーIDではなく戦闘マップIDで検索する。
            string requestedStageId =
                (stageId ?? "").Trim();

            foreach (CsvRow row in CsvReader.Read(
                stageConfigCsv,
                "StageID"))
            {
                if (row.Get("StageID") != requestedStageId)
                    continue;

                matches++;
                CurrentStage = StageConfigData.FromCsv(row);
            }

            if (matches != 1)
            {
                throw new FormatException(
                    $"StageID={requestedStageId} の行数が" +
                    $"{matches}件です。1件だけ必要です。"
                );
            }

            grid.Initialize(
                CurrentStage.Width,
                CurrentStage.Height
            );

            LoadField();

            initialized = true;

            Debug.Log(
                $"ステージ初期化: {CurrentStage.StageId}, " +
                $"{CurrentStage.Width}×{CurrentStage.Height}, " +
                $"ストーリーID={CurrentStage.StoryId}, " +
                $"地形Prefab名={CurrentStage.FieldName}",
                this
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"StageConfig読込エラー: {ex.Message}",
                this
            );
        }
    }

    private void Start()
    {
        if (!initialized)
            return;

        if (stageCharaLayoutCsv == null ||
            spawner == null)
        {
            Debug.LogError(
                "BattleStageLoader: Stage_CharaLayout.csvか" +
                "Spawnerが未設定です。",
                this
            );
            return;
        }

        // 配置CSVも戦闘マップのStageIDで検索する。
        spawner.SpawnStage(
            CurrentStage.StageId,
            stageCharaLayoutCsv
        );
    }

    private GameObject fieldInstance;

    /// <summary>
    /// Resources/Stages/{3D_Name}の地形Prefabを生成する。
    /// Prefabが無い場合は警告だけ出して続行する。
    /// </summary>
    private void LoadField()
    {
        string fieldName = CurrentStage.FieldName;

        if (string.IsNullOrEmpty(fieldName))
            return;

        GameObject prefab =
            Resources.Load<GameObject>(
                $"Stages/{fieldName}"
            );

        if (prefab == null)
        {
            Debug.LogWarning(
                $"地形Prefabが見つかりません: " +
                $"Resources/Stages/{fieldName}",
                this
            );
            return;
        }

        fieldInstance = Instantiate(
            prefab,
            grid.transform.position,
            Quaternion.identity
        );

        fieldInstance.name = fieldName;
    }
}