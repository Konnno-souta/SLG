using System;
using UnityEngine;

// マップ上を移動するキャラクター
public class MapCharacter : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;

    Vector3 target;
    public bool IsMoving { get; private set; }
    public event Action OnArrived; // 目的地に着いたら通知

    public void Warp(Vector3 pos)
    {
        pos.y = transform.position.y;
        transform.position = pos;
        target = pos;
        IsMoving = false;
    }

    public void MoveTo(Vector3 pos)
    {
        pos.y = transform.position.y; // 高さは変えない
        target = pos;
        IsMoving = true;
    }

    void Update()
    {
        if (!IsMoving) return;

        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude > 0.0001f) transform.forward = dir.normalized;

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            IsMoving = false;
            OnArrived?.Invoke();
        }
    }
}
