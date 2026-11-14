using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 選択中のボタンの横に表示されるカーソル（羽ペンなど）
//
// ・EventSystem の「今選ばれているボタン」に自動でついていく
//   → キーボード・コントローラーの上下操作、マウスのホバー、どれでも動く
// ・ボタン側にスクリプトを書き足す必要はない。どのシーンでも、このスクリプトを付けた画像を置くだけで使える
// ・Scope を指定すると、その中のボタンが選ばれているときだけ表示する（メニューごとにカーソルを分けられる）
[RequireComponent(typeof(RectTransform))]
public class UISelectCursor : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("対象")]
    [Tooltip("この中のボタンが選ばれているときだけ表示する（MenuPanel など）。空ならどのボタンでも表示")]
    [SerializeField] RectTransform scope;
    [Tooltip("マウスを乗せたボタンを選択状態にする（Scope 内のボタンに自動で設定）")]
    [SerializeField] bool selectOnHover = true;

    [Header("位置")]
    [Tooltip("ボタンのどちら側に出すか")]
    [SerializeField] Side side = Side.Left;
    [Tooltip("ボタンの端からのずれ（UIの単位）。Left なら X をマイナスにすると左に離れる")]
    [SerializeField] Vector2 offset = new Vector2(-20f, 0f);

    [Header("動き")]
    [Tooltip("別のボタンに移るときの速さ。0 で瞬間移動")]
    [SerializeField] float moveSpeed = 18f;
    [Tooltip("ボタンをつつくように横に揺れる幅。0 で揺れない")]
    [SerializeField] float bobAmount = 6f;
    [Tooltip("揺れる速さ")]
    [SerializeField] float bobSpeed = 5f;

    [Header("演出")]
    [Tooltip("カーソルが動いたときに、きらきらを出す UISparkleBurst（空なら出さない）")]
    [SerializeField] UISparkleBurst sparkle;
    [Tooltip("きらきらを出す位置を、カーソルからずらす量（UIの単位）")]
    [SerializeField] Vector2 sparkleOffset = Vector2.zero;
    [Tooltip("カーソルが最初に出たときにも、きらきらを出す")]
    [SerializeField] bool sparkleOnAppear = false;

    RectTransform rect;
    RectTransform parentRect;
    Graphic[] graphics;
    GameObject lastSelected;
    Vector3 smoothOffset;   // 前のボタンから今のボタンへ移る途中のずれ
    bool visible = true;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        parentRect = rect.parent as RectTransform;

        // 親に Layout Group があっても、ボタンと一緒に並べられないようにする
        if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
        {
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        // カーソルがクリックの邪魔をしないようにする
        graphics = GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics) g.raycastTarget = false;

        SetVisible(false);
    }

    void Start()
    {
        if (selectOnHover) AddHoverSelect();
    }

    // Scope 内のボタンに「マウスを乗せたら選択」を付ける
    // 実行中にボタンを増やしたときは、これを呼び直す
    public void AddHoverSelect()
    {
        Transform root = scope != null ? scope : rect.root;
        foreach (var s in root.GetComponentsInChildren<Selectable>(true))
            if (s.GetComponent<UISelectOnHover>() == null) s.gameObject.AddComponent<UISelectOnHover>();
    }

    void LateUpdate()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (!IsValidTarget(selected))
        {
            SetVisible(false);
            lastSelected = null;
            return;
        }

        Vector3 goal = GetGoal((RectTransform)selected.transform);

        bool burst = false;
        if (selected != lastSelected)
        {
            // きらきら：別のボタンに移ったとき（設定によっては最初に出たときも）
            burst = lastSelected != null || sparkleOnAppear;

            // 表示し始めたときはその場に出す。別のボタンに移ったときは、今の位置からなめらかに移る
            smoothOffset = (lastSelected == null || moveSpeed <= 0f) ? Vector3.zero : rect.localPosition - goal;
            lastSelected = selected;
        }

        if (moveSpeed > 0f) smoothOffset *= Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
        else smoothOffset = Vector3.zero;

        // ボタンをつつくように揺れる
        float bob = bobAmount > 0f ? (Mathf.Sin(Time.unscaledTime * bobSpeed) * 0.5f + 0.5f) * bobAmount : 0f;
        Vector3 bobVec = new Vector3(side == Side.Left ? -bob : bob, 0f, 0f);

        rect.localPosition = goal + smoothOffset + bobVec;
        SetVisible(true);

        // 移動先（ボタンの横）できらきらを出す
        if (burst && sparkle != null)
        {
            Vector3 at = goal + (Vector3)sparkleOffset;
            sparkle.Burst(parentRect != null ? parentRect.TransformPoint(at) : at);
        }
    }

    bool IsValidTarget(GameObject selected)
    {
        if (selected == null || !selected.activeInHierarchy) return false;
        if (!(selected.transform is RectTransform)) return false;
        if (scope != null && !selected.transform.IsChildOf(scope)) return false;

        // 押せない状態（パネルが隠れている間など）は表示しない
        var s = selected.GetComponent<Selectable>();
        return s == null || s.IsInteractable();
    }

    // ボタンの端の位置を、カーソルの親の座標に直して返す
    Vector3 GetGoal(RectTransform target)
    {
        Vector3[] c = new Vector3[4];
        target.GetWorldCorners(c); // 0:左下 1:左上 2:右上 3:右下

        Vector3 edge = side == Side.Left ? (c[0] + c[1]) * 0.5f : (c[2] + c[3]) * 0.5f;
        Vector3 local = parentRect != null ? parentRect.InverseTransformPoint(edge) : edge;
        local.z = 0f;
        return local + (Vector3)offset;
    }

    void SetVisible(bool on)
    {
        if (visible == on) return;
        visible = on;
        foreach (var g in graphics) if (g != null) g.enabled = on;
    }
}