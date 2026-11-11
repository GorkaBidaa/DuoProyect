using UnityEngine;

public class ResourceSpawnPoint : MonoBehaviour
{
    [SerializeField] private ResourcePickup pickupPrefab;
    private ResourcePickup current;
    private bool initialized;

    private void Start()
    {
        if (!initialized) ResetResource();
    }

    public void ResetResource()
    {
        if (!isActiveAndEnabled) return;
        if (!pickupPrefab)
        {
            Debug.LogError("Assign a ResourcePickup prefab to ResourceSpawnPoint.", this);
            return;
        }
        initialized = true;
        if (current) WorldResetCoordinator.Remove(current.gameObject);
        current = Instantiate(pickupPrefab, transform.position, transform.rotation);
        current.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (current) WorldResetCoordinator.Remove(current.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
