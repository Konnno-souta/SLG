#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Meshy AI などで作ったFBX(+テクスチャ)を、ステージに置ける小物プレハブにするエディタ拡張。
/// 使い方: Project で FBX を右クリック > SLG > 小物プレハブを作る(Meshy)
/// ・URP Lit のマテリアルを作る(色・ノーマル・金属+なめらかさ)
///   Meshy の「_metallic」「_roughness」を、URP 用の「_MetallicSmoothness」1枚にまとめる
/// ・原点を「底面の中心」にしたプレハブを作る(マップチップや床にそのまま置ける)
/// テクスチャは FBX と同じフォルダの「{FBX名}.png」「{FBX名}_normal.png」
/// 「{FBX名}_metallic.png」「{FBX名}_roughness.png」を自動で探す。
/// このファイルは「Editor」フォルダに置くこと。
/// </summary>
public sealed class MeshyPropSetup : EditorWindow
{
    [SerializeField] private GameObject model;
    [SerializeField] private string prefabName = "";
    [SerializeField] private float targetHeight = 0f;
    [SerializeField] private int maxTextureSize = 1024;
    [SerializeField] private bool removeColliders = true;

    [MenuItem("Assets/SLG/小物プレハブを作る(Meshy)", false, 2000)]
    private static void OpenFromSelection()
    {
        MeshyPropSetup w = GetWindow<MeshyPropSetup>("小物プレハブ作成");
        if (Selection.activeObject is GameObject go && AssetDatabase.Contains(go))
        {
            w.model = go;
            w.prefabName = CleanName(go.name);
        }
    }

    [MenuItem("Assets/SLG/小物プレハブを作る(Meshy)", true)]
    private static bool ValidateSelection()
    {
        if (Selection.activeObject == null) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/小物プレハブ作成(Meshy)")]
    private static void Open() => GetWindow<MeshyPropSetup>("小物プレハブ作成");

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        model = (GameObject)EditorGUILayout.ObjectField("モデル(FBX)", model, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && model != null && string.IsNullOrEmpty(prefabName))
            prefabName = CleanName(model.name);

        prefabName = EditorGUILayout.TextField("プレハブ名", prefabName);
        targetHeight = EditorGUILayout.FloatField(new GUIContent("高さ(m)", "0ならモデルの大きさのまま"), targetHeight);
        maxTextureSize = EditorGUILayout.IntPopup("テクスチャの最大サイズ", maxTextureSize,
            new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
        removeColliders = EditorGUILayout.Toggle("コライダーを付けない", removeColliders);

        EditorGUILayout.HelpBox(
            "FBXと同じフォルダに、マテリアル・まとめたテクスチャ・プレハブを作ります。\n" +
            "原点は底面の中心になります。", MessageType.Info);

        using (new EditorGUI.DisabledScope(model == null || string.IsNullOrWhiteSpace(prefabName)))
        {
            if (GUILayout.Button("作成", GUILayout.Height(30))) Build();
        }
    }

    private static string CleanName(string n)
    {
        // 「Meshy_AI_Rustic_Wooden_Barrel_20261128_texture」→「Rustic_Wooden_Barrel」
        n = n.Replace("Meshy_AI_", "").Replace("_texture", "");
        int us = n.LastIndexOf('_');
        if (us > 0 && n.Length - us - 1 == 8 && long.TryParse(n.Substring(us + 1), out _)) n = n.Substring(0, us);
        return n;
    }

    // ================================================================ 作成

    private void Build()
    {
        string modelPath = AssetDatabase.GetAssetPath(model);
        string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string baseName = Path.GetFileNameWithoutExtension(modelPath);

        string colorPath = FindTex(folder, baseName, "");
        string normalPath = FindTex(folder, baseName, "_normal");
        string metallicPath = FindTex(folder, baseName, "_metallic");
        string roughPath = FindTex(folder, baseName, "_roughness");

        // ---- テクスチャの設定
        if (colorPath != null) SetupTexture(colorPath, TextureImporterType.Default, true);
        if (normalPath != null) SetupTexture(normalPath, TextureImporterType.NormalMap, false);
        string msPath = null;
        if (metallicPath != null || roughPath != null)
        {
            msPath = $"{folder}/{prefabName}_MetallicSmoothness.png";
            BuildMetallicSmoothness(metallicPath, roughPath, msPath);
            SetupTexture(msPath, TextureImporterType.Default, false);
        }

        // ---- マテリアル(URP Lit)
        string matPath = $"{folder}/{prefabName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        Texture2D color = Load(colorPath), normal = Load(normalPath), ms = Load(msPath);
        SetTex(mat, color, "_BaseMap", "_MainTex");
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            SetTex(mat, normal, "_BumpMap");
            mat.EnableKeyword("_NORMALMAP");
        }
        if (ms != null)
        {
            SetTex(mat, ms, "_MetallicGlossMap");
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f);   // マップのαをそのまま使う
            if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f);
        }
        EditorUtility.SetDirty(mat);

        // ---- プレハブ(原点=底面の中心)
        GameObject root = new GameObject(prefabName);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        inst.transform.localPosition = Vector3.zero;

        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        if (removeColliders)
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);

        Bounds b = WorldBounds(inst);
        if (targetHeight > 0f && b.size.y > 0.0001f)
        {
            inst.transform.localScale *= targetHeight / b.size.y;
            b = WorldBounds(inst);
        }
        inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

        string prefabPath = $"{folder}/{prefabName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(saved);
        Selection.activeObject = saved;

        b.center = Vector3.zero;
        Debug.Log($"小物プレハブを作りました: {prefabPath}  大きさ 幅{b.size.x:0.00} × 高さ{b.size.y:0.00} × 奥行{b.size.z:0.00} m", saved);
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ================================================================ テクスチャ

    private static string FindTex(string folder, string baseName, string suffix)
    {
        foreach (string ext in new[] { ".png", ".jpg", ".jpeg", ".tga" })
        {
            string p = $"{folder}/{baseName}{suffix}{ext}";
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private void SetupTexture(string path, TextureImporterType type, bool srgb)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        ti.textureType = type;
        ti.sRGBTexture = type == TextureImporterType.NormalMap || srgb;
        ti.maxTextureSize = maxTextureSize;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.SaveAndReimport();
    }

    // R(=G=B)=金属、A=なめらかさ(1-粗さ) の1枚を作る
    private void BuildMetallicSmoothness(string metallicPath, string roughPath, string outPath)
    {
        Texture2D metal = metallicPath != null ? ReadFile(metallicPath) : null;
        Texture2D rough = roughPath != null ? ReadFile(roughPath) : null;
        int size = Mathf.Min(maxTextureSize, Mathf.Max(metal != null ? metal.width : 0, rough != null ? rough.width : 0));
        size = Mathf.Max(4, size);

        Texture2D outTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float m = metal != null ? metal.GetPixelBilinear(u, v).r : 0f;
            float r = rough != null ? rough.GetPixelBilinear(u, v).r : 0.5f;
            px[y * size + x] = new Color(m, m, m, 1f - r);
        }
        outTex.SetPixels(px);
        outTex.Apply();
        File.WriteAllBytes(outPath, outTex.EncodeToPNG());
        DestroyImmediate(outTex);
        if (metal != null) DestroyImmediate(metal);
        if (rough != null) DestroyImmediate(rough);
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
    }

    private static Texture2D ReadFile(string path)
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        t.LoadImage(File.ReadAllBytes(path));
        return t;
    }

    private static Texture2D Load(string path) => path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;

    private static void SetTex(Material m, Texture t, params string[] props)
    {
        if (t == null) return;
        foreach (string p in props) if (m.HasProperty(p)) m.SetTexture(p, t);
    }
}
#endif
