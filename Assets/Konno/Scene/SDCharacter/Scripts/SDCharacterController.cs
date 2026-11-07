using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// SDキャラ操作：WASD/矢印キー or 左スティックで移動、Shift で走る、E で手を振る。
/// カメラの向き基準で移動します。旧Input Manager / 新Input System の両方に対応。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SDCharacterController : MonoBehaviour
{
    [Header("移動")]
    public float walkSpeed = 1.0f;
    public float runSpeed = 2.5f;
    public float acceleration = 8f;
    public float turnSpeed = 720f;
    public float gravity = -15f;

    CharacterController cc;
    Animator anim;
    Transform cam;
    Vector3 moveDir = Vector3.forward;
    float speed;
    float vy;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int WaveHash = Animator.StringToHash("Wave");

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        if (Camera.main) cam = Camera.main.transform;
    }

    void Update()
    {
        Vector2 input = ReadMove();

        Vector3 dir;
        if (cam)
        {
            Vector3 f = cam.forward; f.y = 0f; f.Normalize();
            Vector3 r = cam.right;   r.y = 0f; r.Normalize();
            dir = f * input.y + r * input.x;
        }
        else dir = new Vector3(input.x, 0f, input.y);
        float amount = Mathf.Clamp01(dir.magnitude);

        if (amount > 0.01f)
        {
            moveDir = dir.normalized;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(moveDir), turnSpeed * Time.deltaTime);
        }

        float target = amount * (ReadRun() ? runSpeed : walkSpeed);
        speed = Mathf.MoveTowards(speed, target, acceleration * Time.deltaTime);

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * speed;
        velocity.y = vy;
        cc.Move(velocity * Time.deltaTime);

        if (anim)
        {
            anim.SetFloat(SpeedHash, speed);
            if (ReadWave()) anim.SetTrigger(WaveHash);
        }
    }

    // ---------- 入力 ----------
    Vector2 ReadMove()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 v = Vector2.zero;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed)    v.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed)  v.y -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed)  v.x -= 1f;
        }
        var g = Gamepad.current;
        if (g != null) v += g.leftStick.ReadValue();
        return Vector2.ClampMagnitude(v, 1f);
#else
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
    }

    bool ReadRun()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; var g = Gamepad.current;
        return (k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed))
            || (g != null && g.leftStickButton.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    bool ReadWave()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; var g = Gamepad.current;
        return (k != null && k.eKey.wasPressedThisFrame)
            || (g != null && g.buttonNorth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
