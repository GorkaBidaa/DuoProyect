using UnityEngine;

using UnityEngine.InputSystem;



[RequireComponent(typeof(PlayerInventory), typeof(PlayerMovement))]

public class RopeUse : MonoBehaviour

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







    private void Awake()

    {

        inventory = GetComponent<PlayerInventory>();

        movement = GetComponent<PlayerMovement>();

        body = GetComponent<Rigidbody2D>();

        anchor = GetComponent<ShadowAnchor>();



        if (ropeItem == null || inventoryMenu == null ||

            craftingMenu == null || body == null)

        {

            Debug.LogError("Assign all RopeUse references.", this);

            enabled = false;

        }

    }



    public void UseItem(ItemData item)

    {

        if (!isActiveAndEnabled ||

            item != ropeItem || inventory.GetItemAmount(item) <= 0 ||

            shortcuts == null)

            return;



        RopeShortcut nearest = null;

        float bestDistance = float.PositiveInfinity;



        foreach (RopeShortcut shortcut in shortcuts)

        {

            if (shortcut == null ||

                !shortcut.CanDeployAt(transform.position, placementDistance))

                continue;



            float distance =

                shortcut.PlacementDistanceSquared(transform.position);



            if (distance < bestDistance)

            {

                nearest = shortcut;

                bestDistance = distance;

            }

        }



        if (nearest == null)

        {

            Debug.Log("No unused rope attachment point nearby.", this);

            return;

        }



        if (nearest.TryDeploy(inventory, item))

            nextTravelTime = Time.time + travelCooldown;

    }



    public bool HasAvailableTravel

    {

        get

        {

            if (!isActiveAndEnabled || Time.time < nextTravelTime || shortcuts == null ||

                (inventoryMenu != null && inventoryMenu.IsOpen) ||

                (craftingMenu != null && craftingMenu.IsOpen)) return false;

            foreach (RopeShortcut shortcut in shortcuts)

                if (shortcut != null && shortcut.TryGetDestination(transform.position, interactDistance, out _, out _))

                    return true;

            return false;

        }

    }



    private void Update()

    {

        if (Time.time < nextTravelTime ||

            shortcuts == null)

            return;



        // Crafting gets priority on F / Triangle near a station.

        if (craftingMenu != null && craftingMenu.IsAtBase)

            return;



        GameInput input = GameInput.Instance;

        if (input == null || !HasAvailableTravel) return;
        // Registered NPC/crafting interactions own the same button and prompt.
        if (InteractionPromptRegistry.Select(transform.position) != null) return;

        bool interact = input.TryInteract();



        if (!interact)

            return;



        bool found = false;

        Vector3 destination = default;

        float bestDistance = float.PositiveInfinity;



        foreach (RopeShortcut shortcut in shortcuts)

        {

            if (shortcut == null ||

                !shortcut.TryGetDestination(

                    transform.position,

                    interactDistance,

                    out Vector3 candidate,

                    out float distance))

                continue;



            if (distance < bestDistance)

            {

                found = true;

                bestDistance = distance;

                destination = candidate;

            }

        }



        if (!found)

            return;



        nextTravelTime = Time.time + travelCooldown;



        // Clear shadow and movement momentum before changing position.

        if (anchor != null)

            anchor.ForceDestroyAnchor();



        movement.ResetMovementState();

        body.position = new Vector2(destination.x, destination.y);

        transform.position = destination;

        Physics2D.SyncTransforms();

    }

}