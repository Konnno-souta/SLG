using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Conversation
{
    /// <summary>
    /// 動作確認用の仮プレイヤー。WASD / 矢印キー / 左スティックで移動。
    /// CharacterController と一緒に使う（自動で付く）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("移動")]
        public float walkSpeed = 4f;
        [Tooltip("Shift / LBボタンを押している間の速さ")]
        public float runSpeed = 7f;
        [Tooltip("向きを変える速さ（度/秒）")]
        public float turnSpeed = 720f;
        public float gravity = -20f;
        [Tooltip("ジャンプの高さ（0でジャンプ無し）。Space / Ⓑボタン")]
        public float jumpHeight = 0f;

        [Header("カメラ")]
        [Tooltip("移動の向きの基準にするカメラ（空なら Main Camera）")]
        public Transform cameraTransform;

        /// <summary>会話中などで操作を止めているか</summary>
        public bool IsLocked { get; private set; }

        CharacterController cc;
        float verticalSpeed;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
        }

        /// <summary>操作の停止・再開</summary>
        public void SetLocked(bool locked) => IsLocked = locked;

        /// <summary>指定した位置の方を向く（上下は無視）</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = IsLocked ? Vector2.zero : ReadMove();

            // カメラから見た前後左右に変換
            Transform cam = cameraTransform ? cameraTransform : Camera.main ? Camera.main.transform : null;
            Vector3 forward = cam ? cam.forward : Vector3.forward;
            Vector3 right = cam ? cam.right : Vector3.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = !IsLocked && ReadRun() ? runSpeed : walkSpeed;

            if (move.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            }

            // 重力・ジャンプ
            if (cc.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (!IsLocked && jumpHeight > 0f && cc.isGrounded && ReadJump())
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalSpeed += gravity * dt;

            cc.Move((move * speed + Vector3.up * verticalSpeed) * dt);
        }

        // =====================================================================
        // 入力
        // =====================================================================

#if ENABLE_INPUT_SYSTEM
        static Vector2 ReadMove()
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
            if (gp != null) v += gp.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        static bool ReadRun()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && kb.leftShiftKey.isPressed) || (gp != null && gp.leftShoulder.isPressed);
        }

        static bool ReadJump()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
        }
#else
        static Vector2 ReadMove()
        {
            var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            return Vector2.ClampMagnitude(v, 1f);
        }

        static bool ReadRun() => Input.GetKey(KeyCode.LeftShift);
        static bool ReadJump() => Input.GetKeyDown(KeyCode.Space);
#endif
    }
}
