using UnityEngine;

/// <summary>
/// 確認用デモ。画面左上のボタン or 数字キー 1〜9 / 0 で再生、S でスロー切替。
/// prefabs が空なら FEEffectFactory からその場で生成する (エディタ再生時)。
/// </summary>
public class FEEffectDemo : MonoBehaviour
{
    public GameObject[] prefabs = new GameObject[17];
    [Tooltip("攻撃側の足元")] public Vector3 attackerPos = Vector3.zero;
    [Tooltip("被弾側の足元")] public Vector3 targetPos = new Vector3(0f, 0f, 2.4f);
    public bool slowMotion;
    public bool autoLoop;

    bool lastSlow;
    float loopTimer;
    int loopIndex;

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < FEEffectFactory.Ids.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Spawn(i);
        if (FEEffectFactory.Ids.Length > 9 && Input.GetKeyDown(KeyCode.Alpha0)) Spawn(9);
        if (Input.GetKeyDown(KeyCode.S)) slowMotion = !slowMotion;
#endif
        // 毎フレーム上書きするとヒットストップを打ち消すので、切り替えた時だけ設定
        if (slowMotion != lastSlow) { lastSlow = slowMotion; Time.timeScale = slowMotion ? 0.2f : 1f; }

        if (autoLoop)
        {
            loopTimer -= Time.deltaTime;
            if (loopTimer <= 0f)
            {
                Spawn(loopIndex);
                loopIndex = (loopIndex + 1) % FEEffectFactory.Ids.Length;
                loopTimer = 2.2f;
            }
        }
    }

    public GameObject Spawn(int i)
    {
        Vector3 dir = targetPos - attackerPos; dir.y = 0f;
        var rot = dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;

        Vector3 pos;
        switch (i)
        {
            case 2:
            case 9:
            case 10:
            case 11: pos = targetPos + Vector3.up * 1.1f - rot * Vector3.forward * 0.3f; break; // 打撃: 被弾点
            case 5: pos = attackerPos + Vector3.up * 1.3f + rot * Vector3.forward * 0.2f; break; // 矢: 発射位置
            case 6:
            case 7:
            case 8: pos = targetPos; break; // 魔法: 相手の足元
            default: pos = attackerPos; break;
        }

        GameObject go;
        if (prefabs != null && i < prefabs.Length && prefabs[i] != null)
        {
            go = Instantiate(prefabs[i], pos, rot);
        }
        else
        {
            go = FEEffectFactory.Build(FEEffectFactory.Ids[i]);
            if (go == null) return null;
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
        }

        if (i == 5)
        {
            var m = go.AddComponent<FEMover>();
            m.velocity = rot * Vector3.forward * 9f;
        }
        return go;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 260, 620), GUI.skin.box);
        GUILayout.Label("FE Effects Demo");
        for (int i = 0; i < FEEffectFactory.Ids.Length; i++)
        {
            if (GUILayout.Button((i + 1) + " : " + FEEffectFactory.Ids[i] + " " + FEEffectFactory.Names[i]))
                Spawn(i);
        }
        slowMotion = GUILayout.Toggle(slowMotion, " Slow x0.2 (S)");
        autoLoop = GUILayout.Toggle(autoLoop, " Auto Loop");
        GUILayout.EndArea();
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
    }
}
