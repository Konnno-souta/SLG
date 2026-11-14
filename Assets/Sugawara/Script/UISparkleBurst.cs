using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI 上で、きらきらした粒をパッと散らす演出
// ・Burst(位置) を呼ぶと、その位置から粒が飛び散って消える
// ・UISelectCursor の Sparkle 欄に入れると、カーソルが動いたときに自動で出る
// ・粒は Canvas の一番手前に出すので、レイアウトや他の UI の邪魔をしない
public class UISparkleBurst : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("粒の画像（星など）。空なら白い四角を45度回したひし形になる")]
    [SerializeField] Sprite sprite;
    [Tooltip("粒の色（この2色の間でランダム）")]
    [SerializeField] Color colorA = new Color(1f, 0.95f, 0.6f, 1f);
    [SerializeField] Color colorB = new Color(1f, 0.7f, 0.85f, 1f);
    [Tooltip("粒の大きさ（最小〜最大）")]
    [SerializeField] Vector2 sizeRange = new Vector2(10f, 22f);

    [Header("動き")]
    [Tooltip("1回に出す粒の数")]
    [SerializeField] int count = 8;
    [Tooltip("粒が消えるまでの秒数（最小〜最大）")]
    [SerializeField] Vector2 lifetimeRange = new Vector2(0.35f, 0.6f);
    [Tooltip("飛び散る速さ（最小〜最大）")]
    [SerializeField] Vector2 speedRange = new Vector2(60f, 160f);
    [Tooltip("回転の速さ（度/秒）")]
    [SerializeField] float spinSpeed = 360f;
    [Tooltip("下に落ちる強さ（0 で落ちない）")]
    [SerializeField] float gravity = 120f;

    class Particle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 velocity;
        public float life, maxLife, size, spin;
        public Color color;
    }

    readonly List<Particle> active = new List<Particle>();
    readonly Stack<Particle> pool = new Stack<Particle>();
    RectTransform canvasRect;

    void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = (RectTransform)canvas.rootCanvas.transform;
    }

    // 指定したワールド座標から粒を散らす
    public void Burst(Vector3 worldPosition)
    {
        if (canvasRect == null) return;

        Vector2 center = canvasRect.InverseTransformPoint(worldPosition);

        for (int i = 0; i < count; i++)
        {
            Particle p = pool.Count > 0 ? pool.Pop() : CreateParticle();
            p.rect.gameObject.SetActive(true);
            p.rect.SetAsLastSibling(); // 一番手前に表示

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(speedRange.x, speedRange.y);
            p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            p.maxLife = Random.Range(lifetimeRange.x, lifetimeRange.y);
            p.life = 0f;
            p.size = Random.Range(sizeRange.x, sizeRange.y);
            p.spin = Random.Range(-spinSpeed, spinSpeed);
            p.color = Color.Lerp(colorA, colorB, Random.value);

            p.rect.localPosition = center;
            p.rect.localRotation = Quaternion.Euler(0f, 0f, sprite == null ? 45f : Random.Range(0f, 360f));
            active.Add(p);
        }
    }

    Particle CreateParticle()
    {
        var go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRect, false);

        var p = new Particle
        {
            rect = (RectTransform)go.transform,
            image = go.GetComponent<Image>(),
        };
        p.image.sprite = sprite;
        p.image.raycastTarget = false;
        p.rect.anchorMin = p.rect.anchorMax = new Vector2(0.5f, 0.5f);
        return p;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Particle p = active[i];
            p.life += dt;
            float t = p.life / p.maxLife;

            if (t >= 1f)
            {
                p.rect.gameObject.SetActive(false);
                active.RemoveAt(i);
                pool.Push(p);
                continue;
            }

            // 動き：だんだん遅くなりながら散って、少し落ちる
            p.velocity *= Mathf.Exp(-3f * dt);
            p.velocity.y -= gravity * dt;
            p.rect.localPosition += (Vector3)(p.velocity * dt);
            p.rect.Rotate(0f, 0f, p.spin * dt);

            // 大きさ：ぱっと大きくなって、だんだん小さく
            float scale = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            p.rect.sizeDelta = Vector2.one * p.size * scale;

            // 色：最後にふっと消える
            Color c = p.color;
            c.a *= 1f - t * t;
            p.image.color = c;
        }
    }

    void OnDestroy()
    {
        foreach (var p in active) if (p.rect != null) Destroy(p.rect.gameObject);
        foreach (var p in pool) if (p.rect != null) Destroy(p.rect.gameObject);
    }
}
