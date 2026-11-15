using UnityEngine;

[RequireComponent(typeof(PlayerInventory), typeof(PlayerMovement))]
public class RopeUse : MonoBehaviour, IInteractionPromptSource
{
    [SerializeField] private ItemData ropeItem;
    [SerializeField] private InventoryMenu inventoryMenu;
    [SerializeField] private CraftingMenu craftingMenu;
    [SerializeField] private RopeShortcut[] shortcuts;
    [SerializeField, Min(0.1f)] private float placementDistance = 2f;
    [SerializeField, Min(0.1f)] private float interactDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float travelCooldown = 0.5f;

    private PlayerInventory inventory;
    private PlayerMovement movement;
    private Rigidbody2D body;
    private ShadowAnchor anchor;
    private float nextTravelTime;
    [Header("Travel feedback")]
    [SerializeField] private RopeTravelFeedback travelFeedback;
    public bool IsTravelling => travelFeedback != null && travelFeedback.IsPlaying;

    public bool PromptAvailable => CanReadInteraction && !craftingMenu.IsAtBase &&
        (TryFindDestination(out _) || FindAttachment() != null);
    public Vector3 PromptPosition => transform.position;
    public int PromptPriority => 50;
    public string PromptAction => "Interact";
    public string PromptLabel => TryFindDestination(out _) ? "Use rope" :
        inventory.GetItemAmount(ropeItem) > 0 ? "Attach rope" : "Rope required";

    private bool CanReadInteraction
    {
        get
        {
            if (!isActiveAndEnabled || IsTravelling || body == null || inventoryMenu == null ||
                craftingMenu == null || Time.timeScale <= 0f ||
                Time.time < nextTravelTime || shortcuts == null ||
                inventoryMenu.IsOpen || craftingMenu.IsOpen || movement.IsTeleporting)
                return false;

            GameInput input = GameInput.Instance;
            return input == null || input.PromptsAllowed;
        }
    }

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody2D>();
        anchor = GetComponent<ShadowAnchor>();
        if (!travelFeedback) travelFeedback = GetComponent<RopeTravelFeedback>();

        if (ropeItem == null || inventoryMenu == null || craftingMenu == null || body == null)
        {
            Debug.LogError("Assign all RopeUse references.", this);
            enabled = false;
            return;
        }

        bool hasShortcut = false;
        if (shortcuts != null)
            foreach (RopeShortcut shortcut in shortcuts)
                if (shortcut != null) { hasShortcut = true; break; }

        if (!hasShortcut)
            shortcuts = FindObjectsByType<RopeShortcut>(FindObjectsSortMode.None);

        if (shortcuts.Length == 0)
            Debug.LogWarning("No active RopeShortcut found. Place a rope in the scene and assign its RopeShortcut component to RopeUse's Shortcuts list.", this);
    }

    private void OnEnable() => InteractionPromptRegistry.Register(this);
    private void OnDisable()
    {
        InteractionPromptRegistry.Unregister(this);
        if (travelFeedback) travelFeedback.CancelTravel();
    }

    public void UseItem(ItemData item)
    {
        if (!CanReadInteraction || item == null || item != ropeItem ||
            inventory.GetItemAmount(item) <= 0)
            return;

        RopeShortcut nearest = FindAttachment();
        if (nearest == null)
        {
            Debug.Log("No unused rope attachment point nearby.", this);
            return;
        }
        Deploy(nearest);
    }

    private RopeShortcut FindAttachment()
    {
        if (shortcuts == null) return null;
        RopeShortcut nearest = null;
        float bestDistance = float.PositiveInfinity;
        foreach (RopeShortcut shortcut in shortcuts)
        {
            if (shortcut == null || !shortcut.CanDeployAt(transform.position, placementDistance))
                continue;
            float distance = shortcut.PlacementDistanceSquared(transform.position);
            if (distance < bestDistance)
            {
                nearest = shortcut;
                bestDistance = distance;
            }
        }
        return nearest;
    }

    private void Deploy(RopeShortcut shortcut)
    {
        if (shortcut != null && shortcut.TryDeploy(inventory, ropeItem))
            nextTravelTime = Time.time + travelCooldown;
    }

    public bool HasAvailableTravel => CanReadInteraction && TryFindDestination(out _);

    private bool TryFindDestination(out Vector3 destination)
    {
        destination = default;
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        foreach (RopeShortcut shortcut in shortcuts)
        {
            if (shortcut == null || !shortcut.TryGetDestination(
                transform.position, interactDistance, out Vector3 candidate, out float distance))
                continue;

            if (distance < bestDistance)
            {
                found = true;
                bestDistance = distance;
                destination = candidate;
            }
        }
        return found;
    }

    private void Update()
    {
        GameInput input = GameInput.Instance;
        if (input == null || !input.PromptsAllowed || !PromptAvailable)
            return;

        // Only the source displayed by the shared prompt may consume Interact.
        if (!ReferenceEquals(InteractionPromptRegistry.Select(transform.position), this))
            return;

        if (!input.TryInteract())
            return;

        if (!TryFindDestination(out Vector3 destination))
        {
            // Interact installs from inventory; the rope need not be selected in the wheel.
            if (inventory.GetItemAmount(ropeItem) > 0)
                Deploy(FindAttachment());
            return;
        }

        nextTravelTime = Time.time + travelCooldown;
        if (travelFeedback && travelFeedback.TryTravel(destination))
            return;

        if (anchor != null)
            anchor.ForceDestroyAnchor();

        movement.ResetMovementState();
        body.position = new Vector2(destination.x, destination.y);
        transform.position = destination;
        Physics2D.SyncTransforms();
        InteractionAudio.Play(InteractionCue.RopeTravel, transform.position);
    }
}
