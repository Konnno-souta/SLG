using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BattleSaveManager : MonoBehaviour
{
    // ========================================
    // Inspector参照
    // ========================================

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
    private BattleTimeOfDayController
        timeOfDayController;

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
    // セーブ先
    // ========================================

    public string SaveDirectory =>
        Path.Combine(
            Application.persistentDataPath,
            "BattleSaves"
        );

    public string GetSavePath(
        int slotNumber)
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

        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        int currentSlot =
            BattleLaunchSession.CurrentSlot;

        // F5：セーブ
        if (keyboard.f5Key.wasPressedThisFrame)
        {
            Debug.Log(
                "【F5保存要求】" +
                $"\nモード：{BattleLaunchSession.Mode}" +
                $"\nDATA：{currentSlot}" +
                $"\n保存先：{GetSavePath(currentSlot)}",
                this
            );

            SaveSlot(currentSlot);
        }

        // F9：セーブ内容確認
        if (keyboard.f9Key.wasPressedThisFrame)
        {
            Debug.Log(
                "【F9保存内容確認】" +
                $"\nDATA：{currentSlot}",
                this
            );

            ReadSlot(currentSlot);
        }
    }

    // ========================================
    // セーブ実行
    // ========================================

    public bool SaveSlot(
        int slotNumber)
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

            BattleLaunchSession.NotifySaveCompleted(
                slotNumber
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
    // セーブデータ作成
    // ========================================

    private bool TryCreateSaveData(
        out BattleSaveData saveData)
    {
        saveData = null;

        // Inspector参照確認
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

        // PLAYER PHASE中のみ保存可能。
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

        // ====================================
        // 基本情報
        // ====================================

        BattleSaveData result =
            new BattleSaveData
            {
                version =
                    SaveVersion,

                savedAtUtc =
                    DateTime.UtcNow.ToString("o"),

                stageId =
                    stage.StageId,

                gridWidth =
                    grid.Width,

                gridHeight =
                    grid.Height,

                turnNumber =
                    turnManager.TurnNumber,

                battlePhase =
                    turnManager.CurrentPhase.ToString(),

                classMode =
                    timeOfDayController.CurrentMode
                        .ToString()
            };

        UnitView[] units =
            FindCurrentUnits();

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
        // 全ユニットの状態取得
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

            // SpawnId確認
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

            // SpawnIdの重複確認
            if (!usedSpawnIds.Add(
                unit.SpawnId))
            {
                Debug.LogError(
                    $"SpawnIdが重複しています：" +
                    unit.SpawnId,
                    unit
                );

                return false;
            }

            // 移動後に行動が確定していない場合。
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

            // 実際のモデル座標と
            // グリッド座標が一致しているか確認。
            Vector3 expectedWorldPosition =
                grid.CellToWorld(
                    state.Position
                );

            float squaredDistance =
                (unit.transform.position -
                 expectedWorldPosition)
                    .sqrMagnitude;

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
            // スキル状態確認
            // =================================

            // 暫定処理：
            // スキル状態の保存機能が完成するまで、
            // 状態が失われたセーブデータの
            // 作成を防止する。
            if (stats.Skills.HasState)
            {
                Debug.LogWarning(
                    "スキル状態の保存には未対応です。" +
                    "セーブを中止します。",
                    this
                );

                return false;
            }

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

                    // スキル補正前の移動力を保存。
                    moveRange =
                        state.BaseMoveRange,

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

            result.units.Add(
                savedUnit
            );
        }

        // 保存対象確認
        if (result.units.Count == 0)
        {
            Debug.LogError(
                "保存可能なユニットが0体です。",
                this
            );

            return false;
        }

        // SpawnId順に並べる。
        result.units.Sort(
            (a, b) => string.CompareOrdinal(
                a.spawnId,
                b.spawnId
            )
        );

        saveData = result;

        return true;
    }

    // ========================================
    // セーブデータ読み込み
    // ========================================

    public bool ReadSlot(
        int slotNumber)
    {
        return TryReadSlot(
            slotNumber,
            out _
        );
    }

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

            // 保存した全ユニットの内容表示
            if (showLogs)
            {
                foreach (
                    BattleSavedUnit unit
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
    // セーブデータの検証
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

        // バージョン確認
        if (data.version != SaveVersion)
        {
            Debug.LogError(
                $"未対応のセーブ形式です：" +
                data.version,
                this
            );

            return false;
        }

        // 基本情報確認
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

        // フェーズ情報確認
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

        // 昼夜情報確認
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

        // ユニット確認
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

        foreach (BattleSavedUnit unit in data.units)
        {
            // ID確認
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

            // ID重複確認
            if (!ids.Add(unit.spawnId))
            {
                Debug.LogError(
                    "保存ユニットIDが重複：" +
                    unit.spawnId,
                    this
                );

                return false;
            }

            // 座標範囲確認
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

            // 生存ユニットのみ座標重複確認。
            if (unit.currentHP > 0)
            {
                if (!positions.Add(position))
                {
                    Debug.LogError(
                        "生存ユニットの配置が重複：" +
                        position,
                        this
                    );

                    return false;
                }
            }

            // HP・移動力確認
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

            // 所属チーム確認
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
    // セーブスロット確認
    // ========================================

    private bool IsValidSlot(
        int slotNumber)
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
    // 全ユニット取得
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