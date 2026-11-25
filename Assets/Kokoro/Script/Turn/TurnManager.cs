using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum BattlePhase
{
    None,
    Player,
    Enemy
}

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

    [Header("戦闘結果UI")]
    [SerializeField]
    private GameObject stageClearPanel;

    [SerializeField]
    private GameObject gameOverPanel;

    [Header("クリア演出")]
    [SerializeField]
    private StageClearPanel stageClearPresentation;

    [Header("戦闘カメラ")]
    [SerializeField]
    private BattleCombatCameraController combatCamera;

    // =====================================
    // 主人公の識別
    // =====================================

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

    private bool isBattleFinished;

    public bool IsBattleFinished =>
        isBattleFinished;

    // HP0のユニットもロード用に保持する。
    private readonly List<UnitView> battleUnits =
        new List<UnitView>();

    // スキルシステムから全ユニットを参照する。
    public IReadOnlyList<UnitView> BattleUnits =>
        battleUnits.AsReadOnly();

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

        // StageLoaderによるユニット配置を待つ。
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

        // 戦闘カメラ演出中は進行を停止。
        if (combatCamera != null &&
            combatCamera.IsInCombat)
        {
            return;
        }

        UpdateUnitVisuals();

        // フェーズ進行より勝敗判定を優先。
        if (TryFinishBattle())
        {
            return;
        }

        if (CurrentPhase != BattlePhase.Player)
        {
            return;
        }

        // スキル対象選択中もフェーズを進めない。
        if (isChangingPhase ||
            (selectionController != null &&
             selectionController.IsSkillSelectionActive))
        {
            return;
        }

        // 生存している味方全員の行動終了を確認。
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

    private bool IsValidUnit(UnitView unit)
    {
        return BattleUnitLife.IsAlive(unit);
    }

    // =====================================
    // 味方全員の行動終了確認
    // =====================================

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
    // 主人公存在確認
    // =====================================

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

    // =====================================
    // 主人公生存確認
    // =====================================

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

    // =====================================
    // 勝敗判定
    // =====================================

    private bool TryFinishBattle()
    {
        if (isBattleFinished)
        {
            return true;
        }

        // 敗北を優先する。
        if (!IsProtagonistAlive())
        {
            FinishBattle(false);

            return true;
        }

        // 敵全滅で勝利。
        if (CountUnits(UnitTeam.Enemy) == 0)
        {
            FinishBattle(true);

            return true;
        }

        return false;
    }

    // =====================================
    // 戦闘終了
    // =====================================

    private void FinishBattle(bool isVictory)
    {
        if (isBattleFinished)
        {
            return;
        }

        isBattleFinished = true;

        isChangingPhase = false;

        CurrentPhase = BattlePhase.None;

        SetPlayerControlEnabled(false);

        // 結果パネル表示
        if (stageClearPanel != null)
        {
            stageClearPanel.SetActive(isVictory);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(!isVictory);
        }

        // ステージクリア演出
        if (isVictory)
        {
            if (stageClearPresentation != null)
            {
                stageClearPresentation.SetValue(
                    "TURN",
                    TurnNumber.ToString()
                );

                stageClearPresentation.Play();
            }
            else
            {
                Debug.LogWarning(
                    "StageClearPanelの演出参照が" +
                    "設定されていません。",
                    this
                );
            }
        }

        // ターン表示に戦闘結果を反映。
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

        // 戦闘終了時はPhaseChangedを呼ばない。
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

        // 生存している味方の行動をリセット。
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

        // スキルシステムへの通知。
        SkillManager.Instance?.NotifyPhaseStarted(
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

        // スキルシステムへの通知。
        SkillManager.Instance?.NotifyPhaseStarted(
            CurrentPhase,
            TurnNumber
        );

        // 敵を1体ずつ行動させる。
        foreach (UnitView unit in battleUnits)
        {
            if (TryFinishBattle())
            {
                yield break;
            }

            if (!IsValidUnit(unit) ||
                unit.State.Team != UnitTeam.Enemy)
            {
                continue;
            }

            // 再行動によって行動済みが解除された
            // 場合にも同じ敵を行動させる。
            while (IsValidUnit(unit) &&
                   !unit.State.HasActed)
            {
                yield return new WaitForSeconds(
                    enemyActionDelay
                );

                // 待機中の戦闘終了にも対応。
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

                // 主人公が倒された場合は停止。
                if (TryFinishBattle())
                {
                    yield break;
                }

                if (!IsValidUnit(unit))
                {
                    continue;
                }

                // 行動終了とスキル通知。
                if (!SkillBattleAPI.TryCompleteAction(
                        unit) &&
                    !unit.State.HasActed)
                {
                    break;
                }

                Debug.Log(
                    $"{unit.name}: 敵の行動終了"
                );

                UpdateUnitVisuals();
            }
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

        BeginPlayerPhase();
    }

    // =====================================
    // プレイヤー操作の切り替え
    // =====================================

    private void SetPlayerControlEnabled(
        bool value)
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

        // 既存の行動状態を維持する。
        TurnNumber = restoredTurnNumber;

        // 昼夜・危険範囲の更新。
        PhaseChanged?.Invoke(
            BattlePhase.Player,
            TurnNumber
        );

        // 通常のターン開始と区別する。
        SkillManager.Instance?.NotifyBattleRestored(
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