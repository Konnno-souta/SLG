using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 2つのステージをつなぐ道
// ・From のステージがクリアされると道が開く
// ・道は「1マス分のプレハブ」を並べて作るので、見た目はプレハブを差し替えるだけで変えられる
// ・長さはステージの位置から自動で計算する
// ・Connect をオンにすると、マスを道の向きに伸ばして隙間なくつなげる
// ・Bend Points（曲がり角）を置くと、そこを通るように道が曲がる（Player もその道に沿って歩く）
public class StageRoad : MonoBehaviour
{
    [Header("つなぐステージ")]
    [SerializeField] StagePoint from;   // このステージをクリアすると開く
    [SerializeField] StagePoint to;

    [Header("曲がり角")]
    [Tooltip("道が通る曲がり角（From → 曲がり角 → To の順に通る）。Road の「︙」→「曲がり角を追加」で作れる")]
    [SerializeField] List<Transform> bendPoints = new List<Transform>();
    [Tooltip("オンにすると、曲がり角をなめらかなカーブでつなぐ。オフならカクッと曲がる")]
    [SerializeField] bool smoothCurve = true;
    [Tooltip("カーブのなめらかさ（大きいほどなめらか）")]
    [SerializeField, Range(4, 32)] int curveResolution = 12;

    [Header("見た目")]
    [Tooltip("開いた道の1マス（Quad、Cube、画像を貼った板など何でもOK）")]
    [SerializeField] GameObject segmentPrefab;
    [Tooltip("まだ開いていない道の1マス。空なら未開放のときは何も表示しない")]
    [SerializeField] GameObject lockedSegmentPrefab;
    [Tooltip("マスの間隔")]
    [SerializeField] float spacing = 1f;
    [Tooltip("ステージから道をどれだけ離して始めるか")]
    [SerializeField] float startOffset = 1f;
    [SerializeField] float endOffset = 1f;
    [Tooltip("道の高さ（地面に埋まらないよう少し浮かせる）")]
    [SerializeField] float roadY = 0.02f;
    [Tooltip("マスを道の向きに合わせて回転させる")]
    [SerializeField] bool alignToDirection = true;

    [Header("つなげる")]
    [Tooltip("オンにすると、マスを道の向きに伸ばして隙間なくつなげる")]
    [SerializeField] bool connect = true;
    [Tooltip("つなぎ目に線が見えるときに少し重ねる量（マスの長さに対する割合）。カーブでは少し大きめがおすすめ")]
    [SerializeField, Range(0f, 0.3f)] float overlap = 0.02f;

    [Header("開放演出")]
    [SerializeField] float openDelay = 0.3f;       // 演出が始まるまでの待ち
    [SerializeField] float appearInterval = 0.08f; // 1マスずつ出る間隔
    [SerializeField] float popDuration = 0.25f;    // 1マスが出きるまでの時間

    // 開放演出中の道の数（演出中は操作を止めるため）
    public static int OpeningCount { get; private set; }
    public static bool AnyOpening => OpeningCount > 0;

    readonly List<GameObject> openSegments = new List<GameObject>();
    readonly List<GameObject> lockedSegments = new List<GameObject>();
    readonly List<Vector3> baseScales = new List<Vector3>();
    bool opened;
    bool animating;

    string RoadKey => $"{from.stageId}_{to.stageId}";
    bool ShouldBeOpen => from != null && from.IsCleared;

    // ほかのスクリプトから道のつながりを調べるため
    public StagePoint From => from;
    public StagePoint To => to;
    public bool IsOpen => ShouldBeOpen;

    // 道を通れるか：両側のステージが解放されていれば通れる
    // （クリア以外の方法で解放した場合でも、行き来できるようにするため）
    public bool IsWalkable => from != null && to != null && from.IsUnlocked && to.IsUnlocked;

    // stage から、この道を通って行ける先（通れない・つながっていなければ null）
    public StagePoint GetOtherSide(StagePoint stage)
    {
        if (!IsWalkable) return null;
        if (stage == from) return to;
        if (stage == to) return from;
        return null;
    }

    // start のステージから道に沿って distance だけ進んだ位置（道がどちらへ伸びているかを調べるため）
    public Vector3 GetLeavePoint(StagePoint start, float distance)
    {
        List<Vector3> line = GetPathLine();
        if (start == to) line.Reverse();

        var cum = new List<float> { 0f };
        for (int i = 1; i < line.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(line[i - 1], line[i]));

        // 道が短いときは、道の長さの半分まで
        float d = Mathf.Min(distance, cum[cum.Count - 1] * 0.5f);
        Sample(line, cum, d, out Vector3 pos, out _);
        return pos;
    }

    // start のステージから反対側のステージまで、道に沿って歩くための通過点
    // （最後の点は行き先の Stand Position）
    public List<Vector3> GetWalkPath(StagePoint start)
    {
        var result = new List<Vector3>();
        StagePoint goal = start == from ? to : from;

        List<Vector3> line = GetPathLine();
        if (start == to) line.Reverse();

        // 最初（出発地点）と最後（到着地点）を除いた、途中の点
        for (int i = 1; i < line.Count - 1; i++) result.Add(line[i]);
        result.Add(goal.StandPosition);
        return result;
    }

    void OnEnable() { StageProgress.OnCleared += HandleCleared; }
    void OnDisable() { StageProgress.OnCleared -= HandleCleared; }

    void Start()
    {
        if (from == null || to == null || segmentPrefab == null)
        {
            Debug.LogWarning($"[StageRoad] {name}: From / To / Segment Prefab を設定してください", this);
            return;
        }

        Build();

        if (!ShouldBeOpen) return;

        if (StageProgress.IsRoadShown(RoadKey)) ShowOpenedImmediately();
        else StartCoroutine(OpenAnimation());   // クリア後はじめてマップに来たときだけ演出
    }

    // プレイ中にクリアされた場合（デバッグ用のクリアなど）
    void HandleCleared(int stageId)
    {
        if (from != null && stageId == from.stageId && !opened && openSegments.Count > 0)
            StartCoroutine(OpenAnimation());
    }

    // ---- 道の形 ----

    // From → 曲がり角 → To の点（高さは roadY）
    List<Vector3> GetControlPoints()
    {
        var pts = new List<Vector3>();
        pts.Add(Flat(from.transform.position));
        foreach (var b in bendPoints) if (b != null) pts.Add(Flat(b.position));
        pts.Add(Flat(to.transform.position));
        return pts;
    }

    Vector3 Flat(Vector3 p) { p.y = roadY; return p; }

    // 道の中心線（カーブの場合は細かく分けた折れ線）
    List<Vector3> GetPathLine()
    {
        List<Vector3> cp = GetControlPoints();
        if (!smoothCurve || cp.Count < 3) return cp;

        // Catmull-Rom 曲線：すべての曲がり角を通る、なめらかなカーブ
        var line = new List<Vector3>();
        for (int i = 0; i < cp.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? cp[i - 1] : cp[i] * 2f - cp[i + 1];
            Vector3 p1 = cp[i];
            Vector3 p2 = cp[i + 1];
            Vector3 p3 = i + 2 < cp.Count ? cp[i + 2] : cp[i + 1] * 2f - cp[i];

            for (int s = 0; s < curveResolution; s++)
            {
                float t = s / (float)curveResolution;
                line.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        line.Add(cp[cp.Count - 1]);
        return line;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // 中心線上で、始点から distance だけ進んだ位置と向き
    static void Sample(List<Vector3> line, List<float> cum, float distance, out Vector3 pos, out Vector3 dir)
    {
        int i = 1;
        while (i < line.Count - 1 && cum[i] < distance) i++;

        float segLen = cum[i] - cum[i - 1];
        float t = segLen > 0.0001f ? (distance - cum[i - 1]) / segLen : 0f;
        pos = Vector3.Lerp(line[i - 1], line[i], Mathf.Clamp01(t));
        dir = line[i] - line[i - 1];
        dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector3.forward;
    }

    // マスを置く位置・向きを計算する（両端の余白を除いた区間に、道に沿って均等に並べる）
    void GetSegments(List<Vector3> positions, List<Quaternion> rotations, out float step)
    {
        step = 0f;

        List<Vector3> line = GetPathLine();
        var cum = new List<float> { 0f };
        for (int i = 1; i < line.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(line[i - 1], line[i]));
        float total = cum[cum.Count - 1];

        float usable = total - startOffset - endOffset;
        if (usable < 0f || line.Count < 2) return;

        int count;
        float firstDist;
        if (connect)
        {
            // つなげる場合：区間をちょうど埋めるように、マスの中心を並べる
            count = Mathf.Max(1, Mathf.RoundToInt(usable / Mathf.Max(0.01f, spacing)));
            step = usable / count;
            firstDist = startOffset + step * 0.5f;
        }
        else
        {
            count = Mathf.Max(1, Mathf.FloorToInt(usable / Mathf.Max(0.01f, spacing)) + 1);
            step = count > 1 ? usable / (count - 1) : 0f;
            firstDist = count > 1 ? startOffset : total * 0.5f;
        }

        for (int i = 0; i < count; i++)
        {
            Sample(line, cum, firstDist + step * i, out Vector3 pos, out Vector3 dir);
            positions.Add(pos);
            rotations.Add(alignToDirection ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity);
        }
    }

    // ---- 道を作る ----
    void Build()
    {
        var positions = new List<Vector3>();
        var rotations = new List<Quaternion>();
        GetSegments(positions, rotations, out float step);

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject seg = CreateSegment(segmentPrefab, positions[i], rotations[i], step, $"Segment_{i}");
            baseScales.Add(seg.transform.localScale);
            seg.SetActive(false);
            openSegments.Add(seg);

            if (lockedSegmentPrefab != null)
                lockedSegments.Add(CreateSegment(lockedSegmentPrefab, positions[i], rotations[i], step, $"Locked_{i}"));
        }
    }

    // 1マスを作る
    // 入れ物（holder）の中にプレハブを入れ、入れ物を道の向きに伸ばす。
    // こうするとプレハブの向き（Quadを寝かせているなど）に関係なく、道の方向に伸ばせる
    GameObject CreateSegment(GameObject prefab, Vector3 pos, Quaternion rot, float step, string objName)
    {
        GameObject holder = new GameObject(objName);
        holder.transform.SetParent(transform, false);
        holder.transform.position = pos;
        holder.transform.rotation = Quaternion.identity;

        GameObject body = Instantiate(prefab, holder.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = prefab.transform.rotation;

        if (connect && step > 0f)
        {
            // 道の向き（Z）の長さを測って、マスの間隔ぴったりに伸ばす
            float length = MeasureLengthZ(holder);
            if (length > 0.0001f)
            {
                Vector3 s = holder.transform.localScale;
                s.z = step * (1f + overlap) / length;
                holder.transform.localScale = s;
            }
        }

        holder.transform.rotation = rot;
        return holder;
    }

    // 回転させる前の状態で、Z方向の長さを測る
    static float MeasureLengthZ(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.size.z;
    }

    // ---- 表示 ----
    void ShowOpenedImmediately()
    {
        opened = true;
        foreach (var l in lockedSegments) l.SetActive(false);
        foreach (var s in openSegments) s.SetActive(true);
    }

    IEnumerator OpenAnimation()
    {
        opened = true;
        animating = true;
        OpeningCount++;

        // カメラ演出が終わるのを待ってから始める
        CameraIntro cam = FindFirstObjectByType<CameraIntro>();
        while (cam != null && cam.IsPlaying) yield return null;
        yield return new WaitForSeconds(openDelay);

        // From 側から1マスずつ出す
        for (int i = 0; i < openSegments.Count; i++)
        {
            if (i < lockedSegments.Count) lockedSegments[i].SetActive(false);
            StartCoroutine(Pop(openSegments[i], baseScales[i]));
            yield return new WaitForSeconds(appearInterval);
        }
        yield return new WaitForSeconds(popDuration);

        StageProgress.SetRoadShown(RoadKey);
        animating = false;
        OpeningCount--;
    }

    IEnumerator Pop(GameObject seg, Vector3 baseScale)
    {
        seg.SetActive(true);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / popDuration;
            float x = Mathf.Clamp01(t);
            // 少し大きくなってから戻る（ポンっと出る）
            float e = 1f + 2.7f * Mathf.Pow(x - 1f, 3f) + 1.7f * Mathf.Pow(x - 1f, 2f);
            seg.transform.localScale = baseScale * e;
            yield return null;
        }
        seg.transform.localScale = baseScale;
    }

    void OnDestroy()
    {
        // 演出途中でシーンを抜けたときにカウントが残らないようにする
        if (animating) OpeningCount--;
    }

#if UNITY_EDITOR
    // 道の設定と、行き先のステージの Required Stage が食い違っていないか確認する
    // （道は開いたのにステージが開放されない、というずれを防ぐ）
    void OnValidate()
    {
        if (from == null || to == null) return;

        if (from == to)
            Debug.LogWarning($"[StageRoad] {name}: From と To が同じステージになっています", this);
        else if (to.requiredStage != from)
            Debug.LogWarning($"[StageRoad] {name}: 道は {from.name} → {to.name} ですが、{to.name} の Required Stage が " +
                             $"{(to.requiredStage != null ? to.requiredStage.name : "空")} になっています。{from.name} にしてください", to);
    }

    // Inspector の「︙」から実行：曲がり角を1つ追加する（To の手前、最後の区間の真ん中に作る）
    [ContextMenu("曲がり角を追加")]
    void AddBendPoint()
    {
        if (from == null || to == null)
        {
            Debug.LogWarning("[StageRoad] 先に From と To を設定してください", this);
            return;
        }

        bendPoints.RemoveAll(b => b == null);
        Vector3 prev = bendPoints.Count > 0 ? bendPoints[bendPoints.Count - 1].position : from.transform.position;
        Vector3 mid = (prev + to.transform.position) * 0.5f;

        var go = new GameObject($"Bend_{bendPoints.Count + 1}");
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "曲がり角を追加");
        UnityEditor.Undo.RecordObject(this, "曲がり角を追加");
        go.transform.SetParent(transform, true);
        go.transform.position = mid;
        bendPoints.Add(go.transform);
        UnityEditor.Selection.activeGameObject = go; // すぐ動かせるように選択する
    }
#endif

    // ---- Sceneビューで道の形とマスの位置を確認できるようにする ----
    void OnDrawGizmos()
    {
        if (from == null || to == null || Application.isPlaying) return;

        // 中心線
        List<Vector3> line = GetPathLine();
        Gizmos.color = Color.cyan;
        for (int i = 1; i < line.Count; i++) Gizmos.DrawLine(line[i - 1], line[i]);

        // 曲がり角
        Gizmos.color = Color.magenta;
        foreach (var b in bendPoints) if (b != null) Gizmos.DrawWireSphere(Flat(b.position), 0.15f);

        // マス
        var positions = new List<Vector3>();
        var rotations = new List<Quaternion>();
        GetSegments(positions, rotations, out float step);
        float len = connect ? step : 0.4f;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < positions.Count; i++)
        {
            Gizmos.matrix = Matrix4x4.TRS(positions[i], rotations[i], Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.4f, 0.05f, len));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}