
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

/// <summary>
/// 戦闘中のフェーズ。
/// </summary>
public enum BattlePhase
{
    None,
    Player,
    Enemy
}

/// <summary>
/// 戦闘のターン・フェーズを管理する。
///
/// PLAYER PHASE
///     ↓
/// ENEMY PHASE
///     ↓
/// 次のPLAYER PHASE
///
/// 敵AIは未実装のため、現在は仮の待機処理。
/// </summary>
public sealed class TurnManager : MonoBehaviour
{
    [Header("システム参照")]
    [SerializeField]
    private GridClickInput gridInput;

    [SerializeField]
    private UnitSelectionController selectionController;

    [Header("フェーズ表示（任意）")]
    [SerializeField]
    private TMP_Text phaseText;

    [Header("敵フェーズのテスト設定")]
    [SerializeField, Min(0f)]
    private float enemyActionDelay = 0.3f;

    [Header("敵AI")]
    [SerializeField]
    private EnemyAiMovementController enemyAiMovementController;

    // 現在のフェーズ
    public BattlePhase CurrentPhase { get; private set; }
        = BattlePhase.None;

    // 現在のターン数
    public int TurnNumber { get; private set; } = 1;

    // フェーズ切り替え中か
    private bool isChangingPhase;

    // 戦闘参加ユニット一覧
    private readonly List<UnitView> battleUnits = new();

    /// <summary>
    /// 将来、昼夜システムやUIが
    /// フェーズ変更を受け取るためのイベント。
    /// </summary>
    public event Action<BattlePhase, int> PhaseChanged;

    /// <summary>
    /// 全システムのStartが終了した後、
    /// 登録済みユニットを取得する。
    /// </summary>
    private IEnumerator Start()
    {
        if (gridInput == null ||
            selectionController == null)
        {
            Debug.LogError(
                "TurnManagerの参照が不足しています。",
                this
            );

            enabled = false;
            yield break;
        }

        // StageLoaderによるCSV配置や、
        // UnitSelectionControllerの初期化を待つ。
        yield return null;

        RefreshUnits();

        int allyCount = CountUnits(UnitTeam.Ally);
        int enemyCount = CountUnits(UnitTeam.Enemy);

        if (allyCount == 0)
        {
            Debug.LogError(
                "TurnManager: 味方ユニットがいません。" +
                "キャラクターの配置を確認してください。",
                this
            );

            yield break;
        }

        Debug.Log(
            $"ターン管理開始 / 味方:{allyCount}体 " +
            $"敵:{enemyCount}体",
            this
        );

        TurnNumber = 1;

        BeginPlayerPhase();
    }

    private void Update()
    {
        if (CurrentPhase == BattlePhase.None)
            return;

        // 行動終了に応じてユニットの見た目を更新
        UpdateUnitVisuals();

        // 味方フェーズ以外では何もしない
        if (CurrentPhase != BattlePhase.Player)
            return;

        if (isChangingPhase)
            return;

        // 味方全員が行動を終えたら敵フェーズへ
        if (AreAllAlliesFinished())
        {
            StartCoroutine(EnemyPhaseRoutine());
        }
    }

    // =====================================
    // ユニット管理
    // =====================================

    /// <summary>
    /// シーン内の初期化済みユニットを取得。
    ///
    /// CSV自動生成・手動配置の両方に対応する。
    /// 将来、増援が発生した場合にも再取得できる。
    /// </summary>
    public void RefreshUnits()
    {
        battleUnits.Clear();

        UnitView[] foundUnits;

#if UNITY_2022_2_OR_NEWER
        foundUnits = FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        foundUnits = FindObjectsOfType<UnitView>();
#endif

        foreach (UnitView unit in foundUnits)
        {
            if (unit == null ||
                unit.State == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            battleUnits.Add(unit);
        }

        UpdateUnitVisuals();
    }

    /// <summary>
    /// 指定チームのユニット数を取得する。
    /// </summary>
    private int CountUnits(UnitTeam team)
    {
        int count = 0;

        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
                continue;

            if (unit.State.Team == team)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 現在の戦闘参加ユニットか確認する。
    /// </summary>
    private bool IsValidUnit(UnitView unit)
    {
        return unit != null &&
               unit.State != null &&
               unit.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 味方が全員行動終了しているか。
    /// </summary>
    private bool AreAllAlliesFinished()
    {
        int allyCount = 0;

        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
                continue;

            if (unit.State.Team != UnitTeam.Ally)
                continue;

            allyCount++;

            // まだ行動できる味方がいる
            if (!unit.State.HasActed)
            {
                return false;
            }
        }

        // 味方がいない場合は切り替えない
        return allyCount > 0;
    }

    // =====================================
    // PLAYER PHASE
    // =====================================

    /// <summary>
    /// 味方フェーズを開始する。
    /// </summary>
    private void BeginPlayerPhase()
    {
        CurrentPhase = BattlePhase.Player;

        // 味方の行動状態をリセット
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
                continue;

            if (unit.State.Team == UnitTeam.Ally)
            {
                unit.State.ResetAction();
            }
        }

        // ユニットの暗転状態を更新
        UpdateUnitVisuals();

        // プレイヤー操作を有効にする
        SetPlayerControlEnabled(true);

        isChangingPhase = false;

        UpdatePhaseText();

        Debug.Log(
            $"TURN {TurnNumber} : PLAYER PHASE"
        );

        PhaseChanged?.Invoke(
            CurrentPhase,
            TurnNumber
        );
    }

    // =====================================
    // ENEMY PHASE
    // =====================================

    /// <summary>
    /// 敵フェーズの処理。
    ///
    /// 現在は敵AI未実装のため、
    /// 敵が順番に待機する仮処理。
    /// </summary>
    private IEnumerator EnemyPhaseRoutine()
    {
        isChangingPhase = true;

        CurrentPhase = BattlePhase.Enemy;

        // プレイヤー操作を停止する
        SetPlayerControlEnabled(false);

        UpdatePhaseText();

        Debug.Log(
            $"TURN {TurnNumber} : ENEMY PHASE"
        );

        PhaseChanged?.Invoke(
            CurrentPhase,
            TurnNumber
        );

        // 敵の行動状態をリセット
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
                continue;

            if (unit.State.Team == UnitTeam.Enemy)
            {
                unit.State.ResetAction();
            }
        }

        UpdateUnitVisuals();

        // 敵を1体ずつ仮で行動させる
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
                continue;

            if (unit.State.Team != UnitTeam.Enemy)
                continue;

            // 将来ここで敵AIを実行する
            // 現在は少し待って行動終了とする
            yield return new WaitForSeconds(
                enemyActionDelay
            );

            if (!IsValidUnit(unit))
                continue;

            // 敵の移動が終わるまで待つ。
            if (enemyAiMovementController != null)
            {
                yield return StartCoroutine(
                    enemyAiMovementController.ExecuteAction(unit));
            }

            // この後に、既存の
            // enemy.State.CompleteAction();
            // を実行する。
            unit.State.CompleteAction();

            Debug.Log(
                $"{unit.name}: 敵の仮行動終了"
            );

            UpdateUnitVisuals();
        }

        // 敵全員の仮行動が終了
        EndTurn();
    }

    // =====================================
    // ターン終了
    // =====================================

    /// <summary>
    /// 1ターンを終了し、次のターンへ進む。
    /// </summary>
    private void EndTurn()
    {
        Debug.Log(
            $"TURN {TurnNumber} 終了"
        );

        TurnNumber++;

        // 将来ここに昼夜判定を接続する。
        // 例：TimeOfDayManagerなど

        BeginPlayerPhase();
    }

    // =====================================
    // 入力の切り替え
    // =====================================

    /// <summary>
    /// 味方フェーズ以外の操作を禁止する。
    /// </summary>
    private void SetPlayerControlEnabled(bool value)
    {
        // グリッドのマウス・ゲームパッド入力
        if (gridInput != null)
        {
            gridInput.SetGridInputEnabled(value);
        }

        // ユニット選択やRキー操作も停止
        if (selectionController != null)
        {
            selectionController.enabled = value;
        }
    }

    // =====================================
    // 行動終了表示
    // =====================================

    /// <summary>
    /// 各ユニットの行動終了状態を
    /// 3Dモデルの見た目に反映する。
    /// </summary>
    private void UpdateUnitVisuals()
    {
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
                continue;

            UnitActionVisual actionVisual =
                unit.GetComponent<UnitActionVisual>();

            if (actionVisual == null)
                continue;

            actionVisual.SetDimmed(
                unit.State.HasActed
            );
        }
    }

    // =====================================
    // UI表示
    // =====================================

    /// <summary>
    /// 現在のフェーズ名を画面に表示する。
    /// </summary>
    private void UpdatePhaseText()
    {
        if (phaseText == null)
            return;

        string phaseName = "";

        switch (CurrentPhase)
        {
            case BattlePhase.Player:
                phaseName = "PLAYER PHASE";
                break;

            case BattlePhase.Enemy:
                phaseName = "ENEMY PHASE";
                break;

            default:
                phaseName = "";
                break;
        }

        phaseText.text =
            $"TURN {TurnNumber}  {phaseName}";
    }


    /// <summary>
    /// セーブデータからPLAYER PHASEのターン番号を復元する。
    /// 
    /// すでにPLAYER PHASEである場合だけ許可。
    ///
    /// 通常のStartPlayerPhaseとは異なり、
    /// 味方の行動状態をリセットしない。
    ///
    /// UnitStateのHasActedなどは
    /// ロード管理側で復元済みのため。
    /// </summary>
    public bool TryRestorePlayerTurnForLoad(
        int restoredTurnNumber)
    {
        if (restoredTurnNumber < 1)
        {
            Debug.LogError(
                "ロード失敗：ターン番号が不正です。",
                this
            );

            return false;
        }

        // 敵AIが動いている途中には
        // ターンを差し替えない。
        if (CurrentPhase != BattlePhase.Player)
        {
            Debug.LogWarning(
                "ロードはPLAYER PHASE中のみ可能です。",
                this
            );

            return false;
        }

        // 通常のターン開始処理は呼ばない。
        // ここでResetActionしてしまうと
        // 保存した行動終了状態が失われる。
        TurnNumber = restoredTurnNumber;

        // 第11段階の昼夜管理や、
        // 第13段階の危険範囲管理へ通知する。
        PhaseChanged?.Invoke(
            BattlePhase.Player,
            TurnNumber
        );

        Debug.Log(
            $"【ターン復元】TURN {TurnNumber}" +
            " / PLAYER PHASE",
            this
        );

        return true;
    }

}
