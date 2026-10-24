using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Conversation
{
    /// <summary>会話中のボタンの扱い</summary>
    public enum ButtonWhileTalking
    {
        Keep,      // そのまま
        Disable,   // 押せなくする（グレー表示）
        Hide       // 見えなくする
    }

    /// <summary>
    /// UI のボタンを押したら会話を始める。
    /// Button と同じオブジェクトに付けるだけで、クリック時に自動で再生する。
    /// （他のボタンやイベントから Play() を呼んでも使える）
    /// </summary>
    [DisallowMultipleComponent]
    public class ConversationButton : MonoBehaviour
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

        [Header("起動のしかた")]
        [Tooltip("同じオブジェクトの Button を押した時に、自動で再生する")]
        public bool playOnClick = true;
        [Tooltip("一度だけ起動する")]
        public bool playOnce = false;
        [Tooltip("会話が終わってから、もう一度押せるまでの秒数")]
        public float cooldown = 0.5f;
        [Tooltip("押してから会話が始まるまでの秒数")]
        public float startDelay = 0f;
        [Tooltip("ConversationPlayer の Play On Start を自動でオフにする（シーン開始時に勝手に始まらないように）")]
        public bool disablePlayOnStart = true;

        [Header("ボタン")]
        [Tooltip("会話中のボタンの扱い")]
        public ButtonWhileTalking whileTalking = ButtonWhileTalking.Hide;
        [Tooltip("一度だけ起動する時、会話の後はボタンを消す")]
        public bool hideAfterPlayOnce = true;
        [Tooltip("会話の後、このボタンを選択状態に戻す（コントローラーで続けて操作する時用）")]
        public bool reselectAfterTalk = true;

        [Header("プレイヤー")]
        [Tooltip("会話中はプレイヤーを動けなくする（GridPlayerController / SimplePlayerController の場合）")]
        public bool lockPlayer = true;
        [Tooltip("止めるプレイヤー（空ならタグで探す）")]
        public GameObject player;
        public string playerTag = "Player";
        [Tooltip("会話開始時にプレイヤーをこの方向に向ける（空なら向きを変えない）")]
        public Transform faceTarget;
        [Tooltip("会話中だけ止めたい他のスクリプト（カメラ操作など）")]
        public Behaviour[] disableDuringConversation;

        [Header("確認用")]
        [Tooltip("押された時・起動できない理由を Console に出す")]
        public bool debugLog = true;

        [Header("イベント")]
        public UnityEvent onConversationStarted;
        public UnityEvent onConversationFinished;

        /// <summary>一度でも起動したか</summary>
        public bool HasPlayed { get; private set; }
        /// <summary>このボタンの会話が進行中か</summary>
        public bool IsRunning { get; private set; }

        Button button;
        CanvasGroup group;
        float cooldownUntil;
        IConversationPlayerControl lockedController;

        // =====================================================================
        // ライフサイクル
        // =====================================================================

        void Awake()
        {
            button = GetComponent<Button>();
            if (!conversationPlayer) conversationPlayer = FindAnyObjectByType<ConversationPlayer>();
            if (conversationPlayer && disablePlayOnStart) conversationPlayer.playOnStart = false;

            if (!conversationPlayer)
                Debug.LogWarning($"[ConversationButton] {name}: シーンに ConversationPlayer がありません", this);
            if (playOnClick && !button)
                Debug.LogWarning($"[ConversationButton] {name}: Button がありません。Button と同じオブジェクトに付けるか、他から Play() を呼んでください", this);
        }

        void OnEnable()
        {
            if (playOnClick && button) button.onClick.AddListener(Play);
        }

        void OnDisable()
        {
            if (button) button.onClick.RemoveListener(Play);
        }

        // =====================================================================
        // 会話の実行
        // =====================================================================

        /// <summary>今起動できるか</summary>
        public bool CanStart() => WhyCannotStart() == null;

        string WhyCannotStart()
        {
            if (!isActiveAndEnabled) return "このコンポーネントが無効になっています";
            if (IsRunning) return "このボタンの会話が進行中です";
            if (playOnce && HasPlayed) return "Play Once がオンで、すでに一度起動しています";
            if (Time.unscaledTime < cooldownUntil) return "Cooldown 中です";
            if (!conversationPlayer) return "シーンに ConversationPlayer がありません";
            if (conversationPlayer.IsPlaying) return "別の会話が再生中です";
            return null;
        }

        /// <summary>会話を始める（ボタンのクリックや、他のイベントから呼ぶ）</summary>
        public void Play()
        {
            string reason = WhyCannotStart();
            if (reason != null) { Log($"押されましたが起動しません: {reason}"); return; }
            Log("押されたので会話を開始します");
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsRunning = true;

            // コントローラーの決定ボタンで、もう一度押されないように選択を外す
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);

            SetButtonState(talking: true);
            LockPlayer(true);

            if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

            var cp = conversationPlayer;
            bool finished = false;
            UnityAction onFinished = () => finished = true;
            cp.onFinished.AddListener(onFinished);

            if (source == TriggerSource.CsvStory)
            {
                var csv = csvAsset ? csvAsset : cp.csvAsset;
                if (!csv) Debug.LogWarning($"[ConversationButton] {name}: CSV が設定されていません（このボタンの Csv Asset か、ConversationPlayer の Csv Asset に入れてください）", this);
                cp.PlayStory(csv, storyId);
            }
            else
            {
                cp.Play(scriptAsset);
            }

            if (cp.IsPlaying)
            {
                HasPlayed = true;
                onConversationStarted?.Invoke();
                while (!finished) yield return null;
            }
            else
            {
                Debug.LogWarning($"[ConversationButton] {name}: 会話を開始できませんでした（CSV・ストーリーID・スクリプトの設定を確認してください）", this);
            }

            cp.onFinished.RemoveListener(onFinished);
            LockPlayer(false);
            IsRunning = false;
            cooldownUntil = Time.unscaledTime + cooldown;

            bool finishedForGood = playOnce && HasPlayed;
            if (finishedForGood && hideAfterPlayOnce) SetHidden(true);
            else SetButtonState(talking: false);

            if (HasPlayed) onConversationFinished?.Invoke();

            if (reselectAfterTalk && !finishedForGood && EventSystem.current && button && button.interactable)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        // =====================================================================
        // ボタンの見た目
        // =====================================================================

        void SetButtonState(bool talking)
        {
            switch (whileTalking)
            {
                case ButtonWhileTalking.Disable:
                    if (button) button.interactable = !talking;
                    break;
                case ButtonWhileTalking.Hide:
                    SetHidden(talking);
                    break;
            }
        }

        /// <summary>
        /// ボタンを隠す（SetActive(false) にすると、このスクリプトも止まってしまうので CanvasGroup で隠す）
        /// </summary>
        void SetHidden(bool hidden)
        {
            if (!group && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
            group.interactable = !hidden;
            group.blocksRaycasts = !hidden;
        }

        // =====================================================================
        // プレイヤー
        // =====================================================================

        void LockPlayer(bool locked)
        {
            if (locked)
            {
                var p = player;
                if (!p && !string.IsNullOrEmpty(playerTag))
                {
                    try { p = GameObject.FindWithTag(playerTag); }
                    catch (UnityException) { Debug.LogWarning($"[ConversationButton] タグ「{playerTag}」が Tags に登録されていません", this); }
                }

                lockedController = p ? p.GetComponentInParent<IConversationPlayerControl>() : null;
                if (lockedController != null)
                {
                    if (lockPlayer) lockedController.SetLocked(true);
                    if (faceTarget) lockedController.FaceTowards(faceTarget.position);
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

        void Log(string message)
        {
            if (debugLog) Debug.Log($"[ConversationButton] {name}: {message}", this);
        }
    }
}
