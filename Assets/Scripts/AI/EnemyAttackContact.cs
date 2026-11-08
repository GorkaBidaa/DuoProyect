using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Added automatically by BatEnemy/WolfEnemy. Keep physical terrain collisions,
// while sensing the player independently so a committed attack can pass through.
public class EnemyAttackContact : MonoBehaviour
{
    private PlayerHealth player;
    private SpriteRenderer visuals;
    private Collider2D shape;
    private Vector2 previousCenter;
    private bool attacking, attemptedHit;
    private readonly List<Collider2D> ownColliders = new List<Collider2D>();
    private readonly List<Collider2D> playerColliders = new List<Collider2D>();
    private readonly List<bool> originallyIgnored = new List<bool>();

    public void Setup(PlayerHealth target, SpriteRenderer sprite)
    {
        player = target;
        visuals = sprite;
        shape = GetComponent<Collider2D>();
        if (shape) previousCenter = shape.bounds.center;
        if (!player) return;
        foreach (Collider2D mine in GetComponentsInChildren<Collider2D>())
            foreach (Collider2D theirs in player.GetComponentsInChildren<Collider2D>())
            {
                // Leave trigger interactions such as hazards and pickups intact.
                if (mine.isTrigger || theirs.isTrigger) continue;
                ownColliders.Add(mine);
                playerColliders.Add(theirs);
                originallyIgnored.Add(Physics2D.GetIgnoreCollision(mine, theirs));
                Physics2D.IgnoreCollision(mine, theirs, true);
            }
    }

    public void BeginAttack()
    {
        attacking = true;
        attemptedHit = false;
        if (shape) previousCenter = shape.bounds.center;
    }

    public void EndAttack() => attacking = false;

    public void CheckHit(int damage, Vector2 direction)
    {
        if (!shape || !player) return;
        Vector2 current = shape.bounds.center;
        if (attacking && !attemptedHit)
        {
            Vector2 displacement = current - previousCenter;
            // Sweep the body bounds across the completed physics step, including zero-motion overlaps.
            RaycastHit2D[] hits = Physics2D.BoxCastAll(previousCenter, shape.bounds.size, 0f,
                displacement.normalized, displacement.magnitude);
            foreach (RaycastHit2D hit in hits)
            {
                if (!hit.collider || hit.collider.isTrigger ||
                    hit.collider.GetComponentInParent<PlayerHealth>() != player) continue;
                attemptedHit = true;
                if (player.TryTakeHit(damage, direction) && visuals)
                    StartCoroutine(Flash());
                break;
            }
        }
        previousCenter = current;
    }

    private IEnumerator Flash()
    {
        Color before = visuals.color;
        visuals.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        if (visuals && visuals.color == Color.white) visuals.color = before;
    }

    private void OnEnable()
    {
        for (int i = 0; i < ownColliders.Count; i++)
            if (ownColliders[i] && playerColliders[i]) Physics2D.IgnoreCollision(ownColliders[i], playerColliders[i], true);
    }

    private void OnDisable()
    {
        attacking = false;
        for (int i = 0; i < ownColliders.Count; i++)
            if (ownColliders[i] && playerColliders[i])
                Physics2D.IgnoreCollision(ownColliders[i], playerColliders[i], originallyIgnored[i]);
    }
}
