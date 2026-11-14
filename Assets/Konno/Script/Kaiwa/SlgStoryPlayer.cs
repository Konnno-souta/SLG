using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Conversation
{
    /// <summary>
    /// 「SLG_ストーリー会話テキスト」の CSV（IntroText / AfterText）を、会話パートで流す。
    ///   PlayIntro(1) … ステージ1の前の会話
    ///   PlayAfter(1) … ステージ1のクリア後の会話
    /// 立ち絵（2D_ilust_ID）は 左・真ん中・右 に出し、リアクション（Reaction_ID）は頭の上の記号にする。
    /// ConversationPlayer と同じシーンに置いて使う。
    /// </summary>
    public class SlgStoryPlayer : MonoBehaviour
    {
        [Header("つなぐもの")]
        [Tooltip("会話を流す ConversationPlayer（空なら自動で探す）")]
        public ConversationPlayer player;

        [Header("CSV（Excel で「CSV UTF-8」で保存したもの）")]
        [Tooltip("ステージ前の会話（IntroText_ID の表）")]
        public TextAsset introCsv;
        [Tooltip("クリア後の会話（AfterText_ID の表）")]
        public TextAsset afterCsv;
        [Tooltip("2Dイラスト一覧（2D01,クラン_通常 …）。空なら Intro / After の CSV の右側に貼ってある表を使う")]
        public TextAsset illustListCsv;
        [Tooltip("リアクション一覧（Rea1,！マーク …）。空なら Reaction Emotes の設定だけを使う")]
        public TextAsset reactionListCsv;

        [Header("立ち絵の位置（DisplayChara1, 2, 3）")]
        [Tooltip("left / center / right、または 0〜1 の数字（画面の左端が 0、右端が 1）")]
        public string[] slotPositions = { "left", "center", "right" };

        [Header("リアクション → 頭の上の記号")]
        public List<SlgReactionEmote> reactionEmotes = new List<SlgReactionEmote>
        {
            new SlgReactionEmote("Rea1", "!"),
            new SlgReactionEmote("Rea2", "?"),
            new SlgReactionEmote("Rea3", "♪"),
            new SlgReactionEmote("Rea4", "☆"),
            new SlgReactionEmote("Rea5", ""),   // 非表示
        };

        [Header("名前")]
        [Tooltip("この名前の行は、名前なしのナレーションにする（Chara_ID が空の行も）")]
        public List<string> narratorNames = new List<string> { "ログ", "ナレーション" };
        [Tooltip("CSV の名前と Database のキャラID が違う時だけ設定する（同じなら空でいい）")]
        public List<SlgNameAlias> nameAliases = new List<SlgNameAlias>();
        [Tooltip("本文の中の改行の印")]
        public List<string> newlineMarks = new List<string> { "/n", "n/" };

        [Header("本文に書かれた演出メモ")]
        [Tooltip("上から順に調べて、最初に当てはまったルールを使う。セリフにしたくない行（画面暗転・〇〇エフェクト など）はここで消す")]
        public List<SlgDirectionRule> directionRules = new List<SlgDirectionRule>
        {
            new SlgDirectionRule("画面暗転", SlgDirectionRule.MatchMode.Exact, "@fade out 0.5 | @fade in 0.5"),
            new SlgDirectionRule("目を覚ます", SlgDirectionRule.MatchMode.Exact, "@fade out 0 | @fade in 1.2"),
            new SlgDirectionRule("", SlgDirectionRule.MatchMode.Contains, "", "サウンド"),
            new SlgDirectionRule("エフェクト", SlgDirectionRule.MatchMode.Contains, ""),
            new SlgDirectionRule("地鳴り", SlgDirectionRule.MatchMode.Contains, ""),
            new SlgDirectionRule("出現", SlgDirectionRule.MatchMode.Contains, ""),
            new SlgDirectionRule("画面右端へ", SlgDirectionRule.MatchMode.Contains, ""),
            new SlgDirectionRule("敵ゾロゾロ", SlgDirectionRule.MatchMode.Exact, ""),
            new SlgDirectionRule("市街地へ", SlgDirectionRule.MatchMode.Exact, ""),
        };

        [Header("その他")]
        [Tooltip("会話の最初に、前の会話で出ていたキャラを全員消す")]
        public bool clearCharactersAtStart = true;
        [Tooltip("「～城内～」のように ～ で囲んだ本文は、話者が書いてあっても名前なしのナレーションにする")]
        public bool placeCardAsNarration = true;
        [Tooltip("DisplayChara が空欄でも、話者が画面にいなければ出す。空いている位置に出し、3人とも埋まっていたら一番長く話していないキャラと入れ替える（同じ位置に重ならない）")]
        public bool autoShowSpeakers = true;
        [Tooltip("変換した結果（会話スクリプト）をコンソールに出す。うまく出ない時の確認用")]
        public bool logScript = false;

        [Header("ゲーム開始時に自動で流す（ステージのシーンに置く時など）")]
        public bool playOnStart = false;
        public SlgStoryKind startKind = SlgStoryKind.Intro;
        public int startStageId = 1;

        [Header("確認用（画面左下にボタンを出す）")]
        public bool showTestButtons = true;
        public float testButtonScale = 1.5f;

        [Header("イベント")]
        [Tooltip("会話が終わった時")]
        public UnityEvent onFinished;

        /// <summary>会話を流している最中か</summary>
        public bool IsPlaying => player && player.IsPlaying;
        public ConversationPlayer Player => player;

        int testStage = 1;
        bool waitingEnd;
        bool restoreAutoShow = true;

        // =====================================================================
        // 公開API
        // =====================================================================

        /// <summary>ステージ前の会話を流す</summary>
        public bool PlayIntro(int stageId) => Play(SlgStoryKind.Intro, stageId);

        /// <summary>クリア後の会話を流す</summary>
        public bool PlayAfter(int stageId) => Play(SlgStoryKind.After, stageId);

        /// <summary>会話を流す。流せなかった時（CSV が無い・その Stage_ID が無い）は false</summary>
        public bool Play(SlgStoryKind kind, int stageId)
        {
            if (!player) player = FindAnyObjectByType<ConversationPlayer>(FindObjectsInactive.Include);
            if (!player) { Debug.LogWarning("[SlgStory] ConversationPlayer が見つかりません", this); return false; }

            string script = BuildScript(kind, stageId);
            if (string.IsNullOrEmpty(script)) return false;

            if (!player.gameObject.activeInHierarchy) player.gameObject.SetActive(true);
            if (!player.enabled) player.enabled = true;
            // 位置は CSV（とこちらの自動配置）で決めるので、ConversationPlayer の自動登場は会話中だけ止める
            // （止めないと、ConversationPlayer が別の位置に勝手に出して、同じ位置に重なることがある）
            if (!waitingEnd) restoreAutoShow = player.autoShowSpeakers;
            player.autoShowSpeakers = false;
            player.Play(script);
            waitingEnd = true;
            return true;
        }

        /// <summary>CSV から会話スクリプト（文字）を作る。セリフが無ければ null</summary>
        public string BuildScript(SlgStoryKind kind, int stageId)
        {
            var csv = kind == SlgStoryKind.Intro ? introCsv : afterCsv;
            string label = kind == SlgStoryKind.Intro ? "IntroText" : "AfterText";
            if (!csv) { Debug.LogWarning($"[SlgStory] {label} の CSV が入っていません", this); return null; }

            var conv = new SlgStoryConverter
            {
                slotPositions = slotPositions != null && slotPositions.Length >= 3 ? slotPositions : new[] { "left", "center", "right" },
                narratorNames = narratorNames,
                newlineMarks = newlineMarks,
                reactionEmotes = reactionEmotes,
                nameAliases = nameAliases,
                directionRules = directionRules,
                clearCharactersAtStart = clearCharactersAtStart,
                placeCardAsNarration = placeCardAsNarration,
                autoShowSpeakers = autoShowSpeakers,
                knownCharacters = KnownCharacters(),
            };

            // 2Dイラスト一覧（別の CSV が無ければ、会話の CSV の右側の表から読む）
            if (illustListCsv) conv.LoadIllustList(illustListCsv.text);
            else
            {
                if (introCsv) conv.LoadIllustList(introCsv.text);
                if (afterCsv) conv.LoadIllustList(afterCsv.text);
            }
            if (reactionListCsv) conv.LoadReactionList(reactionListCsv.text);
            else
            {
                if (introCsv) conv.LoadReactionList(introCsv.text);
                if (afterCsv) conv.LoadReactionList(afterCsv.text);
            }

            var rows = conv.ParseStory(csv.text, label);
            string script = conv.BuildScript(rows, stageId.ToString(), label);

            foreach (var w in conv.warnings) Debug.LogWarning("[SlgStory] " + w, this);
            if (logScript) Debug.Log($"[SlgStory] {label} Stage {stageId}\n{script}", this);

            bool hasLine = false;
            foreach (var line in script.Split('\n'))
                if (line.Length > 0 && !line.StartsWith("@")) { hasLine = true; break; }
            return hasLine ? script : null;
        }

        /// <summary>Database にいるキャラのID</summary>
        HashSet<string> KnownCharacters()
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (player && player.database)
                foreach (var c in player.database.characters)
                    if (c != null && !string.IsNullOrEmpty(c.id)) set.Add(c.id.Trim());
            return set;
        }

        // =====================================================================
        // Unity
        // =====================================================================

        void Start()
        {
            testStage = Mathf.Max(1, startStageId);
            if (playOnStart) Play(startKind, startStageId);
        }

        void Update()
        {
            if (waitingEnd && player && !player.IsPlaying)
            {
                waitingEnd = false;
                player.autoShowSpeakers = restoreAutoShow;
                onFinished?.Invoke();
            }
        }

        void OnGUI()
        {
            if (!showTestButtons) return;
            var old = GUI.matrix;
            float s = testButtonScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            float y = Screen.height / s - 50f;
            if (GUI.Button(new Rect(10, y, 36, 40), "<")) testStage = Mathf.Max(1, testStage - 1);
            GUI.Box(new Rect(48, y, 90, 40), "Stage " + testStage);
            if (GUI.Button(new Rect(140, y, 36, 40), ">")) testStage++;
            if (GUI.Button(new Rect(182, y, 90, 40), "Intro")) PlayIntro(testStage);
            if (GUI.Button(new Rect(276, y, 90, 40), "After")) PlayAfter(testStage);

            GUI.matrix = old;
        }
    }
}