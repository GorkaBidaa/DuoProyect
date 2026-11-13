using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class EnemyRepellantUse : MonoBehaviour
{
    [SerializeField] private ItemData repellantItem;
    [SerializeField, Min(0.1f)] private float duration = 8f;
    [SerializeField, Min(0.1f)] private float radius = 5f;

    private static readonly List<EnemyRepellantUse> sources = new List<EnemyRepellantUse>();
    private PlayerInventory inventory;
    private StaminaSystem stamina;
    private float expiresAt;
    private bool soundActive;

    public Vector2 Position => transform.position;
    public float Radius => radius;
    public bool IsActive => isActiveAndEnabled && Time.time < expiresAt &&
        (stamina == null || !stamina.IsDead);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => sources.Clear();

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        stamina = GetComponent<StaminaSystem>();
        if (repellantItem == null)
        {
            Debug.LogError("Assign the Enemy Repellant item on EnemyRepellantUse.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!sources.Contains(this))
            sources.Add(this);
    }

    private void OnDisable()
    {
        soundActive = false;
        sources.Remove(this);
        expiresAt = 0f;
    }

    private void Update()
    {
        if (stamina != null && stamina.IsDead)
        {
            expiresAt = 0f;
            soundActive = false;
        }
        if (soundActive && !IsActive)
        {
            soundActive = false;
            InteractionAudio.Play(InteractionCue.RepellantOff, transform.position);
        }
    }

    public void UseItem(ItemData item)
    {
        if (!isActiveAndEnabled || item == null || item != repellantItem ||
            Time.timeScale <= 0f || (stamina != null && stamina.IsDead) ||
            inventory.GetItemAmount(item) <= 0)
            return;

        if (IsActive)
        {
            Debug.Log("Enemy Repellant is already active. Nothing was consumed.", this);
            return;
        }

        expiresAt = Time.time + Mathf.Max(0.1f, duration);
        if (item.consumeOnUse && !inventory.RemoveItem(item, 1))
            expiresAt = 0f;
        if (IsActive)
        {
            soundActive = true;
            InteractionAudio.Play(InteractionCue.RepellantOn, transform.position);
        }
    }

    public bool CanAffect(Vector2 enemyPosition, float extraDistance = 0f)
    {
        float distance = radius + Mathf.Max(0f, extraDistance);
        return IsActive && (Position - enemyPosition).sqrMagnitude <= distance * distance;
    }

    public static EnemyRepellantUse FindNearest(Vector2 enemyPosition)
    {
        EnemyRepellantUse nearest = null;
        float bestDistance = float.PositiveInfinity;
        foreach (EnemyRepellantUse source in sources)
        {
            if (source == null || !source.CanAffect(enemyPosition))
                continue;
            float distance = (source.Position - enemyPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                nearest = source;
                bestDistance = distance;
            }
        }
        return nearest;
    }

    public static bool Protects(Transform target)
    {
        if (target == null)
            return false;
        foreach (EnemyRepellantUse source in sources)
        {
            if (source != null && source.IsActive &&
                (target == source.transform || target.IsChildOf(source.transform)))
                return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || IsActive)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
