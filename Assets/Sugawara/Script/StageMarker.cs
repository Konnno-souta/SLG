using System.Collections.Generic;
using UnityEngine;

// ステージのマスの見た目を、進行状況で切り替える（StagePoint と同じオブジェクトに付ける）
//
// 状態は3つ
//   Locked  : まだ行けない
//   Open    : 行けるがまだクリアしていない
//   Cleared : クリア済み
//
// 見た目の変え方は2通り（両方使ってもOK）
//   1. 色を変える：元の色に「状態ごとの色」を掛け合わせる（暗くする、金色っぽくする など）
//      マテリアルは書き換えないので、同じマテリアルを使う他のオブジェクトには影響しない
//   2. 状態ごとの子オブジェクトを用意して、表示を切り替える（モデルや画像ごと変えたいとき）
[RequireComponent(typeof(StagePoint))]
public class StageMarker : MonoBehaviour
{
    public enum State { Locked, Open, Cleared }

    [Header("1. 色（元の色に掛け合わせる。白 = 元のまま）")]
    [Tooltip("色を変える")]
    [SerializeField] bool useTint = true;
    [Tooltip("まだ行けないときの色（暗い灰色にすると暗くなる）")]
    [SerializeField] Color lockedTint = new Color(0.3f, 0.3f, 0.3f, 1f);
    [Tooltip("行けるがまだクリアしていないときの色")]
    [SerializeField] Color openTint = Color.white;
    [Tooltip("クリア済みのときの色")]
    [SerializeField] Color clearedTint = new Color(1f, 0.9f, 0.5f, 1f);
    [Tooltip("色を変える対象。空なら、このオブジェクトと子の Renderer すべて")]
    [SerializeField] Renderer[] targetRenderers;

    [Header("2. 状態ごとの見た目（子オブジェクト）")]
    [Tooltip("まだ行けないときに表示（空なら Open の見た目を使う）")]
    [SerializeField] GameObject lockedVisual;
    [Tooltip("行けるがまだクリアしていないときに表示")]
    [SerializeField] GameObject openVisual;
    [Tooltip("クリア済みのときに表示（空なら Open の見た目を使う）")]
    [SerializeField] GameObject clearedVisual;

    StagePoint stage;
    MaterialPropertyBlock block;
    readonly Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>();

    public State Current { get; private set; }

    void Awake()
    {
        stage = GetComponent<StagePoint>();
        block = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        // 元の色を覚えておく（ここに掛け合わせる）
        foreach (var r in targetRenderers)
            if (r != null) baseColors[r] = GetBaseColor(r);
    }

    void OnEnable()  { StageProgress.OnCleared += HandleCleared; }
    void OnDisable() { StageProgress.OnCleared -= HandleCleared; }

    void Start()
    {
        Refresh();
    }

    // どこかのステージがクリアされたら更新する（次のステージが行けるようになることもあるため）
    void HandleCleared(int stageId) => Refresh();

    // 今の進行状況に合わせて見た目を更新する
    public void Refresh()
    {
        if (stage == null) stage = GetComponent<StagePoint>();

        if (!stage.IsUnlocked) Current = State.Locked;
        else if (stage.IsCleared) Current = State.Cleared;
        else Current = State.Open;

        ApplyVisual();
        ApplyTint();
    }

    // ---- 1. 色 ----
    void ApplyTint()
    {
        if (!useTint) return;

        Color tint = Current switch
        {
            State.Locked  => lockedTint,
            State.Cleared => clearedTint,
            _             => openTint,
        };

        foreach (var pair in baseColors)
        {
            Renderer r = pair.Key;
            if (r == null) continue;
            Color c = pair.Value * tint;

            if (r is SpriteRenderer sr)
            {
                sr.color = c;
                continue;
            }

            string prop = ColorProperty(r);
            if (prop == null) continue;

            r.GetPropertyBlock(block);
            block.SetColor(prop, c);
            r.SetPropertyBlock(block);
        }
    }

    // マテリアルの色のプロパティ名（URP は _BaseColor、標準は _Color）
    static string ColorProperty(Renderer r)
    {
        Material m = r.sharedMaterial;
        if (m == null) return null;
        if (m.HasProperty("_BaseColor")) return "_BaseColor";
        if (m.HasProperty("_Color")) return "_Color";
        return null;
    }

    static Color GetBaseColor(Renderer r)
    {
        if (r is SpriteRenderer sr) return sr.color;
        string prop = ColorProperty(r);
        return prop != null ? r.sharedMaterial.GetColor(prop) : Color.white;
    }

    // ---- 2. 子オブジェクトの切り替え ----
    void ApplyVisual()
    {
        // 何も設定していなければ切り替えない
        if (lockedVisual == null && openVisual == null && clearedVisual == null) return;

        // 表示するもの（未設定なら Open の見た目で代用）
        GameObject show = Current switch
        {
            State.Locked  => lockedVisual  != null ? lockedVisual  : openVisual,
            State.Cleared => clearedVisual != null ? clearedVisual : openVisual,
            _             => openVisual,
        };

        SetActive(lockedVisual, lockedVisual == show);
        SetActive(openVisual, openVisual == show);
        SetActive(clearedVisual, clearedVisual == show);
    }

    static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
