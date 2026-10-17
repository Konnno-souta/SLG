using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ステージ選択シーン全体の進行役
public class StageSelectManager : MonoBehaviour
{
    [SerializeField] StagePoint[] stages; // Stage1, Stage2, Stage3 の順
    [SerializeField] MapCharacter character;
    [SerializeField] MenuListWindow menu;
    [SerializeField] StageInfoWindow stageInfo;
    [SerializeField] ConfirmDialog dialog;
    [SerializeField] string titleSceneName = "TitleScene";

    int currentStage;

    void Start()
    {
        character.Warp(stages[currentStage].StandPosition);
        character.OnArrived += OnCharacterArrived;
        stageInfo.Show(stages[currentStage].data);
        menu.Show(true);
    }

    void OnDestroy() => character.OnArrived -= OnCharacterArrived;

    void Update()
    {
        if (dialog.IsOpen) { HandleDialog(); return; }
        if (character.IsMoving) return; // 移動中は操作を受け付けない

        if (PressedRight())      SelectStage(currentStage + 1);
        else if (PressedLeft())  SelectStage(currentStage - 1);
        else if (PressedUp())    menu.MoveCursor(-1);
        else if (PressedDown())  menu.MoveCursor(1);
        else if (PressedDecide()) DecideMenu();
    }

    void SelectStage(int index)
    {
        index = Mathf.Clamp(index, 0, stages.Length - 1);
        if (index == currentStage) return;

        currentStage = index;
        menu.Show(false);     // 移動中はメニュー非表示（左へスライド）
        stageInfo.Hide();
        character.MoveTo(stages[currentStage].StandPosition);
    }

    void OnCharacterArrived()
    {
        menu.Show(true);
        stageInfo.Show(stages[currentStage].data);
    }

    void DecideMenu()
    {
        StageData data = stages[currentStage].data;
        switch (menu.Index)
        {
            case 0: // 章開始
                dialog.Open($"「{data.stageTitle}」を開始しますか？",
                    () => SceneManager.LoadScene(data.talkSceneName));
                break;
            case 1: Debug.Log("仲間（未実装）"); break;
            case 2: Debug.Log("セーブ（未実装）"); break;
            case 3: // タイトルへ
                dialog.Open("タイトルに戻りますか？",
                    () => SceneManager.LoadScene(titleSceneName));
                break;
        }
    }

    void HandleDialog()
    {
        if (PressedUp() || PressedDown()) dialog.Toggle();
        else if (PressedDecide()) dialog.Decide();
        else if (PressedCancel()) dialog.Cancel();
    }

    // ---- 入力（キーボード＋コントローラー） ----
    static Keyboard K => Keyboard.current;
    static Gamepad G => Gamepad.current;

    bool PressedRight() => (K != null && (K.rightArrowKey.wasPressedThisFrame || K.dKey.wasPressedThisFrame))
                        || (G != null && G.dpad.right.wasPressedThisFrame);
    bool PressedLeft()  => (K != null && (K.leftArrowKey.wasPressedThisFrame || K.aKey.wasPressedThisFrame))
                        || (G != null && G.dpad.left.wasPressedThisFrame);
    bool PressedUp()    => (K != null && (K.upArrowKey.wasPressedThisFrame || K.wKey.wasPressedThisFrame))
                        || (G != null && G.dpad.up.wasPressedThisFrame);
    bool PressedDown()  => (K != null && (K.downArrowKey.wasPressedThisFrame || K.sKey.wasPressedThisFrame))
                        || (G != null && G.dpad.down.wasPressedThisFrame);
    bool PressedDecide() => (K != null && (K.enterKey.wasPressedThisFrame || K.spaceKey.wasPressedThisFrame || K.zKey.wasPressedThisFrame))
                         || (G != null && G.buttonSouth.wasPressedThisFrame);
    bool PressedCancel() => (K != null && (K.escapeKey.wasPressedThisFrame || K.xKey.wasPressedThisFrame))
                         || (G != null && G.buttonEast.wasPressedThisFrame);
}
