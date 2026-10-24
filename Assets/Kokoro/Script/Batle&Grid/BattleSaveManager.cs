
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第15段階：戦闘JSONセーブ管理。
///
/// 現段階の機能：
/// ・現在の戦闘状態を収集
/// ・JSONファイルに保存
/// ・保存ファイルを読み戻す
/// ・読み込んだデータの整合性を確認
///
/// 未実装：
/// ・読み込んだ情報の戦闘シーンへの反映
/// ・タイトル画面のセーブスロットUI連携
/// ・ロード時のユニット再生成
///
/// 保存先にはApplication.persistentDataPathを使用。
/// </summary>
public sealed class BattleSaveManager : MonoBehaviour
{
    [Header("ステージ参照")]
    [SerializeField]
    private BattleStageLoader stageLoader;

    [SerializeField]
    private BattleGrid grid;

    [Header("ターン管理")]
    [SerializeField]
    private TurnManager turnManager;

    [Header("昼夜管理")]
    [SerializeField]
    private BattleTimeOfDayController timeOfDayController;

    [Header("セーブスロット")]
    [SerializeField, Min(1)]
    private int slotCount = 3;

    [Header("開発用キー")]
    [SerializeField]
    private bool enableDebugKeys = true;

    [Header("ログ")]
    [SerializeField]
    private bool showLogs = true;

    private const int SaveVersion = 1;

    // ========================================
    // ファイル保存先
    // ========================================

    public string SaveDirectory =>
        Path.Combine(
            Application.persistentDataPath,
            "BattleSaves"
        );

    /// <summary>
    /// セーブファイルの保存先。
    /// 例：BattleSaves/battle_slot_1.json
    /// </summary>
    public string GetSavePath(int slotNumber)
    {
        return Path.Combine(
            SaveDirectory,
            $"battle_slot_{slotNumber}.json"
        );
    }

    // ========================================
    // 開発用入力
    // ========================================

    private void Update()
    {
        if (!enableDebugKeys)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        // F5でスロット1に保存
        if (keyboard.f5Key.wasPressedThisFrame)
        {
            SaveSlot(1);
        }

        // F9でスロット1を読み込み、
        // ファイル内容を検証する。
        if (keyboard.f9Key.wasPressedThisFrame)
        {
            ReadSlot(1);
        }
    }

    // ========================================
    // セーブ処理
    // ========================================

    /// <summary>
    /// 指定したスロットへ保存する。
    ///
    /// 最終的にはタイトル・ゲーム内UIから
    /// この関数を呼び出す予定。
    /// </summary>
    public bool SaveSlot(int slotNumber)
    {
        if (!IsValidSlot(slotNumber))
        {
            return false;
        }

        if (!TryCreateSaveData(
            out BattleSaveData saveData))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(
                SaveDirectory
            );

            string json =
                JsonUtility.ToJson(
                    saveData,
                    true
                );

            string path =
                GetSavePath(slotNumber);

            File.WriteAllText(
                path,
                json,
                new UTF8Encoding(false)
            );

            Debug.Log(
                "【戦闘セーブ成功】" +
                $"\nスロット：{slotNumber}" +
                $"\nステージ：{saveData.stageId}" +
                $"\nターン：{saveData.turnNumber}" +
                $"\nユニット：{saveData.units.Count}体" +
                $"\n保存先：{path}",
                this
            );

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "【戦闘セーブ失敗】" +
                $"\n{ex.Message}",
                this
            );

            return false;
        }
    }

    // ========================================
    // 保存データの収集
    // ========================================

    private bool TryCreateSaveData(
        out BattleSaveData saveData)
    {
        saveData = null;

        if (stageLoader == null ||
            grid == null ||
            turnManager == null ||
            timeOfDayController == null)
        {
            Debug.LogError(
                "BattleSaveManager: " +
                "Inspector参照が不足しています。",
                this
            );

            return false;
        }

        StageConfigData stage =
            stageLoader.CurrentStage;

        if (stage == null)
        {
            Debug.LogError(
                "BattleSaveManager: " +
                "ステージが初期化されていません。",
                this
            );

            return false;
        }

        // 現段階では敵フェーズ中に
        // 保存することを禁止する。
        if (turnManager.CurrentPhase !=
            BattlePhase.Player)
        {
            Debug.LogWarning(
                "セーブはPLAYER PHASE中に" +
                "実行してください。",
                this
            );

            return false;
        }

        // 現在のステージ・ターン情報
        BattleSaveData result =
            new BattleSaveData
            {
                version = SaveVersion,

                savedAtUtc =
                    DateTime.UtcNow.ToString("o"),

                stageId = stage.StageId,

                gridWidth = grid.Width,
                gridHeight = grid.Height,

                turnNumber =
                    turnManager.TurnNumber,

                battlePhase =
                    turnManager.CurrentPhase.ToString(),

                classMode =
                    timeOfDayController.CurrentMode
                        .ToString()
            };

        UnitView[] units = FindCurrentUnits();

        if (units.Length == 0)
        {
            Debug.LogError(
                "保存対象のユニットがありません。",
                this
            );

            return false;
        }

        HashSet<string> usedSpawnIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        // ====================================
        // ユニットを1体ずつ収集
        // ====================================

        foreach (UnitView unit in units)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            UnitState state =
                unit.State;

            CharacterData character =
                unit.MasterData;

            if (state == null ||
                character == null ||
                state.CombatStats == null)
            {
                Debug.LogError(
                    $"{unit.name}: " +
                    "ステータスが未初期化のため、" +
                    "セーブを中止しました。",
                    unit
                );

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                unit.SpawnId))
            {
                Debug.LogError(
                    $"{unit.name}: " +
                    "SpawnIdがありません。",
                    unit
                );

                return false;
            }

            if (!usedSpawnIds.Add(unit.SpawnId))
            {
                Debug.LogError(
                    $"SpawnIdが重複しています：" +
                    unit.SpawnId,
                    unit
                );

                return false;
            }

            // =================================
            // セーブできるタイミングか確認
            // =================================

            // 移動したが行動を確定していない場合は、
            // コマンド選択中の可能性があるので
            // セーブしない。
            if (state.HasMoved &&
                !state.HasActed)
            {
                Debug.LogWarning(
                    $"{unit.name}: 移動後の行動が" +
                    "未確定のため保存できません。",
                    unit
                );

                return false;
            }

            // アニメーション途中でState.Positionと
            // 3Dモデルの位置がずれている場合も禁止。
            Vector3 expectedWorldPosition =
                grid.CellToWorld(state.Position);

            float squaredDistance =
                (unit.transform.position -
                 expectedWorldPosition).sqrMagnitude;

            if (squaredDistance > 0.0004f)
            {
                Debug.LogWarning(
                    $"{unit.name}: 移動中の可能性があるため" +
                    "保存を中止しました。",
                    unit
                );

                return false;
            }

            UnitBattleStats stats =
                state.CombatStats;

            // =================================
            // 1体分の状態を保存
            // =================================

            BattleSavedUnit savedUnit =
                new BattleSavedUnit
                {
                    spawnId =
                        unit.SpawnId,

                    characterId =
                        character.Id,

                    team =
                        state.Team.ToString(),

                    x =
                        state.Position.x,

                    y =
                        state.Position.y,

                    moveRange =
                        state.MoveRange,

                    currentHP =
                        stats.CurrentHP,

                    maxHP =
                        stats.MaxHP,

                    hasMoved =
                        state.HasMoved,

                    hasActed =
                        state.HasActed,

                    currentClassId =
                        stats.CurrentClassId
                };

            result.units.Add(savedUnit);
        }

        if (result.units.Count == 0)
        {
            Debug.LogError(
                "保存可能なユニットが0体です。",
                this
            );

            return false;
        }

        // ファイル内容の順序を安定させる。
        result.units.Sort(
            (a, b) =>
                string.CompareOrdinal(
                    a.spawnId,
                    b.spawnId
                )
        );

        saveData = result;

        return true;
    }

    // ========================================
    // ロードファイルの読み込み
    // ========================================

    /// <summary>
    /// 指定スロットを読み込んで検証する。
    ///
    /// 注意：
    /// 今回はファイルを読むだけ。
    /// 戦闘中のユニット配置やHPには
    /// 一切反映しない。
    /// </summary>
    public bool ReadSlot(int slotNumber)
    {
        return TryReadSlot(
            slotNumber,
            out _
        );
    }

    /// <summary>
    /// 保存データをほかのシステムから
    /// 利用できる形で返す。
    ///
    /// 次のロード機能実装でも再利用する。
    /// </summary>
    public bool TryReadSlot(
        int slotNumber,
        out BattleSaveData saveData)
    {
        saveData = null;

        if (!IsValidSlot(slotNumber))
        {
            return false;
        }

        string path =
            GetSavePath(slotNumber);

        if (!File.Exists(path))
        {
            Debug.LogWarning(
                $"スロット{slotNumber}には" +
                "セーブデータがありません。",
                this
            );

            return false;
        }

        try
        {
            string json =
                File.ReadAllText(
                    path,
                    Encoding.UTF8
                );

            BattleSaveData loaded =
                JsonUtility.FromJson<BattleSaveData>(
                    json
                );

            if (!ValidateSaveData(loaded))
            {
                return false;
            }

            saveData = loaded;

            Debug.Log(
                "【セーブデータ読み込み成功】" +
                $"\nスロット：{slotNumber}" +
                $"\nステージ：{loaded.stageId}" +
                $"\nターン：{loaded.turnNumber}" +
                $"\nフェーズ：{loaded.battlePhase}" +
                $"\n昼夜：{loaded.classMode}" +
                $"\nユニット：{loaded.units.Count}体" +
                "\n※まだ戦闘状態には反映しません。",
                this
            );

            if (showLogs)
            {
                foreach (BattleSavedUnit unit
                    in loaded.units)
                {
                    Debug.Log(
                        $"【保存ユニット】" +
                        $"{unit.characterId}" +
                        $" / {unit.spawnId}" +
                        $" / {unit.team}" +
                        $"\n位置：({unit.x},{unit.y})" +
                        $"\nHP：{unit.currentHP}" +
                        $"/{unit.maxHP}" +
                        $"\nクラス：{unit.currentClassId}" +
                        $"\n移動済み：{unit.hasMoved}" +
                        $"\n行動済み：{unit.hasActed}",
                        this
                    );
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "【セーブデータ読み込み失敗】" +
                $"\n{ex.Message}",
                this
            );

            return false;
        }
    }

    // ========================================
    // 保存内容の検証
    // ========================================

    private bool ValidateSaveData(
        BattleSaveData data)
    {
        if (data == null)
        {
            Debug.LogError(
                "セーブデータが空です。",
                this
            );

            return false;
        }

        if (data.version != SaveVersion)
        {
            Debug.LogError(
                $"未対応のセーブ形式です：" +
                data.version,
                this
            );

            return false;
        }

        if (string.IsNullOrWhiteSpace(
            data.stageId) ||
            data.turnNumber < 1 ||
            data.gridWidth < 1 ||
            data.gridHeight < 1)
        {
            Debug.LogError(
                "ステージ・ターン情報が不正です。",
                this
            );

            return false;
        }

        if (data.battlePhase !=
                BattlePhase.Player.ToString() &&
            data.battlePhase !=
                BattlePhase.Enemy.ToString())
        {
            Debug.LogError(
                "フェーズ情報が不正です。",
                this
            );

            return false;
        }

        if (data.classMode !=
                BattleClassMode.Light.ToString() &&
            data.classMode !=
                BattleClassMode.Dark.ToString())
        {
            Debug.LogError(
                "昼夜情報が不正です。",
                this
            );

            return false;
        }

        if (data.units == null ||
            data.units.Count == 0)
        {
            Debug.LogError(
                "保存ユニットがありません。",
                this
            );

            return false;
        }

        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        HashSet<Vector2Int> positions =
            new HashSet<Vector2Int>();

        foreach (BattleSavedUnit unit
            in data.units)
        {
            if (unit == null ||
                string.IsNullOrWhiteSpace(
                    unit.spawnId) ||
                string.IsNullOrWhiteSpace(
                    unit.characterId) ||
                string.IsNullOrWhiteSpace(
                    unit.currentClassId))
            {
                Debug.LogError(
                    "保存ユニットのIDが不正です。",
                    this
                );

                return false;
            }

            if (!ids.Add(unit.spawnId))
            {
                Debug.LogError(
                    "保存ユニットIDが重複：" +
                    unit.spawnId,
                    this
                );

                return false;
            }

            if (unit.x < 0 ||
                unit.x >= data.gridWidth ||
                unit.y < 0 ||
                unit.y >= data.gridHeight)
            {
                Debug.LogError(
                    $"ユニット座標が不正：" +
                    unit.spawnId,
                    this
                );

                return false;
            }

            Vector2Int position =
                new Vector2Int(
                    unit.x,
                    unit.y
                );

            if (!positions.Add(position))
            {
                Debug.LogError(
                    $"ユニット配置が重複：" +
                    position,
                    this
                );

                return false;
            }

            if (unit.maxHP <= 0 ||
                unit.currentHP < 0 ||
                unit.currentHP > unit.maxHP ||
                unit.moveRange < 0)
            {
                Debug.LogError(
                    $"HPまたは移動力が不正：" +
                    unit.spawnId,
                    this
                );

                return false;
            }

            if (unit.team !=
                    UnitTeam.Ally.ToString() &&
                unit.team !=
                    UnitTeam.Enemy.ToString())
            {
                Debug.LogError(
                    $"Teamが不正：" +
                    unit.spawnId,
                    this
                );

                return false;
            }
        }

        return true;
    }

    // ========================================
    // スロット番号の確認
    // ========================================

    private bool IsValidSlot(int slotNumber)
    {
        if (slotNumber >= 1 &&
            slotNumber <= slotCount)
        {
            return true;
        }

        Debug.LogError(
            $"無効なスロット番号：" +
            $"{slotNumber}" +
            $" / 設定スロット数：{slotCount}",
            this
        );

        return false;
    }

    // ========================================
    // シーン内ユニットの取得
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
}
