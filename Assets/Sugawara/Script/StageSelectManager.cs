using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ステージ選択シーンの進行役
// ・最初は Start Stage（Stage1）に立った状態から始まる
// ・Stageをクリック → Playerが移動（パネルは隠れる）
// ・P キー → MenuPanel と StageInfo を出す／しまう
// ・Start → DiaLogPanel を表示
// ・はい → そのステージのシーンへ / いいえ → ダイアログを閉じる
[DefaultExecutionOrder(-100)] // カメラより先にプレイヤーを初期位置へ置くため
public class StageSelectManager : MonoBehaviour
{
    [SerializeField] MapCharacter player;
    [SerializeField] StagePoint startStage;     // 最初に立っているステージ（Stage1）
    [SerializeField] MenuSlider menuPanel;
    [SerializeField] StageInfoWindow stageInfo;
    [SerializeField] GameObject dialogPanel;
    [SerializeField] CameraIntro cameraIntro;

    StagePoint selectedStage;
    bool panelsShown;

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
        if (string.IsNullOrEmpty(selectedStage.SceneName))
        {
            Debug.LogWarning($"{selectedStage.name} の Scene が設定されていません", selectedStage);
            dialogPanel.SetActive(false);
            return;
        }
        SceneManager.LoadScene(selectedStage.SceneName);
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