using UnityEngine;
using UnityEngine.UI;

namespace CutIn
{
    public enum BandShape
    {
        Slash,      // 上下のふちがギザギザの帯（ペルソナ風）
        Rectangle,  // まっすぐな横長の帯
        Wedge       // 左端が底辺、右がとがった三角形（くさび形）
    }

    /// <summary>
    /// カットインの帯の形を描く UI 部品（CutInPlayer が自動で使う）。
    /// 塗りつぶし（帯の中身を切り抜く Mask 用・後ろの帯用）と、ふちの線の両方に使う。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CutInBandShape : MaskableGraphic
    {
        public BandShape shape = BandShape.Slash;
        [Tooltip("オンなら、ふちの線だけを描く")]
        public bool outlineOnly = false;
        public float outlineWidth = 6f;

        [Header("Slash（ギザギザ）")]
        [Tooltip("ふちのギザギザの大きさ (px)")]
        public float jag = 18f;
        [Tooltip("ギザギザの細かさ（帯の長さ 100px あたりの折れ目の数）")]
        public float jagDensity = 1.2f;
        [Tooltip("形を決める乱数。同じ値なら同じ形になる（塗りとふちの線を合わせるのに使う）")]
        public int seed = 1;

        [Header("右に向かって広がる形（Slash・Rectangle）")]
        [Tooltip("左端の太さ（右端に対する割合）。1 でまっすぐ、小さいほど左が細い")]
        [Range(0.05f, 1f)] public float flareLeft = 1f;
        [Tooltip("細い状態から広がり始める位置（帯の中心からの x）")]
        public float flareFromX = -960f;
        [Tooltip("広がりきる位置（帯の中心からの x）")]
        public float flareToX = 960f;

        public void Refresh() => SetVerticesDirty();

        /// <summary>その x での太さの割合（左ほど細く、右ほど太い）</summary>
        float HeightRatio(float x)
        {
            if (flareLeft >= 0.999f || flareToX <= flareFromX) return 1f;
            float t = Mathf.Clamp01((x - flareFromX) / (flareToX - flareFromX));
            return Mathf.Lerp(flareLeft, 1f, t);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Color32 c = color;

            switch (shape)
            {
                case BandShape.Wedge: PopulateWedge(vh, r, c); break;
                case BandShape.Slash: PopulateSlash(vh, r, c); break;
                default: PopulateRectangle(vh, r, c); break;
            }
        }

        // ---------------------------------------------------------------------
        // Slash：上下のふちがギザギザの帯
        // ---------------------------------------------------------------------

        void PopulateSlash(VertexHelper vh, Rect r, Color32 c)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(r.width / 100f * jagDensity), 2, 400);
            PopulateEdges(vh, r, c, n, jag);
        }

        /// <summary>上下のふちを折れ線で作って塗る（ギザギザ・広がりに対応）</summary>
        void PopulateEdges(VertexHelper vh, Rect r, Color32 c, int n, float jagAmount)
        {
            var rand = new System.Random(seed);
            var top = new Vector2[n + 1];
            var bottom = new Vector2[n + 1];
            float cy = r.center.y;
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(r.xMin, r.xMax, i / (float)n);
                float half = r.height * 0.5f * HeightRatio(x);
                // 外側にも内側にも少しずつ出っ張らせる
                top[i] = new Vector2(x, cy + half + ((float)rand.NextDouble() * 2f - 1f) * jagAmount);
                bottom[i] = new Vector2(x, cy - half + ((float)rand.NextDouble() * 2f - 1f) * jagAmount);
            }

            if (!outlineOnly)
            {
                for (int i = 0; i <= n; i++)
                {
                    vh.AddVert(bottom[i], c, Vector4.zero);
                    vh.AddVert(top[i], c, Vector4.zero);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = i * 2;
                    vh.AddTriangle(a, a + 1, a + 3);
                    vh.AddTriangle(a + 3, a + 2, a);
                }
            }
            else if (outlineWidth > 0f)
            {
                // ふちのギザギザに沿って、内側に向かって線を引く
                AddPolylineBand(vh, top, -outlineWidth, c);
                AddPolylineBand(vh, bottom, outlineWidth, c);
            }
        }

        static void AddPolylineBand(VertexHelper vh, Vector2[] pts, float dy, Color32 c)
        {
            int start = vh.currentVertCount;
            foreach (var p in pts)
            {
                vh.AddVert(p, c, Vector4.zero);
                vh.AddVert(p + new Vector2(0f, dy), c, Vector4.zero);
            }
            for (int i = 0; i < pts.Length - 1; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a + 3, a + 2, a);
            }
        }

        // ---------------------------------------------------------------------
        // Rectangle：まっすぐな帯
        // ---------------------------------------------------------------------

        void PopulateRectangle(VertexHelper vh, Rect r, Color32 c)
        {
            // 広がる形の時は、ギザギザ無しの折れ線で作る
            if (flareLeft < 0.999f)
            {
                PopulateEdges(vh, r, c, 64, 0f);
                return;
            }

            if (!outlineOnly)
            {
                AddQuad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax), c);
            }
            else
            {
                float h = outlineWidth * 0.5f;
                AddQuad(vh, new Vector2(r.xMin, r.yMax - h), new Vector2(r.xMax, r.yMax + h), c);
                AddQuad(vh, new Vector2(r.xMin, r.yMin - h), new Vector2(r.xMax, r.yMin + h), c);
            }
        }

        // ---------------------------------------------------------------------
        // Wedge：三角形
        // ---------------------------------------------------------------------

        void PopulateWedge(VertexHelper vh, Rect r, Color32 c)
        {
            var top = new Vector2(r.xMin, r.yMax);
            var bottom = new Vector2(r.xMin, r.yMin);
            var tip = new Vector2(r.xMax, r.center.y);

            if (!outlineOnly)
            {
                vh.AddVert(top, c, Vector4.zero);
                vh.AddVert(tip, c, Vector4.zero);
                vh.AddVert(bottom, c, Vector4.zero);
                vh.AddTriangle(0, 1, 2);
            }
            else
            {
                AddEdge(vh, top, tip, inwardDown: true, c);
                AddEdge(vh, bottom, tip, inwardDown: false, c);
            }
        }

        void AddEdge(VertexHelper vh, Vector2 a, Vector2 b, bool inwardDown, Color32 c)
        {
            if (outlineWidth <= 0f) return;
            Vector2 dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x);
            if (inwardDown ? normal.y > 0f : normal.y < 0f) normal = -normal;   // 内側を向ける
            Vector2 off = normal * outlineWidth;

            int i = vh.currentVertCount;
            vh.AddVert(a, c, Vector4.zero);
            vh.AddVert(b, c, Vector4.zero);
            vh.AddVert(b + off, c, Vector4.zero);
            vh.AddVert(a + off, c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color32 c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(min.x, min.y), c, Vector4.zero);
            vh.AddVert(new Vector2(min.x, max.y), c, Vector4.zero);
            vh.AddVert(new Vector2(max.x, max.y), c, Vector4.zero);
            vh.AddVert(new Vector2(max.x, min.y), c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}