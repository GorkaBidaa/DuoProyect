using UnityEngine;

public class RopeShortcut : MonoBehaviour
{
    [SerializeField] private Transform placementPoint;
    [SerializeField] private Transform endA;
    [SerializeField] private Transform endB;

    [Header("Optional visuals")]
    [SerializeField] private GameObject ropeVisual;
    [SerializeField] private LineRenderer ropeLine;

    private bool deployed;

    private bool IsConfigured =>
        placementPoint != null &&
        endA != null &&
        endB != null &&
        (endA.position - endB.position).sqrMagnitude > 0.01f;

    private void Awake()
    {
        RefreshVisual();
    }

    public bool CanDeployAt(Vector3 position, float range)
    {
        return isActiveAndEnabled &&
            IsConfigured &&
            !deployed &&
            (placementPoint.position - position).sqrMagnitude
                <= range * range;
    }

    public float PlacementDistanceSquared(Vector3 position)
    {
        return placementPoint != null
            ? (placementPoint.position - position).sqrMagnitude
            : float.PositiveInfinity;
    }

    public bool TryDeploy(PlayerInventory inventory, ItemData rope)
    {
        if (!isActiveAndEnabled || !IsConfigured || deployed ||
            inventory == null || rope == null ||
            inventory.GetItemAmount(rope) <= 0)
            return false;

        // Lock this shortcut before notifying inventory listeners.
        deployed = true;

        if (rope.consumeOnUse && !inventory.RemoveItem(rope, 1))
        {
            deployed = false;
            return false;
        }

        RefreshVisual();
        return true;
    }

    public bool TryGetDestination(
        Vector3 position,
        float range,
        out Vector3 destination,
        out float distanceSquared)
    {
        destination = default;
        distanceSquared = float.PositiveInfinity;

        if (!isActiveAndEnabled || !IsConfigured || !deployed)
            return false;

        float distanceA = (endA.position - position).sqrMagnitude;
        float distanceB = (endB.position - position).sqrMagnitude;

        bool nearerA = distanceA <= distanceB;
        distanceSquared = nearerA ? distanceA : distanceB;

        if (distanceSquared > range * range)
            return false;

        destination = nearerA ? endB.position : endA.position;
        return true;
    }

    private void RefreshVisual()
    {
        if (ropeVisual != null)
            ropeVisual.SetActive(deployed);

        if (ropeLine == null)
            return;

        ropeLine.enabled = deployed && IsConfigured;

        if (!ropeLine.enabled)
            return;

        ropeLine.useWorldSpace = true;
        ropeLine.positionCount = 2;
        ropeLine.SetPosition(0, endA.position);
        ropeLine.SetPosition(1, endB.position);
    }
}