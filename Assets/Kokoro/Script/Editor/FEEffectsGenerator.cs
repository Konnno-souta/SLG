using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// メニュー
///   Tools/FE Effects/1. Generate Prefabs        … EF01〜EF08 + EF07B のマテリアルとプレハブを生成 (再実行で上書き)
///   Tools/FE Effects/2. Setup Demo In Scene     … 今のシーンにカメラ・床・デモを配置
///   Tools/FE Effects/3. Export ... (.unitypackage) … 他のプロジェクト用に書き出し
/// </summary>
public static class FEEffectsGenerator
{
    const string Root = "Assets/FEEffects/Generated";
    const string MatDir = Root + "/Materials";
    const string PrefabDir = Root + "/Prefabs";
    const string BattleDir = Root + "/Battle";

    [MenuItem("Tools/FE Effects/1. Generate Prefabs (残像: 黒)")]
    public static void GeneratePrefabs()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(PrefabDir);
        AssetDatabase.Refresh();

        int ok = 0;
        for (int i = 0; i < FEEffectFactory.Ids.Length; i++)
        {
            var go = FEEffectFactory.Build(FEEffectFactory.Ids[i]);
            if (go == null) continue;
            go.SetActive(true);
            SaveMaterials(go, FEEffectFactory.Ids[i]);
            string path = PrefabDir + "/" + FEEffectFactory.FileNames[i] + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            ok++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FE] " + ok + " prefabs generated in " + PrefabDir);
        GenerateBattlePrefabs();
    }

    // 攻撃エフェクト (ATK_*) → Generated/Battle/Attack、被弾エフェクト (HIT_*) → Generated/Battle/Hit
    // 赤ver (*_Red)          → Generated/Battle/Red/Attack と Generated/Battle/Red/Hit
    const string AttackDir = BattleDir + "/Attack";
    const string HitDir = BattleDir + "/Hit";
    const string RedDir = BattleDir + "/Red";

    static bool IsRed(string id) { return id.EndsWith(FEEffectFactory.RedSuffix); }

    static string BattlePath(string id)
    {
        string dir = IsRed(id) ? RedDir + (id.StartsWith("ATK_") ? "/Attack" : "/Hit")
                               : (id.StartsWith("ATK_") ? AttackDir : HitDir);
        return dir + "/" + id + ".prefab";
    }

    static bool IsBattleId(string id)
    {
        foreach (var ids in FEEffectFactory.AllBattleIdGroups)
            foreach (var x in ids) if (x == id) return true;
        return false;
    }

    static void GenerateBattlePrefabs()
    {
        Directory.CreateDirectory(AttackDir);
        Directory.CreateDirectory(HitDir);
        Directory.CreateDirectory(RedDir + "/Attack");
        Directory.CreateDirectory(RedDir + "/Hit");
        AssetDatabase.Refresh();

        // 前のバージョンで別の場所に生成されたプレハブは、正しいフォルダへ「移動」(GUID が変わらないので参照は切れない)。
        // 打撃の統合で無くなった HIT_BluntCrit (と赤ver) は削除する
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BattleDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var id = Path.GetFileNameWithoutExtension(path);
            if (id.StartsWith("HIT_BluntCrit")) { AssetDatabase.DeleteAsset(path); continue; }
            if (!IsBattleId(id)) continue;
            var dest = BattlePath(id);
            if (path == dest) continue;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) == null) AssetDatabase.MoveAsset(path, dest);
            else AssetDatabase.DeleteAsset(path);
        }

        int ok = 0;
        foreach (var ids in FEEffectFactory.AllBattleIdGroups)
        {
            foreach (var id in ids)
            {
                var go = FEEffectFactory.BuildBattle(id);
                if (go == null) continue;
                go.SetActive(true);
                SaveMaterials(go, id);
                PrefabUtility.SaveAsPrefabAsset(go, BattlePath(id));
                Object.DestroyImmediate(go);
                ok++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FE] " + ok + " battle prefabs (攻撃/被弾) generated in " + BattleDir);
    }

    static GameObject[] LoadBattle(string[] ids)
    {
        var arr = new GameObject[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            arr[i] = AssetDatabase.LoadAssetAtPath<GameObject>(BattlePath(ids[i]));
        return arr;
    }

    // ---------------------------------------------------------------- 別プロジェクト用の書き出し
    //  攻撃/被弾のプレハブを1個ずつ .unitypackage にする。中身:
    //    プレハブ + マテリアル + シェーダー4つ (FECommon.cginc 含む) + 再生用スクリプト7本
    //  FECommon.cginc は Unity の依存関係に出てこないので明示的に入れる。
    //  FEBattleFX / FEEffectFactory / デモ は入れない (プレハブを置くか Instantiate するだけで再生される)。
    const string ExportDir = "FEEffects_Export";
    const string Base = "Assets/FEEffects";
    static readonly string[] RuntimeFiles =
    {
        Base + "/Shaders/FECommon.cginc",
        Base + "/Shaders/FE_Slash.shader",
        Base + "/Shaders/FE_Column.shader",
        Base + "/Shaders/FE_Particle.shader",
        Base + "/Scripts/FEEffect.cs",
        Base + "/Scripts/FEMeshPart.cs",
        Base + "/Scripts/FEMeshBuilder.cs",
        Base + "/Scripts/FELightFlash.cs",
        Base + "/Scripts/FECameraShake.cs",
        Base + "/Scripts/FESpiralMotion.cs",
        Base + "/Scripts/FEMover.cs",
    };

    [MenuItem("Tools/FE Effects/3. Export Each Attack & Hit (.unitypackage)")]
    public static void ExportBattlePackages()
    {
        // プレハブが無ければ先に生成
        bool missing = false;
        foreach (var ids in FEEffectFactory.AllBattleIdGroups)
            foreach (var id in ids)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(BattlePath(id)) == null) missing = true;
        if (missing) GenerateBattlePrefabs();

        foreach (var f in RuntimeFiles)
        {
            if (!File.Exists(f))
            {
                EditorUtility.DisplayDialog("FE Effects", "必要なファイルが見つかりません:\n" + f + "\n\nFEEffects フォルダが Assets 直下にあるか確認してください。", "OK");
                return;
            }
        }

        // 青 → FEEffects_Export/ 、赤ver → FEEffects_Export/Red/ に1個ずつ + それぞれの全部入り
        int ok = 0;
        ok += ExportGroup(new[] { FEEffectFactory.AttackIds, FEEffectFactory.HitIds }, ExportDir, "FE_AllAttackAndHit");
        ok += ExportGroup(new[] { FEEffectFactory.AttackIdsRed, FEEffectFactory.HitIdsRed }, ExportDir + "/Red", "FE_AllAttackAndHit_Red");

        var full = Path.GetFullPath(ExportDir);
        Debug.Log("[FE] " + ok + " packages (+ 青・赤の全部入り) exported to " + full);
        EditorUtility.RevealInFinder(full);
    }

    static int ExportGroup(string[][] groups, string dir, string allName)
    {
        Directory.CreateDirectory(dir);
        var all = new HashSet<string>(RuntimeFiles);
        int ok = 0;
        foreach (var ids in groups)
        {
            foreach (var id in ids)
            {
                var prefab = BattlePath(id);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null) continue;
                var files = new HashSet<string>(RuntimeFiles);
                foreach (var d in AssetDatabase.GetDependencies(prefab, true))
                    if (!d.Contains("/Editor/")) files.Add(d);
                var arr = new string[files.Count];
                files.CopyTo(arr);
                AssetDatabase.ExportPackage(arr, dir + "/FE_" + id + ".unitypackage", ExportPackageOptions.Default);
                all.UnionWith(files);
                ok++;
            }
        }
        var allArr = new string[all.Count];
        all.CopyTo(allArr);
        AssetDatabase.ExportPackage(allArr, dir + "/" + allName + ".unitypackage", ExportPackageOptions.Default);
        return ok;
    }

    // EF01〜EF09 などの単体エフェクトも含めた、すべてのプレハブの全部入り
    [MenuItem("Tools/FE Effects/3b. Export All Effects (.unitypackage)")]
    public static void ExportAllEffects()
    {
        foreach (var f in RuntimeFiles)
        {
            if (!File.Exists(f))
            {
                EditorUtility.DisplayDialog("FE Effects", "必要なファイルが見つかりません:\n" + f, "OK");
                return;
            }
        }
        if (AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }).Length == 0) GeneratePrefabs();

        var files = new HashSet<string>(RuntimeFiles);
        foreach (var dir in new[] { PrefabDir, BattleDir })
        {
            if (!AssetDatabase.IsValidFolder(dir)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
                foreach (var d in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true))
                    if (!d.Contains("/Editor/")) files.Add(d);
        }
        Directory.CreateDirectory(ExportDir);
        var arr = new string[files.Count];
        files.CopyTo(arr);
        AssetDatabase.ExportPackage(arr, ExportDir + "/FE_AllEffects.unitypackage", ExportPackageOptions.Default);
        var full = Path.GetFullPath(ExportDir);
        Debug.Log("[FE] all effects exported to " + full);
        EditorUtility.RevealInFinder(full);
    }

    static void SaveMaterials(GameObject go, string id)
    {
        var map = new Dictionary<Material, Material>();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || AssetDatabase.Contains(m)) continue;
                Material saved;
                if (!map.TryGetValue(m, out saved))
                {
                    string p = MatDir + "/" + m.name + ".mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (existing != null)
                    {
                        // 既存アセットを上書き (GUID を保って参照切れを防ぐ)
                        existing.shader = m.shader;
                        existing.CopyPropertiesFromMaterial(m);
                        EditorUtility.SetDirty(existing);
                        saved = existing;
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(m, p);
                        saved = m;
                    }
                    map[m] = saved;
                }
                mats[i] = saved;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    [MenuItem("Tools/FE Effects/1b. Generate Prefabs (残像: 暗い青)")]
    public static void GeneratePrefabsDarkAfterimage()
    {
        GenerateWithAfterimage(FEEffectFactory.AfterimageStyle.DarkBlue);
    }

    [MenuItem("Tools/FE Effects/1c. Generate Prefabs (残像: 青く光る)")]
    public static void GeneratePrefabsGlowAfterimage()
    {
        GenerateWithAfterimage(FEEffectFactory.AfterimageStyle.BlueGlow);
    }

    static void GenerateWithAfterimage(FEEffectFactory.AfterimageStyle style)
    {
        var prev = FEEffectFactory.Afterimage;
        FEEffectFactory.Afterimage = style;
        try { GeneratePrefabs(); }
        finally { FEEffectFactory.Afterimage = prev; }
    }

    [MenuItem("Tools/FE Effects/2. Setup Demo In Scene")]
    public static void SetupDemo()
    {
        // カメラ (斜め45°くらいのバトル視点)
        var cam = Camera.main;
        if (cam == null)
        {
            var cgo = new GameObject("Main Camera");
            cgo.tag = "MainCamera";
            cam = cgo.AddComponent<Camera>();
            Undo.RegisterCreatedObjectUndo(cgo, "FE Demo");
        }
        Undo.RecordObject(cam.transform, "FE Demo");
        Undo.RecordObject(cam, "FE Demo");
        cam.transform.position = new Vector3(4.2f, 2.1f, -1.6f);
        cam.transform.LookAt(new Vector3(0f, 1.1f, 1f));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.22f, 0.24f, 0.3f);
        cam.allowHDR = true;
        if (cam.GetComponent<FECameraShake>() == null) Undo.AddComponent<FECameraShake>(cam.gameObject);

        if (Object.FindObjectOfType<Light>() == null)
        {
            var lgo = new GameObject("Directional Light");
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Directional;
            lgo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(lgo, "FE Demo");
        }

        if (GameObject.Find("FE_Ground") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "FE_Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            Undo.RegisterCreatedObjectUndo(ground, "FE Demo");
        }

        Dummy("FE_Attacker", new Vector3(0f, 1f, -0.5f));
        Dummy("FE_Target", new Vector3(0f, 1f, 2.6f));

        var demoGo = GameObject.Find("FE_Demo");
        if (demoGo == null)
        {
            demoGo = new GameObject("FE_Demo");
            Undo.RegisterCreatedObjectUndo(demoGo, "FE Demo");
        }
        var demo = demoGo.GetComponent<FEEffectDemo>();
        if (demo == null) demo = Undo.AddComponent<FEEffectDemo>(demoGo);

        var prefabs = new GameObject[FEEffectFactory.Ids.Length];
        for (int i = 0; i < prefabs.Length; i++)
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + FEEffectFactory.FileNames[i] + ".prefab");
        Undo.RecordObject(demo, "FE Demo");
        demo.prefabs = prefabs;
        EditorUtility.SetDirty(demo);

        // 攻撃/被弾を別々に出す窓口 + そのデモ
        var bfx = Object.FindObjectOfType<FEBattleFX>();
        if (bfx == null) bfx = Undo.AddComponent<FEBattleFX>(demoGo);
        Undo.RecordObject(bfx, "FE Demo");
        bfx.attackPrefabs = LoadBattle(FEEffectFactory.AttackIds);
        bfx.hitPrefabs = LoadBattle(FEEffectFactory.HitIds);
        bfx.attackPrefabsRed = LoadBattle(FEEffectFactory.AttackIdsRed);
        bfx.hitPrefabsRed = LoadBattle(FEEffectFactory.HitIdsRed);
        EditorUtility.SetDirty(bfx);
        var bdemo = demoGo.GetComponent<FEBattleDemo>();
        if (bdemo == null) bdemo = Undo.AddComponent<FEBattleDemo>(demoGo);
        Undo.RecordObject(bdemo, "FE Demo");
        bdemo.attackerPos = demo.attackerPos;
        bdemo.targetPos = demo.targetPos;
        EditorUtility.SetDirty(bdemo);

        EditorSceneManager.MarkSceneDirty(demoGo.scene);
        Selection.activeGameObject = demoGo;
        Debug.Log("[FE] Demo ready. Press Play → ボタン or 1〜9キー");
    }

    static void Dummy(string name, Vector3 pos)
    {
        if (GameObject.Find(name) != null) return;
        var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        c.name = name;
        c.transform.position = pos;
        c.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        Undo.RegisterCreatedObjectUndo(c, "FE Demo");
    }
}
