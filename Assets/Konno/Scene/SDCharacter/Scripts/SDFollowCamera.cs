using UnityEngine;

/// <summary>キャラクターを斜め後ろから追いかけるシンプルなカメラ。</summary>
public class SDFollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 1.4f, -2.6f);
    public float lookHeight = 0.5f;
    public float smooth = 6f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 want = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
