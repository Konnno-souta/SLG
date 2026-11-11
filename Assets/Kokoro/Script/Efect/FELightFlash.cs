using UnityEngine;

/// <summary>ライトの明滅 (光球爆発・打撃の一瞬の照り返し)</summary>
[RequireComponent(typeof(Light))]
public class FELightFlash : MonoBehaviour
{
    public float delay;
    public float duration = 0.3f;
    public float peakIntensity = 3f;
    public AnimationCurve curve = new AnimationCurve();

    Light lt;
    FEEffect owner;
    float solo;

    void Awake()
    {
        lt = GetComponent<Light>();
        owner = GetComponentInParent<FEEffect>();
        lt.intensity = 0f;
    }

    void LateUpdate()
    {
        float e; bool playing;
        if (owner != null) { e = owner.Elapsed; playing = owner.IsPlaying; }
        else { solo += Time.deltaTime; e = solo; playing = true; }
        float t = (e - delay) / Mathf.Max(duration, 1e-4f);
        float k = 0f;
        if (playing && t >= 0f && t <= 1f) k = curve != null && curve.length > 0 ? curve.Evaluate(t) : 1f - t;
        lt.intensity = peakIntensity * k;
        lt.enabled = k > 0.001f;
    }
}
