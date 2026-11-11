using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Crafting : MonoBehaviour, IInteractionPromptSource
{
    [SerializeField] private CraftingMenu menu;
    [Tooltip("Legacy prompt; hidden in favor of the shared InteractionPromptUI.")]
    [SerializeField] private GameObject popupUI;
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    public CraftingMenu Menu => menu;
    public bool PromptAvailable => isActiveAndEnabled && menu && playerColliders.Count > 0;
    public Vector3 PromptPosition => transform.position;
    public int PromptPriority => 100;
    public string PromptAction => "Interact";
    public string PromptLabel => "Craft";

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;

    private void Awake()
    {
        if (popupUI) popupUI.SetActive(false);
        if (!menu)
        {
            Debug.LogError("Assign the CraftingMenu on the crafting station.", this);
            enabled = false;
        }
    }

    private void OnEnable() => InteractionPromptRegistry.Register(this);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!menu || other.GetComponentInParent<PlayerInventory>() != menu.Inventory) return;
        playerColliders.Add(other);
        menu.SetAtBase(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other)) return;
        if (menu) menu.SetAtBase(InteractionPromptRegistry.HasCraftingStation(menu));
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        InteractionPromptRegistry.Unregister(this);
        if (menu) menu.SetAtBase(InteractionPromptRegistry.HasCraftingStation(menu));
    }

    private void Update()
    {
        // Destroyed colliders can otherwise leave a stale interaction behind.
        int removed = playerColliders.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
        if (removed > 0 && menu) menu.SetAtBase(InteractionPromptRegistry.HasCraftingStation(menu));
        if (!PromptAvailable) return;
        GameInput input = GameInput.Instance;
        if (!input) return;
        // Pick the same station that the prompt displays when triggers overlap.
        PlayerInventory inventory = menu.Inventory;
        if (!inventory || !ReferenceEquals(InteractionPromptRegistry.Select(inventory.transform.position), this)) return;
        if (input.TryInteract())
        {
            if (menu.IsOpen) menu.Close();
            else menu.Open();
        }
    }
}
