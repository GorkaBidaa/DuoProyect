using UnityEngine;

public class FoodUse : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private StaminaSystem stamina;
    [SerializeField] private ItemData foodItem;
    [SerializeField, Min(1f)] private float staminaRestored = 25f;

    public void UseItem(ItemData item)
    {
        
        if (foodItem == null || item != foodItem)
            return;

        if (inventory == null || stamina == null)
        {
            Debug.LogWarning("Assign the inventory and stamina references on FoodUse.", this);
            return;
        }

        if (inventory.GetItemAmount(item) <= 0)
            return;

        if (!stamina.TryRestoreStamina(staminaRestored))
        {
            Debug.Log("Stamina is full. Food was not used.", this);
            return;
        }

        if (item.consumeOnUse)
            inventory.RemoveItem(item, 1);

        Debug.Log($"Used {item.itemName} to restore stamina.", this);
    }
}
