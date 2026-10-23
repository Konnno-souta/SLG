
using System.Collections;
using UnityEngine;

/// <summary>
/// CSVから生成された各ユニットに
/// 戦闘ステータスを設定する。
///
/// GameDataRepositoryに読み込み済みの
/// Character.csvとClass.csvを使用する。
///
/// 既存のBattleUnitSpawnerは変更しない。
/// </summary>
public sealed class BattleStatsInitializer : MonoBehaviour
{
    [Header("CSVデータ参照")]
    [SerializeField]
    private GameDataRepository repository;

    [Header("初期クラス（動作確認用）")]
    [SerializeField]
    private BattleClassMode initialClassMode =
        BattleClassMode.Light;

    [Header("デバッグ")]
    [SerializeField]
    private bool showUnitLogs = true;

    // =====================================
    // 初期化タイミング
    // =====================================

    private IEnumerator Start()
    {
        // BattleStageLoader.Startでの
        // ユニット自動生成が終わるのを待つ。
        //
        // 現在のSpawnerは同期的に生成するため、
        // 1フレーム後には生成が完了している。
        yield return null;

        InitializeExistingUnits();
    }

    // =====================================
    // 全ユニットの初期化
    // =====================================

    /// <summary>
    /// シーン内の生成済みユニットへ
    /// 戦闘ステータスを設定する。
    ///
    /// 将来、ステージ再読み込みや
    /// 増援が発生する場合にも使用可能。
    /// </summary>
    public void InitializeExistingUnits()
    {
        if (repository == null ||
            !repository.IsLoaded)
        {
            Debug.LogError(
                "BattleStatsInitializer: " +
                "GameDataRepositoryが設定されていないか、" +
                "CSVの読み込みに失敗しています。",
                this
            );

            return;
        }

        UnitView[] foundUnits;

        // UnityのバージョンによるAPIの違いに対応
#if UNITY_2022_2_OR_NEWER
        foundUnits = FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        foundUnits = FindObjectsOfType<UnitView>();
#endif

        int successCount = 0;
        int alreadyInitialized = 0;
        int errorCount = 0;

        foreach (UnitView unit in foundUnits)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            // UnitView.Initializeがまだの場合
            if (unit.State == null)
            {
                Debug.LogWarning(
                    $"{unit.name}: UnitStateが未初期化です。",
                    unit
                );

                errorCount++;
                continue;
            }

            // 既にステータス設定済みの場合は
            // 再初期化しない（HPを保持するため）
            if (unit.State.CombatStats != null)
            {
                alreadyInitialized++;
                continue;
            }

            if (InitializeUnit(unit))
            {
                successCount++;
            }
            else
            {
                errorCount++;
            }
        }

        Debug.Log(
            "戦闘ステータス初期化結果: " +
            $"成功{successCount}体 / " +
            $"設定済み{alreadyInitialized}体 / " +
            $"失敗{errorCount}体",
            this
        );

        if (successCount == 0 &&
            alreadyInitialized == 0)
        {
            Debug.LogWarning(
                "戦闘ステータスを設定できたユニットが" +
                "ありません。CSV配置やUnitViewを" +
                "確認してください。",
                this
            );
        }
    }

    // =====================================
    // ユニット1体の初期化
    // =====================================

    /// <summary>
    /// 指定されたユニットに
    /// CSVの戦闘ステータスを適用する。
    ///
    /// 今後、敵の増援を生成したときにも
    /// この関数を呼べる。
    /// </summary>
    public bool InitializeUnit(UnitView unit)
    {
        if (unit == null ||
            unit.State == null)
        {
            Debug.LogError(
                "BattleStatsInitializer: " +
                "UnitViewまたはUnitStateが未設定です。",
                this
            );

            return false;
        }

        if (repository == null ||
            !repository.IsLoaded)
        {
            Debug.LogError(
                "BattleStatsInitializer: " +
                "CSVデータが読み込まれていません。",
                this
            );

            return false;
        }

        // 既に初期化済みならそのまま使う
        if (unit.State.CombatStats != null)
        {
            return true;
        }

        // BattleUnitSpawner.Configureで設定した
        // CharacterDataを取得する。
        CharacterData character = unit.MasterData;

        if (character == null)
        {
            Debug.LogError(
                $"{unit.name}: MasterDataがありません。" +
                "正式CSVから生成したユニットか" +
                "確認してください。",
                unit
            );

            return false;
        }

        // =================================
        // 朝のクラスを取得
        // =================================

        if (!repository.TryGetClass(
            character.LightClassId,
            out ClassData lightClass))
        {
            Debug.LogError(
                $"{unit.name}: 朝クラス " +
                $"{character.LightClassId} が" +
                "Class.csvにありません。",
                unit
            );

            return false;
        }

        // =================================
        // 夜のクラスを取得
        // =================================

        if (!repository.TryGetClass(
            character.DarkClassId,
            out ClassData darkClass))
        {
            Debug.LogError(
                $"{unit.name}: 夜クラス " +
                $"{character.DarkClassId} が" +
                "Class.csvにありません。",
                unit
            );

            return false;
        }

        // =================================
        // 最終ステータスを計算
        // =================================

        UnitBattleStats stats = new UnitBattleStats(
            character,
            lightClass,
            darkClass,
            initialClassMode
        );

        // UnitStateに登録
        if (!unit.State.TrySetCombatStats(stats))
        {
            Debug.LogError(
                $"{unit.name}: 戦闘ステータスの" +
                "登録に失敗しました。",
                unit
            );

            return false;
        }

        // =================================
        // デバッグ表示
        // =================================

        if (showUnitLogs)
        {
            Debug.Log(
                "【ステータス生成】" +
                $"\nキャラ: {stats.CharacterName}" +
                $"\nCharacterID: {stats.CharacterId}" +
                $"\nSpawnID: {unit.SpawnId}" +
                $"\nクラス: {stats.CurrentClassName}" +
                $" ({stats.CurrentClassId})" +
                $"\nHP: {stats.CurrentHP}/{stats.MaxHP}" +
                $"\nATK: {stats.Attack:0.##}" +
                $"\nMAGIC: {stats.Magic:0.##}" +
                $"\nATK_DEF: {stats.AttackDefense:0.##}" +
                $"\nMAGIC_DEF: {stats.MagicDefense:0.##}" +
                $"\nSPD: {stats.Speed:0.##}" +
                $"\nHIT: {stats.Hit:0.##}" +
                $"\nEVASION: {stats.Evasion:0.##}" +
                $"\n攻撃射程: " +
                $"{stats.MinAttackRange}～" +
                $"{stats.MaxAttackRange}" +
                $"\nRangeType: {stats.RangeType}",
                unit
            );
        }

        return true;
    }
}
