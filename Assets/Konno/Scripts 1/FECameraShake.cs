using UnityEngine;

/// <summary>メインカメラに付けるとエフェクトの揺れが有効になる</summary>
public class FECameraShake : MonoBehaviour
{
    public static FECameraShake Instance { get; private set; }
    [Tooltip("全体の揺れ倍率")] public float strength = 1f;

    float amp, dur, time;
    Vector3 applied;

    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    public static void Shake(float amplitude, float duration)
    {
        if (Instance == null) return;
        Instance.amp = amplitude;
        Instance.dur = Mathf.Max(duration, 0.01f);
        Instance.time = 0f;
    }

    void LateUpdate()
    {
        transform.localPosition -= applied;
        applied = Vector3.zero;
        if (time >= dur) return;
        time += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(time / dur);
        float tt = Time.time * 45f;
        var o = new Vector3(Mathf.PerlinNoise(tt, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, tt) - 0.5f, 0f) * (2f * amp * k * k * strength);
        applied = transform.localRotation * o;
        transform.localPosition += applied;
    }
}
