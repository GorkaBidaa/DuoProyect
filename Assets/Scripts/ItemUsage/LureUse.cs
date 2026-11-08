using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class LureUse : MonoBehaviour
{
    [SerializeField] private ItemData lureItem;
    [SerializeField] private EnemyLure lurePrefab;
    [SerializeField] private Vector2 throwVelocity = new Vector2(6f, 6.5f);
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 0.25f);
    [SerializeField, Min(0.1f)] private float useCooldown = 0.4f;

    private PlayerInventory inventory;
    private PlayerAnimatorController playerAnimator;
    private SpriteRenderer playerSprite;
    private StaminaSystem stamina;
    private float nextUseTime;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        playerAnimator = GetComponent<PlayerAnimatorController>();
        playerSprite = GetComponentInChildren<SpriteRenderer>();
        stamina = GetComponent<StaminaSystem>();

        if (lureItem == null || lurePrefab == null || !lurePrefab.IsConfigured)
        {
            Debug.LogError("Assign the Lure item and a Lure prefab with Rigidbody2D, CircleCollider2D and Landing Layers.", this);
            enabled = false;
        }
    }

    public void UseItem(ItemData item)
    {
        if (!isActiveAndEnabled || item == null || item != lureItem ||
            Time.timeScale <= 0f || Time.time < nextUseTime ||
            (stamina != null && stamina.IsDead) || inventory.GetItemAmount(item) <= 0)
            return;

        bool facingLeft = playerAnimator != null
            ? playerAnimator.FacingLeft : playerSprite != null && playerSprite.flipX;
        float facing = facingLeft ? -1f : 1f;
        Vector3 position = transform.position + new Vector3(spawnOffset.x * facing, spawnOffset.y, 0f);
        Vector2 velocity = new Vector2(Mathf.Abs(throwVelocity.x) * facing, Mathf.Abs(throwVelocity.y));

        EnemyLure lure = Instantiate(lurePrefab, position, Quaternion.identity);
        lure.gameObject.SetActive(true);

        if (!lure.Launch(velocity, GetComponentsInChildren<Collider2D>()))
        {
            Destroy(lure.gameObject);
            return;
        }

        nextUseTime = Time.time + useCooldown;
        if (item.consumeOnUse && !inventory.RemoveItem(item, 1))
            Destroy(lure.gameObject);
    }
}
