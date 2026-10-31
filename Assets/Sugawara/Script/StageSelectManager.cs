using System.Collections;
using System.Collections.Generic;
using Conversation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ステージ選択シーンの進行役
// ・最初は Start Stage（Stage1）に立った状態から始まる
// ・Stageをクリック → Playerが移動（パネルは隠れる）
// ・P キー → MenuPanel と StageInfo を出す／しまう
// ・Start → DiaLogPanel を表示
// ・はい → 会話パート（設定があれば）→ 会話が終わったらそのステージのシーンへ / いいえ → ダイアログを閉じる
[DefaultExecutionOrder(-100)] // カメラより先にプレイヤーを初期位置へ置くため
public class StageSelectManager : MonoBehaviour
{
    [SerializeField] MapCharacter player;
    [SerializeField] StagePoint startStage;     // 最初に立っているステージ（Stage1）
    [SerializeField] MenuSlider menuPanel;
    [SerializeField] StageInfoWindow stageInfo;
    [SerializeField] GameObject dialogPanel;
    [SerializeField] CameraIntro cameraIntro;

    // ---- シーンに移る前の会話 ----
    [System.Serializable]
    public class StageStory
    {
        [Tooltip("このステージを選んだ時に")]
        public StagePoint stage;
        [Tooltip("流す会話のストーリーID（CSV の story_id）。空なら会話なし")]
        public string storyId;
        [Tooltip("このステージだけ別の CSV を使う時に入れる（空なら Conversation Player の Csv Asset）")]
        public TextAsset csv;
    }

    [Header("シーンに移る前の会話")]
    [Tooltip("会話を流す ConversationPlayer（空なら会話なしで、すぐシーンへ）")]
    [SerializeField] ConversationPlayer conversationPlayer;
    [Tooltip("ステージごとの会話。ここに無いステージは Default Story Id を使う")]
    [SerializeField] List<StageStory> stageStories = new List<StageStory>();
    [Tooltip("ステージごとの設定が無い時に流す会話（空なら会話なしで、すぐシーンへ）")]
    [SerializeField] string defaultStoryId = "";
    [Tooltip("会話が終わってからシーンに移るまでの間（秒）")]
    [SerializeField] float delayAfterConversation = 0.2f;

    StagePoint selectedStage;
    bool panelsShown;
    bool loading;   // 会話中〜シーンに移るまで（操作を受け付けない）

    void Awake()
    {
        // 最初から Start Stage に立たせる
        selectedStage = startStage;
        if (startStage != null) player.Warp(startStage.StandPosition);
    }

    void Start()
    {
        stageInfo.Hide();
        dialogPanel.SetActive(false);
    }

    void Update()
    {
        // カメラ演出中は操作を受け付けない
        if (cameraIntro != null && cameraIntro.IsPlaying) return;

        // 会話中・シーンに移る途中は操作を受け付けない
        if (loading) return;

        // ダイアログ表示中・移動中は操作を受け付けない
        if (dialogPanel.activeSelf || player.IsMoving) return;

        if (PressedTogglePanels())
        {
            SetPanels(!panelsShown);
            return;
        }

        if (PressedSelect(out StagePoint stage)) SelectStage(stage);
    }

    void SelectStage(StagePoint stage)
    {
        if (stage == selectedStage) return; // 今いるステージなら何もしない

        selectedStage = stage;
        SetPanels(false);
        player.MoveTo(stage.StandPosition);
    }

    void SetPanels(bool show)
    {
        if (selectedStage == null) return;

        panelsShown = show;
        if (show)
        {
            menuPanel.Show();
            stageInfo.Show(selectedStage.data);
        }
        else
        {
            menuPanel.Hide();
            stageInfo.Hide();
        }
    }

    // ---- ボタンの OnClick に登録する ----
    public void OnClickStart()
    {
        dialogPanel.SetActive(true);
    }

    public void OnClickYes()
    {
        if (loading) return;   // 2回押し防止
        if (string.IsNullOrEmpty(selectedStage.SceneName))
        {
            Debug.LogWarning($"{selectedStage.name} の Scene が設定されていません", selectedStage);
            dialogPanel.SetActive(false);
            return;
        }
        StartCoroutine(ConversationThenLoad(selectedStage));
    }

    // 会話を流して、終わったらシーンへ（会話の設定が無ければすぐシーンへ）
    IEnumerator ConversationThenLoad(StagePoint stage)
    {
        loading = true;
        string sceneName = stage.SceneName;

        string storyId;
        TextAsset csv;
        GetStory(stage, out storyId, out csv);
        if (conversationPlayer != null && !string.IsNullOrEmpty(storyId))
        {
            // ダイアログとパネルを閉じてから会話
            dialogPanel.SetActive(false);
            SetPanels(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (csv != null) conversationPlayer.PlayStory(csv, storyId);
            else conversationPlayer.PlayStory(storyId);

            // 会話が始まるのを少し待つ（ストーリーIDが見つからない時は始まらない）
            float wait = 0f;
            while (!conversationPlayer.IsPlaying && wait < 0.5f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!conversationPlayer.IsPlaying)
                Debug.LogWarning($"会話「{storyId}」が始まらなかったので、そのまま {sceneName} に移ります", this);

            // 会話が終わるまで待つ（ストーリースキップでも終わる）
            while (conversationPlayer.IsPlaying) yield return null;

            if (delayAfterConversation > 0f) yield return new WaitForSecondsRealtime(delayAfterConversation);
        }

        SceneManager.LoadScene(sceneName);
    }

    // このステージで流す会話を探す
    void GetStory(StagePoint stage, out string storyId, out TextAsset csv)
    {
        foreach (var s in stageStories)
        {
            if (s != null && s.stage == stage)
            {
                storyId = s.storyId;
                csv = s.csv;
                return;
            }
        }
        storyId = defaultStoryId;
        csv = null;
    }

    public void OnClickNo()
    {
        dialogPanel.SetActive(false);
    }

    // ---- 入力（操作を増やすときはここだけ変える） ----

    // パネルを出す／しまう
    bool PressedTogglePanels()
    {
        var k = Keyboard.current;
        if (k != null && k.pKey.wasPressedThisFrame) return true;

        // コントローラー対応時はここに追加する（例：Yボタン）
        // var g = Gamepad.current;
        // if (g != null && g.buttonNorth.wasPressedThisFrame) return true;

        return false;
    }

    // ステージを選ぶ（今はマウスクリック）
    bool PressedSelect(out StagePoint stage)
    {
        stage = null;

        var m = Mouse.current;
        if (m == null || !m.leftButton.wasPressedThisFrame) return false;

        // UIの上をクリックしたときは無視
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

        Ray ray = Camera.main.ScreenPointToRay(m.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
            stage = hit.collider.GetComponentInParent<StagePoint>();

        return stage != null;
    }
}