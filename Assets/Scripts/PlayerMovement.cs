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
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Environment Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // floor check
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        horizontalInput = 0f;
        bool jumpPressed = false;
        bool jumpHeld = false;

        var kb = Keyboard.current;
        float keyboard = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboard -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboard += 1f;
            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) jumpPressed = true;
            if (kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed) jumpHeld = true;
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
        }

        horizontalInput = (Mathf.Abs(gamepad) > 0.01f) ? gamepad : keyboard;

        // Jump
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // Asimetric gravity
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
            rb.gravityScale = 1f;
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
    }
}
