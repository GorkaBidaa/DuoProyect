using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Stats")]
    public float maxSpeed = 8f;
    public float acceleration = 10f;
    public float deceleration = 12f;

    [Header("Jump Stats")]
    public float jumpForce = 12f;
    [Tooltip("Gravedad base sobre la que se aplican los multiplicadores (1 = igual que antes)")]
    public float baseGravityScale = 1f;
    public float upwardGravityMultiplier = 2f;
    public float fallMultiplier = 2.5f;
    [Tooltip("Debe ser MAYOR que upwardGravityMultiplier para que soltar el salto acorte el salto")]
    public float lowJumpMultiplier = 3f;

    [Header("Invisible Mechanics (Game Feel)")]
    public float coyoteTime = 0.1f;
    private float coyoteTimeCounter;
    public float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;

    [Header("Environment Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Corner Correction")]
    public Transform headLeft;
    public Transform headRight;
    public float headCheckDistance = 0.3f;
    public float nudgeAmount = 0.1f;
    public float maxCornerDistance = 0.2f;

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isTeleporting = false;
    private bool isBouncing = false;
    private bool isJumping = false;

    private MovingPlatform currentPlatform;
    private Vector2 surfaceVelocity;
    private Vector2 lastSurfaceVelocity;
    private bool wasOnPlatform = false;

    public bool isGrounded { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetTeleportingState(bool state)
    {
        isTeleporting = state;
        if (state)
        {
            isJumping = false;
            wasOnPlatform = false;
            currentPlatform = null;
            lastSurfaceVelocity = Vector2.zero;
        }
    }

    private void UpdateGroundCheck()
    {
        if (groundCheck != null)
        {
            Collider2D groundHit = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            isGrounded = groundHit != null;
            currentPlatform = isGrounded ? groundHit.GetComponentInParent<MovingPlatform>() : null;
            surfaceVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector2.zero;
        }
        else
        {
            isGrounded = false;
            currentPlatform = null;
            surfaceVelocity = Vector2.zero;
        }
    }

    void Update()
    {
        UpdateGroundCheck();

        bool jumpPressed = false;
        bool jumpHeld = false;
        bool jumpReleased = false;

        var kb = Keyboard.current;
        float keyboard = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboard -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboard += 1f;
            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) jumpPressed = true;
            if (kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed) jumpHeld = true;
            if (kb.spaceKey.wasReleasedThisFrame || kb.wKey.wasReleasedThisFrame || kb.upArrowKey.wasReleasedThisFrame) jumpReleased = true;
        }

        var gp = Gamepad.current;
        float gamepad = 0f;
        if (gp != null)
        {
            gamepad = gp.leftStick.x.ReadValue();
            if (gp.dpad.left.isPressed) gamepad = -1f;
            if (gp.dpad.right.isPressed) gamepad = 1f;
            if (gp.buttonSouth.wasPressedThisFrame) jumpPressed = true;
            if (gp.buttonSouth.isPressed) jumpHeld = true;
            if (gp.buttonSouth.wasReleasedThisFrame) jumpReleased = true;
        }

        float relativeVelY = rb.linearVelocity.y - surfaceVelocity.y;

        if (isBouncing && relativeVelY <= 0f)
        {
            isBouncing = false;
        }

        if (isJumping && relativeVelY <= 0.1f)
        {
            isJumping = false;
        }

        if (isTeleporting || isBouncing)
        {
            jumpHeld = true;
            jumpReleased = false;
        }

        horizontalInput = (Mathf.Abs(gamepad) > 0.01f) ? gamepad : keyboard;

        // Evaluamos Coyote Time usando la velocidad relativa a la plataforma
        if (isGrounded && !isJumping && !isBouncing && relativeVelY <= 0.1f)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (!isTeleporting && !isBouncing && jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            // Si la plataforma sube, sumamos su velocidad vertical para que no absorba el salto
            float jumpVelY = jumpForce + Mathf.Max(0f, surfaceVelocity.y);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelY);

            isJumping = true;
            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        if (jumpReleased && isJumping && rb.linearVelocity.y > surfaceVelocity.y)
        {
            float reducedRelativeY = (rb.linearVelocity.y - surfaceVelocity.y) * 0.5f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, surfaceVelocity.y + reducedRelativeY);
            coyoteTimeCounter = 0f;
        }

        float currentRelVelY = rb.linearVelocity.y - surfaceVelocity.y;
        float multiplier;
        if (!isGrounded && currentRelVelY < 0f) multiplier = fallMultiplier;
        else if (isJumping && currentRelVelY > 0f && !jumpHeld) multiplier = lowJumpMultiplier;
        else multiplier = upwardGravityMultiplier;

        rb.gravityScale = baseGravityScale * multiplier;
    }

    void FixedUpdate()
    {
        UpdateGroundCheck();

        bool onPlatform = currentPlatform != null && !isJumping && !isBouncing && !isTeleporting;
        float targetSpeed = (horizontalInput * maxSpeed) + (onPlatform ? surfaceVelocity.x : 0f);

        if (onPlatform)
        {
            float deltaX = surfaceVelocity.x - lastSurfaceVelocity.x;
            float newVelX;

            if (!wasOnPlatform)
            {
                // Al pisar la plataforma: igualar su velocidad inmediatamente sin resbalar
                if (Mathf.Abs(horizontalInput) <= 0.01f)
                {
                    newVelX = surfaceVelocity.x;
                }
                else
                {
                    newVelX = rb.linearVelocity.x + deltaX;
                    if (deltaX > 0f && newVelX > targetSpeed) newVelX = Mathf.Max(rb.linearVelocity.x, targetSpeed);
                    else if (deltaX < 0f && newVelX < targetSpeed) newVelX = Mathf.Min(rb.linearVelocity.x, targetSpeed);
                }
            }
            else
            {
                // Mientras seguimos sobre la plataforma: compensar al instante cambios de sentido
                newVelX = rb.linearVelocity.x + deltaX;
            }

            // Mantener al jugador pegado a la plataforma cuando se mueve en vertical
            float newVelY = wasOnPlatform
                ? surfaceVelocity.y
                : Mathf.Min(rb.linearVelocity.y, surfaceVelocity.y);

            rb.linearVelocity = new Vector2(newVelX, newVelY);
        }

        float accelRate = Mathf.Abs(horizontalInput) > 0.01f ? acceleration : deceleration;
        float speedDifference = targetSpeed - rb.linearVelocity.x;
        float movement = speedDifference * accelRate;

        rb.AddForce(movement * rb.mass * Vector2.right, ForceMode2D.Force);

        wasOnPlatform = onPlatform;
        lastSurfaceVelocity = onPlatform ? surfaceVelocity : Vector2.zero;

        CornerCorrection();
    }

    private void CornerCorrection()
    {
        if (isGrounded || rb.linearVelocity.y <= 0f || headLeft == null || headRight == null) return;

        bool hitLeft = Physics2D.Raycast(headLeft.position, Vector2.up, headCheckDistance, groundLayer).collider != null;
        bool hitRight = Physics2D.Raycast(headRight.position, Vector2.up, headCheckDistance, groundLayer).collider != null;

        if (hitLeft && !hitRight)
        {
            Vector2 checkPos = new Vector2(headLeft.position.x + maxCornerDistance, headLeft.position.y);
            if (Physics2D.Raycast(checkPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
                rb.position += Vector2.right * nudgeAmount;
        }
        else if (hitRight && !hitLeft)
        {
            Vector2 checkPos = new Vector2(headRight.position.x - maxCornerDistance, headRight.position.y);
            if (Physics2D.Raycast(checkPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
                rb.position += Vector2.left * nudgeAmount;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        Gizmos.color = Color.yellow;
        if (headLeft != null) Gizmos.DrawLine(headLeft.position, headLeft.position + Vector3.up * headCheckDistance);
        if (headRight != null) Gizmos.DrawLine(headRight.position, headRight.position + Vector3.up * headCheckDistance);
    }

    public void Bounce(float force)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
        isBouncing = true;
        isJumping = false;
        wasOnPlatform = false;
        currentPlatform = null;
        lastSurfaceVelocity = Vector2.zero;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }
}