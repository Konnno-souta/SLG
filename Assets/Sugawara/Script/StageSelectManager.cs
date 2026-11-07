using System.Collections;
using System.Collections.Generic;
using Conversation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
 
// ステージ選択シーンの進行役
// ・最初は Start Stage（Stage1）に立った状態から始まる（ステージから戻ったときはそのステージ）
// ・Stageをクリック → Playerが移動（パネルは隠れる）。まだ開放されていないステージには行けない
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

    [Header("会話に入る前の演出（演出の追加）")]
    [Tooltip("画面を暗くする MapScreenFader（空なら暗くしない）")]
    [SerializeField] MapScreenFader screenFader;
    [Tooltip("カメラをどこまで寄せるか（1 = 今の距離、0.4 = 4割の距離まで寄る）")]
    [SerializeField, Range(0.1f, 1f)] float zoomRatio = 0.4f;
    [Tooltip("寄りながら暗くなるまでの秒数")]
    [SerializeField] float transitionDuration = 1.2f;

    StagePoint selectedStage;
    bool panelsShown;
    bool loading;   // 会話中〜シーンに移るまで（操作を受け付けない）

    void Awake()
    {
        // ステージから戻ってきたときは、そのステージに立たせる（道の追加）
        selectedStage = FindStage(StageProgress.CurrentStageId);
        if (selectedStage == null || !selectedStage.IsUnlocked) selectedStage = startStage;

        if (selectedStage != null) player.Warp(selectedStage.StandPosition);
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

        // 道の開放演出中は操作を受け付けない（道の追加）
        if (StageRoad.AnyOpening) return;

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

        // WASD（方向入力）で、その方向にあるステージへ移動する（WASDの追加）
        if (PressedMove(out Vector2 dir))
        {
            StagePoint next = FindStageInDirection(dir);
            if (next != null) SelectStage(next);
        }
    }

    // 今いるステージから見て、画面上で dir の方向にある一番近いステージを探す（WASDの追加）
    // 開放されていないステージは候補にしない
    StagePoint FindStageInDirection(Vector2 dir)
    {
        if (selectedStage == null || Camera.main == null) return null;

        Camera cam = Camera.main;
        Vector2 from = cam.WorldToScreenPoint(selectedStage.StandPosition);

        StagePoint best = null;
        float bestScore = float.MaxValue;

        foreach (var s in FindObjectsByType<StagePoint>(FindObjectsSortMode.None))
        {
            if (s == selectedStage || !s.IsUnlocked) continue;

            Vector2 to = cam.WorldToScreenPoint(s.StandPosition);
            Vector2 diff = to - from;
            float dist = diff.magnitude;
            if (dist < 1f) continue;

            float dot = Vector2.Dot(diff / dist, dir);
            if (dot < 0.5f) continue; // 押した方向から60度以上ずれていたら対象外

            // 近くて、押した方向にまっすぐなほど優先
            float score = dist * (2f - dot);
            if (score < bestScore)
            {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    void SelectStage(StagePoint stage)
    {
        if (stage == selectedStage) return; // 今いるステージなら何もしない

        // まだ開放されていないステージには行けない（道の追加）
        if (!stage.IsUnlocked)
        {
            Debug.Log($"{stage.name} はまだ開放されていません");
            return;
        }

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

    // ステージ番号から StagePoint を探す（道の追加）
    StagePoint FindStage(int stageId)
    {
        if (stageId < 0) return null;
        foreach (var s in FindObjectsByType<StagePoint>(FindObjectsSortMode.None))
            if (s.stageId == stageId) return s;
        return null;
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

        // どのステージを遊ぶか覚えておく（クリア判定とマップに戻った時の立ち位置に使う）（道の追加）
        StageProgress.CurrentStageId = selectedStage.stageId;

        StartCoroutine(TransitionThenConversation(selectedStage));   // 演出のあとに会話へ（演出の追加）
    }

    // カメラを Player へ寄せながら画面を暗くして、そのあと会話へ（演出の追加）
    IEnumerator TransitionThenConversation(StagePoint stage)
    {
        loading = true;

        // ダイアログとパネルを閉じる
        dialogPanel.SetActive(false);
        SetPanels(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        // 寄るのと暗くなるのを同時に行う
        Coroutine zoom = null;
        if (cameraIntro != null) zoom = StartCoroutine(cameraIntro.ZoomTo(zoomRatio, transitionDuration));

        if (screenFader != null) yield return StartCoroutine(screenFader.FadeOut(transitionDuration));
        else yield return new WaitForSeconds(transitionDuration);

        if (zoom != null) yield return zoom;

        // ここから先は今までの会話の処理
        yield return StartCoroutine(ConversationThenLoad(stage));
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

    // ---- デバッグ（再生中に Inspector の「︙」から実行）（道の追加） ----
    [ContextMenu("デバッグ：今いるステージをクリアにする")]
    void DebugClearCurrent()
    {
        if (selectedStage != null) StageProgress.SetCleared(selectedStage.stageId);
    }

    [ContextMenu("デバッグ：進行状況をリセット")]
    void DebugReset()
    {
        StageProgress.ResetAll();
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

    // 移動する方向（WASD・矢印キー）（WASDの追加）
    bool PressedMove(out Vector2 dir)
    {
        dir = Vector2.zero;

        var k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) dir = Vector2.up;
            else if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) dir = Vector2.down;
            else if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) dir = Vector2.left;
            else if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) dir = Vector2.right;
        }

        // コントローラー対応時はここに追加する（例：十字キー）
        // var g = Gamepad.current;
        // if (g != null)
        // {
        //     if (g.dpad.up.wasPressedThisFrame) dir = Vector2.up;
        //     else if (g.dpad.down.wasPressedThisFrame) dir = Vector2.down;
        //     else if (g.dpad.left.wasPressedThisFrame) dir = Vector2.left;
        //     else if (g.dpad.right.wasPressedThisFrame) dir = Vector2.right;
        // }

        return dir != Vector2.zero;
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