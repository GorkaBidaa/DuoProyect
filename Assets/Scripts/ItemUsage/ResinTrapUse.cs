using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class ResinTrapUse : MonoBehaviour
{
    [SerializeField] private ItemData resinTrapItem;
    [SerializeField] private LethalResinTrap trapPrefab;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField, Min(0.1f)] private float groundSearchDistance = 3f;
    [SerializeField, Min(0.01f)] private float surfaceOffset = 0.03f;
    [SerializeField, Min(0.1f)] private float minimumTrapSpacing = 1f;
    [SerializeField, Min(0.1f)] private float useCooldown = 0.4f;

    private PlayerInventory inventory;
    private PlayerMovement movement;
    private StaminaSystem stamina;
    private Collider2D playerCollider;
    private float nextUseTime;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[1];

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        movement = GetComponent<PlayerMovement>();
        stamina = GetComponent<StaminaSystem>();
        playerCollider = GetComponent<Collider2D>();

        if (resinTrapItem == null || trapPrefab == null || !trapPrefab.IsConfigured || groundLayers.value == 0)
        {
            Debug.LogError("Assign the Resin Trap item, trap prefab with BoxCollider2D, and Ground Layers.", this);
            enabled = false;
        }
    }

    public void UseItem(ItemData item)
    {
        if (!isActiveAndEnabled || item == null || item != resinTrapItem ||
            Time.timeScale <= 0f || Time.time < nextUseTime ||
            (stamina != null && stamina.IsDead) || inventory.GetItemAmount(item) <= 0)
            return;

        if (!TryGetPlacement(out Vector3 position, out Transform surface))
        {
            Debug.Log("Stand on solid ground to place a Resin Trap. Nothing was consumed.", this);
            return;
        }

        if (LethalResinTrap.HasTrapNear(position, minimumTrapSpacing))
        {
            Debug.Log("A Resin Trap is already too close. Nothing was consumed.", this);
            return;
        }

        LethalResinTrap trap = Instantiate(trapPrefab, position, Quaternion.identity);
        trap.gameObject.SetActive(true);
        nextUseTime = Time.time + useCooldown;

        if (item.consumeOnUse && !inventory.RemoveItem(item, 1))
        {
            Destroy(trap.gameObject);
            return;
        }

        // Follow the supporting platform if it moves.
        trap.transform.SetParent(surface, true);
        trap.Initialize();
        InteractionAudio.Play(InteractionCue.TrapPlaced, position);
    }

    private bool TryGetPlacement(out Vector3 position, out Transform surface)
    {
        position = default;
        surface = null;
        if (movement != null && !movement.isGrounded)
            return false;

        Vector2 origin = playerCollider != null
            ? (Vector2)playerCollider.bounds.center : (Vector2)transform.position;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;

        if (Physics2D.Raycast(origin, Vector2.down, filter, groundHits, groundSearchDistance) == 0 ||
            groundHits[0].normal.y < 0.8f)
            return false;

        Vector2 point = groundHits[0].point + Vector2.up * surfaceOffset;
        position = new Vector3(point.x, point.y, transform.position.z);
        surface = groundHits[0].collider.transform;
        return true;
    }
}
