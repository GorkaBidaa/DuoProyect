using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public class PlayerAnimatorController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float sleepIdleDelay = 2.5f; // Seconds idle at base to trigger sleep

    private PlayerMovement movement;
    private Rigidbody2D rb;
    private StaminaSystem stamina;
    private float idleTimer;
    private bool hasIsDeadParam;

    public bool FacingLeft => spriteRenderer != null && spriteRenderer.flipX;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        stamina = GetComponent<StaminaSystem>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        CacheAnimatorParameters();
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null) return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == "IsDead" && param.type == AnimatorControllerParameterType.Bool)
            {
                hasIsDeadParam = true;
                break;
            }
        }
    }

    private void Update()
    {
        if (animator == null)
            return;

        // Keep movement parameters zeroed while dead so no AnyState/locomotion transition interrupts death
        if (stamina != null && stamina.IsDead)
            return;

        float inputX = movement.HorizontalInput;

        // Flip sprite based on movement direction
        if (Mathf.Abs(inputX) > 0.01f && spriteRenderer != null)
        {
            spriteRenderer.flipX = inputX < 0f;
        }

        // Check if character is resting/sleeping (idle on ground or at Base)
        bool isStillOnGround = movement.isGrounded &&
                               Mathf.Abs(inputX) <= 0.01f &&
                               Mathf.Abs(rb.linearVelocity.y) <= 0.1f;

        // Only accumulate idle time when still on ground and in safe zone
        if (isStillOnGround && (stamina == null || stamina.IsInSafeZone))
        {
            idleTimer += Time.deltaTime;
        }
        else
        {
            idleTimer = 0f;
        }

        // Update animator parameters
        animator.SetFloat("Speed", Mathf.Abs(inputX));
        animator.SetFloat("VelocityY", rb.linearVelocity.y);
        animator.SetBool("IsGrounded", movement.isGrounded);
        animator.SetBool("IsWallSliding", movement.IsWallSliding);
        animator.SetBool("IsSleeping", idleTimer >= sleepIdleDelay);
    }

    public void TriggerCastShadow()
    {
        if (animator == null || (stamina != null && stamina.IsDead))
            return;

        animator.SetTrigger("CastShadow");
    }

    public void TriggerDeath()
    {
        idleTimer = 0f;

        if (animator == null)
            return;

        animator.ResetTrigger("CastShadow");
        animator.SetFloat("Speed", 0f);
        animator.SetFloat("VelocityY", 0f);
        animator.SetBool("IsGrounded", true);
        animator.SetBool("IsSleeping", false);

        if (hasIsDeadParam)
            animator.SetBool("IsDead", true);

        animator.SetTrigger("Die");
    }

    public void ResetAfterRespawn()
    {
        idleTimer = 0f;

        if (animator == null)
            return;

        animator.ResetTrigger("Die");
        animator.ResetTrigger("CastShadow");
        animator.SetBool("IsSleeping", false);

        if (hasIsDeadParam)
            animator.SetBool("IsDead", false);

        animator.Rebind();
        animator.Update(0f);
    }
}