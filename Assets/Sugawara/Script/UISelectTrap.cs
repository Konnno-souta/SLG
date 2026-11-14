using UnityEngine;
using UnityEngine.EventSystems;

// ウィンドウが開いている間、ボタンの選択（カーソル）をウィンドウの中に閉じ込める
// ・キー操作やマウスで、ウィンドウの外のボタンが選ばれたら、直前に選んでいた中のボタンに戻す
// ・何も選ばれていない状態になったときも、中のボタンに戻す（コントローラーで操作できなくなるのを防ぐ）
// ・このオブジェクトが表示されている間だけ働く（DiaLogPanel や SettingWindow に付ける）
public class UISelectTrap : MonoBehaviour
{
    [Tooltip("この中に選択を閉じ込める（空ならこのオブジェクト）")]
    [SerializeField] RectTransform area;

    GameObject lastInside;

    Transform Area => area != null ? area : transform;

    void OnEnable()
    {
        lastInside = null;
    }

    void LateUpdate()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        GameObject current = es.currentSelectedGameObject;

        // 中のボタンが選ばれていれば、それを覚えておく
        if (current != null && current.transform.IsChildOf(Area))
        {
            lastInside = current;
            return;
        }

        // 外に出た（または何も選ばれていない）ので、中のボタンに戻す
        if (lastInside != null && lastInside.activeInHierarchy)
            es.SetSelectedGameObject(lastInside);
    }
}
