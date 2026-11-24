using UnityEngine;

/// <summary>
/// 松明の火のエフェクト。炎・火の粉・光の輪のパーティクルと、揺らめく点光源を自動で作る。
/// 使い方: 松明の火を付けたい位置に空のGameObjectを置いてこのコンポーネントを付けるだけ
/// (Castle Stage Generator の「松明に火を付ける」を使うと、壁の小物に自動で付く)。
/// エフェクトは再生時に作られる。エディタ上ではオレンジの印(Gizmo)で位置だけ表示する。
/// ※このファイルは Editor フォルダに入れないこと(ゲーム中に使うため)。
/// </summary>
[DisallowMultipleComponent]
public sealed class TorchFlame : MonoBehaviour
{
    [Header("炎")]
    [Tooltip("加算合成のパーティクル用マテリアル。空なら再生時に自動で作る(ビルドでは設定推奨)")]
    [SerializeField] private Material flameMaterial;
    [SerializeField, Min(0.05f)] private float size = 1f;
    [SerializeField] private Color coreColor = new Color(1f, 0.92f, 0.6f);
    [SerializeField] private Color outerColor = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private bool sparks = true;
    [SerializeField] private bool halo = true;

    [Header("灯り")]
    [SerializeField] private bool useLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.62f, 0.3f);
    [SerializeField, Min(0f)] private float lightIntensity = 2f;
    [SerializeField, Min(0.1f)] private float lightRange = 5f;
    [SerializeField, Range(0f, 0.6f)] private float flicker = 0.25f;
    [SerializeField, Min(0f)] private float flickerSpeed = 8f;
    [SerializeField] private LightShadows shadows = LightShadows.None;

    [Header("その他")]
    [SerializeField] private bool litOnStart = true;

    private ParticleSystem flamePs;
    private ParticleSystem sparkPs;
    private ParticleSystem haloPs;
    private Light pointLight;
    private Vector3 lightBasePos;
    private float seed;
    private bool built;

    private static Material sharedRuntimeMaterial;

    public bool IsLit { get; private set; }

    private void Start()
    {
        seed = Random.value * 100f;
        if (litOnStart) Ignite();
    }

    private void Update()
    {
        if (pointLight == null || !pointLight.enabled) return;
        float t = Time.time * flickerSpeed + seed;
        float n = Mathf.PerlinNoise(t, seed);
        float n2 = Mathf.PerlinNoise(seed, t * 0.7f);
        pointLight.intensity = lightIntensity * (1f - flicker + 2f * flicker * n);
        pointLight.transform.localPosition =
            lightBasePos + new Vector3(n - 0.5f, 0f, n2 - 0.5f) * 0.04f * size;
    }

    // ---------------------------------------------------------------- 操作

    /// <summary>火を付ける。</summary>
    public void Ignite()
    {
        if (!built) Build();
        flamePs.Play();
        if (sparkPs != null) sparkPs.Play();
        if (haloPs != null) haloPs.Play();
        if (pointLight != null) pointLight.enabled = true;
        IsLit = true;
    }

    /// <summary>火を消す(炎はすっと消える)。</summary>
    public void Extinguish()
    {
        if (!built) return;
        flamePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (sparkPs != null) sparkPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (haloPs != null) haloPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (pointLight != null) pointLight.enabled = false;
        IsLit = false;
    }

    /// <summary>ステージ生成ツールから設定する。</summary>
    public void Configure(Material material, float flameSize, bool withLight)
    {
        flameMaterial = material;
        size = flameSize;
        useLight = withLight;
    }

    // ---------------------------------------------------------------- 生成

    private void Build()
    {
        built = true;
        Material mat = flameMaterial != null ? flameMaterial : GetRuntimeMaterial();

        flamePs = NewSystem("Flame", mat);
        SetupFlame(flamePs);

        if (sparks)
        {
            sparkPs = NewSystem("Sparks", mat);
            SetupSparks(sparkPs);
        }
        if (halo)
        {
            haloPs = NewSystem("Halo", mat);
            SetupHalo(haloPs);
        }
        if (useLight)
        {
            GameObject lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(transform, false);
            lightBasePos = new Vector3(0f, 0.12f * size, 0f);
            lightGo.transform.localPosition = lightBasePos;
            pointLight = lightGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = lightColor;
            pointLight.intensity = lightIntensity;
            float scale = Mathf.Max(0.01f, transform.lossyScale.x);
            pointLight.range = lightRange * size * scale;
            pointLight.shadows = shadows;
        }
    }

    private ParticleSystem NewSystem(string name, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 1f;
        return ps;
    }

    private void SetupFlame(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f * size, 0.5f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f * size, 0.24f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.08f;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.025f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 上向きに噴き出す

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeGradient(
            new[] { coreColor, outerColor, outerColor * 0.55f },
            new[] { 0f, 0.35f, 1f },
            new[] { 0f, 1f, 0.75f, 0f },
            new[] { 0f, 0.12f, 0.55f, 1f }));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.1f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f * size;
        noise.frequency = 2f;
        noise.scrollSpeed = 1.2f;
    }

    private void SetupSparks(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * size, 0.8f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f * size, 0.028f * size);
        main.gravityModifier = -0.04f;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.03f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeGradient(
            new[] { coreColor, outerColor },
            new[] { 0f, 1f },
            new[] { 1f, 1f, 0f },
            new[] { 0f, 0.6f, 1f }));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f * size;
        noise.frequency = 3f;
    }

    private void SetupHalo(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f * size, 0.75f * size);
        main.maxParticles = 4;

        var emission = ps.emission;
        emission.rateOverTime = 3.5f;

        var shape = ps.shape;
        shape.enabled = false;

        Color c = outerColor;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeGradient(
            new[] { c, c },
            new[] { 0f, 1f },
            new[] { 0f, 0.16f, 0f },
            new[] { 0f, 0.5f, 1f }));

        ps.transform.localPosition = new Vector3(0f, 0.12f * size, 0f);
    }

    private static Gradient MakeGradient(Color[] colors, float[] colorTimes, float[] alphas, float[] alphaTimes)
    {
        GradientColorKey[] ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], colorTimes[i]);
        GradientAlphaKey[] ak = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i], alphaTimes[i]);
        Gradient g = new Gradient();
        g.SetKeys(ck, ak);
        return g;
    }

    // ---------------------------------------------------------------- マテリアル

    private static Material GetRuntimeMaterial()
    {
        if (sharedRuntimeMaterial == null)
        {
            sharedRuntimeMaterial = CreateFlameMaterial(CreateGlowTexture());
            if (sharedRuntimeMaterial == null)
                Debug.LogWarning("TorchFlame: パーティクル用シェーダーが見つかりません。Flame Material を設定してください。");
        }
        return sharedRuntimeMaterial;
    }

    /// <summary>中心が明るく外へ向かって柔らかく消える丸いテクスチャ。</summary>
    public static Texture2D CreateGlowTexture(int resolution = 64)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true)
        {
            name = "TorchGlow",
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] px = new Color[resolution * resolution];
        float half = (resolution - 1) * 0.5f;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // なめらかに
                px[y * resolution + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>加算合成のパーティクル用マテリアル(URP。無ければ旧パイプライン用)。</summary>
    public static Material CreateFlameMaterial(Texture2D tex)
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urp != null)
        {
            Material m = new Material(urp) { name = "TorchFlame" };
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 2f);     // Additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            return m;
        }

        Shader legacy = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive");
        if (legacy == null) return null;
        Material lm = new Material(legacy) { name = "TorchFlame" };
        lm.SetTexture("_MainTex", tex);
        return lm;
    }

    // ---------------------------------------------------------------- エディタ表示

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        float s = 0.08f * size * Mathf.Max(0.01f, transform.lossyScale.x);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * s, s);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * s * 3f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!useLight) return;
        Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, lightRange * size * Mathf.Max(0.01f, transform.lossyScale.x));
    }
}
