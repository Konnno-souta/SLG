using System;
using System.Collections.Generic;
using UnityEngine;

// マップ上を移動するキャラクター
// ・MoveTo(位置)：まっすぐ移動する
// ・MoveAlong(通過点のリスト)：通過点を順番にたどって移動する（曲がった道に沿って歩く）
public class MapCharacter : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [Tooltip("向きを変える速さ。0 で一瞬で向く")]
    [SerializeField] float turnSpeed = 15f;

    readonly List<Vector3> path = new List<Vector3>();
    int index;

    public bool IsMoving { get; private set; }
    public event Action OnArrived; // 目的地に着いたら通知

    public void Warp(Vector3 pos)
    {
        pos.y = transform.position.y;
        transform.position = pos;
        path.Clear();
        IsMoving = false;
    }

    // まっすぐ移動する
    public void MoveTo(Vector3 pos)
    {
        MoveAlong(new List<Vector3> { pos });
    }

    // 通過点を順番にたどって移動する（最後の点が目的地）
    public void MoveAlong(IList<Vector3> points)
    {
        path.Clear();
        foreach (var p in points)
        {
            Vector3 q = p;
            q.y = transform.position.y; // 高さは変えない
            path.Add(q);
        }
        index = 0;
        IsMoving = path.Count > 0;
    }

    void Update()
    {
        if (!IsMoving) return;

        Vector3 target = path[index];
        Vector3 dir = target - transform.position;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = turnSpeed > 0f
                ? Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime))
                : look;
        }

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            index++;
            if (index >= path.Count)
            {
                IsMoving = false;
                OnArrived?.Invoke();
            }
        }
    }
}