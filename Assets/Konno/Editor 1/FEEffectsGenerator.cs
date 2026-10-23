using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// メニュー
///   Tools/FE Effects/1. Generate Prefabs        … EF01〜EF08 + EF07B のマテリアルとプレハブを生成 (再実行で上書き)
///   Tools/FE Effects/2. Setup Demo In Scene     … 今のシーンにカメラ・床・デモを配置
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

    // 攻撃エフェクト (ATK_*) と被弾エフェクト (HIT_*) を Generated/Battle に生成
    static void GenerateBattlePrefabs()
    {
        Directory.CreateDirectory(BattleDir);
        AssetDatabase.Refresh();
        int ok = 0;
        foreach (var ids in new[] { FEEffectFactory.AttackIds, FEEffectFactory.HitIds })
        {
            foreach (var id in ids)
            {
                var go = FEEffectFactory.BuildBattle(id);
                if (go == null) continue;
                go.SetActive(true);
                SaveMaterials(go, id);
                PrefabUtility.SaveAsPrefabAsset(go, BattleDir + "/" + id + ".prefab");
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
            arr[i] = AssetDatabase.LoadAssetAtPath<GameObject>(BattleDir + "/" + ids[i] + ".prefab");
        return arr;
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
