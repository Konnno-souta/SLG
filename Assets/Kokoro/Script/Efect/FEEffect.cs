using UnityEngine;

/// <summary>
/// エフェクト全体の再生管理。ルートに1つ付ける。
/// 子の FEMeshPart / ParticleSystem / FELightFlash はこの Elapsed を基準に動く。
/// </summary>
[DisallowMultipleComponent]
public class FEEffect : MonoBehaviour
{
    public string effectId;
    public string displayName;

    [Tooltip("この秒数で再生終了")]
    public float lifetime = 1.5f;
    public bool playOnEnable = true;
    public bool destroyOnFinish = true;
    [Min(0f)] public float playbackSpeed = 1f;

    [Header("Camera Shake (FECameraShake がカメラに付いている場合)")]
    public float shakeDelay;
    public float shakeAmplitude;
    public float shakeDuration = 0.2f;

    [Header("攻撃エフェクト用: 相手に当たるタイミングの目安 (秒)。FEBattleFX が被弾エフェクトを出す時刻に使う")]
    public float hitTime;

    [Header("Hit Stop (着弾の瞬間に時間を止めて重さを出す)")]
    [Tooltip("0 で無効。実時間の秒数")] public float hitStopDuration;
    public float hitStopDelay;
    [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;

    // 複数エフェクトが同時にヒットストップしても元の速度に戻せるよう共有する
    static float hitStopEnd = -1f;
    static float hitStopRestore = 1f;
    static bool hitStopActive;

    public float Elapsed { get; private set; }
    public bool IsPlaying { get; private set; }

    ParticleSystem[] systems;
    FEMeshPart[] parts;
    bool shook;
    bool stopped;

    void Cache()
    {
        if (systems == null) systems = GetComponentsInChildren<ParticleSystem>(true);
        if (parts == null) parts = GetComponentsInChildren<FEMeshPart>(true);
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play()
    {
        Cache();
        Elapsed = 0f;
        IsPlaying = true;
        shook = false;
        stopped = false;
        foreach (var p in parts) p.ResetPart();
        foreach (var ps in systems)
        {
            var main = ps.main;
            main.simulationSpeed = playbackSpeed;
            ps.Clear(false);
            ps.Play(false);
        }
    }

    public void Stop()
    {
        Cache();
        IsPlaying = false;
        foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (var p in parts) p.ResetPart();
    }

    void Update()
    {
        UpdateHitStop();
        if (!IsPlaying) return;
        Elapsed += Time.deltaTime * playbackSpeed;

        if (!shook && shakeAmplitude > 0f && Elapsed >= shakeDelay)
        {
            shook = true;
            FECameraShake.Shake(shakeAmplitude, shakeDuration);
        }

        if (!stopped && hitStopDuration > 0f && Elapsed >= hitStopDelay)
        {
            stopped = true;
            if (!hitStopActive) { hitStopRestore = Time.timeScale; hitStopActive = true; hitStopEnd = 0f; }
            Time.timeScale = Mathf.Min(Time.timeScale, hitStopTimeScale);
            hitStopEnd = Mathf.Max(hitStopEnd, Time.unscaledTime + hitStopDuration);
        }

        if (Elapsed >= lifetime)
        {
            IsPlaying = false;
            if (destroyOnFinish && Application.isPlaying) Destroy(gameObject);
        }
    }

    static void UpdateHitStop()
    {
        if (hitStopActive && Time.unscaledTime >= hitStopEnd)
        {
            hitStopActive = false;
            Time.timeScale = hitStopRestore;
        }
    }

    void OnDisable()
    {
        // ヒットストップ中に消えても時間が止まったままにならないように
        if (hitStopActive && stopped && Time.unscaledTime >= hitStopEnd) UpdateHitStop();
    }

    void OnDestroy()
    {
        if (hitStopActive && stopped)
        {
            hitStopActive = false;
            Time.timeScale = hitStopRestore;
        }
    }
}
