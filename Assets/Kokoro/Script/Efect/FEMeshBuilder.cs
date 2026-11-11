using UnityEngine;

/// <summary>エフェクト用の簡易メッシュ生成</summary>
public static class FEMeshBuilder
{
    /// 弧のリボン (YZ平面)。uv.x=軌跡 uv.y=0内側/1外側(刃)
    public static Mesh Arc(float radius, float width, float startDeg, float endDeg, float bend, int seg)
    {
        return Arc(radius, width, startDeg, endDeg, bend, seg, 0f, 0f, 0f);
    }

    /// 立体版の弧。
    ///   depth     : 振り始め→振り終わりで X 方向にずれる量 (らせん状の奥行き)
    ///   twistDeg  : 進むにつれて帯の断面をねじる角度 (始点 -半分 → 終点 +半分)
    ///   thickness : 刃側ほど厚いくさび形にする (上下2面)。0 なら1枚の帯
    public static Mesh Arc(float radius, float width, float startDeg, float endDeg, float bend, int seg,
                           float depth, float twistDeg, float thickness)
    {
        seg = Mathf.Max(2, seg);
        int faces = thickness > 0f ? 2 : 1;
        int per = (seg + 1) * 2;
        var v = new Vector3[per * faces]; var uv = new Vector2[v.Length]; var col = new Color[v.Length];
        var tri = new int[seg * 6 * faces];
        float dirSign = Mathf.Sign(endDeg - startDeg);
        for (int f = 0; f < faces; f++)
        {
            float side = faces == 1 ? 0f : (f == 0 ? 1f : -1f);
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                float a = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                var radial = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                var tangent = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a)) * dirSign;
                float x = bend * Mathf.Sin(t * Mathf.PI) + depth * (t - 0.5f);
                float tw = twistDeg * (t - 0.5f) * Mathf.Deg2Rad;
                // 断面: 幅方向 (半径方向を接線まわりにねじる) と 厚み方向
                var across = radial * Mathf.Cos(tw) + Vector3.right * Mathf.Sin(tw);
                var thick = Vector3.Cross(tangent, across).normalized;
                var edge = radial * radius + Vector3.right * x;
                int b = f * per + i * 2;
                v[b] = edge - across * width;                                   // 内側 (厚み0)
                v[b + 1] = edge + thick * (thickness * 0.5f * side);            // 刃側 (厚み最大)
                uv[b] = new Vector2(t, 0f);
                uv[b + 1] = new Vector2(t, 1f);
                col[b] = col[b + 1] = Color.white;
            }
            Quads(tri, seg, f * per, f * seg * 6);
        }
        return Make("FE_Arc", v, uv, col, tri, null);
    }

    public static Mesh Line(float length, float width, int seg)
    {
        return Line(length, width, seg, 2);
    }

    /// 直線リボン (+Z方向)。fins 枚の帯を軸まわりに等間隔で重ねる (2 = 十字, 4 = 米字)
    public static Mesh Line(float length, float width, int seg, int fins)
    {
        seg = Mathf.Max(2, seg);
        fins = Mathf.Max(1, fins);
        int per = (seg + 1) * 2;
        var v = new Vector3[per * fins]; var uv = new Vector2[v.Length]; var col = new Color[v.Length];
        var tri = new int[seg * 6 * fins];
        float h = width * 0.5f;
        for (int r = 0; r < fins; r++)
        {
            float ang = Mathf.PI * r / fins;
            var across = new Vector3(Mathf.Sin(ang), Mathf.Cos(ang), 0f);
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                var p = new Vector3(0f, 0f, t * length);
                int b = r * per + i * 2;
                v[b] = p - across * h;
                v[b + 1] = p + across * h;
                uv[b] = new Vector2(t, 0f);
                uv[b + 1] = new Vector2(t, 1f);
                col[b] = col[b + 1] = Color.white;
            }
            Quads(tri, seg, r * per, r * seg * 6);
        }
        return Make("FE_Line", v, uv, col, tri, null);
    }

    /// 筒/円錐 (+Y方向)。uv.x=周 uv.y=高さ
    public static Mesh Tube(float r0, float r1, float h, int segU, int segV)
    {
        segU = Mathf.Max(3, segU); segV = Mathf.Max(1, segV);
        int cols = segU + 1, rows = segV + 1;
        var v = new Vector3[cols * rows]; var nr = new Vector3[v.Length];
        var uv = new Vector2[v.Length]; var col = new Color[v.Length];
        var tri = new int[segU * segV * 6];
        float slope = (r0 - r1) / Mathf.Max(h, 1e-4f);
        for (int j = 0; j < rows; j++)
        {
            float vv = (float)j / segV;
            float r = Mathf.Lerp(r0, r1, vv);
            for (int i = 0; i < cols; i++)
            {
                float u = (float)i / segU;
                float ang = u * Mathf.PI * 2f;
                float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                int idx = j * cols + i;
                v[idx] = new Vector3(c * r, vv * h, s * r);
                nr[idx] = new Vector3(c, slope, s).normalized;
                uv[idx] = new Vector2(u, vv);
                col[idx] = Color.white;
            }
        }
        int k = 0;
        for (int j = 0; j < segV; j++)
            for (int i = 0; i < segU; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                tri[k++] = a; tri[k++] = c; tri[k++] = b;
                tri[k++] = b; tri[k++] = c; tri[k++] = d;
            }
        return Make("FE_Tube", v, uv, col, tri, nr);
    }

    /// 平面リング (XZ平面, 上向き)。uv.x=周 uv.y=0内側/1外側
    public static Mesh Ring(float rIn, float rOut, int seg)
    {
        seg = Mathf.Max(3, seg);
        int n = (seg + 1) * 2;
        var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n]; var col = new Color[n];
        var tri = new int[seg * 6];
        for (int i = 0; i <= seg; i++)
        {
            float u = (float)i / seg;
            float ang = u * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            v[i * 2] = dir * rIn;
            v[i * 2 + 1] = dir * rOut;
            nr[i * 2] = nr[i * 2 + 1] = Vector3.up;
            uv[i * 2] = new Vector2(u, 0f);
            uv[i * 2 + 1] = new Vector2(u, 1f);
            col[i * 2] = col[i * 2 + 1] = Color.white;
        }
        Quads(tri, seg, 0, 0);
        return Make("FE_Ring", v, uv, col, tri, nr);
    }

    /// UV球。uv.x=経度 uv.y=0下極→1上極。FE/Column シェーダーで光球に使う
    public static Mesh Sphere(float r, int segU)
    {
        segU = Mathf.Max(8, segU);
        int segV = Mathf.Max(6, segU / 2);
        int cols = segU + 1, rows = segV + 1;
        var v = new Vector3[cols * rows]; var nr = new Vector3[v.Length];
        var uv = new Vector2[v.Length]; var col = new Color[v.Length];
        var tri = new int[segU * segV * 6];
        for (int j = 0; j < rows; j++)
        {
            float vv = (float)j / segV;
            float lat = (vv - 0.5f) * Mathf.PI;
            float y = Mathf.Sin(lat), ring = Mathf.Cos(lat);
            for (int i = 0; i < cols; i++)
            {
                float u = (float)i / segU;
                float ang = u * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(ang) * ring, y, Mathf.Sin(ang) * ring);
                int idx = j * cols + i;
                v[idx] = n * r; nr[idx] = n;
                uv[idx] = new Vector2(u, vv);
                col[idx] = Color.white;
            }
        }
        int k = 0;
        for (int j = 0; j < segV; j++)
            for (int i = 0; i < segU; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                tri[k++] = a; tri[k++] = c; tri[k++] = b;
                tri[k++] = b; tri[k++] = c; tri[k++] = d;
            }
        return Make("FE_Sphere", v, uv, col, tri, nr);
    }

    static void Quads(int[] tri, int seg, int baseV, int triOffset)
    {
        for (int i = 0; i < seg; i++)
        {
            int b = baseV + i * 2;
            int k = triOffset + i * 6;
            tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
            tri[k + 3] = b + 1; tri[k + 4] = b + 3; tri[k + 5] = b + 2;
        }
    }

    static Mesh Make(string name, Vector3[] v, Vector2[] uv, Color[] col, int[] tri, Vector3[] normals)
    {
        var m = new Mesh { name = name };
        m.vertices = v;
        m.uv = uv;
        m.colors = col;
        m.triangles = tri;
        if (normals != null) m.normals = normals; else m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
