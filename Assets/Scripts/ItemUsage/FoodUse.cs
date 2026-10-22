using UnityEngine;

public class FoodUse : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private StaminaSystem stamina;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private ItemData foodItem;

    [SerializeField, Min(0f)] private float staminaRestored = 25f;
    [SerializeField, Min(0)] private int healthRestored = 25;

    public void UseItem(ItemData item)
    {
        if (foodItem == null || item != foodItem)
            return;

        if (inventory == null || stamina == null || health == null)
        {
            Debug.LogWarning("Assign all FoodUse references.", this);
            return;
        }

        if (inventory.GetItemAmount(item) <= 0)
            return;

        bool restoredStamina =
            stamina.TryRestoreStamina(staminaRestored);
        bool restoredHealth =
            health.TryRestoreHealth(healthRestored);

        if (!restoredStamina && !restoredHealth)
            return;

        if (item.consumeOnUse)
            inventory.RemoveItem(item, 1);
    }
}