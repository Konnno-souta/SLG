using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 選ばれているボタンに重ねて、光を点滅させる
// ・この Image が、選ばれているボタンと同じ位置・大きさに自動でついていく
// ・ボタン側に何も付けなくてよいので、どのメニューにも使える
// ・Hierarchy でボタンより下に置くとボタンの上に光が重なり、上に置くとボタンの後ろで光る
[RequireComponent(typeof(Image))]
public class UISelectHighlight : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("この中のボタンが選ばれているときだけ表示する（MenuPanel など）。空ならどのボタンでも表示")]
    [SerializeField] RectTransform scope;
    [Tooltip("ボタンより少し大きくする量（UIの単位）。0 でボタンとぴったり同じ大きさ")]
    [SerializeField] Vector2 padding = new Vector2(0f, 0f);

    [Header("形")]
    [Tooltip("選ばれたボタンの画像（角丸など）を自動でコピーして、ボタンと同じ形にする")]
    [SerializeField] bool matchButtonShape = true;

    [Header("点滅")]
    [Tooltip("点滅の速さ（1秒あたりの回数）")]
    [SerializeField] float blinkSpeed = 1.2f;
    [Tooltip("一番薄いときの濃さ")]
    [SerializeField, Range(0f, 1f)] float minAlpha = 0.1f;
    [Tooltip("一番濃いときの濃さ")]
    [SerializeField, Range(0f, 1f)] float maxAlpha = 0.6f;
    [Tooltip("ボタンが変わったときに、一番明るいところから点滅を始める")]
    [SerializeField] bool flashOnChange = true;

    RectTransform rect;
    Image image;
    Color baseColor;
    GameObject lastSelected;
    float timer;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        image.raycastTarget = false;
        baseColor = image.color;

        // 親に Layout Group があっても、ボタンと一緒に並べられないようにする
        if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
        {
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        image.enabled = false;
    }

    void LateUpdate()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (!IsValidTarget(selected))
        {
            image.enabled = false;
            lastSelected = null;
            return;
        }

        RectTransform target = (RectTransform)selected.transform;

        if (selected != lastSelected)
        {
            lastSelected = selected;
            if (flashOnChange) timer = 0f;
            if (matchButtonShape) CopyShape(selected);
        }

        // ボタンと同じ位置・向き・大きさに合わせる
        rect.position = target.TransformPoint(target.rect.center);
        rect.rotation = target.rotation;
        Vector3 ratio = new Vector3(
            target.lossyScale.x / Mathf.Max(0.0001f, rect.parent.lossyScale.x),
            target.lossyScale.y / Mathf.Max(0.0001f, rect.parent.lossyScale.y), 1f);
        rect.sizeDelta = new Vector2(target.rect.width * ratio.x, target.rect.height * ratio.y) + padding;

        // 点滅（cos を使って、切り替わった瞬間が一番明るくなるようにする）
        timer += Time.unscaledDeltaTime;
        float wave = Mathf.Cos(timer * blinkSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
        Color c = baseColor;
        c.a = baseColor.a * Mathf.Lerp(minAlpha, maxAlpha, wave);
        image.color = c;
        image.enabled = true;
    }

    // ボタンの画像の形（角丸の画像・スライス設定）をコピーする
    void CopyShape(GameObject selected)
    {
        Image source = null;
        var selectable = selected.GetComponent<Selectable>();
        if (selectable != null) source = selectable.targetGraphic as Image;
        if (source == null) source = selected.GetComponent<Image>();
        if (source == null) return;

        image.sprite = source.sprite;
        image.type = source.type;
        image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        image.preserveAspect = source.preserveAspect;
        image.fillCenter = source.fillCenter;
    }

    bool IsValidTarget(GameObject selected)
    {
        if (selected == null || !selected.activeInHierarchy) return false;
        if (!(selected.transform is RectTransform)) return false;
        if (scope != null && !selected.transform.IsChildOf(scope)) return false;

        var s = selected.GetComponent<Selectable>();
        return s == null || s.IsInteractable();
    }
}