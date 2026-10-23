
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// カーソルが合っているユニットの
/// ステータスを画面に表示する。
///
/// Character.csvとClass.csvから
/// 初期化済みのUnitBattleStatsを利用する。
///
/// CSVを直接読み直したり、
/// ステータスを変更したりはしない。
/// </summary>
public sealed class UnitCursorStatusPanel : MonoBehaviour
{
    [Header("入力参照")]
    [SerializeField]
    private GridClickInput gridInput;

    [Header("UI参照")]
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private TMP_Text statusText;

    // 戦闘参加ユニットの参照
    private UnitView[] cachedUnits;

    // 現在表示しているユニット
    private UnitView displayedUnit;

    // HPやクラスが変わったか確認するための値
    private int lastHP;
    private bool lastStatsReady;
    private BattleClassMode lastClassMode;

    private void Awake()
    {
        if (gridInput == null ||
            panelRoot == null ||
            statusText == null)
        {
            Debug.LogError(
                "UnitCursorStatusPanelの参照が" +
                "不足しています。",
                this
            );

            enabled = false;
            return;
        }

        // 初期状態は非表示
        panelRoot.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (!enabled)
            yield break;

        // カーソル移動通知を受け取る
        gridInput.CursorChanged += OnCursorChanged;

        // BattleStageLoaderによる自動生成を待つ
        yield return null;

        RefreshUnits();
    }

    private void OnDestroy()
    {
        if (gridInput != null)
        {
            gridInput.CursorChanged -= OnCursorChanged;
        }
    }

    private void Update()
    {
        if (gridInput == null)
            return;

        // 移動後の位置やHPの変化を反映する。
        // 毎フレーム文字列を作るのではなく、
        // 対象または表示内容が変わったときだけ更新。
        UpdatePanel();
    }

    // =====================================
    // ユニット参照
    // =====================================

    /// <summary>
    /// シーン内のUnitViewをまとめて取得する。
    ///
    /// ステージ開始時に呼び出す。
    /// 将来、増援を生成した場合にも再実行できる。
    /// </summary>
    public void RefreshUnits()
    {
#if UNITY_2022_2_OR_NEWER
        cachedUnits = FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        cachedUnits = FindObjectsOfType<UnitView>();
#endif

        // 再取得後は表示内容を更新
        displayedUnit = null;

        UpdatePanel();
    }

    /// <summary>
    /// 指定座標にいるユニットを探す。
    /// </summary>
    private UnitView FindUnitAt(Vector2Int position)
    {
        if (cachedUnits == null)
            return null;

        foreach (UnitView unit in cachedUnits)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy ||
                unit.State == null)
            {
                continue;
            }

            if (unit.State.Position == position)
            {
                return unit;
            }
        }

        return null;
    }

    // =====================================
    // カーソル移動通知
    // =====================================

    private void OnCursorChanged(
        Vector2Int position)
    {
        UpdatePanel();
    }

    // =====================================
    // ステータス表示
    // =====================================

    private void UpdatePanel()
    {
        if (gridInput == null ||
            !gridInput.HasCursor)
        {
            HidePanel();
            return;
        }

        UnitView unit =
            FindUnitAt(gridInput.CursorPosition);

        // キャラクターがいないマス
        if (unit == null)
        {
            HidePanel();
            return;
        }

        UnitBattleStats stats =
            unit.State.CombatStats;

        bool statsReady = stats != null;

        // 現在HP
        int hp = statsReady
            ? stats.CurrentHP
            : -1;

        // 現在のクラス
        BattleClassMode mode = statsReady
            ? stats.ClassMode
            : BattleClassMode.Light;

        // 同じユニット・HP・クラスなら、
        // 毎フレーム文字列を作り直さない。
        if (panelRoot.activeSelf &&
            displayedUnit == unit &&
            lastHP == hp &&
            lastStatsReady == statsReady &&
            (!statsReady || lastClassMode == mode))
        {
            return;
        }

        displayedUnit = unit;
        lastHP = hp;
        lastStatsReady = statsReady;
        lastClassMode = mode;

        if (statsReady)
        {
            ShowFullStatus(unit, stats);
        }
        else
        {
            ShowLoadingStatus(unit);
        }

        panelRoot.SetActive(true);
    }

    /// <summary>
    /// 戦闘能力をすべて表示する。
    /// </summary>
    private void ShowFullStatus(
        UnitView unit,
        UnitBattleStats stats)
    {
        string teamName =
            unit.State.Team == UnitTeam.Ally
                ? "味方"
                : "敵";

        string classModeName =
            stats.ClassMode == BattleClassMode.Light
                ? "朝"
                : "夜";

        // 仕様上、CSVの配置座標は1始まり。
        // Unity内部座標から表示用に変換する。
        Vector2Int cell = unit.State.Position;

        int csvX = cell.x + 1;
        int csvY = cell.y + 1;

        StringBuilder builder =
            new StringBuilder(300);

        builder.AppendLine(
            $"【{stats.CharacterName}】"
        );

        builder.AppendLine(
            $"{teamName} / {stats.CharacterId}"
        );

        builder.AppendLine();

        builder.AppendLine(
            $"クラス: {stats.CurrentClassName}"
        );

        builder.AppendLine(
            $"クラスID: {stats.CurrentClassId}"
        );

        builder.AppendLine(
            $"区分: {classModeName}"
        );

        builder.AppendLine();

        builder.AppendLine(
            $"HP: {stats.CurrentHP} / {stats.MaxHP}"
        );

        builder.AppendLine(
            $"物理攻撃: {stats.Attack:0.##}"
        );

        builder.AppendLine(
            $"魔法攻撃: {stats.Magic:0.##}"
        );

        builder.AppendLine(
            $"物理防御: {stats.AttackDefense:0.##}"
        );

        builder.AppendLine(
            $"魔法防御: {stats.MagicDefense:0.##}"
        );

        builder.AppendLine(
            $"素早さ: {stats.Speed:0.##}"
        );

        builder.AppendLine(
            $"命中: {stats.Hit:0.##}"
        );

        builder.AppendLine(
            $"回避: {stats.Evasion:0.##}"
        );

        builder.AppendLine();

        builder.AppendLine(
            $"攻撃射程: " +
            $"{stats.MinAttackRange} ～ " +
            $"{stats.MaxAttackRange}"
        );

        string rangeType =
            string.IsNullOrWhiteSpace(stats.RangeType)
                ? "未設定"
                : stats.RangeType;

        builder.AppendLine(
            $"RangeType: {rangeType}"
        );

        builder.AppendLine(
            $"配置座標: ({csvX}, {csvY})"
        );

        statusText.text =
            builder.ToString();
    }

    /// <summary>
    /// ステータスがまだ初期化されていないとき。
    /// </summary>
    private void ShowLoadingStatus(UnitView unit)
    {
        string characterName =
            unit.MasterData != null
                ? unit.MasterData.Name
                : unit.name;

        statusText.text =
            $"【{characterName}】\n" +
            "ステータス初期化待ち";
    }

    /// <summary>
    /// 対象がいない場合は非表示。
    /// </summary>
    private void HidePanel()
    {
        displayedUnit = null;

        if (panelRoot != null &&
            panelRoot.activeSelf)
        {
            panelRoot.SetActive(false);
        }
    }
}
