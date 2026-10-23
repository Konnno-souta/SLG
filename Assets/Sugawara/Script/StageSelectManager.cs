using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ステージ選択シーンの進行役
// ・Stageをクリック → Playerが移動
// ・到着したら MenuPanel と StageInfo を表示
// ・Start → DiaLogPanel を表示
// ・はい → そのステージのシーンへ / いいえ → ダイアログを閉じる
public class StageSelectManager : MonoBehaviour
{
    [SerializeField] MapCharacter player;
    [SerializeField] GameObject menuPanel;
    [SerializeField] StageInfoWindow stageInfo;
    [SerializeField] GameObject dialogPanel;

    StagePoint selectedStage;

    void Start()
    {
        // 最初は全部隠しておく
        menuPanel.SetActive(false);
        stageInfo.Hide();
        dialogPanel.SetActive(false);

        player.OnArrived += OnPlayerArrived;
    }

    void OnDestroy()
    {
        player.OnArrived -= OnPlayerArrived;
    }

    void Update()
    {
        // ダイアログ表示中・移動中はステージを選べない
        if (dialogPanel.activeSelf || player.IsMoving) return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        // UIの上をクリックしたときは無視（ボタンを押したのにステージ選択されるのを防ぐ）
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // クリックした位置にあるStageを調べる
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            StagePoint stage = hit.collider.GetComponentInParent<StagePoint>();
            if (stage != null) SelectStage(stage);
        }
    }

    void SelectStage(StagePoint stage)
    {
        selectedStage = stage;
        menuPanel.SetActive(false);
        stageInfo.Hide();
        player.MoveTo(stage.StandPosition);
    }

    void OnPlayerArrived()
    {
        menuPanel.SetActive(true);
        stageInfo.Show(selectedStage.data);
    }

    // ---- ボタンの OnClick に登録する ----
    public void OnClickStart()
    {
        dialogPanel.SetActive(true);
    }

    public void OnClickYes()
    {
        SceneManager.LoadScene("Main");
    }

    public void OnClickNo()
    {
        dialogPanel.SetActive(false);
    }
}