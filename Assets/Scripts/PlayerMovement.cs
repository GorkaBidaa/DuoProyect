using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Stats")]
    public float maxSpeed = 8f;
    public float acceleration = 10f;
    public float deceleration = 12f;

    [Header("Air Momentum & Preservation")]
    [Tooltip("Gentle deceleration when releasing horizontal input in mid-air so jumps don't stop abruptly")]
    public float airDeceleration = 3.5f;
    [Tooltip("Very low drag applied in mid-air when moving faster than maxSpeed (e.g. after jumping from a steep slope)")]
    public float airOverSpeedDrag = 0.8f;
    [Tooltip("Acceleration when steering against a high-speed slope jump in mid-air")]
    public float airTurnAcceleration = 6f;

    [Header("Ice Movement Stats")]
    public string iceTag = "Ice";
    [Tooltip("max velocity on ice")]
    public float iceMaxSpeed = 9.5f;
    [Tooltip("acceleration when starting to move on ice")]
    public float iceAcceleration = 4f;
    [Tooltip("deceleration when releasing input (the closer to 0, the further it slides)")]
    public float iceDeceleration = 0.35f;
    [Tooltip("force when trying to turn against the slide direction")]
    public float iceTurnAcceleration = 2.2f;

    public bool isOnIce { get; private set; }

    [Header("Diagonal Slope Sliding (Uncrossable Slopes)")]
    [Tooltip("Minimum surface angle in degrees to trigger heavy downhill sliding")]
    [Range(15f, 60f)] public float minSlopeAngle = 25f;
    [Tooltip("Maximum surface angle in degrees treated as a diagonal slope instead of a vertical wall")]
    [Range(50f, 85f)] public float maxSlopeAngle = 75f;
    [Tooltip("Initial downhill speed applied immediately when stepping onto a diagonal slope")]
    public float minSlopeSlideSpeed = 7f;
    [Tooltip("Downhill acceleration applied while on a diagonal slope")]
    public float slopeSlideAcceleration = 45f;
    [Tooltip("Maximum downhill sliding speed on diagonal slopes")]
    public float slopeSlideMaxSpeed = 16f;
    [Tooltip("Vertical downward force that keeps the player glued to the diagonal slope while sliding")]
    public float slopeStickSpeed = 3f;
    [Tooltip("If enabled, the player can jump while sliding down a diagonal slope (still cannot climb uphill)")]
    public bool allowJumpOnSlope = true;

    public bool isOnSlope { get; private set; }
    public Vector2 SlopeNormal { get; private set; } = Vector2.up;

    [Header("Jump Stats")]
    public float jumpForce = 12f;
    [Tooltip("Base gravity affected by the multipliers (1 = previous behavior)")]
    public float baseGravityScale = 1f;
    public float upwardGravityMultiplier = 2f;
    public float fallMultiplier = 2.5f;
    [Tooltip("Must be greater than upwardGravityMultiplier to shorten the jump when released")]
    public float lowJumpMultiplier = 3f;

    [Header("Jump Audio")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioMixerGroup jumpMixerGroup;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.65f;
    private AudioSource jumpSource;

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

    private enum SlopeZone
    {
        None,
        TopCrest,
        SlidingMiddle,
        BottomBase
    }

    private SlopeZone currentSlopeZone = SlopeZone.None;
    private float currentSlopeSlideSpeed = 0f;
    private float lastActiveSlopeSpeed = 0f;
    private float lastSlopeDownhillSign = 0f;
    private float slopeCoyoteCounter = 0f;
    private float slopeJumpCooldownTimer = 0f;
    private float slopeUphillLockTimer = 0f;

    private Rigidbody2D rb;
    private Collider2D col;
    private StaminaSystem stamina;
    private PhysicsMaterial2D originalPhysicsMaterial;
    private PhysicsMaterial2D frictionlessMaterial;

    private readonly Collider2D[] groundHitsBuffer = new Collider2D[8];
    private readonly RaycastHit2D[] rayHitsBuffer = new RaycastHit2D[8];
    private readonly RaycastHit2D[] verifyHitsBuffer = new RaycastHit2D[8];
    private readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[16];

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

    private float hitStunUntil;

    public bool isGrounded { get; private set; }
    public bool IsWallSliding => isWallSliding;
    public int WallSide => wallSide;
    public bool WallJumpUnlocked => wallJumpUnlocked;

    public float HorizontalInput => horizontalInput;
    public bool IsTeleporting => isTeleporting;
    public float RelativeVelocityY => rb != null ? rb.linearVelocity.y - surfaceVelocity.y : 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        stamina = GetComponent<StaminaSystem>();

        var jumpAudioObject = new GameObject("JumpAudio");
        jumpAudioObject.transform.SetParent(transform, false);
        jumpSource = jumpAudioObject.AddComponent<AudioSource>();
        jumpSource.playOnAwake = false;
        jumpSource.loop = false;
        jumpSource.spatialBlend = 0f;
        jumpSource.dopplerLevel = 0f;
        jumpSource.outputAudioMixerGroup = jumpMixerGroup;

        if (rb != null)
        {
            // Ensure the Rigidbody2D is dynamic and X/Y positions are not frozen in the Inspector
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.WakeUp();
        }

        if (col != null)
        {
            frictionlessMaterial = new PhysicsMaterial2D("PlayerFrictionlessSlope")
            {
                friction = 0f,
                bounciness = 0f
            };

            // Use frictionless material if none was assigned so Box2D default friction (0.4) doesn't block AddForce or ice sliding
            originalPhysicsMaterial = col.sharedMaterial != null ? col.sharedMaterial : frictionlessMaterial;
            col.sharedMaterial = originalPhysicsMaterial;
        }

        if (wallLayer.value == 0)
            wallLayer = groundLayer;
    }

    void Start()
    {
        if (Time.timeScale <= 0f)
            Debug.LogWarning("[PlayerMovement] Time.timeScale es 0 al iniciar la escena. El jugador no podrá moverse hasta reanudar el tiempo.", this);

        if (stamina != null && stamina.IsDead)
            Debug.LogWarning("[PlayerMovement] StaminaSystem.IsDead es TRUE al iniciar la escena. Revisa la inicialización de estamina en Awake/Start.", this);

        if (GameInput.Instance == null)
            Debug.LogWarning("[PlayerMovement] No se encontró GameInput.Instance en la escena. Usando lectura directa de Teclado/Gamepad como respaldo.", this);
        else if (GameInput.Instance.GameplayBlocked)
            Debug.LogWarning("[PlayerMovement] GameInput.Instance.GameplayBlocked es TRUE al iniciar. El input del jugador está bloqueado.", this);

        if (groundLayer.value == 0)
            Debug.LogWarning("[PlayerMovement] 'groundLayer' está configurado en Nothing (0). Asigna la capa del suelo en el Inspector.", this);
    }

    private void OnDestroy()
    {
        // Clean up dynamically created physics material to prevent memory leaks
        if (frictionlessMaterial != null)
            Destroy(frictionlessMaterial);
    }

    private void PlayJumpSound()
    {
        if (!jumpClip || !jumpSource) return;
        jumpSource.clip = jumpClip;
        jumpSource.volume = jumpVolume;
        // Reuse one voice so repeated jumps do not stack overlapping sound tails.
        jumpSource.Play();
    }

    public void UnlockWallJump()
    {
        wallJumpUnlocked = true;
    }

    public void SetTeleportingState(bool state)
    {
        isTeleporting = state;
        isJumping = false;
        isBouncing = false;
        jumpCutApplied = false;
        isWallSliding = false;
        isOnSlope = false;
        currentSlopeZone = SlopeZone.None;
        currentSlopeSlideSpeed = 0f;
        lastActiveSlopeSpeed = 0f;
        slopeCoyoteCounter = 0f;
        slopeJumpCooldownTimer = 0f;
        slopeUphillLockTimer = 0f;
        wallSide = 0;
        lastWallSide = 0;
        wallCoyoteTimeCounter = 0f;
        wallJumpLockCounter = 0f;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        wasOnPlatform = false;
        lastGroundCollider = null;
        currentPlatform = null;
        surfaceVelocity = Vector2.zero;
        lastSurfaceVelocity = Vector2.zero;
        ApplyColliderMaterial(originalPhysicsMaterial);

        if (state)
        {
            isGrounded = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }
        }
        else
        {
            UpdateGroundCheck();
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;
        }
    }

    public void StopForDialogue()
    {
        ResetMovementState();
        horizontalInput = 0f;
    }

    public void ResetMovementState()
    {
        hitStunUntil = 0f;
        horizontalInput = 0f;
        isJumping = false;
        isBouncing = false;
        jumpCutApplied = false;
        isWallSliding = false;
        isOnSlope = false;
        currentSlopeZone = SlopeZone.None;
        currentSlopeSlideSpeed = 0f;
        lastActiveSlopeSpeed = 0f;
        slopeCoyoteCounter = 0f;
        slopeJumpCooldownTimer = 0f;
        slopeUphillLockTimer = 0f;
        SlopeNormal = Vector2.up;
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
        ApplyColliderMaterial(originalPhysicsMaterial);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = baseGravityScale * upwardGravityMultiplier;
        }
    }

    private void ApplyColliderMaterial(PhysicsMaterial2D targetMaterial)
    {
        // Only assign sharedMaterial when it actually changes so Box2D never rebuilds the collider every frame
        if (col != null && col.sharedMaterial != targetMaterial)
        {
            col.sharedMaterial = targetMaterial;
        }
    }

    // Reads input from GameInput singleton if present, or falls back directly to Unity InputSystem (Keyboard/Gamepad)
    private void ReadPlayerInput(out float moveInput, out bool jumpPressed, out bool jumpHeld, out bool jumpReleased, out bool inputAllowed)
    {
        GameInput input = GameInput.Instance;
        if (input != null)
        {
            inputAllowed = !input.GameplayBlocked;
            moveInput = inputAllowed ? input.Move : 0f;
            jumpPressed = inputAllowed && input.Pressed("Jump");
            jumpHeld = inputAllowed && input.Held("Jump");
            jumpReleased = inputAllowed && input.Released("Jump");
            return;
        }

        // Fallback to direct InputSystem when testing a scene without GameInput.Instance
        inputAllowed = true;
        moveInput = 0f;
        jumpPressed = false;
        jumpHeld = false;
        jumpReleased = false;

        if (Keyboard.current != null)
        {
            float left = (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) ? -1f : 0f;
            float right = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) ? 1f : 0f;
            moveInput = left + right;

            jumpPressed |= Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame;
            jumpHeld |= Keyboard.current.spaceKey.isPressed || Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            jumpReleased |= Keyboard.current.spaceKey.wasReleasedThisFrame || Keyboard.current.wKey.wasReleasedThisFrame || Keyboard.current.upArrowKey.wasReleasedThisFrame;
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.x.ReadValue();
            float dpadX = Gamepad.current.dpad.x.ReadValue();
            float padMove = Mathf.Abs(stickX) > Mathf.Abs(dpadX) ? stickX : dpadX;
            if (Mathf.Abs(padMove) > 0.1f)
                moveInput = Mathf.Clamp(padMove, -1f, 1f);

            jumpPressed |= Gamepad.current.buttonSouth.wasPressedThisFrame;
            jumpHeld |= Gamepad.current.buttonSouth.isPressed;
            jumpReleased |= Gamepad.current.buttonSouth.wasReleasedThisFrame;
        }
    }

    // Helper for non-allocating solid raycasts that ignore triggers and the player's own collider
    private RaycastHit2D SolidRaycast(Vector2 origin, Vector2 direction, float distance, LayerMask mask)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = false;

        int count = Physics2D.Raycast(origin, direction, filter, rayHitsBuffer, distance);
        RaycastHit2D bestHit = default;
        float closestDist = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = rayHitsBuffer[i];
            if (hit.collider == null || hit.collider == col || hit.collider.isTrigger)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestHit = hit;
            }
        }

        return bestHit;
    }

    private void UpdateGroundCheck()
    {
        if (groundCheck != null)
        {
            // Non-allocating overlap check using ContactFilter2D
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useTriggers = false;

            int hitCount = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, filter, groundHitsBuffer);

            isGrounded = false;
            isOnIce = false;

            Collider2D groundHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = groundHitsBuffer[i];
                // Ignore the player's own collider and triggers
                if (hit == null || hit == col || hit.isTrigger)
                    continue;

                isGrounded = true;

                if (groundHit == null)
                    groundHit = hit;

                if (!string.IsNullOrEmpty(iceTag) && hit.CompareTag(iceTag))
                {
                    isOnIce = true;
                    groundHit = hit;
                    break;
                }
            }

            if (groundHit != lastGroundCollider)
            {
                lastGroundCollider = groundHit;
                currentPlatform = isGrounded && groundHit != null ? groundHit.GetComponentInParent<MovingPlatform>() : null;
            }

            surfaceVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector2.zero;
        }
        else
        {
            isGrounded = false;
            isOnIce = false;
            lastGroundCollider = null;
            currentPlatform = null;
            surfaceVelocity = Vector2.zero;
        }
    }

    // Distinguishes between the top crest, middle sliding surface, and bottom base of a diagonal slope
    private void UpdateSlopeCheck()
    {
        // Do not snap back onto the slope while taking off from a jump
        if (col == null || isTeleporting || isBouncing || slopeJumpCooldownTimer > 0f || (isJumping && rb.linearVelocity.y > 0.2f))
        {
            currentSlopeZone = SlopeZone.None;
            isOnSlope = false;
            SlopeNormal = Vector2.up;
            ApplyColliderMaterial(originalPhysicsMaterial);
            return;
        }

        Bounds b = col.bounds;
        float edgeOffset = Mathf.Max(0.01f, b.extents.x - 0.02f);

        Vector2 leftOrigin = new Vector2(b.center.x - edgeOffset, b.center.y);
        Vector2 centerOrigin = new Vector2(b.center.x, b.center.y);
        Vector2 rightOrigin = new Vector2(b.center.x + edgeOffset, b.center.y);

        // Reach far enough for a 45-degree diagonal under the center and edges of a BoxCollider2D
        float slopeFootDist = b.extents.y + b.extents.x + 0.25f;
        float slopeCrestProbeDist = b.extents.y + b.extents.x + 0.45f;
        float flatCheckMargin = (isOnSlope || currentSlopeSlideSpeed > 0.1f) ? 0.22f : 0.08f;
        float flatCheckDist = b.extents.y + flatCheckMargin;

        // 1. Check if any part of the feet is resting on flat ground
        bool leftFlat = TryDetectFlatGroundRay(leftOrigin, flatCheckDist, out Vector2 leftFlatPt);
        bool centerFlat = TryDetectFlatGroundRay(centerOrigin, flatCheckDist, out Vector2 centerFlatPt);
        bool rightFlat = TryDetectFlatGroundRay(rightOrigin, flatCheckDist, out Vector2 rightFlatPt);
        bool touchingFlatGround = leftFlat || centerFlat || rightFlat;

        // 2. Check active physics contacts and vertical foot rays for a diagonal slope
        Vector2 detectedNormal = Vector2.up;
        Vector2 detectedSlopePt = Vector2.zero;
        bool hasDirectSlope = false;

        if (TryDetectSlopeFromContacts(b, out Vector2 contactNorm, out Vector2 contactPt))
        {
            hasDirectSlope = true;
            detectedNormal = contactNorm;
            detectedSlopePt = contactPt;
        }
        else
        {
            bool hitC = TryDetectSlopeVerticalRay(centerOrigin, slopeFootDist, out Vector2 cNorm, out Vector2 cPt);
            bool hitL = TryDetectSlopeVerticalRay(leftOrigin, slopeFootDist, out Vector2 lNorm, out Vector2 lPt);
            bool hitR = TryDetectSlopeVerticalRay(rightOrigin, slopeFootDist, out Vector2 rNorm, out Vector2 rPt);

            // Pick the highest contact point among the 3 rays and ensure the bottom of the collider is close to it
            float highestY = float.NegativeInfinity;
            if (hitL && lPt.y > highestY) { highestY = lPt.y; detectedNormal = lNorm; detectedSlopePt = lPt; }
            if (hitC && cPt.y > highestY) { highestY = cPt.y; detectedNormal = cNorm; detectedSlopePt = cPt; }
            if (hitR && rPt.y > highestY) { highestY = rPt.y; detectedNormal = rNorm; detectedSlopePt = rPt; }

            if (highestY > float.NegativeInfinity && (b.min.y - highestY) <= 0.22f)
            {
                hasDirectSlope = true;
            }
        }

        // 3. Check horizontal ankle rays for bottom-of-slope contact when walking uphill into the ramp
        bool hasAnkleSlope = false;
        if (!hasDirectSlope)
        {
            Vector2 ankleCenter = new Vector2(b.center.x, b.min.y + 0.08f);
            float ankleRayDist = b.extents.x + 0.14f;

            if (TryDetectSlopeHorizontalRay(ankleCenter, Vector2.left, ankleRayDist, out Vector2 alNorm, out Vector2 alPt))
            {
                hasAnkleSlope = true;
                detectedNormal = alNorm;
                detectedSlopePt = alPt;
            }
            else if (TryDetectSlopeHorizontalRay(ankleCenter, Vector2.right, ankleRayDist, out Vector2 arNorm, out Vector2 arPt))
            {
                hasAnkleSlope = true;
                detectedNormal = arNorm;
                detectedSlopePt = arPt;
            }
        }

        // 4. If standing on flat ground at the top ledge, probe under the leading foot to detect the downward slope crest
        bool hasTopCrestProbe = false;
        if (!hasDirectSlope && !hasAnkleSlope && touchingFlatGround && !isJumping)
        {
            if (!rightFlat && TryDetectSlopeVerticalRay(rightOrigin, slopeCrestProbeDist, out Vector2 rcNorm, out Vector2 rcPt) &&
                rcNorm.x > 0.15f && rcPt.y < b.min.y - 0.01f)
            {
                hasTopCrestProbe = true;
                detectedNormal = rcNorm;
                detectedSlopePt = rcPt;
            }
            else if (!leftFlat && TryDetectSlopeVerticalRay(leftOrigin, slopeCrestProbeDist, out Vector2 lcNorm, out Vector2 lcPt) &&
                     lcNorm.x < -0.15f && lcPt.y < b.min.y - 0.01f)
            {
                hasTopCrestProbe = true;
                detectedNormal = lcNorm;
                detectedSlopePt = lcPt;
            }
        }

        if (!hasDirectSlope && !hasAnkleSlope && !hasTopCrestProbe)
        {
            currentSlopeZone = SlopeZone.None;
            isOnSlope = false;
            SlopeNormal = Vector2.up;
            currentSlopeSlideSpeed = 0f;
            ApplyColliderMaterial(originalPhysicsMaterial);
            return;
        }

        SlopeNormal = detectedNormal;
        float downhillSign = Mathf.Sign(SlopeNormal.x);

        if (touchingFlatGround)
        {
            float refFlatX = centerFlat ? centerFlatPt.x : (leftFlat ? leftFlatPt.x : rightFlatPt.x);
            bool slopeIsDownhillOfFlat = ((detectedSlopePt.x - refFlatX) * downhillSign) > 0f || hasTopCrestProbe;

            if (slopeIsDownhillOfFlat)
            {
                bool movingTowardsSlope = (horizontalInput * downhillSign) > 0.01f || (rb.linearVelocity.x * downhillSign) > 0.1f || !centerFlat;
                if (movingTowardsSlope)
                {
                    currentSlopeZone = SlopeZone.TopCrest;
                    isOnSlope = true;
                    isGrounded = true;
                    lastSlopeDownhillSign = downhillSign;
                    slopeCoyoteCounter = coyoteTime;
                }
                else
                {
                    currentSlopeZone = SlopeZone.None;
                    isOnSlope = false;
                }
            }
            else
            {
                currentSlopeZone = SlopeZone.BottomBase;
                isOnSlope = false;
                isGrounded = true;
            }
        }
        else
        {
            currentSlopeZone = SlopeZone.SlidingMiddle;
            isOnSlope = true;
            isGrounded = true;
            lastSlopeDownhillSign = downhillSign;
            slopeCoyoteCounter = coyoteTime;
        }

        PhysicsMaterial2D desiredMat = (currentSlopeZone != SlopeZone.None && frictionlessMaterial != null)
            ? frictionlessMaterial
            : originalPhysicsMaterial;
        ApplyColliderMaterial(desiredMat);
    }

    // Checks actual Rigidbody2D contacts and verifies with a downward ray that the surface is a true diagonal slope (not a 90-degree step corner)
    private bool TryDetectSlopeFromContacts(Bounds b, out Vector2 slopeNormal, out Vector2 hitPoint)
    {
        slopeNormal = Vector2.up;
        hitPoint = Vector2.zero;

        if (rb == null)
            return false;

        LayerMask combinedGroundMask = groundLayer | wallLayer;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(combinedGroundMask);
        filter.useTriggers = false;

        int count = rb.GetContacts(filter, contactBuffer);
        float maxAllowedY = b.center.y + b.extents.y * 0.2f;

        for (int i = 0; i < count; i++)
        {
            ContactPoint2D cp = contactBuffer[i];
            if (cp.point.y > maxAllowedY || !IsDiagonalSlopeNormal(cp.normal))
                continue;

            // Verify at the contact X that a vertical 0-width ray also sees a diagonal normal
            Vector2 verifyOrigin = new Vector2(cp.point.x, cp.point.y + 0.2f);
            if (TryDetectSlopeVerticalRay(verifyOrigin, 0.45f, out Vector2 verifiedNorm, out Vector2 verifiedPt))
            {
                slopeNormal = verifiedNorm;
                hitPoint = verifiedPt;
                return true;
            }
        }

        return false;
    }

    private bool TryDetectFlatGroundRay(Vector2 origin, float distance, out Vector2 hitPoint)
    {
        hitPoint = Vector2.zero;
        LayerMask combinedGroundMask = groundLayer | wallLayer;
        int count = Physics2D.RaycastNonAlloc(origin, Vector2.down, rayHitsBuffer, distance, combinedGroundMask);

        RaycastHit2D bestHit = default;
        float closestDist = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = rayHitsBuffer[i];
            if (hit.collider == null || hit.collider == col || hit.collider.isTrigger || hit.distance <= 0.001f)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestHit = hit;
            }
        }

        if (bestHit.collider != null && bestHit.normal.y >= 0.92f)
        {
            hitPoint = bestHit.point;
            return true;
        }

        return false;
    }

    private bool TryDetectSlopeVerticalRay(Vector2 origin, float distance, out Vector2 slopeNormal, out Vector2 hitPoint)
    {
        slopeNormal = Vector2.up;
        hitPoint = Vector2.zero;
        LayerMask combinedGroundMask = groundLayer | wallLayer;

        int count = Physics2D.RaycastNonAlloc(origin, Vector2.down, rayHitsBuffer, distance, combinedGroundMask);
        RaycastHit2D bestHit = default;
        float closestDist = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = rayHitsBuffer[i];
            if (hit.collider == null || hit.collider == col || hit.collider.isTrigger || hit.distance <= 0.001f)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestHit = hit;
            }
        }

        if (bestHit.collider == null || !IsDiagonalSlopeNormal(bestHit.normal))
            return false;

        // Verify with a parallel vertical ray 0.04 units away that the surface height actually changes diagonally
        const float probeOffset = 0.04f;
        float probeDir = bestHit.normal.x >= 0f ? 1f : -1f;
        Vector2 verifyOrigin = new Vector2(origin.x + (probeOffset * probeDir), origin.y);
        int verifyCount = Physics2D.RaycastNonAlloc(verifyOrigin, Vector2.down, verifyHitsBuffer, distance + 0.15f, combinedGroundMask);

        RaycastHit2D bestVerifyHit = default;
        float closestVerifyDist = float.PositiveInfinity;

        for (int i = 0; i < verifyCount; i++)
        {
            RaycastHit2D vHit = verifyHitsBuffer[i];
            if (vHit.collider == null || vHit.collider == col || vHit.collider.isTrigger || vHit.distance <= 0.001f)
                continue;

            if (vHit.distance < closestVerifyDist)
            {
                closestVerifyDist = vHit.distance;
                bestVerifyHit = vHit;
            }
        }

        if (bestVerifyHit.collider != null)
        {
            float heightDiff = Mathf.Abs(bestHit.point.y - bestVerifyHit.point.y);
            if (IsDiagonalSlopeNormal(bestVerifyHit.normal) && heightDiff > 0.008f)
            {
                slopeNormal = bestHit.normal.normalized;
                hitPoint = bestHit.point;
                return true;
            }
        }

        return false;
    }

    private bool TryDetectSlopeHorizontalRay(Vector2 origin, Vector2 direction, float distance, out Vector2 slopeNormal, out Vector2 hitPoint)
    {
        slopeNormal = Vector2.up;
        hitPoint = Vector2.zero;
        LayerMask combinedGroundMask = groundLayer | wallLayer;

        int count = Physics2D.RaycastNonAlloc(origin, direction, rayHitsBuffer, distance, combinedGroundMask);
        RaycastHit2D bestHit = default;
        float closestDist = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = rayHitsBuffer[i];
            if (hit.collider == null || hit.collider == col || hit.collider.isTrigger || hit.distance <= 0.001f)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestHit = hit;
            }
        }

        if (bestHit.collider == null || !IsDiagonalSlopeNormal(bestHit.normal))
            return false;

        Vector2 upperOrigin = new Vector2(origin.x, origin.y + 0.05f);
        int verifyCount = Physics2D.RaycastNonAlloc(upperOrigin, direction, verifyHitsBuffer, distance + 0.15f, combinedGroundMask);

        RaycastHit2D bestVerifyHit = default;
        float closestVerifyDist = float.PositiveInfinity;

        for (int i = 0; i < verifyCount; i++)
        {
            RaycastHit2D vHit = verifyHitsBuffer[i];
            if (vHit.collider == null || vHit.collider == col || vHit.collider.isTrigger || vHit.distance <= 0.001f)
                continue;

            if (vHit.distance < closestVerifyDist)
            {
                closestVerifyDist = vHit.distance;
                bestVerifyHit = vHit;
            }
        }

        if (bestVerifyHit.collider != null &&
            IsDiagonalSlopeNormal(bestVerifyHit.normal) &&
            Mathf.Abs(bestVerifyHit.distance - bestHit.distance) > 0.01f)
        {
            slopeNormal = bestHit.normal.normalized;
            hitPoint = bestHit.point;
            return true;
        }

        return false;
    }

    private bool IsDiagonalSlopeNormal(Vector2 normal)
    {
        if (normal.y <= 0.05f)
            return false;

        float angle = Vector2.Angle(normal, Vector2.up);
        return angle >= minSlopeAngle && angle <= maxSlopeAngle && Mathf.Abs(normal.x) > 0.15f;
    }

    private void UpdateWallCheck()
    {
        if (!wallJumpUnlocked || col == null || isGrounded || isOnSlope || currentSlopeZone != SlopeZone.None)
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

        // Right rays (ignore triggers and diagonal slopes so the player cannot wall-jump up them)
        RaycastHit2D rightTopHit = SolidRaycast(topCenter, Vector2.right, rayLength, effectiveWallLayer);
        RaycastHit2D rightBottomHit = SolidRaycast(bottomCenter, Vector2.right, rayLength, effectiveWallLayer);
        bool hasRightTop = rightTopHit.collider != null && !IsDiagonalSlopeNormal(rightTopHit.normal);
        bool hasRightBottom = rightBottomHit.collider != null && !IsDiagonalSlopeNormal(rightBottomHit.normal);
        bool touchingRight = requireBothWallRays ? (hasRightTop && hasRightBottom) : (hasRightTop || hasRightBottom);

        // Left rays (ignore triggers and diagonal slopes so the player cannot wall-jump up them)
        RaycastHit2D leftTopHit = SolidRaycast(topCenter, Vector2.left, rayLength, effectiveWallLayer);
        RaycastHit2D leftBottomHit = SolidRaycast(bottomCenter, Vector2.left, rayLength, effectiveWallLayer);
        bool hasLeftTop = leftTopHit.collider != null && !IsDiagonalSlopeNormal(leftTopHit.normal);
        bool hasLeftBottom = leftBottomHit.collider != null && !IsDiagonalSlopeNormal(leftBottomHit.normal);
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

    public void ApplyHitKnockback(Vector2 velocity, float duration)
    {
        if (isTeleporting) return;
        ResetMovementState();
        hitStunUntil = Time.time + Mathf.Max(0f, duration);
        rb.linearVelocity = velocity;
    }

    void Update()
    {
        // Ignore player input when dead, paused, or hit-stunned
        if ((Time.time < hitStunUntil || Time.timeScale <= 0f || (stamina != null && stamina.IsDead)) && !isTeleporting)
        {
            horizontalInput = 0f;
            jumpBufferCounter = 0f;
            return;
        }
        if (isTeleporting)
        {
            rb.gravityScale = 0f;
            return;
        }

        if (slopeJumpCooldownTimer > 0f)
            slopeJumpCooldownTimer -= Time.deltaTime;
        if (slopeUphillLockTimer > 0f)
            slopeUphillLockTimer -= Time.deltaTime;
        if (slopeCoyoteCounter > 0f)
            slopeCoyoteCounter -= Time.deltaTime;

        if (wallJumpLockCounter > 0f)
            wallJumpLockCounter -= Time.deltaTime;

        ReadPlayerInput(
            out float rawMove,
            out bool jumpPressed,
            out bool jumpHeld,
            out bool jumpReleased,
            out bool inputAllowed
        );

        if (!inputAllowed)
            jumpBufferCounter = 0f;

        horizontalInput = rawMove;

        float relativeVelY = rb.linearVelocity.y - surfaceVelocity.y;

        if (isBouncing && relativeVelY <= 0f)
        {
            isBouncing = false;
        }

        if (isJumping && slopeJumpCooldownTimer <= 0f && relativeVelY <= 0.1f)
        {
            isJumping = false;
            jumpCutApplied = false;
        }

        if (isTeleporting || isBouncing)
        {
            jumpHeld = true;
            jumpReleased = false;
        }

        bool pressingTowardsWall = (wallSide == 1 && horizontalInput > 0.1f) ||
                                   (wallSide == -1 && horizontalInput < -0.1f);

        isWallSliding = wallJumpUnlocked &&
                        !isGrounded &&
                        !isOnSlope &&
                        !isTeleporting &&
                        !isBouncing &&
                        wallJumpLockCounter <= 0f &&
                        wallSide != 0 &&
                        pressingTowardsWall &&
                        rb.linearVelocity.y <= 0.1f;

        if (wallJumpUnlocked && !isGrounded && !isOnSlope && wallSide != 0 && (isWallSliding || pressingTowardsWall))
        {
            wallCoyoteTimeCounter = wallCoyoteTime;
            lastWallSide = wallSide;
        }
        else
        {
            wallCoyoteTimeCounter -= Time.deltaTime;
        }

        bool canUseGroundJump = isGrounded && (!isOnSlope || allowJumpOnSlope);

        if (canUseGroundJump && !isJumping && !isBouncing && (relativeVelY <= 0.1f || isOnSlope))
            coyoteTimeCounter = coyoteTime;
        else if (isOnSlope && !allowJumpOnSlope)
            coyoteTimeCounter = 0f;
        else
            coyoteTimeCounter -= Time.deltaTime;

        if (jumpPressed && (!isOnSlope || allowJumpOnSlope)) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        // Ground jump (preserves full downhill momentum when jumping from a diagonal slope)
        if (!isTeleporting && !isBouncing && (!isOnSlope || allowJumpOnSlope) && jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            float jumpVelY = jumpForce + Mathf.Max(0f, surfaceVelocity.y);
            float jumpVelX = rb.linearVelocity.x;

            if (isOnSlope || slopeCoyoteCounter > 0f)
            {
                float downhillSign = isOnSlope ? Mathf.Sign(SlopeNormal.x) : lastSlopeDownhillSign;
                float preservedSpeed = Mathf.Max(Mathf.Abs(rb.linearVelocity.x), currentSlopeSlideSpeed, lastActiveSlopeSpeed, minSlopeSlideSpeed);
                jumpVelX = downhillSign * preservedSpeed;
                slopeJumpCooldownTimer = 0.2f;
                // Only block uphill steering for a short window, without zeroing downhill momentum
                slopeUphillLockTimer = 0.22f;
                slopeCoyoteCounter = 0f;
            }

            rb.linearVelocity = new Vector2(jumpVelX, jumpVelY);
            PlayJumpSound();

            isJumping = true;
            jumpCutApplied = false;
            isWallSliding = false;
            isOnSlope = false;
            currentSlopeZone = SlopeZone.None;
            currentSlopeSlideSpeed = 0f;
            lastActiveSlopeSpeed = 0f;
            ApplyColliderMaterial(originalPhysicsMaterial);
            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            wallCoyoteTimeCounter = 0f;

            // Consume stamina when jumping
            if (stamina != null)
                stamina.ConsumeStamina(stamina.jumpStaminaCost);
        }
        // Wall jump
        else if (wallJumpUnlocked && !isTeleporting && !isBouncing && !isOnSlope && jumpBufferCounter > 0f &&
                 (wallCoyoteTimeCounter > 0f || (!isGrounded && wallSide != 0)))
        {
            int jumpWallDir = wallSide != 0 ? wallSide : lastWallSide;
            float launchX = -jumpWallDir * wallJumpForce.x;

            rb.linearVelocity = new Vector2(launchX, wallJumpForce.y);
            PlayJumpSound();

            isJumping = true;
            jumpCutApplied = false;
            isWallSliding = false;
            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            wallCoyoteTimeCounter = 0f;
            wallJumpLockCounter = wallJumpMovementLockTime;

            // Consume stamina when wall jumping
            if (stamina != null)
                stamina.ConsumeStamina(stamina.jumpStaminaCost);
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
        if ((Time.time < hitStunUntil || (stamina != null && stamina.IsDead)) && !isTeleporting) return;
        if (isTeleporting) return;

        UpdateGroundCheck();
        UpdateSlopeCheck();
        UpdateWallCheck();

        float downhillSign = Mathf.Sign(SlopeNormal.x);

        // 1. Top crest of the slope: carry the player horizontally across the flat ledge onto the downward slope
        if (currentSlopeZone == SlopeZone.TopCrest)
        {
            currentSlopeSlideSpeed = Mathf.Max(currentSlopeSlideSpeed, minSlopeSlideSpeed, Mathf.Abs(rb.linearVelocity.x));
            lastActiveSlopeSpeed = currentSlopeSlideSpeed;
            rb.linearVelocity = new Vector2(downhillSign * currentSlopeSlideSpeed, Mathf.Min(0f, rb.linearVelocity.y));

            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            CornerCorrection();
            return;
        }

        // 2. Middle of the diagonal slope: accelerate rapidly downhill and never push uphill
        if (currentSlopeZone == SlopeZone.SlidingMiddle)
        {
            Vector2 downhillTangent = new Vector2(SlopeNormal.y * downhillSign, -Mathf.Abs(SlopeNormal.x)).normalized;

            float tangentVel = Vector2.Dot(rb.linearVelocity, downhillTangent);
            currentSlopeSlideSpeed = Mathf.Max(currentSlopeSlideSpeed, minSlopeSlideSpeed, tangentVel);

            bool pressingDownhill = (horizontalInput * downhillSign) > 0.05f;
            float targetSlide = pressingDownhill ? slopeSlideMaxSpeed * 1.15f : slopeSlideMaxSpeed;
            float slideAccel = pressingDownhill ? slopeSlideAcceleration * 1.25f : slopeSlideAcceleration;

            currentSlopeSlideSpeed = Mathf.MoveTowards(currentSlopeSlideSpeed, targetSlide, slideAccel * Time.fixedDeltaTime);
            lastActiveSlopeSpeed = currentSlopeSlideSpeed;

            // Apply pure vertical stick force (never -SlopeNormal.x) so horizontal velocity is always strictly downhill
            rb.linearVelocity = (downhillTangent * currentSlopeSlideSpeed) + (Vector2.down * slopeStickSpeed);

            wasOnPlatform = false;
            lastSurfaceVelocity = Vector2.zero;
            CornerCorrection();
            return;
        }

        // 3. Bottom base of the slope: eject horizontally onto the flat floor and block uphill climbing without trapping the player
        if (currentSlopeZone == SlopeZone.BottomBase)
        {
            bool tryingToClimbUphill = (horizontalInput * downhillSign) < -0.01f || (rb.linearVelocity.x * downhillSign) < -0.01f;

            if (currentSlopeSlideSpeed > 0.1f || lastActiveSlopeSpeed > 0.1f)
            {
                float slideSpeed = Mathf.Max(currentSlopeSlideSpeed, lastActiveSlopeSpeed);
                float exitSpeed = Mathf.Max(minSlopeSlideSpeed * 0.6f, slideSpeed * 0.9f);
                rb.linearVelocity = new Vector2(downhillSign * exitSpeed, 0f);
                currentSlopeSlideSpeed = 0f;
                lastActiveSlopeSpeed = 0f;
            }
            else if (tryingToClimbUphill)
            {
                float repelSpeed = Mathf.Max(3.5f, minSlopeSlideSpeed * 0.5f);
                rb.linearVelocity = new Vector2(downhillSign * repelSpeed, 0f);
                CornerCorrection();
                return;
            }
            else if (!isJumping && !isBouncing && slopeJumpCooldownTimer <= 0f && rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }
        else if (currentSlopeZone == SlopeZone.None && slopeCoyoteCounter > 0f && !isJumping && !isBouncing && slopeJumpCooldownTimer <= 0f)
        {
            if (lastActiveSlopeSpeed > 0.1f && isGrounded)
            {
                float exitSpeed = Mathf.Max(minSlopeSlideSpeed * 0.6f, lastActiveSlopeSpeed * 0.9f);
                rb.linearVelocity = new Vector2(lastSlopeDownhillSign * exitSpeed, 0f);
                lastActiveSlopeSpeed = 0f;
            }
            else if (rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }
        }

        bool onPlatform = currentPlatform != null && !isJumping && !isBouncing && !isTeleporting;

        float currentMaxSpeed = isOnIce ? iceMaxSpeed : maxSpeed;
        float effectiveHorizontalInput = wallJumpLockCounter > 0f ? 0f : horizontalInput;

        // Prevent steering back uphill immediately after jumping off a diagonal slope
        if (slopeUphillLockTimer > 0f && (effectiveHorizontalInput * lastSlopeDownhillSign) < 0f)
        {
            effectiveHorizontalInput = 0f;
        }

        float targetSpeed = (effectiveHorizontalInput * currentMaxSpeed) + (onPlatform ? surfaceVelocity.x : 0f);

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
            float accelRate;
            bool hasInput = Mathf.Abs(effectiveHorizontalInput) > 0.01f;
            float currentVelX = rb.linearVelocity.x;

            if (!isGrounded)
            {
                bool isOverMaxSpeed = Mathf.Abs(currentVelX) > currentMaxSpeed + 0.1f;
                bool inputMatchesVelocity = hasInput && Mathf.Sign(effectiveHorizontalInput) == Mathf.Sign(currentVelX);
                bool inputOpposesVelocity = hasInput && Mathf.Sign(effectiveHorizontalInput) != Mathf.Sign(currentVelX);

                if (isOverMaxSpeed)
                {
                    if (inputMatchesVelocity || !hasInput)
                    {
                        // Preserve high horizontal speed in mid-air (e.g. slope jump momentum) with minimal air drag
                        targetSpeed = Mathf.Sign(currentVelX) * currentMaxSpeed;
                        accelRate = airOverSpeedDrag;
                    }
                    else
                    {
                        // Smooth turning in mid-air when steering against a high-speed launch
                        accelRate = airTurnAcceleration;
                    }
                }
                else
                {
                    accelRate = hasInput
                        ? (inputOpposesVelocity ? airTurnAcceleration : acceleration)
                        : airDeceleration;
                }
            }
            else if (isOnIce)
            {
                bool isTurning = hasInput &&
                                 Mathf.Abs(currentVelX) > 0.5f &&
                                 Mathf.Sign(effectiveHorizontalInput) != Mathf.Sign(currentVelX);

                accelRate = !hasInput ? iceDeceleration
                          : isTurning ? iceTurnAcceleration
                          : iceAcceleration;
            }
            else
            {
                accelRate = hasInput ? acceleration : deceleration;
            }

            float speedDifference = targetSpeed - currentVelX;
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

        bool hitLeft = SolidRaycast(headLeft.position, Vector2.up, headCheckDistance, groundLayer).collider != null;
        bool hitRight = SolidRaycast(headRight.position, Vector2.up, headCheckDistance, groundLayer).collider != null;

        if (hitLeft && !hitRight)
        {
            Vector2 checkPos = new Vector2(headLeft.position.x + maxCornerDistance, headLeft.position.y);
            if (SolidRaycast(checkPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
            {
                float exactOffset = FindExactCornerOffset(headLeft.position, 1f);
                rb.position += Vector2.right * exactOffset;
            }
        }
        else if (hitRight && !hitLeft)
        {
            Vector2 checkPos = new Vector2(headRight.position.x - maxCornerDistance, headRight.position.y);
            if (SolidRaycast(checkPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
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
            if (SolidRaycast(testPos, Vector2.up, headCheckDistance, groundLayer).collider == null)
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
        isOnSlope = false;
        currentSlopeZone = SlopeZone.None;
        currentSlopeSlideSpeed = 0f;
        lastActiveSlopeSpeed = 0f;
        slopeCoyoteCounter = 0f;
        slopeJumpCooldownTimer = 0f;
        slopeUphillLockTimer = 0f;
        wallCoyoteTimeCounter = 0f;
        wallJumpLockCounter = 0f;
        wasOnPlatform = false;
        lastGroundCollider = null;
        currentPlatform = null;
        lastSurfaceVelocity = Vector2.zero;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        ApplyColliderMaterial(originalPhysicsMaterial);
    }
}