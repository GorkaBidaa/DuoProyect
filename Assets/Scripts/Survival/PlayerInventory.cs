using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{

    [SerializeField] private List<InventorySlot> inventory = new List<InventorySlot>();
    [SerializeField] private List<ItemSlot> items = new List<ItemSlot>();

    public event System.Action InventoryChanged;

    public void AddItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0)
            return;

        ItemSlot slot = items.Find(entry => entry.item == item);

        if (slot == null)
        {
            items.Add(new ItemSlot
            {
                item = item,
                amount = amount
            });
        }
        else
        {
            slot.amount += amount;
        }

        InventoryChanged?.Invoke();
    }

    public IReadOnlyList<InventorySlot> GetAvailableResources()
    {
        return inventory.FindAll(slot =>
            slot != null &&
            slot.resource != null &&
            slot.amount > 0
        ).AsReadOnly();
    }
    public int GetItemAmount(ItemData item)
    {
        if (item == null)
            return 0;

        ItemSlot slot = items.Find(entry => entry.item == item);
        return slot != null ? slot.amount : 0;
    }

    public bool RemoveItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0)
            return false;

        ItemSlot slot = items.Find(entry => entry.item == item);

        if (slot == null || slot.amount < amount)
            return false;

        slot.amount -= amount;

        if (slot.amount == 0)
            items.Remove(slot);

        InventoryChanged?.Invoke();
        return true;
    }

    public IReadOnlyList<ItemSlot> GetAvailableItems()
    {
        return items.FindAll(slot =>
            slot != null &&
            slot.item != null &&
            slot.amount > 0
        ).AsReadOnly();
    }

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

        InventoryChanged?.Invoke();
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

        InventoryChanged?.Invoke();
        Debug.Log($"Removed {amount} of {resource.resourceName} from inventory.");
    }

    // Validate and commit the full price before notifying any inventory listeners.
    public bool TrySpendResources(RecipeIngredient[] costs)
    {
        if (costs == null || costs.Length == 0) return false;
        var slots = new List<InventorySlot>();
        for (int i = 0; i < costs.Length; i++)
        {
            var cost = costs[i];
            if (cost == null || cost.resource == null || cost.amount <= 0) return false;
            for (int j = 0; j < i; j++) if (costs[j].resource == cost.resource) return false;
            var slot = inventory.Find(entry => entry != null && entry.resource == cost.resource);
            if (slot == null || slot.amount < cost.amount) return false;
            slots.Add(slot);
        }
        for (int i = 0; i < costs.Length; i++) slots[i].amount -= costs[i].amount;
        inventory.RemoveAll(slot => slot != null && slot.amount <= 0);
        InventoryChanged?.Invoke();
        return true;
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

