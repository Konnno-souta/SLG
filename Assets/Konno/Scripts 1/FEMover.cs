using UnityEngine;

/// <summary>等速移動 (矢のデモ用)</summary>
public class FEMover : MonoBehaviour
{
    public Vector3 velocity;
    void Update() { transform.position += velocity * Time.deltaTime; }
}
