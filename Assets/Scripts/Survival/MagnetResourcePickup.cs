using UnityEngine;

public class MagnetResourcePickup : MonoBehaviour
{
    [Header("Initial rise")]
    [SerializeField, Min(0f)] private float launchDuration = 0.2f;
    [SerializeField, Min(0f)] private float launchHeight = 0.35f;

    [Header("Magnet movement")]
    [SerializeField, Min(0.1f)] private float startingSpeed = 3f;
    [SerializeField, Min(0.1f)] private float maximumSpeed = 16f;
    [SerializeField, Min(0.1f)] private float acceleration = 24f;
    [SerializeField, Min(0.01f)] private float collectDistance = 0.25f;

    private PlayerInventory target;
    private ResourceData resource;
    private int amount;
    private Vector3 spawnPosition;
    private float launchTime;
    private float speed;
    private bool initialized;
    private bool collected;

    public bool Initialize(PlayerInventory inventory, ResourceData resource, int amount)
    {
        if (initialized)
            return false;

        if (inventory == null || resource == null || amount <= 0)
        {
            Debug.LogError("Magnet pickup needs a target inventory, resource and positive amount.", this);
            Destroy(gameObject);
            return false;
        }

        target = inventory;
        this.resource = resource;
        this.amount = amount;
        spawnPosition = transform.position;
        speed = Mathf.Max(0.1f, startingSpeed);
        initialized = true;

        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null && resource.icon != null)
            sprite.sprite = resource.icon;

        return true;
    }

    private void Update()
    {
        if (!initialized || collected)
            return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!target.isActiveAndEnabled)
            return;

        if (launchTime < launchDuration)
        {
            launchTime = Mathf.Min(launchTime + Time.deltaTime, launchDuration);
            float progress = launchTime / launchDuration;
            transform.position = spawnPosition + Vector3.up * (launchHeight * progress);
            return;
        }

        // Stay in this pickup's visual plane and home toward the player's XY position.
        Vector3 destination = target.transform.position;
        destination.z = transform.position.z;
        speed = Mathf.MoveTowards(speed, Mathf.Max(startingSpeed, maximumSpeed),
            Mathf.Max(0.1f, acceleration) * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        if (((Vector2)transform.position - (Vector2)destination).sqrMagnitude > collectDistance * collectDistance)
            return;

        // Credit once, on arrival. No collider or trigger is needed.
        collected = true;
        try
        {
            target.AddResource(resource, amount);
            InteractionAudio.Play(InteractionCue.GloomCollect, target.transform.position);
        }
        finally
        {
            Destroy(gameObject);
        }
    }
}
