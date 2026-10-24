
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第16段階：戦闘セーブデータの復元。
///
/// ・既存BattleSaveManagerからJSONを取得
/// ・ステージとユニットIDを照合
/// ・全員分のデータを先に検証
/// ・昼夜、クラス、HP、座標、行動状態を復元
/// ・ターン番号を復元
/// ・敵危険範囲の再計算を要求
///
/// 対応範囲：
/// ・現在ロード済みのステージのみ
/// ・PLAYER PHASEのみ
/// ・ユニット未選択時のみ
///
/// 別ステージへの遷移と再生成は未実装。
/// </summary>
public sealed class BattleLoadRestorer : MonoBehaviour
{
    // ========================================
    // Inspector参照
    // ========================================

    [Header("セーブ管理")]
    [SerializeField]
    private BattleSaveManager saveManager;

    [Header("ステージ")]
    [SerializeField]
    private BattleStageLoader stageLoader;

    [SerializeField]
    private BattleGrid grid;

    [Header("ターン・昼夜")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private BattleTimeOfDayController timeOfDayController;

    [Header("味方操作")]
    [SerializeField]
    private UnitSelectionController selectionController;

    [Header("敵危険範囲")]
    [SerializeField]
    private EnemyThreatRangeController threatController;

    [Header("ターン表示（任意）")]
    [SerializeField]
    private TMP_Text phaseText;

    [Header("開発用キー")]
    [SerializeField]
    private bool enableDebugLoadKey = true;

    [SerializeField, Min(1)]
    private int debugSlotNumber = 1;

    [Header("デバッグログ")]
    [SerializeField]
    private bool showLogs = true;

    // ========================================
    // 復元対象1体分
    // ========================================

    private sealed class RestorePair
    {
        public UnitView Unit;
        public BattleSavedUnit Saved;
    }

    // ========================================
    // 開発用F10
    // ========================================

    private void Update()
    {
        if (!enableDebugLoadKey)
        {
            return;
        }

        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f10Key.wasPressedThisFrame)
        {
            RestoreSlot(debugSlotNumber);
        }
    }

    // ========================================
    // 指定スロットを復元
    // ========================================

    /// <summary>
    /// 指定したセーブスロットから
    /// 現在の戦闘状態を復元する。
    ///
    /// 将来、タイトル画面や
    /// ポーズメニューからも呼び出せる。
    /// </summary>
    public bool RestoreSlot(int slotNumber)
    {
        // ====================================
        // 1. 必須参照の確認
        // ====================================

        if (saveManager == null ||
            stageLoader == null ||
            grid == null ||
            turnManager == null ||
            timeOfDayController == null ||
            selectionController == null)
        {
            Debug.LogError(
                "BattleLoadRestorer: " +
                "Inspectorの必須参照が不足しています。",
                this
            );

            return false;
        }

        // ====================================
        // 2. ロード可能な操作状態か
        // ====================================

        if (turnManager.CurrentPhase !=
            BattlePhase.Player)
        {
            Debug.LogWarning(
                "ロードはPLAYER PHASE中のみ可能です。",
                this
            );

            return false;
        }

        if (!selectionController.CanRestoreBattleState)
        {
            Debug.LogWarning(
                "ロードする前にユニットの選択を" +
                "解除してください。",
                this
            );

            return false;
        }

        // ====================================
        // 3. JSONを読み込む
        // ====================================

        // 第15段階の既存メソッドを再利用。
        // JSONのバージョン・値・重複なども
        // ここで検証される。
        if (!saveManager.TryReadSlot(
            slotNumber,
            out BattleSaveData data))
        {
            Debug.LogWarning(
                "ロードするセーブデータを" +
                "読み込めませんでした。",
                this
            );

            return false;
        }

        // ====================================
        // 4. 現在の戦闘と照合
        // ====================================

        if (!TryBuildRestorePlan(
            data,
            out List<RestorePair> pairs,
            out BattleClassMode savedMode))
        {
            // 検証失敗時は現在の戦闘状態を
            // 一切変更しない。
            return false;
        }

        // ====================================
        // 5. 昼夜の復元
        // ====================================

        // 現在の時間帯と違うなら
        // BattleTimeOfDayControllerが
        // 全ユニットのクラスを切り替える。
        timeOfDayController.SetMode(savedMode);

        // ====================================
        // 6. ユニット全員を復元
        // ====================================

        foreach (RestorePair pair in pairs)
        {
            UnitView unit = pair.Unit;

            BattleSavedUnit saved =
                pair.Saved;

            UnitState state =
                unit.State;

            UnitBattleStats stats =
                state.CombatStats;

            // クラスを再計算。
            // SetClassModeは現在HPを変更しない。
            stats.SetClassMode(savedMode);

            // 現在HPをJSONの値に戻す。
            if (!stats.TryRestoreCurrentHP(
                saved.currentHP))
            {
                Debug.LogError(
                    $"{unit.name}: HP復元に失敗しました。",
                    unit
                );

                return false;
            }

            // 保存座標はUnity内部の0始まり。
            Vector2Int savedPosition =
                new Vector2Int(
                    saved.x,
                    saved.y
                );

            // 位置・移動力・行動状態を復元。
            if (!state.RestoreFromSave(
                savedPosition,
                saved.moveRange,
                saved.hasMoved,
                saved.hasActed))
            {
                Debug.LogError(
                    $"{unit.name}: 状態復元に失敗しました。",
                    unit
                );

                return false;
            }

            // State.Positionを変更しただけでは
            // GameObjectの座標が変わらない。
            //
            // 第5段階から使っている
            // UnitView.SyncPositionで同期する。
            unit.SyncPosition(grid);

            if (showLogs)
            {
                Debug.Log(
                    "【ユニット復元】" +
                    $"\n名前：{stats.CharacterName}" +
                    $"\nSpawnId：{unit.SpawnId}" +
                    $"\n座標：({saved.x + 1}," +
                    $"{saved.y + 1})" +
                    $"\nHP：{stats.CurrentHP}" +
                    $"/{stats.MaxHP}" +
                    $"\nクラス：{stats.CurrentClassId}" +
                    $"\n移動力：{state.MoveRange}" +
                    $"\n移動済み：{state.HasMoved}" +
                    $"\n行動済み：{state.HasActed}",
                    unit
                );
            }
        }

        // ====================================
        // 7. ターン番号の復元
        // ====================================

        // 行動済み状態をリセットしない
        // 専用のTurnManagerメソッドを使用。
        if (!turnManager.TryRestorePlayerTurnForLoad(
            data.turnNumber))
        {
            Debug.LogError(
                "ターン番号の復元に失敗しました。",
                this
            );

            return false;
        }

        // PhaseChangedを受けた昼夜管理が
        // 正しい時間帯に切り替わったか確認。
        //
        // セーブ内容とターン設定が合わない場合は
        // 警告を出し、保存値を優先する。
        if (timeOfDayController.CurrentMode !=
            savedMode)
        {
            Debug.LogWarning(
                "保存時の昼夜と、現在のターン設定が" +
                "一致していません。" +
                "セーブされた時間帯を適用します。",
                this
            );

            timeOfDayController.SetMode(
                savedMode
            );
        }

        // ====================================
        // 8. 表示の更新
        // ====================================

        // 既存のフェーズUIを使っている場合、
        // 表示するターン番号も揃える。
        if (phaseText != null)
        {
            phaseText.text =
                $"TURN {data.turnNumber}\n" +
                "PLAYER PHASE";
        }

        // 敵の位置・クラス変更を反映。
        if (threatController != null)
        {
            threatController.RefreshUnits();
        }

        // ====================================
        // 9. 復元完了
        // ====================================

        Debug.Log(
            "【戦闘ロード成功】" +
            $"\nスロット：{slotNumber}" +
            $"\nステージ：{data.stageId}" +
            $"\nターン：{data.turnNumber}" +
            $"\n昼夜：{savedMode}" +
            $"\nユニット：{pairs.Count}体" +
            "\n戦闘状態を復元しました。",
            this
        );

        return true;
    }

    // ========================================
    // 復元前のデータ検証
    // ========================================

    /// <summary>
    /// JSON内のユニットと、
    /// 現在シーンに存在するユニットを照合。
    ///
    /// 全体に問題がない場合だけ、
    /// 復元計画を返す。
    /// </summary>
    private bool TryBuildRestorePlan(
        BattleSaveData data,
        out List<RestorePair> pairs,
        out BattleClassMode savedMode)
    {
        pairs = new List<RestorePair>();

        savedMode = BattleClassMode.Light;

        if (data == null ||
            stageLoader.CurrentStage == null)
        {
            return LoadError(
                "ステージまたは保存データがありません。"
            );
        }

        // ====================================
        // 同じステージか確認
        // ====================================

        string currentStageId =
            stageLoader.CurrentStage.StageId;

        if (data.stageId != currentStageId)
        {
            return LoadError(
                $"現在のステージは{currentStageId}、" +
                $"保存データは{data.stageId}です。" +
                "別ステージへのロードは未対応です。"
            );
        }

        if (data.gridWidth != grid.Width ||
            data.gridHeight != grid.Height)
        {
            return LoadError(
                "保存時と現在のグリッドサイズが違います。"
            );
        }

        // ====================================
        // PLAYER PHASEの保存だけ対応
        // ====================================

        if (data.battlePhase !=
            BattlePhase.Player.ToString())
        {
            return LoadError(
                "ENEMY PHASEの保存データには" +
                "まだ対応していません。"
            );
        }

        if (data.turnNumber < 1)
        {
            return LoadError(
                "ターン番号が不正です。"
            );
        }

        if (!Enum.TryParse(
            data.classMode,
            out savedMode))
        {
            return LoadError(
                "昼夜の保存値が不正です。"
            );
        }

        // ====================================
        // 現在のユニットをIDで検索可能にする
        // ====================================

        UnitView[] currentUnits =
            FindCurrentUnits();

        Dictionary<string, UnitView> unitMap =
            new Dictionary<string, UnitView>(
                StringComparer.Ordinal
            );

        foreach (UnitView unit in currentUnits)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (unit.State == null ||
                unit.MasterData == null ||
                unit.State.CombatStats == null ||
                string.IsNullOrWhiteSpace(unit.SpawnId))
            {
                return LoadError(
                    "現在のユニットに未初期化データがあります。"
                );
            }

            if (unitMap.ContainsKey(unit.SpawnId))
            {
                return LoadError(
                    "現在のユニットのSpawnIdが重複：" +
                    unit.SpawnId
                );
            }

            unitMap.Add(
                unit.SpawnId,
                unit
            );
        }

        // ====================================
        // 保存ユニット数を確認
        // ====================================

        if (data.units == null ||
            data.units.Count != unitMap.Count)
        {
            return LoadError(
                "保存時と現在のユニット数が違います。" +
                "配置CSVの変更を確認してください。"
            );
        }

        HashSet<string> usedIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        HashSet<Vector2Int> usedPositions =
            new HashSet<Vector2Int>();

        // ====================================
        // 保存ユニットを1体ずつ照合
        // ====================================

        foreach (BattleSavedUnit saved
            in data.units)
        {
            if (saved == null ||
                string.IsNullOrWhiteSpace(saved.spawnId))
            {
                return LoadError(
                    "保存ユニットのIDが不正です。"
                );
            }

            if (!usedIds.Add(saved.spawnId))
            {
                return LoadError(
                    "保存SpawnIdの重複：" +
                    saved.spawnId
                );
            }

            if (!unitMap.TryGetValue(
                saved.spawnId,
                out UnitView unit))
            {
                return LoadError(
                    "保存時のユニットが見つかりません：" +
                    saved.spawnId
                );
            }

            UnitState state =
                unit.State;

            UnitBattleStats stats =
                state.CombatStats;

            // キャラクターの種類とチームを照合。
            if (unit.MasterData.Id !=
                    saved.characterId ||
                state.Team.ToString() !=
                    saved.team)
            {
                return LoadError(
                    $"{saved.spawnId}: " +
                    "キャラクターIDまたはTeamが違います。"
                );
            }

            // =================================
            // 使用予定クラスを照合
            // =================================

            ClassData expectedClass =
                savedMode == BattleClassMode.Light
                    ? stats.LightClass
                    : stats.DarkClass;

            if (expectedClass == null ||
                expectedClass.Id !=
                    saved.currentClassId)
            {
                return LoadError(
                    $"{saved.spawnId}: クラスIDが" +
                    "現在のCSVと一致しません。"
                );
            }

            // =================================
            // HPと移動力を照合
            // =================================

            if (saved.maxHP != stats.MaxHP ||
                saved.currentHP < 0 ||
                saved.currentHP > stats.MaxHP)
            {
                return LoadError(
                    $"{saved.spawnId}: HPが不正、" +
                    "またはCharacter.csvが更新されています。"
                );
            }

            if (saved.moveRange !=
                expectedClass.MaxMovementRange)
            {
                return LoadError(
                    $"{saved.spawnId}: 移動力が" +
                    "現在のClass.csvと一致しません。"
                );
            }

            // 第15段階のセーブ仕様では
            // 移動後・行動未確定の保存を禁止。
            if (saved.hasMoved &&
                !saved.hasActed)
            {
                return LoadError(
                    $"{saved.spawnId}: " +
                    "移動後の行動が未確定です。"
                );
            }

            // =================================
            // 座標と障害物の確認
            // =================================

            Vector2Int position =
                new Vector2Int(
                    saved.x,
                    saved.y
                );

            if (!grid.IsWalkable(position))
            {
                return LoadError(
                    $"{saved.spawnId}: 保存座標が" +
                    "グリッド外または通行不可です。"
                );
            }

            if (!usedPositions.Add(position))
            {
                return LoadError(
                    "保存座標が重複しています：" +
                    position
                );
            }

            // ここまで正常なら復元対象に追加。
            pairs.Add(
                new RestorePair
                {
                    Unit = unit,
                    Saved = saved
                }
            );
        }

        return true;
    }

    // ========================================
    // シーン内のユニット取得
    // ========================================

    private UnitView[] FindCurrentUnits()
    {
#if UNITY_2022_2_OR_NEWER
        return FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        return FindObjectsOfType<UnitView>();
#endif
    }

    // ========================================
    // エラーメッセージ
    // ========================================

    private bool LoadError(string message)
    {
        Debug.LogError(
            "【戦闘ロード中止】" +
            "\n" + message +
            "\n現在の戦闘状態は変更していません。",
            this
        );

        return false;
    }
}
