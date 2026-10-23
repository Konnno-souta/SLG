using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Conversation
{
    /// <summary>
    /// マス目単位で動くプレイヤー（チェスの駒のように1マスずつ移動）。
    /// WASD / 矢印キー / 十字キー / 左スティックで移動。
    /// 会話トリガーに反応させるため、Rigidbody（自動で付く・Is Kinematic）と Collider を使う。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class GridPlayerController : MonoBehaviour, IConversationPlayerControl
    {
        [Header("マス目")]
        [Tooltip("1マスの大きさ")]
        public float tileSize = 1f;
        [Tooltip("マス目の基準位置（マスの中心がこの位置から Tile Size ごとに並ぶ）")]
        public Vector3 gridOrigin = Vector3.zero;
        [Tooltip("開始時に一番近いマスの中心へ合わせる")]
        public bool snapOnStart = true;

        [Header("移動")]
        [Tooltip("1マス進むのにかかる秒数")]
        public float moveDuration = 0.18f;
        [Tooltip("押しっぱなしで続けて進む（オフなら1回押すごとに1マス）")]
        public bool repeatWhileHeld = true;
        [Tooltip("この秒数以上押し続けると、連続で進む（短く押した時は1マスだけ）")]
        public float repeatDelay = 0.25f;
        [Tooltip("斜めにも動ける")]
        public bool allowDiagonal = false;
        [Tooltip("移動中に跳ねる高さ（0で滑るように移動）")]
        public float hopHeight = 0f;

        [Header("向き")]
        [Tooltip("今と違う方向を押した時、最初は向きだけ変える（もう一度押すと進む）")]
        public bool turnBeforeMove = false;
        [Tooltip("向きを変える速さ（度/秒）。0なら一瞬で向く")]
        public float turnSpeed = 1080f;

        [Header("方向の基準")]
        [Tooltip("カメラから見た上下左右で動く（オフならワールドの前後左右）")]
        public bool cameraRelative = true;
        [Tooltip("基準にするカメラ（空なら Main Camera）")]
        public Transform cameraTransform;

        [Header("通れない場所")]
        [Tooltip("行き先にこのレイヤーの物があると進まない")]
        public LayerMask obstacleLayers = ~0;
        [Tooltip("当たり判定を調べる箱の大きさ（マスに対する割合）")]
        [Range(0.1f, 1f)] public float obstacleCheckSize = 0.8f;
        [Tooltip("当たり判定を調べる高さ（足元からの高さ）")]
        public float obstacleCheckHeight = 1f;
        [Tooltip("行き先に床が無ければ進まない")]
        public bool requireGround = true;
        public LayerMask groundLayers = ~0;
        [Tooltip("床を探す深さ")]
        public float groundCheckDistance = 3f;

        [Header("確認用")]
        [Tooltip("選択中、シーンビューにマス目を表示する")]
        public bool drawGrid = true;
        public int gridDrawRange = 5;

        /// <summary>会話中などで操作を止めているか</summary>
        public bool IsLocked { get; private set; }
        /// <summary>移動中か</summary>
        public bool IsMoving { get; private set; }
        /// <summary>今いるマス（整数座標）</summary>
        public Vector2Int CurrentCell => WorldToCell(IsMoving ? moveTo : transform.position);

        Rigidbody rb;
        Collider ownCollider;
        Vector3 moveFrom, moveTo;
        float moveTime;
        float repeatTimer;
        Vector2Int heldDirection;
        Quaternion targetRotation;

        readonly Collider[] overlapBuffer = new Collider[16];

        // =====================================================================
        // ライフサイクル
        // =====================================================================

        void Reset()
        {
            // 会話トリガーに触れたことを検知するために、Collider と Kinematic の Rigidbody が必要
            if (!GetComponent<Collider>())
            {
                var c = gameObject.AddComponent<CapsuleCollider>();
                c.height = 2f;
                c.radius = 0.4f;
            }
            var r = GetComponent<Rigidbody>();
            r.isKinematic = true;
            r.useGravity = false;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            ownCollider = GetComponent<Collider>();
            if (!ownCollider)
            {
                var c = gameObject.AddComponent<CapsuleCollider>();
                c.height = 2f;
                c.radius = 0.4f;
                ownCollider = c;
            }

            if (GetComponent<CharacterController>())
                Debug.LogWarning("[GridPlayerController] Character Controller が付いています。マス移動では不要なので削除してください", this);
            if (GetComponent<SimplePlayerController>())
                Debug.LogWarning("[GridPlayerController] SimplePlayerController も付いています。どちらか一方にしてください", this);

            targetRotation = transform.rotation;
        }

        void Start()
        {
            if (snapOnStart)
            {
                Vector3 p = CellToWorld(WorldToCell(transform.position));
                p.y = transform.position.y;
                transform.position = p;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (heldDirection != Vector2Int.zero) repeatTimer += dt;   // 押している時間（移動中も数える）

            // 向き
            transform.rotation = turnSpeed > 0f
                ? Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * dt)
                : targetRotation;

            // 移動中
            if (IsMoving)
            {
                moveTime += dt;
                float k = moveDuration > 0f ? Mathf.Clamp01(moveTime / moveDuration) : 1f;
                float eased = k * k * (3f - 2f * k);
                Vector3 p = Vector3.Lerp(moveFrom, moveTo, eased);
                if (hopHeight > 0f) p.y += Mathf.Sin(k * Mathf.PI) * hopHeight;
                transform.position = p;

                if (k >= 1f)
                {
                    transform.position = moveTo;
                    IsMoving = false;
                }
                else return;
            }

            if (IsLocked) { heldDirection = Vector2Int.zero; return; }

            // 入力
            Vector2Int dir = ReadDirection();
            if (dir == Vector2Int.zero)
            {
                heldDirection = Vector2Int.zero;
                return;
            }

            bool newPress = dir != heldDirection;
            if (newPress)
            {
                heldDirection = dir;
                repeatTimer = 0f;
                TryStep(dir, firstPress: true);
            }
            else if (repeatWhileHeld && repeatTimer >= repeatDelay)
            {
                // 押し続けている → 止まらずに次のマスへ
                TryStep(dir, firstPress: false);
            }
        }

        // =====================================================================
        // 移動
        // =====================================================================

        void TryStep(Vector2Int inputDir, bool firstPress)
        {
            Vector3 worldDir = InputToWorld(inputDir);
            if (worldDir == Vector3.zero) return;

            var wantRotation = Quaternion.LookRotation(worldDir);

            // 違う方向を押した時は、向きだけ変える
            if (turnBeforeMove && firstPress && Quaternion.Angle(targetRotation, wantRotation) > 1f)
            {
                targetRotation = wantRotation;
                return;
            }
            targetRotation = wantRotation;

            Vector3 from = transform.position;
            Vector3 to = from + worldDir * tileSize;
            // マスの中心に合わせる（ずれの蓄積を防ぐ）
            Vector3 snapped = CellToWorld(WorldToCell(to));
            to.x = snapped.x;
            to.z = snapped.z;

            if (IsBlocked(to)) return;

            moveFrom = from;
            moveTo = to;
            moveTime = 0f;
            IsMoving = true;
        }

        /// <summary>そのマスに進めないか</summary>
        public bool IsBlocked(Vector3 targetPosition)
        {
            // 物があるか（Is Trigger の Collider と自分は無視）
            Vector3 center = targetPosition + Vector3.up * obstacleCheckHeight;
            Vector3 half = new Vector3(tileSize, tileSize, tileSize) * (obstacleCheckSize * 0.5f);
            int n = Physics.OverlapBoxNonAlloc(center, half, overlapBuffer, Quaternion.identity,
                obstacleLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlapBuffer[i];
                if (c == ownCollider || c.transform.IsChildOf(transform)) continue;
                return true;
            }

            // 床があるか
            if (requireGround)
            {
                Vector3 origin = targetPosition + Vector3.up * 0.5f;
                if (!Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.5f, groundLayers, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return false;
        }

        /// <summary>入力の上下左右を、ワールドの方向（マス目に沿った向き）に変換</summary>
        Vector3 InputToWorld(Vector2Int input)
        {
            Vector3 forward = Vector3.forward, right = Vector3.right;

            if (cameraRelative)
            {
                Transform cam = cameraTransform ? cameraTransform : Camera.main ? Camera.main.transform : null;
                if (cam)
                {
                    // カメラの向きを、一番近いマス目の方向（90度単位）に合わせる
                    Vector3 f = cam.forward;
                    f.y = 0f;
                    if (f.sqrMagnitude > 0.0001f)
                    {
                        float angle = Mathf.Round(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg / 90f) * 90f;
                        var rot = Quaternion.Euler(0f, angle, 0f);
                        forward = rot * Vector3.forward;
                        right = rot * Vector3.right;
                    }
                }
            }

            Vector3 d = forward * input.y + right * input.x;
            d.x = Mathf.Round(d.x);
            d.z = Mathf.Round(d.z);
            d.y = 0f;
            return d;
        }

        // =====================================================================
        // マス目の座標変換
        // =====================================================================

        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = world - gridOrigin;
            return new Vector2Int(Mathf.RoundToInt(local.x / tileSize), Mathf.RoundToInt(local.z / tileSize));
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return gridOrigin + new Vector3(cell.x * tileSize, 0f, cell.y * tileSize);
        }

        /// <summary>指定したマスへ瞬間移動（イベントなどで使う）</summary>
        public void TeleportToCell(Vector2Int cell)
        {
            IsMoving = false;
            Vector3 p = CellToWorld(cell);
            p.y = transform.position.y;
            transform.position = p;
        }

        // =====================================================================
        // 会話との連携
        // =====================================================================

        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            heldDirection = Vector2Int.zero;
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - (IsMoving ? moveTo : transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            // 一番近いマス目の方向（90度単位）に向ける
            float angle = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 90f) * 90f;
            targetRotation = Quaternion.Euler(0f, angle, 0f);
        }

        // =====================================================================
        // 入力
        // =====================================================================

        Vector2Int ReadDirection()
        {
            Vector2 v = ReadRawMove();
            const float dead = 0.5f;
            int x = Mathf.Abs(v.x) >= dead ? (int)Mathf.Sign(v.x) : 0;
            int y = Mathf.Abs(v.y) >= dead ? (int)Mathf.Sign(v.y) : 0;

            if (!allowDiagonal && x != 0 && y != 0)
            {
                // 斜めは無し：押し続けている方向を優先、無ければ強い方
                if (heldDirection.x != 0 && heldDirection.x == x) y = 0;
                else if (heldDirection.y != 0 && heldDirection.y == y) x = 0;
                else if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) y = 0;
                else x = 0;
            }
            return new Vector2Int(x, y);
        }

#if ENABLE_INPUT_SYSTEM
        static Vector2 ReadRawMove()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                v += gp.leftStick.ReadValue();
                v += gp.dpad.ReadValue();
            }
            return v;
        }
#else
        static Vector2 ReadRawMove() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif

        // =====================================================================
        // シーンビューの表示
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            if (!drawGrid || tileSize <= 0f) return;

            Vector2Int c = WorldToCell(transform.position);
            float y = transform.position.y + 0.02f;
            float half = tileSize * 0.5f;
            int r = Mathf.Max(1, gridDrawRange);

            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            for (int i = -r; i <= r + 1; i++)
            {
                Vector3 a = CellToWorld(new Vector2Int(c.x + i, c.y - r)) + new Vector3(-half, 0f, -half);
                Vector3 b = CellToWorld(new Vector2Int(c.x + i, c.y + r)) + new Vector3(-half, 0f, half);
                a.y = b.y = y;
                Gizmos.DrawLine(a, b);

                Vector3 e = CellToWorld(new Vector2Int(c.x - r, c.y + i)) + new Vector3(-half, 0f, -half);
                Vector3 f = CellToWorld(new Vector2Int(c.x + r, c.y + i)) + new Vector3(half, 0f, -half);
                e.y = f.y = y;
                Gizmos.DrawLine(e, f);
            }

            // 今いるマス
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Vector3 cur = CellToWorld(c);
            cur.y = y;
            Gizmos.DrawWireCube(cur, new Vector3(tileSize, 0.01f, tileSize));
        }
    }
}
