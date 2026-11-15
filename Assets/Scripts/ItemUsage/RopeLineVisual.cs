using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class RopeLineVisual : MonoBehaviour
{
    [SerializeField] private Transform endA;
    [SerializeField] private Transform endB;
    [SerializeField, Min(0.01f)] private float width = 0.08f;
    [SerializeField, Range(2, 32)] private int segments = 12;
    [SerializeField, Min(0f)] private float sag = 0.08f;

    private LineRenderer ropeLine;

    private void Awake()
    {
        ropeLine = GetComponent<LineRenderer>();
        if (endA == null || endB == null)
        {
            Debug.LogError("Assign this rope's EndA and EndB to RopeLineVisual.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (ropeLine == null || !ropeLine.enabled || endA == null || endB == null)
            return;

        int count = Mathf.Clamp(segments, 2, 32);
        ropeLine.useWorldSpace = true;
        ropeLine.loop = false;
        ropeLine.startWidth = ropeLine.endWidth = Mathf.Max(0.01f, width);
        ropeLine.positionCount = count + 1;

        Vector3 a = endA.position;
        Vector3 b = endB.position;
        float horizontalDistance = Vector2.Distance(
            new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        float sagAmount = Mathf.Min(sag, horizontalDistance * 0.1f);

        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            Vector3 point = Vector3.Lerp(a, b, t);
            point.y -= sagAmount * 4f * t * (1f - t);
            ropeLine.SetPosition(i, point);
        }
    }
}
