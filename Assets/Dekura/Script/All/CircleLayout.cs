using UnityEngine;

[ExecuteAlways]
public class CircleLayout : MonoBehaviour
{
    public enum Plane { XY, XZ }

    [SerializeField] private Plane plane = Plane.XZ;
    [SerializeField] private float radius = 3f;
    [SerializeField] private float startAngle = 90f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private bool faceCenter = false;

    private void OnValidate() => Layout();
    private void OnTransformChildrenChanged() => Layout();

    [ContextMenu("Layout")]
    public void Layout()
    {
        int count = transform.childCount;
        if (count == 0) return;

        float step = 360f / count * (clockwise ? -1f : 1f);

        for (int i = 0; i < count; i++)
        {
            float rad = (startAngle + step * i) * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

            Vector3 pos = plane == Plane.XZ
                ? new Vector3(c, 0f, s) * radius
                : new Vector3(c, s, 0f) * radius;

            var child = transform.GetChild(i);
            child.localPosition = pos;

            if (faceCenter && pos != Vector3.zero)
            {
                child.localRotation = plane == Plane.XZ
                    ? Quaternion.LookRotation(-pos, Vector3.up)
                    : Quaternion.Euler(0f, 0f, startAngle + step * i - 90f);
            }
        }
    }
}