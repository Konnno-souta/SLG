using UnityEngine;
using UnityEngine.EventSystems;

namespace Conversation
{
    /// <summary>マウス/タッチで押し続けているかを判定するだけの部品</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) IsHeld = true;
        }

        public void OnPointerUp(PointerEventData e) => IsHeld = false;
        public void OnPointerExit(PointerEventData e) => IsHeld = false;
        void OnDisable() => IsHeld = false;
    }
}
