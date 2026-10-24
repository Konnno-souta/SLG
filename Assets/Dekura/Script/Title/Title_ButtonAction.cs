
using System;
using System.Collections.Generic;
using Title;
using UnityEngine;
using UnityEngine.UI;

public interface ICloseButton
{
    public void InitCloseButton(
        Title_ButtonAction act)
    { }
}

namespace Title
{
    [Serializable]
    public class Action
    {
        public Button button;
        public GameObject window;

        // 第18段階で追加。
        // セーブ画面ではNewGameかContinueを指定。
        // それ以外の画面はNoneのままでOK。
        public TitleSlotOpenMode slotMode =
            TitleSlotOpenMode.None;
    }

    public class Title_ButtonAction : MonoBehaviour
    {
        [SerializeField]
        private List<Action> buttons = new();

        public bool isOpen
        {
            get;
            private set;
        } = false;

        //----------------------------------------------------------------//

        private GameObject openingWindow;

        private void Awake()
        {
            foreach (var b in buttons)
            {
                b.button.onClick.AddListener(
                    () => Open(b.button)
                );
            }
        }

        public void Open(Button btn)
        {
            if (isOpen) return;

            foreach (var b in buttons)
            {
                if (b.button != btn) continue;

                // ここは元の生成処理。
                openingWindow = Instantiate(
                    b.window,
                    gameObject.GetComponent<Transform>()
                );

                // ここも元の閉じる処理。
                openingWindow
                    .GetComponent<ICloseButton>()
                    ?.InitCloseButton(this);

                // セーブ選択画面にだけ
                // NewGame / Continueの情報を渡す。
                //
                // Settingsなどの別画面には
                // このスクリプトがないため、
                // 従来の動作を維持する。
                TitleSaveSlotSelectionController
                    slotController =
                    openingWindow
                        .GetComponentInChildren<
                            TitleSaveSlotSelectionController
                        >(true);

                if (slotController != null)
                {
                    slotController.Configure(
                        b.slotMode
                    );
                }

                // =================================

                isOpen = true;
            }
        }

        public void Close()
        {
            if (!isOpen) return;

            Destroy(openingWindow);
            isOpen = false;
        }
    }
}
