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

    public bool FacingLeft => spriteRenderer != null && spriteRenderer.flipX;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        stamina = GetComponent<StaminaSystem>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        // Don't update any animation parameters when dead
        // This allows the death animation to play without interruption
        if (stamina != null && stamina.IsDead)
            return;

        if (animator == null)
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
        animator.SetBool("IsSleeping", idleTimer >= sleepIdleDelay);
    }

    public void TriggerCastShadow()
    {
        if (animator != null)
            animator.SetTrigger("CastShadow");
    }

    public void TriggerDeath()
    {
        if (animator != null)
        {
            // Reset sleeping state before death
            animator.SetBool("IsSleeping", false);
            animator.SetTrigger("Die");
        }
    }
}