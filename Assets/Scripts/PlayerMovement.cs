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

    public bool isGrounded { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void SetTeleportingState(bool state)
    {
        isTeleporting = state;
    }

    void Update()
    {
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

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

        if (isBouncing && rb.linearVelocity.y <= 0f)
        {
            isBouncing = false;
        }
        
        if (isTeleporting || isBouncing)
        {
            jumpHeld = true;
            jumpReleased = false;
        }

        horizontalInput = (Mathf.Abs(gamepad) > 0.01f) ? gamepad : keyboard;

 
        if (isGrounded && rb.linearVelocity.y <= 0.1f) coyoteTimeCounter = coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (!isTeleporting && !isBouncing && jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        if (jumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            coyoteTimeCounter = 0f;
        }

        float vy = rb.linearVelocity.y;
        float multiplier;
        if (vy < 0f) multiplier = fallMultiplier;
        else if (vy > 0f && !jumpHeld) multiplier = lowJumpMultiplier;
        else multiplier = upwardGravityMultiplier;

        rb.gravityScale = baseGravityScale * multiplier;
    }

    void FixedUpdate()
    {
        float targetSpeed = horizontalInput * maxSpeed;
        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;

        float speedDifference = targetSpeed - rb.linearVelocity.x;
        float movement = speedDifference * accelRate;

        // * rb.mass para que el movimiento no dependa de la masa del Rigidbody
        rb.AddForce(movement * rb.mass * Vector2.right, ForceMode2D.Force);

        CornerCorrection();
    }

    private void CornerCorrection()
    {
        if (rb.linearVelocity.y <= 0f || headLeft == null || headRight == null) return;

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
    private bool isBouncing = false;
    public void Bounce(float force)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
        isBouncing = true;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }
}