using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public class PlayerAnimatorController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float sleepIdleDelay = 2.5f; // Segundos quieto en la base para dormirse

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

        if (stamina != null && stamina.IsDead)
        {
            if (animator != null)
                animator.enabled = false;
            return;
        }

        if (animator != null && !animator.enabled)
            animator.enabled = true;

        float inputX = movement.HorizontalInput;

        // Voltear sprite según la dirección de movimiento
        if (Mathf.Abs(inputX) > 0.01f && spriteRenderer != null)
        {
            spriteRenderer.flipX = inputX < 0f;
        }

        // Calcular si el personaje está descansando/durmiendo (quieto en el suelo o en la Base)
        bool isStillOnGround = movement.isGrounded && Mathf.Abs(inputX) <= 0.01f && Mathf.Abs(rb.linearVelocity.y) <= 0.1f;
        if (isStillOnGround && (stamina == null || stamina.IsInSafeZone))
        {
            idleTimer += Time.deltaTime;
        }
        else
        {
            idleTimer = 0f;
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(inputX));
            animator.SetFloat("VelocityY", rb.linearVelocity.y);
            animator.SetBool("IsGrounded", movement.isGrounded);
            animator.SetBool("IsSleeping", idleTimer >= sleepIdleDelay);
        }
    }

    public void TriggerCastShadow()
    {
        if (animator != null)
            animator.SetTrigger("CastShadow");
    }
}