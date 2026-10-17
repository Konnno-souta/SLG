using UnityEngine;

// マップ上のステージアイコン（Stage1〜3のCubeに付ける）
public class StagePoint : MonoBehaviour
{
    public StageData data;
    [Tooltip("キャラが立つ位置。空ならこのオブジェクトの位置")]
    public Transform standPoint;

    public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;
}
