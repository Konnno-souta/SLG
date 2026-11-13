using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FireSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private int count = 12;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float interval = 0.1f;

    private void Start()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // 円周上の位置を均等に計算(XZ平面)
        var positions = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            float angle = 2f * Mathf.PI * i / count;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            positions.Add(transform.position + offset);
        }

        // カメラの前方向への距離(奥行き)で昇順ソート = 手前から奥
        Vector3 camPos = viewCamera.transform.position;
        Vector3 camForward = viewCamera.transform.forward;
        var sorted = positions
            .OrderBy(p => Vector3.Dot(p - camPos, camForward))
            .ToList();

        foreach (var pos in sorted)
        {
            Instantiate(prefab, pos, Quaternion.identity, transform);
            yield return new WaitForSeconds(interval);
        }
    }

    [ContextMenu("ReSpawn")]
    public void ReSpawn()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        StartCoroutine(SpawnRoutine());
    }
}