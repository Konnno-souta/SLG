using System;
using System.Collections.Generic;
using Title;
using UnityEngine;
using UnityEngine.UI;

public interface ICloseButton
{
    public void InitCloseButton(Title_ButtonAction act) { }
}

namespace Title
{
    [Serializable]
    public class Action
    {
        public Button button;
        public GameObject window;
    }

    public class Title_ButtonAction : MonoBehaviour
    {
        [SerializeField] private List<Action> buttons = new();

        public bool isOpen { get; private set; } = false;

        //----------------------------------------------------------------//

        private GameObject openingWindow;

        private void Awake()
        {
            foreach (var b in buttons)
            {
                b.button.onClick.AddListener(() => Open(b.button));
            }
        }

        public void Open (Button btn)
        {
            if (isOpen) return;

            foreach (var b in buttons) 
            {
                if (b.button != btn) return;
                openingWindow = Instantiate(b.window, gameObject.GetComponent<Transform>());
                openingWindow.GetComponent<ICloseButton>()?.InitCloseButton(this);

                isOpen = true;
            }
        }

        public void Close()
        {
            Debug.Log("close");
            if (!isOpen) return;

            Destroy(openingWindow);
            isOpen = false;
        }
    }
}
