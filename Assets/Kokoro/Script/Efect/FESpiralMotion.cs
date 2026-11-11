using UnityEngine;

/// <summary>
/// 中心(親の原点)の周りを回りながら半径を縮める = 台風/ハリケーン状に寄っていく動き。
/// EF07 のミニ光球に使用。子に FEMeshPart / ParticleSystem を置く。
/// 横軸 0〜1 の正規化時間 (delay〜delay+duration)。
/// </summary>
public class FESpiralMotion : MonoBehaviour
{
    public float delay;
    public float duration = 1f;
    [Tooltip("開始角度 (度, XZ平面, 0°=+X)")]
    public float startAngle;
    [Tooltip("半径 (m)")]
    public AnimationCurve radius = AnimationCurve.Linear(0f, 2f, 1f, 0f);
    [Tooltip("開始角度からの回転量 (度)")]
    public AnimationCurve angle = AnimationCurve.Linear(0f, 0f, 1f, 360f);
    public float height;
    [Tooltip("任意。移動中だけ emitting にする (彗星の尾)")]
    public TrailRenderer trail;

    FEEffect owner;
    float solo;

    void Awake()
    {
        owner = GetComponentInParent<FEEffect>();
        Apply(0f);
        if (trail != null) { trail.emitting = false; trail.Clear(); }
    }

    void LateUpdate()
    {
        float e;
        if (owner != null) e = owner.Elapsed;
        else { solo += Time.deltaTime; e = solo; }
        float raw = (e - delay) / Mathf.Max(duration, 1e-4f);
        Apply(Mathf.Clamp01(raw));

        if (trail != null)
        {
            bool playing = owner == null || owner.IsPlaying;
            bool emit = playing && raw >= 0f && raw <= 1f;
            if (emit && !trail.emitting) trail.Clear();
            trail.emitting = emit;
        }
    }

    void Apply(float t)
    {
        float r = Eval(radius, t, 0f);
        float a = (startAngle + Eval(angle, t, 0f)) * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(Mathf.Cos(a) * r, height, Mathf.Sin(a) * r);
    }

    static float Eval(AnimationCurve c, float t, float fallback)
    {
        return (c == null || c.length == 0) ? fallback : c.Evaluate(t);
    }
}
