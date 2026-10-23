using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プロシージャルメッシュ (弧 / 直線 / 筒 / リング) を生成し、
/// シェーダーのプロパティとスケールを 0〜1 の正規化時間カーブで動かす。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FEMeshPart : MonoBehaviour
{
    public enum Kind { Arc, Line, Tube, Ring, Sphere }

    [Serializable]
    public class FloatTrack
    {
        public string property = "_Dissolve";
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float multiplier = 1f;
    }

    public Kind kind = Kind.Arc;

    [Header("Arc : ローカルYZ平面 (0°=前方+Z, 90°=上+Y) / Sphere は radius のみ使用")]
    public float radius = 1.2f;
    public float width = 0.4f;
    public float startAngle = 110f;
    public float endAngle = -70f;
    [Tooltip("弧の中央をX方向に膨らませて立体感を出す")]
    public float bend = 0.1f;
    [Tooltip("振り始め→終わりで X 方向にずれる量 (らせん状の奥行き)")]
    public float depth;
    [Tooltip("帯の断面のねじれ (度)")]
    public float twist;
    [Tooltip("刃側の厚み (くさび形, 上下2面)")]
    public float thickness;

    [Header("Line : +Z方向 (十字リボン)")]
    public float length = 2f;
    public float lineWidth = 0.25f;
    [Tooltip("帯の枚数 (2 = 十字, 4 = 米字)")]
    public int lineFins = 2;

    [Header("Tube : +Y方向 (円錐にもなる)")]
    public float bottomRadius = 0.5f;
    public float topRadius = 0.5f;
    public float height = 2f;

    [Header("Ring : XZ平面")]
    public float innerRadius = 0.3f;
    public float outerRadius = 1f;

    public int segments = 48;

    [Header("Timing (秒)")]
    public float delay;
    public float duration = 0.5f;

    [Header("Animation (横軸 = 0〜1 の正規化時間)")]
    public List<FloatTrack> tracks = new List<FloatTrack>();
    [Tooltip("空のカーブ = 常に1")]
    public AnimationCurve scaleX = new AnimationCurve();
    public AnimationCurve scaleY = new AnimationCurve();
    public AnimationCurve scaleZ = new AnimationCurve();
    [Tooltip("度/秒")]
    public float spinSpeed;
    public Vector3 spinAxis = Vector3.up;

    FEEffect owner;
    MeshRenderer mr;
    MaterialPropertyBlock mpb;
    Mesh mesh;
    Vector3 baseScale;
    Quaternion baseRot;
    bool inited;
    float soloTime;

    void Init()
    {
        if (inited) return;
        inited = true;
        mr = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();
        owner = GetComponentInParent<FEEffect>();
        baseScale = transform.localScale;
        baseRot = transform.localRotation;
        BuildMesh();
        mr.enabled = false;
    }

    public void BuildMesh()
    {
        if (mesh != null) DestroySafe(mesh);
        switch (kind)
        {
            case Kind.Arc:  mesh = FEMeshBuilder.Arc(radius, width, startAngle, endAngle, bend, segments, depth, twist, thickness); break;
            case Kind.Line: mesh = FEMeshBuilder.Line(length, lineWidth, segments, lineFins); break;
            case Kind.Tube: mesh = FEMeshBuilder.Tube(bottomRadius, topRadius, height, segments, 8); break;
            case Kind.Sphere: mesh = FEMeshBuilder.Sphere(radius, segments); break;
            default:        mesh = FEMeshBuilder.Ring(innerRadius, outerRadius, segments); break;
        }
        mesh.hideFlags = HideFlags.DontSave;
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    public void ResetPart()
    {
        Init();
        soloTime = 0f;
        transform.localScale = baseScale;
        transform.localRotation = baseRot;
        mr.enabled = false;
    }

    void OnEnable() { Init(); }

    void LateUpdate()
    {
        Init();
        float elapsed;
        bool playing;
        if (owner != null) { elapsed = owner.Elapsed; playing = owner.IsPlaying; }
        else { soloTime += Time.deltaTime; elapsed = soloTime; playing = true; }

        float t = (elapsed - delay) / Mathf.Max(duration, 1e-4f);
        bool vis = playing && t >= 0f && t <= 1f;
        if (mr.enabled != vis) mr.enabled = vis;
        if (!vis) return;

        mr.GetPropertyBlock(mpb);
        foreach (var tr in tracks)
        {
            if (tr == null || string.IsNullOrEmpty(tr.property) || tr.curve == null || tr.curve.length == 0) continue;
            mpb.SetFloat(tr.property, tr.curve.Evaluate(t) * tr.multiplier);
        }
        mr.SetPropertyBlock(mpb);

        transform.localScale = Vector3.Scale(baseScale, new Vector3(Eval(scaleX, t), Eval(scaleY, t), Eval(scaleZ, t)));
        if (spinSpeed != 0f)
            transform.localRotation = baseRot * Quaternion.AngleAxis(spinSpeed * (elapsed - delay), spinAxis);
    }

    static float Eval(AnimationCurve c, float t)
    {
        return (c == null || c.length == 0) ? 1f : c.Evaluate(t);
    }

    void OnDestroy()
    {
        if (mesh != null) DestroySafe(mesh);
    }

    static void DestroySafe(UnityEngine.Object o)
    {
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
