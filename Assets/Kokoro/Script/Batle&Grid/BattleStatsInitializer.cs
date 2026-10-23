
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Character.csvとClass.csvから
/// 各ユニットの戦闘ステータスを初期化する。
///
/// ・CharacterDataから基礎ステータス取得
/// ・ClassDataから朝夜クラス取得
/// ・現在の朝夜で倍率計算
/// ・UnitStateへ登録
/// ・クラスの移動力も登録
///
/// すでに初期化したHPは再初期化しない。
/// </summary>
public sealed class BattleStatsInitializer : MonoBehaviour
{
    [Header("CSVデータ参照")]
    [SerializeField]
    private GameDataRepository repository;

    [Header("昼夜管理")]
    [SerializeField]
    private BattleTimeOfDayController timeOfDayController;

    [Header("昼夜管理が未設定の場合の代替値")]
    [SerializeField]
    private BattleClassMode initialClassMode =
        BattleClassMode.Light;

    [Header("デバッグ")]
    [SerializeField]
    private bool showUnitLogs = true;

    // ====================================
    // 開始処理
    // ====================================

    private IEnumerator Start()
    {
        // BattleStageLoaderがStartでユニットを
        // 自動生成するのを待つ。
        yield return null;

        InitializeExistingUnits();
    }

    // ====================================
    // 現在の朝夜
    // ====================================

    private BattleClassMode GetCurrentMode()
    {
        if (timeOfDayController != null)
        {
            return timeOfDayController.CurrentMode;
        }

        return initialClassMode;
    }

    // ====================================
    // 全ユニット初期化
    // ====================================

    /// <summary>
    /// ステージ内の全ユニットに
    /// 戦闘ステータスを登録する。
    /// </summary>
    public void InitializeExistingUnits()
    {
        if (repository == null ||
            !repository.IsLoaded)
        {
            Debug.LogError(
                "BattleStatsInitializer: " +
                "CSVが正常に読み込まれていません。",
                this
            );
            return;
        }

        UnitView[] foundUnits;

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

            if (unit.State == null)
            {
                Debug.LogWarning(
                    $"{unit.name}: UnitStateが未初期化です。",
                    unit
                );
                errorCount++;
                continue;
            }

            // 初期化済みのHPを再設定しない
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
    }

    // ====================================
    // 1体分の初期化
    // ====================================

    /// <summary>
    /// 指定ユニットの戦闘ステータスを生成。
    ///
    /// 将来の増援生成時にも使用できる。
    /// </summary>
    public bool InitializeUnit(UnitView unit)
    {
        if (unit == null || unit.State == null)
        {
            Debug.LogError(
                "BattleStatsInitializer: " +
                "ユニットが初期化されていません。",
                this
            );
            return false;
        }

        if (repository == null ||
            !repository.IsLoaded)
        {
            Debug.LogError(
                "BattleStatsInitializer: " +
                "GameDataRepositoryを確認してください。",
                this
            );
            return false;
        }

        // 登録済みならHPなどを初期化しない
        if (unit.State.CombatStats != null)
        {
            return true;
        }

        CharacterData character = unit.MasterData;

        if (character == null)
        {
            Debug.LogError(
                $"{unit.name}: CharacterDataがありません。",
                unit
            );
            return false;
        }

        // 朝のクラスを取得
        if (!repository.TryGetClass(
            character.LightClassId,
            out ClassData lightClass))
        {
            Debug.LogError(
                $"{unit.name}: 朝クラス" +
                $" {character.LightClassId} が存在しません。",
                unit
            );
            return false;
        }

        // 夜のクラスを取得
        if (!repository.TryGetClass(
            character.DarkClassId,
            out ClassData darkClass))
        {
            Debug.LogError(
                $"{unit.name}: 夜クラス" +
                $" {character.DarkClassId} が存在しません。",
                unit
            );
            return false;
        }

        try
        {
            // 現在の朝夜を取得
            BattleClassMode mode = GetCurrentMode();

            // CSVから最終能力を計算
            UnitBattleStats stats =
                new UnitBattleStats(
                    character,
                    lightClass,
                    darkClass,
                    mode
                );

            // UnitStateへ登録。
            // 登録時にMoveRangeも正式CSVの値になる。
            if (!unit.State.TrySetCombatStats(stats))
            {
                Debug.LogError(
                    $"{unit.name}: ステータス登録失敗。",
                    unit
                );
                return false;
            }

            if (showUnitLogs)
            {
                string modeName =
                    mode == BattleClassMode.Light
                        ? "朝"
                        : "夜";

                Debug.Log(
                    "【ステータス生成】" +
                    $"\n名前: {stats.CharacterName}" +
                    $"\nID: {stats.CharacterId}" +
                    $"\nSpawnID: {unit.SpawnId}" +
                    $"\n時間帯: {modeName}" +
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
                    $"\n移動力: {unit.State.MoveRange}" +
                    $"\n攻撃射程: " +
                    $"{stats.MinAttackRange}～" +
                    $"{stats.MaxAttackRange}" +
                    $"\nRangeType: {stats.RangeType}",
                    unit
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"{unit.name}: ステータス生成エラー：" +
                ex.Message,
                unit
            );
            return false;
        }
    }
}
