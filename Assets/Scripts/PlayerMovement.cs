using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Stats")]
    public float maxSpeed = 8f;
    public float acceleration = 10f;
    public float deceleration = 12f;

    [Header("Jump Stats")]
    public float jumpForce = 12f;
    public float upwardGravityMultiplier = 2f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Invisible Mechanics (Game Feel)")]
    public float coyoteTime = 0.1f;
    private float coyoteTimeCounter;
    public float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;

    [Header("Environment Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isGrounded;
    // Corner correction transforms
    public Transform headLeft;
    public Transform headRight;
    public float headCheckDistance = 0.3f;
    public float nudgeAmount = 0.1f;
    public float maxCornerDistance = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // floor check
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // default values
        horizontalInput = 0f;
        bool jumpPressed = false;
        bool jumpHeld = false;
        bool jumpReleased = false;

        // New Input System: keyboard and gamepad
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

        horizontalInput = (Mathf.Abs(gamepad) > 0.01f) ? gamepad : keyboard;

        // COYOTE TIME
        if (isGrounded) coyoteTimeCounter = coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;

        // JUMP BUFFER
        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        // Execute jump if buffered and within coyote time
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // Short hop (stop jump early)
        if (jumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            coyoteTimeCounter = 0f;
        }

        // Asymmetric gravity
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = fallMultiplier;
        }
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.gravityScale = lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = upwardGravityMultiplier;
        }
    }

    void FixedUpdate()
    {
        // Horizontal movement
        float targetSpeed = horizontalInput * maxSpeed;

        float accelRate;
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            accelRate = acceleration;
        }
        else
        {
            accelRate = deceleration;
        }

        float speedDifference = targetSpeed - rb.linearVelocity.x;
        float movement = speedDifference * accelRate;

        rb.AddForce(movement * Vector2.right, ForceMode2D.Force);

        // CORNER CORRECTION
        if (rb.linearVelocity.y > 0f && headLeft != null && headRight != null)
        {
            RaycastHit2D leftHit = Physics2D.Raycast(headLeft.position, Vector2.up, headCheckDistance, groundLayer);
            RaycastHit2D rightHit = Physics2D.Raycast(headRight.position, Vector2.up, headCheckDistance, groundLayer);

            bool hitLeft = leftHit.collider != null;
            bool hitRight = rightHit.collider != null;

            if (hitLeft && !hitRight)
            {
                // Check if space to the right within the forgiven distance is free
                Vector2 checkPos = new Vector2(headLeft.position.x + maxCornerDistance, headLeft.position.y);
                RaycastHit2D check = Physics2D.Raycast(checkPos, Vector2.up, headCheckDistance, groundLayer);
                if (check.collider == null)
                {
                    transform.position = new Vector2(transform.position.x + nudgeAmount, transform.position.y);
                }
            }
            else if (hitRight && !hitLeft)
            {
                // Check if space to the left within the forgiven distance is free
                Vector2 checkPos = new Vector2(headRight.position.x - maxCornerDistance, headRight.position.y);
                RaycastHit2D check = Physics2D.Raycast(checkPos, Vector2.up, headCheckDistance, groundLayer);
                if (check.collider == null)
                {
                    transform.position = new Vector2(transform.position.x - nudgeAmount, transform.position.y);
                }
            }
        }
    }
}
