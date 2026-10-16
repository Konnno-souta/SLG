using UnityEngine;

public class TitleCamera : MonoBehaviour
{
    [Header("回転設定")]

    [Tooltip("回転中心")]
    [SerializeField] private Vector3 forcus = new();
    [Tooltip("中心からの距離")]
    [SerializeField] private float distance;
    [SerializeField] private float height;
    [Tooltip("回転速度(角度/s)")]
    [SerializeField] private float angleSpeed;

    private float angle; // 度

    private void Update()
    {
        angle -= angleSpeed * Time.deltaTime;

        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(rad) * distance, height, Mathf.Cos(rad) * distance);

        transform.position = forcus + offset;
        transform.LookAt(forcus);
    }
}