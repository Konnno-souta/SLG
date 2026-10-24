
using System;
using System.Collections.Generic;

/// <summary>
/// 戦闘状態のセーブデータ。
///
/// JsonUtilityでJSON形式に変換するため、
/// Serializable属性とpublicフィールドを使用する。
///
/// このデータはCharacter.csvやClass.csvの
/// マスターデータそのものは保存しない。
///
/// 保存するのは戦闘中に変化する情報と
/// 元のCSVデータを参照するためのID。
/// </summary>
[Serializable]
public sealed class BattleSaveData
{
    // セーブデータの形式が将来変わった際の
    // 互換性確認に使用する。
    public int version = 1;

    // 保存日時（UTC、ISO 8601形式）
    public string savedAtUtc;

    // ====================================
    // ステージ情報
    // ====================================

    public string stageId;

    public int gridWidth;
    public int gridHeight;

    // ====================================
    // ターン情報
    // ====================================

    public int turnNumber;

    // "Player" または "Enemy"
    public string battlePhase;

    // "Light" または "Dark"
    public string classMode;

    // ====================================
    // 全ユニット
    // ====================================

    public List<BattleSavedUnit> units =
        new List<BattleSavedUnit>();
}

/// <summary>
/// 戦闘中のユニット1体分の保存情報。
///
/// X/Y座標はUnity内部の0始まりで記録する。
/// CSVの1始まり座標とは区別すること。
/// </summary>
[Serializable]
public sealed class BattleSavedUnit
{
    // ====================================
    // ユニットの識別
    // ====================================

    // 現在のBattleUnitSpawnerが生成したID。
    // 今後、安定した個体IDへの移行が必要。
    public string spawnId;

    // Character.csvのCharacter_ID
    public string characterId;

    // "Ally" または "Enemy"
    public string team;

    // ====================================
    // 位置・移動力
    // ====================================

    public int x;
    public int y;

    public int moveRange;

    // ====================================
    // HP
    // ====================================

    public int currentHP;
    public int maxHP;

    // ====================================
    // 行動状態
    // ====================================

    public bool hasMoved;
    public bool hasActed;

    // ====================================
    // 現在のクラス
    // ====================================

    public string currentClassId;
}
