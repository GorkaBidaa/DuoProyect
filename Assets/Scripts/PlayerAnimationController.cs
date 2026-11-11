using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public class PlayerAnimatorController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float sleepIdleDelay = 2.5f; // Seconds idle at base to trigger sleep

    // Precomputed Animator parameter hashes to avoid per-frame string lookups
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsWallSlidingHash = Animator.StringToHash("IsWallSliding");
    private static readonly int IsSleepingHash = Animator.StringToHash("IsSleeping");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int CastShadowHash = Animator.StringToHash("CastShadow");
    private static readonly int DieHash = Animator.StringToHash("Die");

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
            if (param.nameHash == IsDeadHash && param.type == AnimatorControllerParameterType.Bool)
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

        float relativeVelY = movement.RelativeVelocityY;

        // Check if character is resting/sleeping (idle on ground or at Base)
        bool isStillOnGround = movement.isGrounded &&
                               Mathf.Abs(inputX) <= 0.01f &&
                               Mathf.Abs(relativeVelY) <= 0.1f;

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
        animator.SetFloat(SpeedHash, Mathf.Abs(inputX));
        animator.SetFloat(VelocityYHash, relativeVelY);
        animator.SetBool(IsGroundedHash, movement.isGrounded);
        animator.SetBool(IsWallSlidingHash, movement.IsWallSliding);
        animator.SetBool(IsSleepingHash, idleTimer >= sleepIdleDelay);
    }

    public void TriggerCastShadow()
    {
        if (animator == null || (stamina != null && stamina.IsDead))
            return;

        animator.SetTrigger(CastShadowHash);
    }

    public void TriggerDeath()
    {
        idleTimer = 0f;

        if (animator == null)
            return;

        animator.ResetTrigger(CastShadowHash);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetFloat(VelocityYHash, 0f);
        animator.SetBool(IsGroundedHash, true);
        animator.SetBool(IsWallSlidingHash, false);
        animator.SetBool(IsSleepingHash, false);

        if (hasIsDeadParam)
            animator.SetBool(IsDeadHash, true);

        animator.SetTrigger(DieHash);
    }

    public void ResetAfterRespawn()
    {
        idleTimer = 0f;

        if (animator == null)
            return;

        animator.ResetTrigger(DieHash);
        animator.ResetTrigger(CastShadowHash);
        animator.SetBool(IsSleepingHash, false);

        if (hasIsDeadParam)
            animator.SetBool(IsDeadHash, false);

        animator.Rebind();
        animator.Update(0f);
    }
}