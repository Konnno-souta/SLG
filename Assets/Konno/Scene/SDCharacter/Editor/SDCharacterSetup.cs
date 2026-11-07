using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// メニュー「Tools > SD Character > Setup」
///   Project ウィンドウで FBX を選択（複数可）して実行すると、
///   ・FBX のインポート設定（Generic リグ / ループ設定 / クリップ名整理）
///   ・Animator Controller の自動生成（Idle-Walk-Run ブレンド + Wave）
///   ・シーンへの配置（CharacterController・操作スクリプト・追従カメラ・地面）
///   を行います。複数選ぶと横一列に並べ、最初の1体だけを操作キャラにします。
///   何も選んでいない場合は SD_Character_Animated.fbx を探してセットアップします。
/// </summary>
public static class SDCharacterSetup
{
    const string DefaultFbxName = "SD_Character_Animated";
    const float Spacing = 1.2f;

    [MenuItem("Tools/SD Character/Setup")]
    static void SetupMenu()
    {
        var paths = Selection.objects
            .Select(o => AssetDatabase.GetAssetPath(o))
            .Where(p => !string.IsNullOrEmpty(p) && p.ToLower().EndsWith(".fbx"))
            .Distinct().OrderBy(p => p).ToList();

        if (paths.Count == 0)
        {
            string found = AssetDatabase.FindAssets(DefaultFbxName)
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .FirstOrDefault(p => p.ToLower().EndsWith(".fbx"));
            if (found != null) paths.Add(found);
        }
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("SD Character",
                "Project ウィンドウでキャラクターの FBX を選択してから実行してください。", "OK");
            return;
        }

        int existing = Object.FindObjectsByType<SDCharacterController>(FindObjectsSortMode.None).Length;
        bool hasActivePlayer = Object.FindObjectsByType<SDCharacterController>(FindObjectsSortMode.None).Any(c => c.enabled);

        GameObject firstControlled = null;
        for (int i = 0; i < paths.Count; i++)
        {
            bool controlled = !hasActivePlayer && i == 0;
            var go = SetupOne(paths[i], new Vector3((existing + i) * Spacing, 0f, 0f), controlled);
            if (go && controlled) firstControlled = go;
        }

        if (firstControlled) AttachCamera(firstControlled.transform);
        EnsureGround();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SD Character] セットアップ完了！ Play で操作できます（WASD:移動 / Shift:走る / E:手を振る）");
    }

    static GameObject SetupOne(string path, Vector3 position, bool controlled)
    {
        string baseName = Path.GetFileNameWithoutExtension(path);

        // ---------- 1. インポート設定 ----------
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.clipAnimations = new ModelImporterClipAnimation[0];
        imp.SaveAndReimport();

        var clipsSettings = imp.defaultClipAnimations;
        foreach (var c in clipsSettings)
        {
            string shortName = c.takeName.Split('|').Last();   // "Kuran_Armature|Walk" -> "Walk"
            c.name = shortName;
            c.loopTime = shortName != "Wave";
        }
        imp.clipAnimations = clipsSettings;
        imp.SaveAndReimport();

        var clips = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();
        AnimationClip idle = clips.FirstOrDefault(c => c.name == "Idle");
        AnimationClip walk = clips.FirstOrDefault(c => c.name == "Walk");
        AnimationClip run  = clips.FirstOrDefault(c => c.name == "Run");
        AnimationClip wave = clips.FirstOrDefault(c => c.name == "Wave");
        if (!idle || !walk || !run || !wave)
        {
            Debug.LogError("[SD Character] " + baseName + " のクリップが見つかりません: "
                           + string.Join(", ", clips.Select(c => c.name)));
            return null;
        }

        // ---------- 2. Animator Controller（キャラごと） ----------
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        string ctrlPath = dir + "/" + baseName + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Wave", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        BlendTree tree;
        AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1.0f);
        tree.AddChild(run, 2.5f);
        var children = tree.children;          // 足の滑りを減らすため再生速度を調整
        children[1].timeScale = 1.6f;
        children[2].timeScale = 1.2f;
        tree.children = children;
        sm.defaultState = loco;

        AnimatorState waveState = sm.AddState("Wave");
        waveState.motion = wave;
        var toWave = loco.AddTransition(waveState);
        toWave.AddCondition(AnimatorConditionMode.If, 0f, "Wave");
        toWave.hasExitTime = false;
        toWave.duration = 0.15f;
        var back = waveState.AddTransition(loco);
        back.hasExitTime = true;
        back.exitTime = 0.95f;
        back.duration = 0.15f;
        AssetDatabase.SaveAssets();

        // ---------- 3. シーンに配置 ----------
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var player = new GameObject("Player_" + baseName);
        Undo.RegisterCreatedObjectUndo(player, "Create SD Character");
        player.transform.position = position;

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.transform.SetParent(player.transform, false);   // 向きが逆なら visual の Y 回転を 180 に
        var anim = visual.GetComponent<Animator>();
        if (!anim) anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.0f;
        cc.radius = 0.25f;
        cc.center = new Vector3(0f, 0.52f, 0f);
        cc.stepOffset = 0.15f;
        cc.skinWidth = 0.02f;
        var mover = player.AddComponent<SDCharacterController>();
        mover.enabled = controlled;   // 操作キャラ以外は待機アニメだけ再生

        Selection.activeGameObject = player;
        return player;
    }

    static void AttachCamera(Transform target)
    {
        var cam = Camera.main;
        if (!cam) return;
        var follow = cam.GetComponent<SDFollowCamera>();
        if (!follow) follow = Undo.AddComponent<SDFollowCamera>(cam.gameObject);
        follow.target = target;
        cam.transform.position = target.position + follow.offset;
        cam.transform.LookAt(target.position + Vector3.up * follow.lookHeight);
    }

    static void EnsureGround()
    {
        if (GameObject.Find("Ground")) return;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
    }
}
