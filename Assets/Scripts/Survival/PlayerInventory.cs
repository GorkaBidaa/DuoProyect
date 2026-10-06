using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{

    [SerializeField] private List<InventorySlot> inventory = new List<InventorySlot>();

    public void AddResource(ResourceData resource, int amount)
    {
        InventorySlot existingSlot = inventory.Find(slot => slot.resource == resource);

        if (existingSlot != null) { existingSlot.amount += amount; }

        else
        {
            InventorySlot newSlot = new InventorySlot();

            newSlot.resource = resource;
            newSlot.amount = amount;

            inventory.Add(newSlot);
        }

        Debug.Log($"Added {amount} of {resource.resourceName} to inventory.");
    }

    public bool HasResource(ResourceData resource, int amount)
    {
        InventorySlot existingSlot = inventory.Find(slot => slot.resource == resource);

        if (existingSlot == null) { return false; }

        return existingSlot.amount >= amount;

    }

    public void RemoveResource(ResourceData resource, int amount)
    {
        InventorySlot existingSlot = inventory.Find(slot => slot.resource == resource);

        if (existingSlot == null)
        {
            Debug.Log("Resource does not exist");
            return;
        }

        if (existingSlot.amount < amount)
        {
            Debug.LogWarning("Not enough resources");
            return;
        }
        existingSlot.amount -= amount;

        if (existingSlot.amount <= 0)
        {
            inventory.Remove(existingSlot);
        }

        Debug.Log($"Removed {amount} of {resource.resourceName} from inventory.");
    }

    public int GetResourceAmount(ResourceData resource)
    {
        InventorySlot existingSlot = inventory.Find(
            slot => slot.resource == resource
        );

        if (existingSlot == null)
        {
            return 0;
        }

        return existingSlot.amount;
    }
}
