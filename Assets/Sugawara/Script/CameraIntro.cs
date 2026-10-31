using System;
using System.Collections;
using UnityEngine;

// Main Camera に付ける。
// ・シーンに置いた位置・向きから「角度と距離」を覚える
// ・開始時は手前の位置から回り込みながら、プレイヤーが中心に来る位置まで移動する
// ・演出のあとは、プレイヤーが常に画面の中心に来るように追従する
public class CameraIntro : MonoBehaviour
{
    [Header("追従")]
    [Tooltip("中心に映す対象（Player）")]
    [SerializeField] Transform followTarget;
    [Tooltip("追従の遅れ（秒）。0 で常にぴったり中心。0.1〜0.3 でふんわり追いかける")]
    [SerializeField] float followSmooth = 0f;

    [Header("時間")]
    [SerializeField] float delay = 0.3f;       // 開始までの待ち時間
    [SerializeField] float duration = 2.0f;    // 移動にかかる秒数

    [Header("開始位置（最終位置からのずらし）")]
    [Tooltip("注視点からの距離をどれだけ増やすか。＋で手前（遠く）から寄ってくる")]
    [SerializeField] float startDistance = 8f;
    [Tooltip("高さをどれだけずらすか。−で低い位置から上がってくる")]
    [SerializeField] float startHeight = -3f;
    [Tooltip("注視点の周りをどれだけ回り込むか（度）。立体感が出る")]
    [SerializeField] float startYaw = 30f;

    public bool IsPlaying { get; private set; }
    public event Action OnFinished;

    Vector3 offsetFromFocus;  // 注視点からカメラまでのずれ（角度と距離）
    float groundY;            // 注視点の高さ（地面）
    Vector3 finalPos;
    Quaternion finalRot;
    Vector3 pivot;
    Vector3 followVelocity;

    void Awake()
    {
        if (followTarget == null)
            Debug.LogWarning("[CameraIntro] Follow Target に Player を入れてください", this);

        Vector3 placedPos = transform.position;
        finalRot = transform.rotation;

        Vector3 groundPivot = FindGroundPoint(placedPos, finalRot);
        offsetFromFocus = placedPos - groundPivot;
        groundY = groundPivot.y;

        // 追従対象があれば、その足元を注視点にする
        pivot = followTarget != null ? FocusOf(followTarget) : groundPivot;
        finalPos = pivot + offsetFromFocus;

        IsPlaying = true;
        ApplyPose(0f); // 1フレーム目から開始位置にしておく
    }

    void Start()
    {
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            UpdatePivot(); // 演出中もプレイヤーの位置を追いかける
            ApplyPose(EaseInOut(Mathf.Clamp01(t)));
            yield return null;
        }

        UpdatePivot();
        transform.SetPositionAndRotation(finalPos, finalRot);
        IsPlaying = false;
        OnFinished?.Invoke();
    }

    // 演出のあとはプレイヤーを追いかける
    void LateUpdate()
    {
        if (IsPlaying || followTarget == null) return;

        Vector3 desired = FocusOf(followTarget) + offsetFromFocus;
        if (followSmooth <= 0f)
            transform.position = desired; // 常にぴったり中心
        else
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref followVelocity, followSmooth);
        transform.rotation = finalRot;
    }

    // 注視点と最終位置を、今のプレイヤーの位置に合わせ直す
    void UpdatePivot()
    {
        if (followTarget == null) return;
        pivot = FocusOf(followTarget);
        finalPos = pivot + offsetFromFocus;
    }

    // e = 0 で開始位置、e = 1 で最終位置
    void ApplyPose(float e)
    {
        float k = 1f - e; // 残りの割合

        Vector3 offset = finalPos - pivot;
        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        Vector3 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : -Vector3.forward;

        // 距離と高さをずらし、注視点の周りを回転させる
        offset += dir * (startDistance * k);
        offset.y += startHeight * k;
        offset = Quaternion.Euler(0f, startYaw * k, 0f) * offset;

        Vector3 pos = pivot + offset;
        transform.position = pos;

        // 常に注視点を見る。最後は最終の向きにぴったり合わせる
        Quaternion look = Quaternion.LookRotation(pivot - pos, Vector3.up);
        Quaternion lookAtFinal = Quaternion.LookRotation(pivot - finalPos, Vector3.up);
        transform.rotation = look * (Quaternion.Inverse(lookAtFinal) * finalRot);
    }

    // 対象の足元（地面の高さ）を注視点にする
    Vector3 FocusOf(Transform t)
    {
        Vector3 p = t.position;
        p.y = groundY;
        return p;
    }

    // カメラの正面と地面（y = 0）が交わる点
    static Vector3 FindGroundPoint(Vector3 pos, Quaternion rot)
    {
        Ray ray = new Ray(pos, rot * Vector3.forward);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float d)) return ray.GetPoint(d);
        return pos + rot * Vector3.forward * 10f;
    }

    static float EaseInOut(float x)
    {
        // ゆっくり動き出して、ゆっくり止まる
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    // Sceneビューで注視点・最終位置・開始位置を確認できるようにする
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        Vector3 placed = transform.position;
        Quaternion rot = transform.rotation;
        Vector3 ground = FindGroundPoint(placed, rot);
        Vector3 off = placed - ground;

        Vector3 pv = ground;
        if (followTarget != null)
        {
            pv = followTarget.position;
            pv.y = ground.y;
        }
        Vector3 fin = pv + off;

        Vector3 flat = new Vector3(off.x, 0f, off.z);
        Vector3 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : -Vector3.forward;
        Vector3 so = off + dir * startDistance;
        so.y += startHeight;
        Vector3 start = pv + Quaternion.Euler(0f, startYaw, 0f) * so;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pv, 0.3f);     // 注視点
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(fin, 0.4f);    // 演出が終わる位置
        Gizmos.DrawLine(fin, pv);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(start, 0.4f);  // 開始位置
        Gizmos.DrawLine(start, pv);
    }
}