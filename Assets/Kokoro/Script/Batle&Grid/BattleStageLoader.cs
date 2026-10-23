using System;
using UnityEngine;

/// <summary>
/// StageConfigをAwakeで読み込み、GridDebugView.Startより前にサイズを適用。
/// StartでBattleUnitSpawnerを呼んで配置する。
/// この段階では3D_Nameの地形Prefabは自動ロードしない。
/// </summary>
public sealed class BattleStageLoader : MonoBehaviour
{
    [Header("プランナーの正式CSV")]
    [SerializeField] private TextAsset stageConfigCsv;
    [SerializeField] private TextAsset stageCharaLayoutCsv;

    [Header("ステージ指定")]
    [SerializeField] private string stageId = "ST01";

    [Header("参照")]
    [SerializeField] private BattleGrid grid;
    [SerializeField] private BattleUnitSpawner spawner;

    private bool initialized;
    public StageConfigData CurrentStage { get; private set; }

    private void Awake()
    {
        if (grid == null || stageConfigCsv == null)
        {
            Debug.LogError("BattleStageLoader: GridまたはStageConfig.csvが未設定です。", this);
            return;
        }
        try
        {
            int matches = 0;
            foreach (CsvRow row in CsvReader.Read(stageConfigCsv, "StageID"))
            {
                if (row.Get("StageID") != stageId) continue;
                matches++;
                CurrentStage = StageConfigData.FromCsv(row);
            }
            if (matches != 1)
                throw new FormatException($"StageID={stageId} の行数が{matches}件です。1件だけ必要です。");

            grid.Initialize(CurrentStage.Width, CurrentStage.Height);
            initialized = true;
            Debug.Log($"ステージ初期化: {stageId}, {CurrentStage.Width}×{CurrentStage.Height}, " +
                $"地形Prefab名={CurrentStage.FieldName}(読み込みは未実装)", this);
        }
        catch (Exception ex)
        {
            Debug.LogError($"StageConfig読込エラー: {ex.Message}", this);
        }
    }

    private void Start()
    {
        if (!initialized) return;
        if (stageCharaLayoutCsv == null || spawner == null)
        {
            Debug.LogError("BattleStageLoader: Stage_CharaLayout.csvかSpawnerが未設定です。", this);
            return;
        }
        spawner.SpawnStage(stageId, stageCharaLayoutCsv);
    }
}
