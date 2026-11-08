using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class LethalResinTrap : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float armingDelay = 1f;

    private static readonly List<LethalResinTrap> placedTraps = new List<LethalResinTrap>();
    private readonly List<Collider2D> occupants = new List<Collider2D>();
    private BoxCollider2D trigger;
    private float armsAt;
    private bool initialized;
    private bool checkedOnArming;
    private bool spent;

    public bool IsConfigured => GetComponent<BoxCollider2D>() != null;
    private bool CanFire => initialized && !spent && isActiveAndEnabled && Time.time >= armsAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => placedTraps.Clear();

    private void Awake()
    {
        trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
    }

    private void OnEnable()
    {
        if (!placedTraps.Contains(this))
            placedTraps.Add(this);
    }

    private void OnDisable() => placedTraps.Remove(this);

    public void Initialize()
    {
        initialized = true;
        armsAt = Time.time + Mathf.Max(0.1f, armingDelay);
    }

    private void FixedUpdate()
    {
        if (!CanFire || checkedOnArming)
            return;

        checkedOnArming = true;
        // Catch an enemy already inside when the arming delay finishes.
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        occupants.Clear();
        trigger.Overlap(filter, occupants);
        foreach (Collider2D occupant in occupants)
        {
            TryCatch(occupant);
            if (spent)
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryCatch(other);
    private void OnTriggerStay2D(Collider2D other) => TryCatch(other);

    private void TryCatch(Collider2D other)
    {
        if (!CanFire || other == null)
            return;

        ITrapTarget target = other.GetComponentInParent<ITrapTarget>();
        if (target == null || !target.IsAlive)
            return;

        // Mark spent before killing, so several enemy colliders cannot trigger it twice.
        spent = true;
        trigger.enabled = false;
        try
        {
            target.Die();
        }
        finally
        {
            Destroy(gameObject);
        }
    }

    public static bool HasTrapNear(Vector2 position, float distance)
    {
        foreach (LethalResinTrap trap in placedTraps)
        {
            if (trap != null && trap.initialized && !trap.spent && trap.isActiveAndEnabled &&
                ((Vector2)trap.transform.position - position).sqrMagnitude < distance * distance)
                return true;
        }
        return false;
    }
}
