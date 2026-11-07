using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 2つのステージをつなぐ道
// ・From のステージがクリアされると道が開く
// ・道は「1マス分のプレハブ」を並べて作るので、見た目はプレハブを差し替えるだけで変えられる
// ・長さはステージの位置から自動で計算する
// ・Connect をオンにすると、マスを道の向きに伸ばして隙間なくつなげる
public class StageRoad : MonoBehaviour
{
    [Header("つなぐステージ")]
    [SerializeField] StagePoint from;   // このステージをクリアすると開く
    [SerializeField] StagePoint to;

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
    [Tooltip("つなぎ目に線が見えるときに少し重ねる量（マスの長さに対する割合）")]
    [SerializeField, Range(0f, 0.2f)] float overlap = 0.02f;

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

    // ---- 道を作る ----
    void Build()
    {
        List<Vector3> points = GetPoints(out Quaternion rot, out float step);

        for (int i = 0; i < points.Count; i++)
        {
            GameObject seg = CreateSegment(segmentPrefab, points[i], rot, step, $"Segment_{i}");
            baseScales.Add(seg.transform.localScale);
            seg.SetActive(false);
            openSegments.Add(seg);

            if (lockedSegmentPrefab != null)
                lockedSegments.Add(CreateSegment(lockedSegmentPrefab, points[i], rot, step, $"Locked_{i}"));
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

    // マスを置く位置を計算する（両端の余白を除いた区間に均等に並べる）
    List<Vector3> GetPoints(out Quaternion rot, out float step)
    {
        var points = new List<Vector3>();
        step = 0f;

        Vector3 a = from.transform.position; a.y = roadY;
        Vector3 b = to.transform.position; b.y = roadY;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        dir = length > 0.0001f ? dir / length : Vector3.forward;

        rot = alignToDirection ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;

        float usable = length - startOffset - endOffset;
        if (usable < 0f) return points;

        if (connect)
        {
            // つなげる場合：区間をちょうど埋めるように、マスの中心を並べる
            int count = Mathf.Max(1, Mathf.RoundToInt(usable / Mathf.Max(0.01f, spacing)));
            step = usable / count;
            for (int i = 0; i < count; i++)
                points.Add(a + dir * (startOffset + step * (i + 0.5f)));
        }
        else
        {
            int count = Mathf.Max(1, Mathf.FloorToInt(usable / Mathf.Max(0.01f, spacing)) + 1);
            step = count > 1 ? usable / (count - 1) : 0f;
            float startT = count > 1 ? startOffset : length * 0.5f;
            for (int i = 0; i < count; i++)
                points.Add(a + dir * (startT + step * i));
        }

        return points;
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

    // ---- Sceneビューでマスの位置を確認できるようにする ----
    void OnDrawGizmos()
    {
        if (from == null || to == null || Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        List<Vector3> points = GetPoints(out Quaternion rot, out float step);
        float len = connect ? step : 0.4f;

        foreach (Vector3 p in points)
        {
            Gizmos.matrix = Matrix4x4.TRS(p, rot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.4f, 0.05f, len));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}