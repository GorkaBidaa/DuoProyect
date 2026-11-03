using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Stats")]
    public float maxSpeed = 8f;
    public float acceleration = 10f;
    public float deceleration = 12f;

    [Header("Jump Stats")]
    public float jumpForce = 12f;
    [Tooltip("Base gravity affected by the multipliers (1 = previous behavior)")]
    public float baseGravityScale = 1f;
    public float upwardGravityMultiplier = 2f;
    public float fallMultiplier = 2.5f;
    [Tooltip("Must be greater than upwardGravityMultiplier to shorten the jump when released")]
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

    [Header("Wall Jump & Slide (Hollow Knight Style)")]
    [SerializeField] private bool wallJumpUnlocked = false;
    [Tooltip("If set to Nothing, groundLayer is used automatically")]
    public LayerMask wallLayer;
    [Tooltip("Raycast distance from the side of the Collider2D")]
    public float wallCheckDistance = 0.12f;
    [Tooltip("Distance from the wall where the player stops when sliding so the animation does not clip")]
    [Min(0f)] public float wallSlideSeparation = 0.08f;
    [Tooltip("Vertical offset of the two rays from the collider center (0.5 = upper and lower halves)")]
    [Range(0.1f, 0.9f)] public float wallRayHeightOffset = 0.45f;
    [Tooltip("When enabled, both rays (chest and legs) must touch the wall to slide")]
    public bool requireBothWallRays = true;

    public float wallSlideSpeed = 3.5f;
    [Tooltip("Braking force when hitting a wall while falling faster than wallSlideSpeed")]
    public float wallSlideBrakeDeceleration = 45f;
    [Tooltip("Smooth acceleration toward wallSlideSpeed when starting to fall against a wall")]
    public float wallSlideGravityAcceleration = 20f;

    public Vector2 wallJumpForce = new Vector2(10f, 12.5f);
    [Tooltip("Time that horizontal input is locked after a wall jump to launch away")]
    public float wallJumpMovementLockTime = 0.14f;
    public float wallCoyoteTime = 0.12f;

    [Header("Corner Correction")]
    public Transform headLeft;
    public Transform headRight;
    public float headCheckDistance = 0.3f;
    public float nudgeAmount = 0.1f;
    public float maxCornerDistance = 0.2f;

    private Rigidbody2D rb;
    private Collider2D col;
    private float horizontalInput;
    private bool isTeleporting = false;
    private bool isBouncing = false;
    private bool isJumping = false;
    private bool jumpCutApplied = false;

    // Current wall interaction state.
    [SerializeField] private bool isWallSliding = false;
    private int wallSide = 0; // -1 = left, 1 = right, 0 = none
    private int lastWallSide = 0;
    private float wallHitX = 0f;
    private float wallCoyoteTimeCounter = 0f;
    private float wallJumpLockCounter = 0f;

    private Collider2D lastGroundCollider;
    private MovingPlatform currentPlatform;
    private Vector2 surfaceVelocity;
    private Vector2 lastSurfaceVelocity;
    private bool wasOnPlatform = false;

    public bool isGrounded { get; private set; }
    public bool IsWallSliding => isWallSliding;
    public int WallSide => wallSide;
    public bool WallJumpUnlocked => wallJumpUnlocked;

    public float HorizontalInput => horizontalInput;
    public bool IsTeleporting => isTeleporting;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (wallLayer.value == 0)
            wallLayer = groundLayer;
    }

    public void UnlockWallJump()
    {
        wallJumpUnlocked = true;
    }

    public void SetTeleportingState(bool state)
    {
        isTeleporting = state;
        if (state)
        {
            isJumping = false;
            isBouncing = false;
            jumpCutApplied = false;
            isWallSliding = false;
            wallCoyoteTimeCounter = 0f;
            wallJumpLockCounter = 0f;
            wasOnPlatform = false;
            lastGroundCollider = null;
            currentPlatform = null;
            surfaceVelocity = Vector2.zero;
            lastSurfaceVelocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }
    }

    public void ResetMovementState()
    {
        isJumping = false;
        isBouncing = false;
        jumpCutApplied = false;
        isWallSliding = false;
        wallSide = 0;
        lastWallSide = 0;
        wallCoyoteTimeCounter = 0f;
        wallJumpLockCounter = 0f;
        wasOnPlatform = false;
        lastGroundCollider = null;
        currentPlatform = null;
        surfaceVelocity = Vector2.zero;
        lastSurfaceVelocity = Vector2.zero;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = baseGravityScale * upwardGravityMultiplier;
        }
    }

    private void UpdateGroundCheck()
    {
        if (groundCheck != null)
        {
            Collider2D groundHit = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            isGrounded = groundHit != null;

            if (groundHit != lastGroundCollider)
            {
                lastGroundCollider = groundHit;
                currentPlatform = isGrounded ? groundHit.GetComponentInParent<MovingPlatform>() : null;
            }

            surfaceVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector2.zero;
        }
        else
        {
            isGrounded = false;
            lastGroundCollider = null;
            currentPlatform = null;
            surfaceVelocity = Vector2.zero;
        }
    }

    private void UpdateWallCheck()
    {
        if (!wallJumpUnlocked || col == null || isGrounded)
        {
            wallSide = 0;
            return;
        }

        LayerMask effectiveWallLayer = wallLayer.value != 0 ? wallLayer : groundLayer;
        Bounds b = col.bounds;

        float topY = b.center.y + (b.extents.y * wallRayHeightOffset);
        float bottomY = b.center.y - (b.extents.y * wallRayHeightOffset);

        // Ensure ray length is always slightly larger than wallSlideSeparation so contact is never lost when separated
        float effectiveCheckDist = Mathf.Max(wallCheckDistance, wallSlideSeparation + 0.05f);
        float rayLength = b.extents.x + effectiveCheckDist;

        Vector2 topCenter = new Vector2(b.center.x, topY);
        Vector2 bottomCenter = new Vector2(b.center.x, bottomY);

        // Right rays
        RaycastHit2D rightTopHit = Physics2D.Raycast(topCenter, Vector2.right, rayLength, effectiveWallLayer);
        RaycastHit2D rightBottomHit = Physics2D.Raycast(bottomCenter, Vector2.right, rayLength, effectiveWallLayer);
        bool hasRightTop = rightTopHit.collider != null;
        bool hasRightBottom = rightBottomHit.collider != null;
        bool touchingRight = requireBothWallRays ? (hasRightTop && hasRightBottom) : (hasRightTop || hasRightBottom);

        // Left rays
        RaycastHit2D leftTopHit = Physics2D.Raycast(topCenter, Vector2.left, rayLength, effectiveWallLayer);
        RaycastHit2D leftBottomHit = Physics2D.Raycast(bottomCenter, Vector2.left, rayLength, effectiveWallLayer);
        bool hasLeftTop = leftTopHit.collider != null;
        bool hasLeftBottom = leftBottomHit.collider != null;
        bool touchingLeft = requireBothWallRays ? (hasLeftTop && hasLeftBottom) : (hasLeftTop || hasLeftBottom);

        if (touchingRight && touchingLeft)
        {
            wallSide = horizontalInput < -0.01f ? -1 : 1;
        }
        else if (touchingRight)
        {
            wallSide = 1;
        }
        else if (touchingLeft)
        {
            wallSide = -1;
        }
        else
        {
            wallSide = 0;
        }

        // Store the exact X coordinate of the wall surface
        if (wallSide == 1)
        {
            if (hasRightTop && hasRightBottom) wallHitX = Mathf.Min(rightTopHit.point.x, rightBottomHit.point.x);
            else if (hasRightTop) wallHitX = rightTopHit.point.x;
            else wallHitX = rightBottomHit.point.x;
        }
        else if (wallSide == -1)
        {
            if (hasLeftTop && hasLeftBottom) wallHitX = Mathf.Max(leftTopHit.point.x, leftBottomHit.point.x);
            else if (hasLeftTop) wallHitX = leftTopHit.point.x;
            else wallHitX = leftBottomHit.point.x;
        }
    }

    void Update()
    {
        if (isTeleporting)
        {
            rb.gravityScale = 0f;
            return;
        }

        UpdateGroundCheck();
        UpdateWallCheck();

        if (wallJumpLockCounter > 0f)
            wallJumpLockCounter -= Time.deltaTime;

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
            jumpCutApplied = false;
        }

        if (isTeleporting || isBouncing)
        {
            jumpHeld = true;
            jumpReleased = false;
        }

        horizontalInput = (Mathf.Abs(gamepad) > 0.01f) ? gamepad : keyboard;

        bool pressingTowardsWall = (wallSide == 1 && horizontalInput > 0.1f) ||
                                   (wallSide == -1 && horizontalInput < -0.1f);

        isWallSliding = wallJumpUnlocked &&
                        !isGrounded &&
                        !isTeleporting &&
                        !isBouncing &&
                        wallJumpLockCounter <= 0f &&
                        wallSide != 0 &&
                        pressingTowardsWall &&
                        rb.linearVelocity.y <= 0.1f;

        if (wallJumpUnlocked && !isGrounded && wallSide != 0 && (isWallSliding || pressingTowardsWall))
        {
            wallCoyoteTimeCounter = wallCoyoteTime;
            lastWallSide = wallSide;
        }
        else
        {
            wallCoyoteTimeCounter -= Time.deltaTime;
        }

        if (isGrounded && !isJumping && !isBouncing && relativeVelY <= 0.1f)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        // Ground jump
        if (!isTeleporting && !isBouncing && jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            float jumpVelY = jumpForce + Mathf.Max(0f, surfaceVelocity.y);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelY);

            isJumping = true;
            jumpCutApplied = false;
            isWallSliding = false;
            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            wallCoyoteTimeCounter = 0f;
        }
        // Wall jump
        else if (wallJumpUnlocked && !isTeleporting && !isBouncing && jumpBufferCounter > 0f &&
                 (wallCoyoteTimeCounter > 0f || (!isGrounded && wallSide != 0)))
        {
            int jumpWallDir = wallSide != 0 ? wallSide : lastWallSide;
            float launchX = -jumpWallDir * wallJumpForce.x;

            rb.linearVelocity = new Vector2(launchX, wallJumpForce.y);

            isJumping = true;
            jumpCutApplied = false;
            isWallSliding = false;
            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            wallCoyoteTimeCounter = 0f;
            wallJumpLockCounter = wallJumpMovementLockTime;
        }

        if (jumpReleased && isJumping && !jumpCutApplied && rb.linearVelocity.y > surfaceVelocity.y)
        {
            float reducedRelativeY = (rb.linearVelocity.y - surfaceVelocity.y) * 0.5f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, surfaceVelocity.y + reducedRelativeY);
            jumpCutApplied = true;
            coyoteTimeCounter = 0f;
        }

        if (isWallSliding)
        {
            rb.gravityScale = 0f;
        }
        else
        {
            float currentRelVelY = rb.linearVelocity.y - surfaceVelocity.y;
            float multiplier;
            if (!isGrounded && currentRelVelY < 0f) multiplier = fallMultiplier;
            else if (isJumping && currentRelVelY > 0f && !jumpHeld) multiplier = lowJumpMultiplier;
            else multiplier = upwardGravityMultiplier;

            rb.gravityScale = baseGravityScale * multiplier;
        }
    }

    void FixedUpdate()
    {
        if (isTeleporting) return;

        UpdateGroundCheck();
        UpdateWallCheck();

        bool onPlatform = currentPlatform != null && !isJumping && !isBouncing && !isTeleporting;

        float effectiveHorizontalInput = wallJumpLockCounter > 0f ? 0f : horizontalInput;
        float targetSpeed = (effectiveHorizontalInput * maxSpeed) + (onPlatform ? surfaceVelocity.x : 0f);

        if (onPlatform)
        {
            float deltaX = surfaceVelocity.x - lastSurfaceVelocity.x;
            float newVelX;

            if (!wasOnPlatform)
            {
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
                newVelX = rb.linearVelocity.x + deltaX;
            }

            float newVelY = wasOnPlatform
                ? surfaceVelocity.y
                : Mathf.Min(rb.linearVelocity.y, surfaceVelocity.y);

            rb.linearVelocity = new Vector2(newVelX, newVelY);
        }

        // Do not push the player horizontally into the wall while sliding
        if (wallJumpLockCounter <= 0f && !isWallSliding)
        {
            float accelRate = Mathf.Abs(horizontalInput) > 0.01f ? acceleration : deceleration;
            float speedDifference = targetSpeed - rb.linearVelocity.x;
            float movement = speedDifference * accelRate;

            rb.AddForce(movement * rb.mass * Vector2.right, ForceMode2D.Force);
        }

        // Gradually control wall slide speed and stop slightly before the wall
        if (isWallSliding)
        {
            float targetSlideVelY = -wallSlideSpeed;
            float currentVelY = rb.linearVelocity.y;

            float rate = currentVelY < targetSlideVelY
                ? wallSlideBrakeDeceleration
                : wallSlideGravityAcceleration;

            float newSlideVelY = Mathf.MoveTowards(currentVelY, targetSlideVelY, rate * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(0f, newSlideVelY);

            if (wallSlideSeparation > 0f && col != null && wallSide != 0)
            {
                Bounds b = col.bounds;
                if (wallSide == 1)
                {
                    float currentGap = wallHitX - b.max.x;
                    if (currentGap < wallSlideSeparation)
                    {
                        rb.position = new Vector2(rb.position.x - (wallSlideSeparation - currentGap), rb.position.y);
                    }
                }
                else if (wallSide == -1)
                {
                    float currentGap = b.min.x - wallHitX;
                    if (currentGap < wallSlideSeparation)
                    {
                        rb.position = new Vector2(rb.position.x + (wallSlideSeparation - currentGap), rb.position.y);
                    }
                }
            }
        }

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
            {
                float exactOffset = FindExactCornerOffset(headLeft.position, 1f);
                rb.position += Vector2.right * exactOffset;
            }
        }
        else if (hitRight && !hitLeft)
        {
            Vector2 checkPos = new Vector2(headRight.position.x - maxCornerDistance, headRight.position.y);
            if (Physics2D.Raycast(checkPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
            {
                float exactOffset = FindExactCornerOffset(headRight.position, -1f);
                rb.position += Vector2.left * exactOffset;
            }
        }
    }

    private float FindExactCornerOffset(Vector2 origin, float directionSign)
    {
        float low = 0f;
        float high = maxCornerDistance;

        for (int i = 0; i < 5; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector2 testPos = new Vector2(origin.x + (mid * directionSign), origin.y);
            if (Physics2D.Raycast(testPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
                high = mid;
            else
                low = mid;
        }

        return high + 0.01f;
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

        Collider2D gizmoCol = col != null ? col : GetComponent<Collider2D>();
        if (gizmoCol != null)
        {
            Bounds b = gizmoCol.bounds;
            float topY = b.center.y + (b.extents.y * wallRayHeightOffset);
            float bottomY = b.center.y - (b.extents.y * wallRayHeightOffset);
            float effectiveCheckDist = Mathf.Max(wallCheckDistance, wallSlideSeparation + 0.05f);
            float rayLength = b.extents.x + effectiveCheckDist;

            Gizmos.color = isWallSliding ? Color.green : Color.cyan;
            Gizmos.DrawLine(new Vector3(b.center.x, topY, 0f), new Vector3(b.center.x + rayLength, topY, 0f));
            Gizmos.DrawLine(new Vector3(b.center.x, bottomY, 0f), new Vector3(b.center.x + rayLength, bottomY, 0f));
            Gizmos.DrawLine(new Vector3(b.center.x, topY, 0f), new Vector3(b.center.x - rayLength, topY, 0f));
            Gizmos.DrawLine(new Vector3(b.center.x, bottomY, 0f), new Vector3(b.center.x - rayLength, bottomY, 0f));
        }
    }

    public void Bounce(float force)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
        isBouncing = true;
        isJumping = false;
        jumpCutApplied = false;
        isWallSliding = false;
        wallCoyoteTimeCounter = 0f;
        wallJumpLockCounter = 0f;
        wasOnPlatform = false;
        lastGroundCollider = null;
        currentPlatform = null;
        lastSurfaceVelocity = Vector2.zero;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }
}