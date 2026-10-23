
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘マップのカーソル入力を管理する。
///
/// 対応入力：
/// ・マウス移動：カーソル移動
/// ・左クリック：マス決定
/// ・WASD / 矢印キー：カーソル移動
/// ・Enter：マス決定
/// ・ゲームパッド左スティック / 十字キー：移動
/// ・ゲームパッドの南ボタン：決定
///
/// 今回の追加機能：
/// ・カーソル移動を外部に通知
/// ・ユニットにマウスを合わせるだけで情報表示
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

    [Header("カーソル設定")]
    [SerializeField, Range(0.2f, 0.95f)]
    private float stickDeadZone = 0.55f;

    [SerializeField, Min(0.05f)]
    private float moveRepeatInterval = 0.18f;

    // 現在のカーソル座標
    private Vector2Int cursorPosition;

    // カーソルが初期化されているか
    private bool hasCursor;

    // 最後に入力された方向
    private Vector2Int lastDirection;

    // 次の方向入力受付時刻
    private float nextMoveTime;

    // 現在マップ入力を受け付けるか
    private bool inputEnabled = true;

    /// <summary>
    /// マスを決定したことを通知。
    /// 既存のUnitSelectionControllerが使用する。
    /// </summary>
    public event Action<Vector2Int> CellSelected;

    /// <summary>
    /// 今回追加。
    /// カーソルが別のマスに移動したことを通知。
    /// </summary>
    public event Action<Vector2Int> CursorChanged;

    // 外部からカーソル情報を参照する
    public bool HasCursor => hasCursor;

    public Vector2Int CursorPosition => cursorPosition;

    public bool InputEnabled => inputEnabled;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (grid == null || targetCamera == null)
        {
            Debug.LogError(
                "GridClickInputの参照が不足しています。",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (!inputEnabled)
            return;

        // マウスを動かした位置へカーソルを合わせる
        HandleMouseHover();

        // マウス左クリック
        HandleMouseClick();

        // クリックの結果UIが開いた場合は
        // このフレームの残りの入力を処理しない。
        if (!inputEnabled)
            return;

        // WASD / 矢印キー / ゲームパッド
        HandleDirectionalInput();

        if (!inputEnabled)
            return;

        // Enter / ゲームパッド決定
        HandleConfirmInput();
    }

    // ======================================
    // マウス操作
    // ======================================

    /// <summary>
    /// マウスを動かしたマスへカーソルを合わせる。
    ///
    /// 毎フレーム位置を強制更新すると
    /// WASDで動かしたカーソルが
    /// マウス位置へ戻ってしまうため、
    /// マウスが実際に動いたフレームだけ処理する。
    /// </summary>
    private void HandleMouseHover()
    {
        if (Mouse.current == null)
            return;

        Vector2 delta =
            Mouse.current.delta.ReadValue();

        if (delta.sqrMagnitude < 0.01f)
            return;

        // UI上のマウスはマップに反映しない
        if (IsPointerOverUI())
            return;

        if (TryGetMouseCell(out Vector2Int position))
        {
            UpdateCursor(position);
        }
    }

    /// <summary>
    /// 左クリックでマスを決定する。
    /// </summary>
    private void HandleMouseClick()
    {
        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (IsPointerOverUI())
            return;

        if (!TryGetMouseCell(out Vector2Int position))
            return;

        UpdateCursor(position);

        ConfirmCell();
    }

    /// <summary>
    /// マウス位置をグリッド座標に変換する。
    /// 3DモデルのColliderには依存しない。
    /// </summary>
    private bool TryGetMouseCell(
        out Vector2Int position)
    {
        position = Vector2Int.zero;

        if (grid == null || targetCamera == null)
            return false;

        if (Mouse.current == null)
            return false;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray = targetCamera.ScreenPointToRay(
            mousePosition
        );

        // BattleGridのXZ平面を使う
        Plane gridPlane = new Plane(
            grid.transform.up,
            grid.transform.position
        );

        if (!gridPlane.Raycast(
            ray,
            out float distance))
        {
            return false;
        }

        Vector3 worldPosition =
            ray.GetPoint(distance);

        return grid.TryWorldToCell(
            worldPosition,
            out position
        );
    }

    /// <summary>
    /// マウスがUnity UIの上にあるか。
    /// </summary>
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    // ======================================
    // 方向入力
    // ======================================

    private void HandleDirectionalInput()
    {
        Vector2Int direction = Vector2Int.zero;

        Gamepad gamepad = Gamepad.current;

        if (gamepad != null)
        {
            // 十字キーを優先
            Vector2 padDirection =
                gamepad.dpad.ReadValue();

            if (padDirection.sqrMagnitude < 0.01f)
            {
                padDirection =
                    gamepad.leftStick.ReadValue();
            }

            direction =
                GetGridDirection(padDirection);
        }

        // パッド入力がなければキーボード
        if (direction == Vector2Int.zero)
        {
            direction = GetKeyboardDirection();
        }

        // 方向入力を離した
        if (direction == Vector2Int.zero)
        {
            lastDirection = Vector2Int.zero;
            return;
        }

        // 方向が変わった瞬間は即座に移動。
        // 押しっぱなしの場合は一定間隔で移動。
        if (direction != lastDirection ||
            Time.unscaledTime >= nextMoveTime)
        {
            MoveCursor(direction);

            lastDirection = direction;

            nextMoveTime =
                Time.unscaledTime +
                moveRepeatInterval;
        }
    }

    /// <summary>
    /// WASD / 矢印キーから方向取得。
    /// </summary>
    private Vector2Int GetKeyboardDirection()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return Vector2Int.zero;

        int x = 0;
        int y = 0;

        if (keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed)
        {
            x--;
        }

        if (keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed)
        {
            x++;
        }

        if (keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed)
        {
            y++;
        }

        if (keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed)
        {
            y--;
        }

        // 斜め入力ではX方向を優先する
        if (x != 0)
            return new Vector2Int(x, 0);

        return new Vector2Int(0, y);
    }

    /// <summary>
    /// ゲームパッドの方向を上下左右に変換。
    /// </summary>
    private Vector2Int GetGridDirection(Vector2 input)
    {
        if (input.magnitude < stickDeadZone)
            return Vector2Int.zero;

        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
        {
            return new Vector2Int(
                input.x > 0f ? 1 : -1,
                0
            );
        }

        return new Vector2Int(
            0,
            input.y > 0f ? 1 : -1
        );
    }

    // ======================================
    // カーソル移動
    // ======================================

    private void MoveCursor(Vector2Int direction)
    {
        EnsureCursor();

        Vector2Int nextPosition =
            cursorPosition + direction;

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

        UpdateCursor(nextPosition);
    }

    /// <summary>
    /// カーソルが未設定ならマップ中央に生成。
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

    /// <summary>
    /// カーソル位置を更新し、
    /// 位置が変わったときだけイベントを通知。
    /// </summary>
    private void UpdateCursor(Vector2Int position)
    {
        if (!grid.IsInside(position))
            return;

        bool changed =
            !hasCursor ||
            cursorPosition != position;

        cursorPosition = position;
        hasCursor = true;

        if (gridDebugView != null)
        {
            gridDebugView.SetSelectedCell(position);
        }

        if (changed)
        {
            CursorChanged?.Invoke(position);
        }
    }

    // ======================================
    // マス決定
    // ======================================

    private void HandleConfirmInput()
    {
        bool confirmPressed = false;

        Gamepad gamepad = Gamepad.current;

        if (gamepad != null &&
            gamepad.buttonSouth.wasPressedThisFrame)
        {
            confirmPressed = true;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            confirmPressed = true;
        }

        if (!confirmPressed)
            return;

        EnsureCursor();
        ConfirmCell();
    }

    private void ConfirmCell()
    {
        if (!hasCursor)
            return;

        // 移動やユニット選択は
        // これまでどおり既存システムへ通知
        CellSelected?.Invoke(cursorPosition);
    }

    // ======================================
    // 操作制限
    // ======================================

    /// <summary>
    /// 移動演出中やコマンドUI表示中は
    /// グリッド入力を止める。
    /// </summary>
    public void SetGridInputEnabled(bool value)
    {
        inputEnabled = value;

        // ボタンを押しっぱなしにした
        // 古い情報を引き継がない。
        lastDirection = Vector2Int.zero;
        nextMoveTime = 0f;
    }
}
