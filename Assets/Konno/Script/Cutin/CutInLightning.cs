using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CutIn
{
    // 並び順は変えないこと（保存されている設定は番号で記録されるため）
    public enum LightningStyle
    {
        Bolt,      // 塗りつぶしたギザギザの稲妻（先がとがる）
        Electric   // 光るギザギザの線
    }

    /// <summary>
    /// 横に走る雷を描く UI 部品（CutInPlayer が自動で使う）。
    ///   Electric : 外側がぼんやり光る線（Glow）と、中心の明るい芯（Core）の2層。
    ///   Bolt     : 太いギザギザの稲妻。先端だけ細くとがる。左右でグラデーション。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CutInLightning : MaskableGraphic
    {
        public LightningStyle style = LightningStyle.Electric;

        [Header("Electric")]
        public Color coreColor = Color.white;
        public Color glowColor = new Color(0.55f, 0.8f, 1f, 0.9f);
        public float coreWidth = 16f;
        public float glowWidth = 90f;

        [Header("Bolt")]
        public Color boltColorStart = new Color(0.98f, 0.25f, 0.25f, 1f);
        public Color boltColorEnd = new Color(0.6f, 0.1f, 0.15f, 1f);
        public float boltThickness = 120f;
        [Tooltip("先端から何割の長さで細くとがらせるか")]
        [Range(0.05f, 1f)] public float boltTaper = 0.35f;
        public Color boltOutlineColor = new Color(0.3f, 0.05f, 0.05f, 0.8f);
        public float boltOutlineWidth = 3f;

        [Header("共通")]
        [Tooltip("左から何割まで伸びているか（0〜1）")]
        [Range(0f, 1f)] public float reveal = 1f;
        [Tooltip("明るさ（チラつきに使う）")]
        [Range(0f, 1f)] public float intensity = 1f;

        readonly List<Vector2> basePoints = new List<Vector2>();
        readonly List<Vector2> points = new List<Vector2>();
        readonly List<Vector2> visible = new List<Vector2>();
        readonly List<float> visibleT = new List<float>();

        /// <summary>雷の形を新しく作る</summary>
        /// <param name="startX">左端の位置</param>
        /// <param name="length">長さ (px)</param>
        /// <param name="amplitude">ギザギザの上下の大きさ (px)</param>
        /// <param name="segments">折れ曲がりの数</param>
        public void Generate(float startX, float length, float amplitude, int segments)
        {
            basePoints.Clear();
            int n = Mathf.Max(2, segments);
            float step = length / n;

            if (style == LightningStyle.Bolt)
            {
                // 大きく上下に折れる稲妻（上下交互・最後は真ん中に向かってとがる）
                for (int i = 0; i <= n; i++)
                {
                    float x = startX + step * i;
                    if (i > 0 && i < n) x += Random.Range(-0.2f, 0.2f) * step;
                    float sign = (i % 2 == 0) ? 1f : -1f;
                    float y = (i == n) ? 0f : sign * Random.Range(0.4f, 1f) * amplitude;
                    basePoints.Add(new Vector2(x, y));
                }
            }
            else
            {
                float wave = 0f;
                for (int i = 0; i <= n; i++)
                {
                    float x = startX + step * i;
                    if (i > 0 && i < n) x += Random.Range(-0.3f, 0.3f) * step;   // 折れ目の間隔もばらつかせる
                    // 大きなうねり（ゆっくり上下する）
                    wave = Mathf.Clamp(wave + Random.Range(-amplitude, amplitude) * 0.5f, -amplitude * 0.6f, amplitude * 0.6f);
                    // 鋭いギザギザ（点ごとに上下交互に振る）
                    float sign = (i % 2 == 0) ? 1f : -1f;
                    float jag = sign * Random.Range(0.3f, 1f) * amplitude * 0.55f;
                    basePoints.Add(new Vector2(x, wave + jag));
                }
            }
            Jitter(0f);
        }

        /// <summary>形を少しだけ揺らす（チラつき用）</summary>
        public void Jitter(float amount)
        {
            points.Clear();
            foreach (var p in basePoints)
                points.Add(p + new Vector2(0f, amount > 0f ? Random.Range(-amount, amount) : 0f));
            SetVerticesDirty();
        }

        public void Refresh() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = points.Count;
            if (count < 2 || reveal <= 0f || intensity <= 0f) return;

            // 左から reveal の割合までを描く（t = 雷全体の中での位置 0〜1）
            float end = reveal * (count - 1);
            int last = Mathf.Min(count - 1, Mathf.CeilToInt(end));
            visible.Clear();
            visibleT.Clear();
            for (int i = 0; i <= last; i++)
            {
                Vector2 p = points[i];
                float t = i / (float)(count - 1);
                if (i == last && i > 0 && end < i)
                {
                    float f = end - (i - 1);
                    p = Vector2.Lerp(points[i - 1], points[i], f);
                    t = (i - 1 + f) / (count - 1);
                }
                visible.Add(p);
                visibleT.Add(t);
            }
            if (visible.Count < 2) return;

            if (style == LightningStyle.Bolt)
            {
                if (boltOutlineWidth > 0f && boltOutlineColor.a > 0f)
                    AddBolt(vh, boltThickness + boltOutlineWidth * 2f, boltOutlineColor, boltOutlineColor, boltOutlineWidth);
                AddBolt(vh, boltThickness, boltColorStart, boltColorEnd, 0f);
            }
            else
            {
                AddStrip(vh, visible, glowWidth, glowColor, soft: true);
                AddStrip(vh, visible, coreWidth, coreColor, soft: false);
            }
        }

        /// <summary>
        /// 塗りつぶしの稲妻。上下のふちを「真上・真下」にずらすので、段々の稲妻らしい形になる。
        /// 太さはほぼ一定で、先端の boltTaper の範囲だけ細くとがる
        /// </summary>
        void AddBolt(VertexHelper vh, float thickness, Color startColor, Color endColor, float tipExtra)
        {
            int start = vh.currentVertCount;
            int n = visible.Count;
            for (int i = 0; i < n; i++)
            {
                float t = visibleT[i];
                float taper = Mathf.Clamp01((1f - t) / Mathf.Max(0.01f, boltTaper));
                float half = thickness * 0.5f * taper + (t >= 1f ? tipExtra : 0f);
                Color c = Color.Lerp(startColor, endColor, t);
                c.a *= intensity;
                Vector2 p = visible[i];
                if (t >= 1f) p.x += tipExtra;   // ふちの線の分だけ先端を伸ばす
                vh.AddVert(p + new Vector2(0f, half), c, Vector4.zero);
                vh.AddVert(p - new Vector2(0f, half), c, Vector4.zero);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 2, a + 1);
                vh.AddTriangle(a + 1, a + 2, a + 3);
            }
        }

        /// <summary>折れ線に沿った帯状のメッシュを作る（soft = 外側に向かって透明になる）</summary>
        void AddStrip(VertexHelper vh, List<Vector2> pts, float width, Color color, bool soft)
        {
            if (width <= 0f || color.a <= 0f) return;

            Color center = color;
            center.a *= intensity;
            Color edge = center;
            if (soft) edge.a = 0f;

            int start = vh.currentVertCount;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dirPrev = i > 0 ? (pts[i] - pts[i - 1]).normalized : (pts[1] - pts[0]).normalized;
                Vector2 dirNext = i < n - 1 ? (pts[i + 1] - pts[i]).normalized : dirPrev;
                Vector2 tangent = dirPrev + dirNext;
                tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : dirPrev;
                var normal = new Vector2(-tangent.y, tangent.x);

                // 折れ目で太さが細らないように補正（とがりすぎないよう上限あり）
                float dot = Vector2.Dot(normal, new Vector2(-dirPrev.y, dirPrev.x));
                float miter = 1f / Mathf.Max(0.35f, dot);
                Vector2 off = normal * (width * 0.5f * miter);

                vh.AddVert(pts[i] + off, edge, Vector4.zero);   // 上のふち
                vh.AddVert(pts[i], center, Vector4.zero);       // 中心
                vh.AddVert(pts[i] - off, edge, Vector4.zero);   // 下のふち
            }

            for (int i = 0; i < n - 1; i++)
            {
                int a = start + i * 3;
                int b = a + 3;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(a + 1, b, b + 1);
                vh.AddTriangle(a + 1, b + 1, a + 2);
                vh.AddTriangle(a + 2, b + 1, b + 2);
            }
        }
    }
}