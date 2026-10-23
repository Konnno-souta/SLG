using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Conversation
{
    public enum TriggerMode
    {
        OnTouch,     // 触れたらすぐ開始
        PressButton  // 範囲内でボタンを押したら開始
    }

    public enum TriggerSource
    {
        CsvStory,    // CSV のストーリーID
        TextScript   // テキスト形式の会話スクリプト
    }

    /// <summary>「話す」表示に出すボタン名をどちらに合わせるか</summary>
    public enum PromptDevice
    {
        Auto,      // 最後に操作した方（キーボード／コントローラー）に合わせる
        Keyboard,
        Gamepad
    }

    /// <summary>
    /// プレイヤーが触れたら会話を始めるオブジェクト。
    /// 「Is Trigger」をオンにした Collider と一緒に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public class ConversationTrigger : MonoBehaviour
    {
        [Header("再生する会話")]
        [Tooltip("空ならシーン内の ConversationPlayer を自動で探す")]
        public ConversationPlayer conversationPlayer;
        public TriggerSource source = TriggerSource.CsvStory;
        [Tooltip("空なら ConversationPlayer の Csv Asset を使う")]
        public TextAsset csvAsset;
        [Tooltip("再生するストーリーID（章タイトルも、このIDで Database から自動表示される）")]
        public string storyId = "story_001";
        [Tooltip("Source が TextScript の時に再生する会話")]
        public TextAsset scriptAsset;

        [Header("触れる範囲")]
        [Tooltip("「Is Trigger」がオンの Collider が無ければ、実行時に球の範囲を自動で追加する")]
        public bool autoAddTriggerArea = true;
        [Tooltip("自動で追加する範囲の半径")]
        public float triggerRadius = 1.5f;

        [Header("起動のしかた")]
        public TriggerMode mode = TriggerMode.OnTouch;
        [Tooltip("このタグのオブジェクトだけに反応する（空なら何にでも反応）")]
        public string playerTag = "Player";
        [Tooltip("一度だけ起動する")]
        public bool playOnce = false;
        [Tooltip("会話が終わってから、もう一度起動できるまでの秒数")]
        public float cooldown = 1f;
        [Tooltip("触れてから会話が始まるまでの秒数")]
        public float startDelay = 0f;
        [Tooltip("ConversationPlayer の Play On Start を自動でオフにする（シーン開始時に勝手に始まらないように）")]
        public bool disablePlayOnStart = true;

        [Header("プレイヤー")]
        [Tooltip("会話中はプレイヤーを動けなくする（SimplePlayerController / GridPlayerController の場合）")]
        public bool lockPlayer = true;
        [Tooltip("会話開始時にプレイヤーをこのオブジェクトの方へ向ける")]
        public bool facePlayerToObject = true;
        [Tooltip("会話中だけ止めたい他のスクリプト（カメラ操作など）")]
        public Behaviour[] disableDuringConversation;

        [Header("話しかけ表示（Press Button の時）")]
        public bool showPrompt = true;
        [Tooltip("{button} の所が、キー名（E）やボタン名（A）に置き換わる")]
        public string promptFormat = "[{button}] 話す";
        [Tooltip("表示するボタン名を、キーボードとコントローラーのどちらに合わせるか")]
        public PromptDevice promptDevice = PromptDevice.Auto;
        [Tooltip("このオブジェクトからの表示位置")]
        public Vector3 promptOffset = new Vector3(0f, 2.2f, 0f);
        public float promptSize = 3f;
        public Color promptColor = Color.white;
        [Tooltip("空なら ConversationPlayer の Font を使う")]
        public TMP_FontAsset promptFont;

        [Header("入力（Press Button の時）")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("キーボードで話しかけるキー（None で使わない）")]
        public Key interactKey = Key.E;
#else
        [Tooltip("キーボードで話しかけるキー（None で使わない）")]
        public KeyCode interactKey = KeyCode.E;
#endif
        [Tooltip("Xbox コントローラーで話しかけるボタン")]
        public XboxButton interactPadButton = XboxButton.A;

        [Header("終わった後")]
        [Tooltip("会話の後、このオブジェクトを非表示にする")]
        public bool deactivateAfterPlay = false;

        [Header("確認用")]
        [Tooltip("何が触れたか・起動できない理由を Console に出す")]
        public bool debugLog = true;

        [Header("イベント")]
        public UnityEvent onConversationStarted;
        public UnityEvent onConversationFinished;

        /// <summary>一度でも起動したか</summary>
        public bool HasPlayed { get; private set; }
        /// <summary>このトリガーの会話が進行中か</summary>
        public bool IsRunning { get; private set; }

        GameObject playerInRange;
        float cooldownUntil;
        TextMeshPro prompt;
        IConversationPlayerControl lockedController;
        bool usingGamepad;

        // =====================================================================
        // ライフサイクル
        // =====================================================================

        void Reset()
        {
            // 触れる範囲（Is Trigger の Collider）が無ければ作る。
            // Cube などの普通の Collider は、ぶつかる用としてそのまま残す
            if (!HasTriggerCollider() && !GetComponent<Collider2D>()) AddTriggerArea();
        }

        void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            usingGamepad = Gamepad.current != null;
#endif
            if (!conversationPlayer) conversationPlayer = FindAnyObjectByType<ConversationPlayer>();
            if (conversationPlayer && disablePlayOnStart) conversationPlayer.playOnStart = false;

            if (!HasTriggerCollider())
            {
                if (autoAddTriggerArea && !GetComponent<Collider2D>())
                {
                    AddTriggerArea();
                    Log($"「Is Trigger」の Collider が無かったので、半径 {triggerRadius} の範囲を自動で追加しました");
                }
                else
                {
                    Debug.LogWarning($"[ConversationTrigger] {name} に「Is Trigger」がオンの Collider がありません。触れても反応しません", this);
                }
            }

            if (!conversationPlayer)
                Debug.LogWarning($"[ConversationTrigger] {name}: シーンに ConversationPlayer がありません", this);
        }

        void OnDisable()
        {
            playerInRange = null;
            SetPromptVisible(false);
        }

        void Update()
        {
            UpdateLastDevice();
            bool canStart = playerInRange && CanStart();

            if (mode == TriggerMode.PressButton && canStart && InteractPressed())
                StartCoroutine(Run(playerInRange));

            SetPromptVisible(showPrompt && mode == TriggerMode.PressButton && canStart);
        }

        void LateUpdate()
        {
            // 話しかけ表示をカメラに向ける
            if (prompt && prompt.gameObject.activeSelf && Camera.main)
                prompt.transform.rotation = Camera.main.transform.rotation;
        }

        // =====================================================================
        // 触れた・離れた（3D と 2D の両方に対応）
        // =====================================================================

        void OnTriggerEnter(Collider other) => Enter(other.gameObject);
        void OnTriggerExit(Collider other) => Exit(other.gameObject);
        void OnTriggerEnter2D(Collider2D other) => Enter(other.gameObject);
        void OnTriggerExit2D(Collider2D other) => Exit(other.gameObject);

        void Enter(GameObject other)
        {
            var player = FindPlayerRoot(other);
            if (!player)
            {
                Log($"「{other.name}」が触れましたが、タグが「{playerTag}」ではないので無視しました（タグ: {other.tag}）");
                return;
            }
            playerInRange = player;

            string reason = WhyCannotStart();
            if (reason != null) { Log($"「{player.name}」が触れましたが起動しません: {reason}"); return; }

            if (mode == TriggerMode.OnTouch)
            {
                Log($"「{player.name}」が触れたので会話を開始します");
                StartCoroutine(Run(player));
            }
            else Log($"「{player.name}」が範囲に入りました。ボタンを押すと会話を開始します");
        }

        void Exit(GameObject other)
        {
            var player = FindPlayerRoot(other);
            if (player && player == playerInRange) playerInRange = null;
        }

        /// <summary>触れたものがプレイヤーなら、そのプレイヤーのオブジェクトを返す</summary>
        GameObject FindPlayerRoot(GameObject other)
        {
            var controller = other.GetComponentInParent<IConversationPlayerControl>() as Component;
            if (string.IsNullOrEmpty(playerTag))
                return controller ? controller.gameObject : other;

            if (other.CompareTag(playerTag)) return controller ? controller.gameObject : other;
            if (controller && controller.CompareTag(playerTag)) return controller.gameObject;
            var root = other.transform.root.gameObject;
            return root.CompareTag(playerTag) ? root : null;
        }

        // =====================================================================
        // 会話の実行
        // =====================================================================

        /// <summary>今起動できるか</summary>
        public bool CanStart() => WhyCannotStart() == null;

        /// <summary>起動できない理由（起動できるなら null）</summary>
        string WhyCannotStart()
        {
            if (!isActiveAndEnabled) return "このコンポーネントが無効になっています";
            if (IsRunning) return "このオブジェクトの会話が進行中です";
            if (playOnce && HasPlayed) return "Play Once がオンで、すでに一度起動しています";
            if (Time.time < cooldownUntil) return "Cooldown 中です";
            if (!conversationPlayer) return "シーンに ConversationPlayer がありません";
            if (conversationPlayer.IsPlaying) return "別の会話が再生中です（ConversationPlayer の Play On Start がオンになっていないか確認してください）";
            return null;
        }

        void Log(string message)
        {
            if (debugLog) Debug.Log($"[ConversationTrigger] {name}: {message}", this);
        }

        void AddTriggerArea()
        {
            var col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            // 拡大縮小されたオブジェクトでも、見た目どおりの半径になるようにする
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z), 0.0001f);
            col.radius = triggerRadius / scale;
        }

        /// <summary>外部（スクリプトやイベント）から起動する</summary>
        public void Play()
        {
            if (CanStart()) StartCoroutine(Run(playerInRange));
        }

        IEnumerator Run(GameObject player)
        {
            IsRunning = true;
            SetPromptVisible(false);
            LockPlayer(player, true);

            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            var cp = conversationPlayer;
            bool finished = false;
            UnityAction onFinished = () => finished = true;
            cp.onFinished.AddListener(onFinished);

            if (source == TriggerSource.CsvStory)
            {
                var csv = csvAsset ? csvAsset : cp.csvAsset;
                if (!csv) Debug.LogWarning($"[ConversationTrigger] {name}: CSV が設定されていません（このオブジェクトの Csv Asset か、ConversationPlayer の Csv Asset に入れてください）", this);
                cp.PlayStory(csv, storyId);
            }
            else
                cp.Play(scriptAsset);

            if (cp.IsPlaying)
            {
                HasPlayed = true;
                onConversationStarted?.Invoke();
                while (!finished) yield return null;
            }
            else
            {
                Debug.LogWarning($"[ConversationTrigger] {name}: 会話を開始できませんでした（CSV・ストーリーID・スクリプトの設定を確認してください）", this);
            }

            cp.onFinished.RemoveListener(onFinished);
            LockPlayer(player, false);
            IsRunning = false;
            cooldownUntil = Time.time + cooldown;

            if (HasPlayed) onConversationFinished?.Invoke();
            if (HasPlayed && deactivateAfterPlay) gameObject.SetActive(false);
        }

        void LockPlayer(GameObject player, bool locked)
        {
            if (locked)
            {
                lockedController = player ? player.GetComponentInParent<IConversationPlayerControl>() : null;
                if (lockedController != null)
                {
                    if (lockPlayer) lockedController.SetLocked(true);
                    if (facePlayerToObject) lockedController.FaceTowards(transform.position);
                }
            }
            else if (lockedController != null)
            {
                if (lockPlayer) lockedController.SetLocked(false);
                lockedController = null;
            }

            if (disableDuringConversation != null)
                foreach (var b in disableDuringConversation)
                    if (b) b.enabled = !locked;
        }

        // =====================================================================
        // 話しかけ表示
        // =====================================================================

        void SetPromptVisible(bool visible)
        {
            if (!visible)
            {
                if (prompt) prompt.gameObject.SetActive(false);
                return;
            }

            if (!prompt)
            {
                var go = new GameObject("TalkPrompt");
                go.transform.SetParent(transform, false);
                prompt = go.AddComponent<TextMeshPro>();
                prompt.alignment = TextAlignmentOptions.Center;
                prompt.textWrappingMode = TextWrappingModes.NoWrap;
            }

            var f = promptFont ? promptFont : conversationPlayer ? conversationPlayer.font : null;
            if (f) prompt.font = f;
            prompt.text = promptFormat.Replace("{button}", CurrentButtonLabel());
            prompt.fontSize = promptSize;
            prompt.color = promptColor;
            prompt.transform.localPosition = promptOffset;
            prompt.gameObject.SetActive(true);
        }

        // =====================================================================
        // 入力
        // =====================================================================

        bool ShowGamepadLabel =>
            promptDevice == PromptDevice.Gamepad || (promptDevice == PromptDevice.Auto && usingGamepad);

        /// <summary>「話す」表示に出すボタン名</summary>
        string CurrentButtonLabel()
        {
            if (ShowGamepadLabel) return interactPadButton.ToString();
            return interactKey.ToString();
        }

#if ENABLE_INPUT_SYSTEM
        bool InteractPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && interactKey != Key.None && kb[interactKey].wasPressedThisFrame) return true;
            foreach (var gp in Gamepad.all)
            {
                var b = XboxPad.GetButton(gp, interactPadButton);
                if (b != null && b.wasPressedThisFrame) return true;
            }
            return false;
        }

        /// <summary>最後に操作したのがキーボードかコントローラーかを覚える</summary>
        void UpdateLastDevice()
        {
            foreach (var gp in Gamepad.all)
                if (AnyPadInput(gp)) { usingGamepad = true; return; }

            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { usingGamepad = false; return; }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) usingGamepad = false;
        }

        static bool AnyPadInput(Gamepad gp)
        {
            foreach (XboxButton b in System.Enum.GetValues(typeof(XboxButton)))
            {
                var c = XboxPad.GetButton(gp, b);
                if (c != null && c.wasPressedThisFrame) return true;
            }
            return gp.leftStick.ReadValue().sqrMagnitude > 0.25f || gp.dpad.ReadValue().sqrMagnitude > 0.25f;
        }
#else
        bool InteractPressed()
        {
            if (interactKey != KeyCode.None && Input.GetKeyDown(interactKey)) return true;
            var pad = XboxPad.LegacyKeyCode(interactPadButton);
            return pad != KeyCode.None && Input.GetKeyDown(pad);
        }

        void UpdateLastDevice()
        {
            for (var k = KeyCode.JoystickButton0; k <= KeyCode.JoystickButton9; k++)
                if (Input.GetKeyDown(k)) { usingGamepad = true; return; }
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f)
            {
                // 矢印キー・WASD でも動くので、キーボードが押されていない時だけコントローラー扱い
                if (!Input.anyKey) usingGamepad = true;
            }
            if (Input.anyKeyDown) usingGamepad = false;
        }
#endif

        bool HasTriggerCollider()
        {
            foreach (var c in GetComponents<Collider>()) if (c.isTrigger) return true;
            foreach (var c in GetComponents<Collider2D>()) if (c.isTrigger) return true;
            return false;
        }

        // シーンビューで範囲を見やすくする
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            foreach (var c in GetComponents<Collider>())
            {
                if (!c.isTrigger) continue;
                var b = c.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}