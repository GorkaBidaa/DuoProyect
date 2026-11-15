using UnityEngine;

[DisallowMultipleComponent]
public class ResourceBob : MonoBehaviour
{
    [SerializeField] private Transform visual;

    [Tooltip("Maximum distance above/below the resting position, in world units.")]
    [SerializeField, Min(0f)] private float bobHeight = 0.08f;

    [Tooltip("Complete up/down cycles per second.")]
    [SerializeField, Min(0f)] private float cyclesPerSecond = 0.6f;

    [SerializeField] private bool randomStartPhase = true;

    private Vector3 restingPosition;
    private float phase;
    private bool initialized;

    private void Reset()
    {
        visual = transform.Find("Visual");
    }

    private void OnEnable()
    {
        if (visual == null || visual == transform ||
            !visual.IsChildOf(transform))
        {
            Debug.LogError(
                "ResourceBob needs a Visual child. Keep the pickup collider on the root.",
                this);
            enabled = false;
            return;
        }

        restingPosition = visual.localPosition;
        phase = randomStartPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || visual == null || Time.deltaTime <= 0f)
            return;

        phase = Mathf.Repeat(
            phase + Time.deltaTime * cyclesPerSecond * Mathf.PI * 2f,
            Mathf.PI * 2f);

        Vector3 worldOffset =
            Vector3.up * (Mathf.Sin(phase) * bobHeight);

        visual.localPosition = restingPosition +
            visual.parent.InverseTransformVector(worldOffset);
    }

    private void OnDisable()
    {
        if (initialized && visual != null)
            visual.localPosition = restingPosition;

        initialized = false;
    }
}