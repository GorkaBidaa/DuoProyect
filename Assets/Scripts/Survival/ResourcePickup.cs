using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ResourcePickup : MonoBehaviour
{
    [SerializeField] private ResourceData resource;
    [SerializeField, Min(1)] private int amount = 1;
    private bool collected;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || resource == null || amount <= 0)
            return;

        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
            return;

        collected = true;
        inventory.AddResource(resource, amount);
        Destroy(gameObject);
    }
}
