
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// マウス・ゲームパッドからグリッドを操作する。
/// </summary>
public sealed class GridClickInput : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private BattleGrid grid;

    [SerializeField]
    private GridDebugView gridDebugView;

    [SerializeField]
    private Camera targetCamera;

    [Header("コントローラー設定")]

    // スティックをどの程度倒したら反応するか
    [SerializeField, Range(0.2f, 0.95f)]
    private float stickDeadZone = 0.55f;

    // 長押し時のマス移動間隔
    [SerializeField, Min(0.05f)]
    private float moveRepeatInterval = 0.18f;

    // 現在のカーソル位置
    private Vector2Int cursorPosition;

    // カーソルが初期化済みか
    private bool hasCursor;

    // 前フレームの入力方向
    private Vector2Int lastDirection;

    // 次にマスを移動できる時刻
    private float nextMoveTime;

    // ゲーム操作の有効・無効
    private bool inputEnabled = true;

    /// <summary>
    /// マスの決定をほかのシステムへ通知する。
    /// </summary>
    public event Action<Vector2Int> CellSelected;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (grid == null)
        {
            Debug.LogError(
                "BattleGridが設定されていません。",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (!inputEnabled)
            return;

        HandleMouseInput();
        HandleGamepadInput();
    }

    // ====================================
    // マウス操作
    // ====================================

    private void HandleMouseInput()
    {
        if (Mouse.current == null ||
            targetCamera == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // UI上をクリックしたときは無視
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = targetCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        // グリッドのXZ平面との交点を求める
        Plane plane = new Plane(
            grid.transform.up,
            grid.transform.position
        );

        if (!plane.Raycast(ray, out float distance))
            return;

        Vector3 worldPosition = ray.GetPoint(distance);

        if (!grid.TryWorldToCell(
            worldPosition,
            out Vector2Int position))
            return;

        // カーソルの位置を更新
        UpdateCursor(position);

        // マウスはクリックで即決定
        ConfirmCell();
    }

    // ====================================
    // コントローラー操作
    // ====================================

    private void HandleGamepadInput()
    {
        Gamepad gamepad = Gamepad.current;

        if (gamepad == null)
            return;

        // 十字キーを優先
        Vector2 input = gamepad.dpad.ReadValue();

        // 十字キー入力がなければ左スティック
        if (input.sqrMagnitude < 0.25f)
        {
            input = gamepad.leftStick.ReadValue();
        }

        Vector2Int direction =
            GetGridDirection(input);

        // 方向入力によるカーソル移動
        if (direction == Vector2Int.zero)
        {
            lastDirection = Vector2Int.zero;
        }
        else if (
            direction != lastDirection ||
            Time.unscaledTime >= nextMoveTime)
        {
            MoveCursor(direction);

            lastDirection = direction;

            nextMoveTime =
                Time.unscaledTime + moveRepeatInterval;
        }

        // PS: × / Xbox: A
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            EnsureCursor();
            ConfirmCell();
        }
    }

    /// <summary>
    /// スティックの入力を上下左右の
    /// いずれか1方向に変換する。
    /// </summary>
    private Vector2Int GetGridDirection(Vector2 input)
    {
        if (input.magnitude < stickDeadZone)
            return Vector2Int.zero;

        // 斜め入力時は大きい軸を優先
        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
        {
            return new Vector2Int(
                input.x > 0 ? 1 : -1,
                0
            );
        }

        return new Vector2Int(
            0,
            input.y > 0 ? 1 : -1
        );
    }

    /// <summary>
    /// カーソルを指定方向へ1マス移動する。
    /// </summary>
    private void MoveCursor(Vector2Int direction)
    {
        EnsureCursor();

        Vector2Int nextPosition =
            cursorPosition + direction;

        // マップ外に移動しない
        nextPosition.x = Mathf.Clamp(
            nextPosition.x,
            0,
            grid.Width - 1
        );

        nextPosition.y = Mathf.Clamp(
            nextPosition.y,
            0,
            grid.Height - 1
        );

        if (nextPosition == cursorPosition)
            return;

        UpdateCursor(nextPosition);
    }

    /// <summary>
    /// 初めてコントローラーを操作したとき、
    /// カーソルをマップ中央付近に配置する。
    /// </summary>
    private void EnsureCursor()
    {
        if (hasCursor)
            return;

        Vector2Int center = new Vector2Int(
            (grid.Width - 1) / 2,
            (grid.Height - 1) / 2
        );

        UpdateCursor(center);
    }

    // ====================================
    // 共通処理
    // ====================================

    /// <summary>
    /// カーソル位置とハイライトを更新する。
    /// ここではマスの決定は行わない。
    /// </summary>
    private void UpdateCursor(Vector2Int position)
    {
        if (!grid.IsInside(position))
            return;

        cursorPosition = position;
        hasCursor = true;

        if (gridDebugView != null)
        {
            gridDebugView.SetSelectedCell(position);
        }
    }

    /// <summary>
    /// 現在のマスを決定する。
    /// </summary>
    private void ConfirmCell()
    {
        if (!hasCursor)
            return;

        // 今後キャラ選択や移動処理で使用
        CellSelected?.Invoke(cursorPosition);

        Debug.Log(
            $"決定したマス: {cursorPosition} / " +
            $"通行可能: {grid.IsWalkable(cursorPosition)}"
        );
    }

    /// <summary>
    /// UIを開いている間などに
    /// グリッド操作を停止するための処理。
    /// </summary>
    public void SetGridInputEnabled(bool value)
    {
        inputEnabled = value;

        lastDirection = Vector2Int.zero;
    }
}
