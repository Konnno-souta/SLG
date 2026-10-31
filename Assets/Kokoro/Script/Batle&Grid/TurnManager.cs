
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
/// ・主人公のHP0でGAME OVER
/// ・敵全滅でSTAGE CLEAR
/// ・勝敗確定後の操作・ターン進行停止
///
/// 既存の移動・敵AI・昼夜・ロード機能を維持。
/// </summary>
public sealed class TurnManager : MonoBehaviour
{
    // =====================================
    // Inspector参照
    // =====================================

    [Header("システム参照")]
    [SerializeField]
    private GridClickInput gridInput;

    [SerializeField]
    private UnitSelectionController selectionController;

    [Header("フェーズ表示（任意）")]
    [SerializeField]
    private TMP_Text phaseText;

    [Header("敵フェーズ")]
    [SerializeField, Min(0f)]
    private float enemyActionDelay = 0.3f;

    [Header("敵AI")]
    [SerializeField]
    private EnemyAiMovementController
        enemyAiMovementController;

    // 第22段階で追加するInspector項目
    [Header("第22段階：戦闘結果UI")]
    [SerializeField]
    private GameObject stageClearPanel;

    [SerializeField]
    private GameObject gameOverPanel;

    // =====================================
    // 主人公の識別
    // =====================================

    // 新Character.csvのクランのID。
    private const string ProtagonistCharacterId = "1";

    // =====================================
    // 戦闘状態
    // =====================================

    public BattlePhase CurrentPhase
    {
        get;
        private set;
    } = BattlePhase.None;

    public int TurnNumber
    {
        get;
        private set;
    } = 1;

    private bool isChangingPhase;

    // 第22段階：勝敗確定済みか。
    private bool isBattleFinished;

    public bool IsBattleFinished =>
        isBattleFinished;

    // すべてのユニットを保持する。
    // HP0のユニットもロード用に残す。
    private readonly List<UnitView> battleUnits =
        new List<UnitView>();

    // 既存のイベントを維持。
    public event Action<BattlePhase, int>
        PhaseChanged;

    // =====================================
    // UI初期化
    // =====================================

    private void Awake()
    {
        if (stageClearPanel != null)
        {
            stageClearPanel.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    // =====================================
    // 初期化
    // =====================================

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

        // StageLoaderによる配置と、
        // UnitSelectionControllerの初期化を待つ。
        yield return null;

        RefreshUnits();

        int allyCount =
            CountUnits(UnitTeam.Ally);

        int enemyCount =
            CountUnits(UnitTeam.Enemy);

        if (allyCount == 0)
        {
            Debug.LogError(
                "TurnManager: 味方ユニットがいません。",
                this
            );

            enabled = false;
            yield break;
        }

        // 主人公はCharacter_IDで識別する。
        // 名前による判定にはしない。
        if (!HasProtagonist())
        {
            Debug.LogError(
                "TurnManager: 主人公のクラン" +
                "（Character_ID=1）が見つかりません。" +
                "Stage_CharaLayout.csvを確認してください。",
                this
            );

            enabled = false;
            yield break;
        }

        Debug.Log(
            $"ターン管理開始 / 味方:{allyCount}体 " +
            $"敵:{enemyCount}体",
            this
        );

        isBattleFinished = false;
        TurnNumber = 1;

        BeginPlayerPhase();
    }

    // =====================================
    // 毎フレーム処理
    // =====================================

    private void Update()
    {
        if (CurrentPhase == BattlePhase.None ||
            isBattleFinished)
        {
            return;
        }

        // 従来の行動終了表示。
        UpdateUnitVisuals();

        // 第22段階：
        // フェーズ進行より先に勝敗を判定する。
        if (TryFinishBattle())
        {
            return;
        }

        if (CurrentPhase != BattlePhase.Player)
        {
            return;
        }

        if (isChangingPhase)
        {
            return;
        }

        // 生存している味方全員が行動終了したら
        // ENEMY PHASEへ進む。
        if (AreAllAlliesFinished())
        {
            StartCoroutine(
                EnemyPhaseRoutine()
            );
        }
    }

    // =====================================
    // ユニット管理
    // =====================================

    /// <summary>
    /// シーン内のユニットを再取得する。
    /// HP0のユニットもリストに保持する。
    /// </summary>
    public void RefreshUnits()
    {
        battleUnits.Clear();

        UnitView[] foundUnits;

#if UNITY_2022_2_OR_NEWER
        foundUnits =
            FindObjectsByType<UnitView>(
                FindObjectsSortMode.None
            );
#else
        foundUnits =
            FindObjectsOfType<UnitView>();
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
    /// 生存している指定チームのユニット数。
    /// </summary>
    private int CountUnits(UnitTeam team)
    {
        int count = 0;

        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
            {
                continue;
            }

            if (unit.State.Team == team)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 第20段階のHP0除外処理を維持。
    /// ターン処理に参加できるのは生存者のみ。
    /// </summary>
    private bool IsValidUnit(UnitView unit)
    {
        return BattleUnitLife.IsAlive(unit);
    }

    /// <summary>
    /// 生存している味方が全員行動を終えたか。
    /// </summary>
    private bool AreAllAlliesFinished()
    {
        int allyCount = 0;

        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
            {
                continue;
            }

            if (unit.State.Team != UnitTeam.Ally)
            {
                continue;
            }

            allyCount++;

            if (!unit.State.HasActed)
            {
                return false;
            }
        }

        return allyCount > 0;
    }

    // =====================================
    // 第22段階：勝敗判定
    // =====================================

    /// <summary>
    /// 主人公が配置されているかを確認する。
    /// </summary>
    private bool HasProtagonist()
    {
        foreach (UnitView unit in battleUnits)
        {
            if (unit == null ||
                unit.State == null ||
                unit.MasterData == null)
            {
                continue;
            }

            if (unit.State.Team == UnitTeam.Ally &&
                unit.MasterData.Id ==
                    ProtagonistCharacterId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 主人公が生存しているか。
    /// 他の味方の生存状態は判定しない。
    /// </summary>
    private bool IsProtagonistAlive()
    {
        foreach (UnitView unit in battleUnits)
        {
            if (unit == null ||
                unit.State == null ||
                unit.MasterData == null)
            {
                continue;
            }

            if (unit.State.Team != UnitTeam.Ally ||
                unit.MasterData.Id !=
                    ProtagonistCharacterId)
            {
                continue;
            }

            return BattleUnitLife.IsAlive(unit);
        }

        return false;
    }

    /// <summary>
    /// 勝利・敗北の判定。
    ///
    /// 1. 主人公HP0なら敗北
    /// 2. 敵全滅なら勝利
    ///
    /// 両方成立した場合は敗北優先。
    /// </summary>
    private bool TryFinishBattle()
    {
        if (isBattleFinished)
        {
            return true;
        }

        // 最優先：主人公の戦闘不能
        if (!IsProtagonistAlive())
        {
            FinishBattle(false);
            return true;
        }

        // 次に敵全滅を確認
        if (CountUnits(UnitTeam.Enemy) == 0)
        {
            FinishBattle(true);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 勝敗を確定して操作とターン進行を停止。
    /// </summary>
    private void FinishBattle(bool isVictory)
    {
        if (isBattleFinished)
        {
            return;
        }

        isBattleFinished = true;
        isChangingPhase = false;

        // NONEにすることで、
        // セーブ・ロードも新たには行えなくなる。
        CurrentPhase = BattlePhase.None;

        // プレイヤーの操作を停止。
        SetPlayerControlEnabled(false);

        // 結果パネルを表示。
        if (stageClearPanel != null)
        {
            stageClearPanel.SetActive(isVictory);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(!isVictory);
        }

        // 既存のターン表示にも結果を反映。
        if (phaseText != null)
        {
            phaseText.text =
                $"TURN {TurnNumber}  " +
                (isVictory
                    ? "STAGE CLEAR"
                    : "GAME OVER");
        }

        if (isVictory)
        {
            Debug.Log(
                "【STAGE CLEAR】\n" +
                "すべての敵ユニットを撃破しました。",
                this
            );
        }
        else
        {
            Debug.Log(
                "【GAME OVER】\n" +
                "主人公クランが戦闘不能になりました。",
                this
            );
        }

        // 既存のPhaseChangedはここでは呼ばない。
        // 昼夜変更や敵AIに誤って
        // フェーズ開始を通知しないため。
    }

    // =====================================
    // PLAYER PHASE
    // =====================================

    private void BeginPlayerPhase()
    {
        if (isBattleFinished)
        {
            return;
        }

        CurrentPhase = BattlePhase.Player;

        // 生存している味方だけリセット。
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
            {
                continue;
            }

            if (unit.State.Team == UnitTeam.Ally)
            {
                unit.State.ResetAction();
            }
        }

        UpdateUnitVisuals();

        SetPlayerControlEnabled(true);

        isChangingPhase = false;

        UpdatePhaseText();

        Debug.Log(
            $"TURN {TurnNumber} : PLAYER PHASE"
        );

        // 既存の昼夜・危険範囲への通知。
        PhaseChanged?.Invoke(
            CurrentPhase,
            TurnNumber
        );
    }

    // =====================================
    // ENEMY PHASE
    // =====================================

    private IEnumerator EnemyPhaseRoutine()
    {
        if (isBattleFinished ||
            TryFinishBattle())
        {
            yield break;
        }

        isChangingPhase = true;
        CurrentPhase = BattlePhase.Enemy;

        SetPlayerControlEnabled(false);
        UpdatePhaseText();

        Debug.Log(
            $"TURN {TurnNumber} : ENEMY PHASE"
        );

        PhaseChanged?.Invoke(
            CurrentPhase,
            TurnNumber
        );

        // 生存している敵の行動をリセット。
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
            {
                continue;
            }

            if (unit.State.Team == UnitTeam.Enemy)
            {
                unit.State.ResetAction();
            }
        }

        UpdateUnitVisuals();

        // 敵を1体ずつ行動させる。
        foreach (UnitView unit in battleUnits)
        {
            // 前の敵が主人公を倒していたら停止。
            if (TryFinishBattle())
            {
                yield break;
            }

            if (!IsValidUnit(unit) ||
                unit.State.Team != UnitTeam.Enemy)
            {
                continue;
            }

            yield return new WaitForSeconds(
                enemyActionDelay
            );

            // 待機中に戦闘終了した場合にも対応。
            if (TryFinishBattle())
            {
                yield break;
            }

            if (!IsValidUnit(unit))
            {
                continue;
            }

            // 既存の敵AIによる移動・攻撃。
            if (enemyAiMovementController != null)
            {
                yield return StartCoroutine(
                    enemyAiMovementController
                        .ExecuteAction(unit)
                );
            }

            // 第22段階：
            // 敵の攻撃によって主人公が倒れた場合、
            // 次の敵を行動させずに終了する。
            if (TryFinishBattle())
            {
                yield break;
            }

            if (!IsValidUnit(unit))
            {
                continue;
            }

            // 行動終了の管理は従来どおり
            // TurnManagerで実施。
            unit.State.CompleteAction();

            Debug.Log(
                $"{unit.name}: 敵の行動終了"
            );

            UpdateUnitVisuals();
        }

        // 全敵行動後にも勝敗を確認。
        if (TryFinishBattle())
        {
            yield break;
        }

        EndTurn();
    }

    // =====================================
    // ターン終了
    // =====================================

    private void EndTurn()
    {
        if (isBattleFinished ||
            TryFinishBattle())
        {
            return;
        }

        Debug.Log(
            $"TURN {TurnNumber} 終了"
        );

        TurnNumber++;

        // 昼夜切り替えは従来の
        // PhaseChangedイベントを使用。
        BeginPlayerPhase();
    }

    // =====================================
    // プレイヤー操作の切り替え
    // =====================================

    private void SetPlayerControlEnabled(bool value)
    {
        if (gridInput != null)
        {
            gridInput.SetGridInputEnabled(value);
        }

        if (selectionController != null)
        {
            selectionController.enabled = value;
        }
    }

    // =====================================
    // 行動終了表示
    // =====================================

    private void UpdateUnitVisuals()
    {
        foreach (UnitView unit in battleUnits)
        {
            if (!IsValidUnit(unit))
            {
                continue;
            }

            UnitActionVisual actionVisual =
                unit.GetComponent<UnitActionVisual>();

            if (actionVisual == null)
            {
                continue;
            }

            actionVisual.SetDimmed(
                unit.State.HasActed
            );
        }
    }

    // =====================================
    // フェーズ表示
    // =====================================

    private void UpdatePhaseText()
    {
        if (phaseText == null)
        {
            return;
        }

        string phaseName = "";

        switch (CurrentPhase)
        {
            case BattlePhase.Player:
                phaseName = "PLAYER PHASE";
                break;

            case BattlePhase.Enemy:
                phaseName = "ENEMY PHASE";
                break;
        }

        phaseText.text =
            $"TURN {TurnNumber}  {phaseName}";
    }

    // =====================================
    // 第16段階：ターン復元
    // =====================================

    /// <summary>
    /// PLAYER PHASE中にターン数を復元。
    /// 味方の行動状態はリセットしない。
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

        if (isBattleFinished ||
            CurrentPhase != BattlePhase.Player)
        {
            Debug.LogWarning(
                "戦闘終了後またはPLAYER PHASE以外では" +
                "ロードできません。",
                this
            );

            return false;
        }

        // 既存のHasActedを維持。
        TurnNumber = restoredTurnNumber;

        // 昼夜・危険範囲の更新。
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
