using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// マウスを乗せたボタンを「選択中」にする
// （UISelectCursor が自動で付けるので、手で付ける必要はない）
public class UISelectOnHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        var s = GetComponent<Selectable>();
        if (s != null && s.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
