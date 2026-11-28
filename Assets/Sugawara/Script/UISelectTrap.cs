using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ウィンドウが開いている間、ボタンの選択（カーソル）をウィンドウの中に閉じ込める
//
// ・開いたときに、中のボタン同士だけで上下左右の移動先を決め直す
//   → キー操作・コントローラーで、そもそも外のボタンへ移動しない（一瞬外に出ることもない）
// ・閉じたら、ボタンの移動先を元の設定に戻す
// ・それでも外が選ばれたとき（スクリプトから選択された場合など）は、中のボタンに戻す
// ・このオブジェクトが表示されている間だけ働く（DiaLogPanel や SettingWindow に付ける）
[DefaultExecutionOrder(-1000)] // カーソルや点滅より先に選択を直すため
public class UISelectTrap : MonoBehaviour
{
    [Tooltip("この中に選択を閉じ込める（空ならこのオブジェクト）")]
    [SerializeField] RectTransform area;
    [Tooltip("押した方向から何度以内のボタンを移動先にするか。小さいほど、斜めのボタンに移りにくい")]
    [SerializeField, Range(10f, 89f)] float maxAngle = 60f;

    readonly Dictionary<Selectable, Navigation> originalNavigation = new Dictionary<Selectable, Navigation>();
    GameObject lastInside;

    Transform Area => area != null ? area : transform;

    void OnEnable()
    {
        lastInside = null;
        Refresh();
    }

    void OnDisable()
    {
        RestoreNavigation();
    }

    // 中のボタンの移動先を決め直す（実行中にボタンを増やしたときは、これを呼び直す）
    public void Refresh()
    {
        RestoreNavigation();

        // レイアウトを確定させてから位置を測る
        Canvas.ForceUpdateCanvases();

        var inside = new List<Selectable>();
        foreach (var s in Area.GetComponentsInChildren<Selectable>(false))
            if (s.navigation.mode != Navigation.Mode.None) inside.Add(s);

        foreach (var s in inside)
        {
            originalNavigation[s] = s.navigation;

            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = FindNext(s, Vector3.up, inside),
                selectOnDown = FindNext(s, Vector3.down, inside),
                selectOnLeft = FindNext(s, Vector3.left, inside),
                selectOnRight = FindNext(s, Vector3.right, inside),
            };
            s.navigation = nav;
        }
    }

    // 中のボタンの中から、dir の方向にある一番近いボタンを探す（Unity 標準の探し方と同じ考え方）
    Selectable FindNext(Selectable from, Vector3 dir, List<Selectable> candidates)
    {
        Vector3 worldDir = Area.rotation * dir;
        Vector3 fromPos = Center(from);

        Selectable best = null;
        float bestScore = 0f;

        foreach (var c in candidates)
        {
            if (c == from) continue;

            Vector3 diff = Center(c) - fromPos;
            float dot = Vector3.Dot(worldDir, diff);
            if (dot <= 0f) continue;

            // 押した方向から大きくずれているボタンは選ばない
            // （横に並んだボタンが少しだけ高さがずれていても、上下キーで移らないようにする）
            float cos = dot / Mathf.Max(0.0001f, diff.magnitude);
            if (cos < Mathf.Cos(maxAngle * Mathf.Deg2Rad)) continue;

            // 方向に近く、距離が近いほど高い点数
            float score = dot / diff.sqrMagnitude;
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        return best;
    }

    static Vector3 Center(Selectable s)
    {
        var rt = s.transform as RectTransform;
        return rt != null ? rt.TransformPoint(rt.rect.center) : s.transform.position;
    }

    void RestoreNavigation()
    {
        foreach (var pair in originalNavigation)
            if (pair.Key != null) pair.Key.navigation = pair.Value;
        originalNavigation.Clear();
    }

    // 念のための保険：それでも外が選ばれたら、中のボタンに戻す
    void LateUpdate()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        GameObject current = es.currentSelectedGameObject;

        if (current != null && current.transform.IsChildOf(Area))
        {
            lastInside = current;
            return;
        }

        if (lastInside != null && lastInside.activeInHierarchy)
            es.SetSelectedGameObject(lastInside);
    }
}