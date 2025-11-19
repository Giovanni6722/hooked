using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public bool allowAirSprint = true;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public bool allowAirDash = true;
    public bool dashReadyInAir = true;
    public bool dashInvincibility = true;
    public float dashInvincibilityDuration = 0.2f;
    public int dashDirection = 0;
    public int dashCount = 1;
    public bool isDashing;
    public float dashTimer;
    public float dashCooldownTimer;
    public int availableAirDashes;
    public int lastMoveDir = 1;
    public bool isInvincible;

    [Header("Dash Cooldown (Frames)")]
    public int dashCooldownFrames = 3;
    public int dashCooldownTicks = 0;

    [Header("Jumping")]
    public float jumpForce = 12f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer; // used for both ground and walls
    public bool allowDoubleJump = true;
    public int maxExtraJumps = 1;
    public int extraJumpsRemaining = 0;

    [Header("Jump Feel")]
    public float coyoteTime = 0.2f;
    public float jumpBufferTime = 0.2f;
    [Range(0f, 1f)] public float shortHopMultiplier = 0.5f;

    [Header("Jump-Canceled Dash")]
    public bool cancelDashOnJump = true;
    [Range(0f, 2f)] public float dashMomentumCarryMultiplier = 1.0f;

    [Header("Wall Detection")]
    public Transform wallCheck;              // should be at about mid-height of player
    public Vector2 wallCheckSize = new Vector2(0.2f, 0.9f);
    public float wallCheckOffset = 0.35f;
    public float wallCheckRadius = 0.35f;
    public float wallSlideSpeed = 2f;
    public bool isOnWall;
    public int wallSide;                     // -1 = left, 1 = right, 0 = none

    [Header("Wall Cling / Climb")]
    public float wallAttachGrace = 0.15f;
    public float maxWallClingTime = 1.25f;
    public float wallClimbSpeed = 3f;
    public float wallClimbSpeedUp = 5f;
    public float wallClimbSpeedDown = 3f;
    public float wallClingDrainWhileClimbing = 2f;

    [Header("Gravity")]
    public float normalGravityScale = 3f;
    public float wallClingGravityScale = 0f;

    [Header("Wall Jump")]
    public float wallJumpHorizontalForce = 10f;   // used when enhanced momentum is disabled
    public float wallJumpVerticalForce = 12f;
    public float wallRegrabDelay = 0.18f;

    [Header("Wall Jump Feel")]
    public float wallCoyoteTime = 0.12f;

    [Header("Wall Jump Momentum (Base)")]
    public float wallJumpLockTime = 0.10f;        // no steering period after kick
    public float wallJumpRampTime = 0.35f;        // time to fade steering back in
    [Range(0f, 1f)] public float wallJumpDrag = 0.20f;      // baseline horizontal bleed during momentum
    [Range(0f, 2f)] public float wallJumpMaxControl = 1.0f; // scales steering at end of ramp
    public float wallJumpSteerLerp = 12f;         // steering responsiveness during ramp

    [Header("Wall Slide Tuning")]
    public float wallSlideSpeedNeutral = 1.6f;  // no horizontal input
    public float wallSlideSpeedInto    = 0.8f;  // holding toward the wall (slower)
    public float wallSlideSpeedAway    = 2.8f;  // holding away from the wall (faster)
    public float wallSlideAccel        = 30f;   // vertical approach rate toward target slide speed

    [Header("Enhanced Wall Jump Momentum")]
    public bool  useEnhancedWallJumpMomentum = true; // master switch
    public float wallJumpKickSpeedX = 12f;           // initial horizontal target speed (reached via short ramp)
    public float wallJumpKickSpeedY = 12f;           // initial vertical launch speed
    [Range(0f, 1f)] public float counterSteerSuppressionStart = 1.0f; // full suppression at start
    [Range(0f, 1f)] public float counterSteerSuppressionEnd   = 0.0f; // no suppression by end of ramp
    public float extraDragStart = 3.0f;              // strong early decay of horizontal speed
    public float extraDragEnd   = 0.8f;              // lighter late decay
    public float turnClampDegreesPerSec = 540f;      // max turn rate during ramp

    [Header("Wall Jump Kick Ramp")]
    public float wallKickAccel = 150f;               // horizontal acceleration applied briefly after kick
    public float wallKickAccelTime = 0.07f;          // duration of the brief acceleration window

    [Header("Momentum Reintroduction")]
    public float inputReturnDelay = 0.06f;           // extra delay (after lock) before input starts affecting opposite turn
    public float inputReturnAccel = 60f;             // max horizontal accel per second while reintroducing input
    [Range(0f,1f)] public float momentumFloorFraction = 0.35f; // minimum fraction of kick speed kept early
    public float momentumFloorTime = 0.10f;          // duration to hold momentum floor

    [Header("Wall Latch / Coyote (Approach)")]
    public float wallApproachCoyoteTime = 0.08f;     // allows brief no-contact latch when pressing into wall
    public float nearWallRadiusMultiplier = 1.25f;   // enlarges the check radius for “near wall” pre-grab

    [Header("Grapple Swing Tuning")]
    [Tooltip("Base tangential force while swinging when you hold left/right.")]
    public float grappleSwingBaseForce = 12f;

    [Tooltip("Extra scaling based on |horizontal input| (0 = constant push).")]
    public float grappleSwingInputScale = 1f;

    [Tooltip("How much horizontal input is needed before we apply any swing force.")]
    public float grappleSwingDeadzone = 0.1f;

    [Tooltip("Damping applied to tangential velocity when there is no swing input. Higher = settles faster.")]
    public float grappleSwingDamping = 3f;

    [Tooltip("Anchor must be at least this much higher than the player (in world units) to allow swing forces. Otherwise the rope acts like a leash.")]
    public float grappleSwingOverheadOffset = 0.25f;

    [Header("Debug")]
    public bool debugWalls = false;

    private Rigidbody2D rb;
    private PlayerControls controls;

    private Vector2 moveInput;
    private bool isGrounded;
    private bool jumpPressed;
    private bool sprintHeld;
    private bool jumpHeld;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private bool wasOnWallLastFrame;
    private float wallAttachTimer;
    private float wallClingTimer;
    private bool isWallClinging;
    private float wallRegrabTimer;

    private int lastWallJumpSide = 0;
    private float wallCoyoteCounter = 0f;
    private int lastWallSideForCoyote = 0;

    // momentum state for wall jump
    private float wallJumpControlTimer = 0f;
    private int wallJumpKickDir = 0;

    private float wallKickAccelTimer = 0f;
    private float wallKickTargetX = 0f;
    private float kickAbsXAtLaunch = 0f;

    private float inputReturnTimer = 0f;
    private float momentumFloorTimer = 0f;

    // maintains a short latch when approaching a wall to avoid contact flicker
    private float wallLatchTimer = 0f;              
    private bool wasOnWallEffective = false;        

    // one-time-per-wall refresh gate
    private bool wallRefreshGiven = false;          // true after granting resources on a wall side
    private int  wallRefreshSideGiven = 0;          // remembers which side granted the refresh

    // Grapple
    private GrappleHookLauncher grappleHookLauncher;

    void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled  += ctx => moveInput = Vector2.zero;
        controls.Player.Jump.performed += _ => { jumpPressed = true; jumpBufferCounter = jumpBufferTime; };
        controls.Player.Sprint.started  += _ => { sprintHeld = true;  TryStartDash(); };
        controls.Player.Sprint.canceled += _ =>  sprintHeld = false;
    }

    void OnEnable()  => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        normalGravityScale = rb.gravityScale;

        grappleHookLauncher = GetComponent<GrappleHookLauncher>();

        if (wallJumpLockTime   <= 0f) wallJumpLockTime   = 0.10f;
        if (wallJumpRampTime   <= 0f) wallJumpRampTime   = 0.35f;
        if (wallJumpSteerLerp  <= 0f) wallJumpSteerLerp  = 12f;
        if (wallJumpMaxControl <= 0f) wallJumpMaxControl = 1.0f;
        if (wallJumpDrag       <  0f) wallJumpDrag       = 0.20f;
        if (turnClampDegreesPerSec <= 0f) turnClampDegreesPerSec = 540f;
    }

    void Update()
    {
        jumpHeld = controls.Player.Jump.IsPressed();

        // ground check
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // let grapple know if we're grounded
        if (grappleHookLauncher != null)
        {
            grappleHookLauncher.SetGrounded(isGrounded);
        }

        // refill on ground
        if (isGrounded)
        {
            availableAirDashes  = dashCount;
            extraJumpsRemaining = maxExtraJumps;
            dashReadyInAir      = true;
            dashCooldownTicks   = 0;
            isWallClinging      = false;
            wallClingTimer      = maxWallClingTime;
            wallAttachTimer     = 0f;
            wallRegrabTimer     = 0f;
            lastWallJumpSide    = 0;
            wallCoyoteCounter   = 0f;
            lastWallSideForCoyote = 0;

            wallJumpControlTimer = 0f;
            wallJumpKickDir = 0;
            wallKickAccelTimer = 0f;
            wallKickTargetX = 0f;

            inputReturnTimer = 0f;
            momentumFloorTimer = 0f;
            kickAbsXAtLaunch = 0f;

            wallLatchTimer = 0f;
            wasOnWallEffective = false;

            // reset one-time wall refresh gating on ground
            wallRefreshGiven = false;
            wallRefreshSideGiven = 0;
        }

        // wall detection positions
        Vector2 basePos = wallCheck.position;
        Vector2 rightPos = basePos + Vector2.right * wallCheckOffset;
        Vector2 leftPos  = basePos + Vector2.left  * wallCheckOffset;

        // contact checks
        bool hitRight = Physics2D.OverlapCircle(rightPos, wallCheckRadius, groundLayer);
        bool hitLeft  = Physics2D.OverlapCircle(leftPos,  wallCheckRadius, groundLayer);

        // “near” checks (slightly larger) to allow approach coyote
        float nearR = wallCheckRadius * nearWallRadiusMultiplier;
        bool nearRight = Physics2D.OverlapCircle(rightPos, nearR, groundLayer);
        bool nearLeft  = Physics2D.OverlapCircle(leftPos,  nearR, groundLayer);

        bool touchingWall = !isGrounded && (hitRight || hitLeft);
        bool nearWall = !isGrounded && (nearRight || nearLeft);

        bool allowWall = wallRegrabTimer <= 0f;

        // input intent toward a side
        bool pressingRight = moveInput.x > 0.1f;
        bool pressingLeft  = moveInput.x < -0.1f;

        // refresh approach latch when either actually touching,
        // or near + pressing toward that side, and allowed to wall
        if (allowWall && (touchingWall ||
            (nearWall && ((nearRight && pressingRight) || (nearLeft && pressingLeft)))))
        {
            wallLatchTimer = wallApproachCoyoteTime; // refresh latch window
        }
        else
        {
            wallLatchTimer = Mathf.Max(0f, wallLatchTimer - Time.deltaTime);
        }

        // effective wall state considers touch OR active latch (pre-coyote)
        bool effectiveOnWall = allowWall && !isGrounded && (touchingWall || wallLatchTimer > 0f);

        // pick an effective side: prefer actual contact, else near side
        int effectiveSide = 0;
        if (hitRight) effectiveSide = 1;
        else if (hitLeft) effectiveSide = -1;
        else if (nearRight) effectiveSide = 1;
        else if (nearLeft)  effectiveSide = -1;

        isOnWall = effectiveOnWall;
        wallSide = effectiveSide;

        // one-time-per-wall refresh:
        if (isOnWall && wallSide != 0)
        {
            // allow a new grant if switching sides
            if (wallRefreshSideGiven != wallSide)
            {
                wallRefreshGiven = false;
            }

            if (!wallRefreshGiven)
            {
                // start grace/cling
                wallAttachTimer = wallAttachGrace;
                wallClingTimer  = maxWallClingTime;
                isWallClinging  = false;

                // grant airtime resources once for this wall side
                availableAirDashes  = dashCount;
                extraJumpsRemaining = maxExtraJumps;
                dashReadyInAir      = true;
                dashCooldownTicks   = 0;

                wallRefreshGiven = true;
                wallRefreshSideGiven = wallSide;
            }
        }

        wasOnWallEffective = effectiveOnWall;
        wasOnWallLastFrame = isOnWall;

        if (debugWalls)
        {
            Debug.Log($"WALL eff={effectiveOnWall} side={wallSide} grounded={isGrounded} latch={wallLatchTimer:0.000} granted={wallRefreshGiven} grantedSide={wallRefreshSideGiven}");
        }

        if (isGrounded) coyoteTimeCounter = coyoteTime;
        else            coyoteTimeCounter = Mathf.Max(0f, coyoteTimeCounter - Time.deltaTime);

        jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
        if (wallRegrabTimer > 0f)   wallRegrabTimer   -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (dashCooldownTicks > 0) dashCooldownTicks--;

        float vx = rb.linearVelocity.x;
        float vy = rb.linearVelocity.y;

        // ---------- Tether / Swing Logic ----------
        bool tetherActive = grappleHookLauncher != null && grappleHookLauncher.IsTetherActive;
        Vector2 anchorPos = Vector2.zero;
        bool anchorOverhead = false;

        if (tetherActive)
        {
            anchorPos = grappleHookLauncher.GetAnchorPosition();
            // anchor must be meaningfully above the player to allow swing pumping
            anchorOverhead = anchorPos.y > rb.position.y + grappleSwingOverheadOffset;
        }

        bool tetheredInAir = tetherActive && !isGrounded && anchorOverhead;

        if (tetheredInAir)
        {
            // Let gravity + joint do the main motion.
            // We only add a tangential "pump" based on left/right input.
            float h = moveInput.x;
            Vector2 r = (Vector2)rb.position - anchorPos;
            float rMag = r.magnitude;

            if (rMag > 0.001f)
            {
                Vector2 Rhat = r / rMag;
                Vector2 That = new Vector2(-Rhat.y, Rhat.x);

                if (Mathf.Abs(h) > grappleSwingDeadzone)
                {
                    // scale force smoothly from deadzone -> full input
                    float absH = Mathf.Abs(h);
                    float t = Mathf.InverseLerp(grappleSwingDeadzone, 1f, absH); // 0..1
                    float forceMag = grappleSwingBaseForce * (t + t * grappleSwingInputScale);

                    rb.AddForce(That * Mathf.Sign(h) * forceMag, ForceMode2D.Force);
                }
                else
                {
                    // no input: damp tangential velocity so you can settle into a dead hang
                    float vTan = Vector2.Dot(rb.linearVelocity, That);
                    float damp = grappleSwingDamping * Time.fixedDeltaTime;
                    float factor = Mathf.Clamp01(1f - damp);
                    float newVTan = vTan * factor;
                    rb.linearVelocity += (newVTan - vTan) * That;
                }
            }

            rb.gravityScale = normalGravityScale;
            return; // skip normal ground/air movement while swinging
        }
        // NOTE: if tetherActive but !anchorOverhead, the rope behaves like a leash:
        // normal movement below, DistanceJoint2D just limits how far you can get.

        // ---------- Normal Movement / Dash / Walls ----------
        if (isDashing)
        {
            float dashVx = dashSpeed * (dashDirection >= 0 ? 1 : -1);

            bool wantJump   = jumpBufferCounter > 0f;
            bool canAirJump = !isGrounded && allowDoubleJump && extraJumpsRemaining > 0;

            if (cancelDashOnJump && wantJump && (isGrounded || canAirJump))
            {
                isDashing = false;
                dashTimer = 0f;
                jumpBufferCounter = 0f;
                if (!isGrounded) extraJumpsRemaining--;

                float carriedVx = Mathf.Abs(dashVx) * dashMomentumCarryMultiplier;
                vx = Mathf.Sign(dashVx) * Mathf.Max(Mathf.Abs(vx), carriedVx);
                vy = jumpForce;
            }
            else
            {
                vx = dashVx;
                dashTimer -= Time.fixedDeltaTime;
                if (dashTimer <= 0f) isDashing = false;
            }
        }
        else
        {
            // avoid zeroing vx during momentum; preserve current velocity and let the momentum gate adjust it
            if (wallJumpControlTimer <= 0f)
            {
                float speed = moveSpeed;
                if (sprintHeld && (allowAirSprint || isGrounded)) speed *= sprintMultiplier;
                vx = moveInput.x * speed;
            }
            else
            {
                vx = rb.linearVelocity.x; // keeps carried momentum before the momentum gate runs
            }

            bool pressingIntoWall = (wallSide == 1 && moveInput.x > 0.1f) || (wallSide == -1 && moveInput.x < -0.1f);

            if (isOnWall)
            {
                bool snapped = false;
                {
                    Vector2 origin = rb.position;
                    Vector2 dir = (wallSide == 1) ? Vector2.right : Vector2.left;
                    float snapDist = 0.25f;
                    RaycastHit2D hit = Physics2D.Raycast(origin, dir, snapDist, groundLayer);
                    if (hit.collider != null)
                    {
                        float newX = hit.point.x - (dir.x * 0.01f);
                        rb.position = new Vector2(newX, rb.position.y);
                        snapped = true;
                    }
                }

                if (pressingIntoWall && snapped) vx = 0;

                bool canCling = sprintHeld && wallClingTimer > 0f;

                if (canCling)
                {
                    isWallClinging = true;
                    vx = 0f;

                    float verticalInput = moveInput.y;

                    if (verticalInput > 0.1f)
                    {
                        vy = wallClimbSpeedUp;
                        wallClingTimer -= Time.fixedDeltaTime * wallClingDrainWhileClimbing;
                    }
                    else if (verticalInput < -0.1f)
                    {
                        vy = -wallClimbSpeedDown;
                        wallClingTimer -= Time.fixedDeltaTime * wallClingDrainWhileClimbing;
                    }
                    else
                    {
                        vy = 0f;
                        wallClingTimer -= Time.fixedDeltaTime;
                    }
                }
                else
                {
                    isWallClinging = false;

                    if (wallAttachTimer > 0f)
                    {
                        wallAttachTimer -= Time.fixedDeltaTime;
                    }
                    else
                    {
                        // CONSISTENT WALL SLIDE (gravity-agnostic)
                        float dot = moveInput.x * wallSide;

                        float targetSpeed;
                        if      (dot >  0.1f) targetSpeed = wallSlideSpeedInto;
                        else if (dot < -0.1f) targetSpeed = wallSlideSpeedAway;
                        else                  targetSpeed = wallSlideSpeedNeutral;

                        float targetVy = -Mathf.Abs(targetSpeed);

                        if (vy < targetVy)
                        {
                            vy = targetVy; // clamp if falling faster than allowed
                        }
                        else
                        {
                            vy = Mathf.MoveTowards(vy, targetVy, wallSlideAccel * Time.fixedDeltaTime);
                        }
                    }
                }
            }

            bool wantJump = jumpBufferCounter > 0f;

            if (wantJump)
            {
                if (coyoteTimeCounter > 0f)
                {
                    vy = jumpForce;
                    jumpBufferCounter = 0f;
                    coyoteTimeCounter = 0f;

                    wallJumpControlTimer = 0f;
                    wallJumpKickDir = 0;
                    wallKickAccelTimer = 0f;
                    wallKickTargetX = 0f;
                    inputReturnTimer = 0f;
                    momentumFloorTimer = 0f;
                    kickAbsXAtLaunch = 0f;
                }
                else if (isOnWall || wallCoyoteCounter > 0f)
                {
                    int jumpWallSide = isOnWall ? wallSide : lastWallSideForCoyote;
                    bool wallJumpAllowed = (jumpWallSide != 0) && (lastWallJumpSide != jumpWallSide);

                    if (wallJumpAllowed)
                    {
                        float dir = -jumpWallSide;

                        if (useEnhancedWallJumpMomentum)
                        {
                            vy = wallJumpKickSpeedY;
                            wallJumpKickDir    = (dir >= 0f) ? 1 : -1;
                            wallKickAccelTimer = wallKickAccelTime;
                            wallKickTargetX    = wallJumpKickSpeedX * wallJumpKickDir;

                            momentumFloorTimer = momentumFloorTime;
                            kickAbsXAtLaunch   = Mathf.Abs(wallKickTargetX);
                            inputReturnTimer   = inputReturnDelay;
                        }
                        else
                        {
                            vx = dir * wallJumpHorizontalForce;
                            vy = wallJumpVerticalForce;
                        }

                        jumpBufferCounter = 0f;
                        isWallClinging = false;
                        wallAttachTimer = 0f;
                        wallRegrabTimer = wallRegrabDelay;
                        lastWallJumpSide = jumpWallSide;

                        isOnWall = false;
                        wasOnWallLastFrame = false;
                        wallCoyoteCounter = 0f;

                        wallJumpControlTimer = wallJumpLockTime + wallJumpRampTime;
                    }
                    else
                    {
                        if (allowDoubleJump && extraJumpsRemaining > 0)
                        {
                            vy = jumpForce;
                            jumpBufferCounter = 0f;
                            extraJumpsRemaining--;

                            isOnWall = false;
                            wasOnWallLastFrame = false;
                            wallRegrabTimer = wallRegrabDelay * 0.5f;
                            isWallClinging = false;

                            wallJumpControlTimer = 0f;
                            wallJumpKickDir = 0;
                            wallKickAccelTimer = 0f;
                            wallKickTargetX = 0f;
                            inputReturnTimer = 0f;
                            momentumFloorTimer = 0f;
                            kickAbsXAtLaunch = 0f;
                        }
                    }
                }
                else if (allowDoubleJump && extraJumpsRemaining > 0)
                {
                    vy = jumpForce;
                    jumpBufferCounter = 0f;
                    extraJumpsRemaining--;

                    wallJumpControlTimer = 0f;
                    wallJumpKickDir = 0;
                    wallKickAccelTimer = 0f;
                    wallKickTargetX = 0f;
                    inputReturnTimer = 0f;
                    momentumFloorTimer = 0f;
                    kickAbsXAtLaunch = 0f;
                }
            }

            // ENHANCED MOMENTUM STEERING GATE (direction-aware)
            if (wallJumpControlTimer > 0f)
            {
                wallJumpControlTimer -= Time.fixedDeltaTime;

                float total   = wallJumpLockTime + wallJumpRampTime;
                float elapsed = total - wallJumpControlTimer;

                int inputSign = (Mathf.Abs(moveInput.x) > 0.01f) ? (moveInput.x > 0f ? 1 : -1) : 0;
                bool holdingAway   = (inputSign != 0) && (inputSign ==  wallJumpKickDir);
                bool holdingToward = (inputSign != 0) && (inputSign == -wallJumpKickDir);

                if (useEnhancedWallJumpMomentum && wallKickAccelTimer > 0f && wallJumpKickDir != 0)
                {
                    wallKickAccelTimer -= Time.fixedDeltaTime;
                    vx = Mathf.MoveTowards(vx, wallKickTargetX, wallKickAccel * Time.fixedDeltaTime);
                }

                if (momentumFloorTimer > 0f && wallJumpKickDir != 0)
                {
                    momentumFloorTimer -= Time.fixedDeltaTime;
                    float floorMag    = kickAbsXAtLaunch * momentumFloorFraction;
                    float signedFloor = floorMag * wallJumpKickDir;
                    if (Mathf.Abs(vx) < floorMag) vx = signedFloor;
                }

                if (holdingAway)
                {
                    float desired = moveInput.x * moveSpeed;
                    float maxAccel = inputReturnAccel * Time.fixedDeltaTime;
                    float delta = Mathf.Clamp(desired - vx, -maxAccel, maxAccel);
                    vx += delta;

                    float dragDelay = 0.05f; // prevents early tug-of-war
                    if (wallKickAccelTimer <= 0f && elapsed >= wallJumpLockTime + dragDelay)
                    {
                        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, total));
                        float extraDrag = Mathf.Lerp(extraDragStart, extraDragEnd, t);
                        float totalDrag = wallJumpDrag + extraDrag;
                        vx = Mathf.Lerp(vx, 0f, totalDrag * Time.fixedDeltaTime);
                    }

                    if (elapsed >= wallJumpLockTime * 0.5f)
                        wallJumpControlTimer = Mathf.Min(wallJumpControlTimer, wallJumpRampTime * 0.5f);
                }
                else
                {
                    float control;
                    if (elapsed <= wallJumpLockTime) control = 0f;
                    else control = Mathf.Clamp01((elapsed - wallJumpLockTime) / Mathf.Max(0.0001f, wallJumpRampTime));
                    control *= wallJumpMaxControl;

                    if (wallKickAccelTimer <= 0f)
                    {
                        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, total));
                        float extraDrag = Mathf.Lerp(extraDragStart, extraDragEnd, t);
                        float totalDrag = wallJumpDrag + extraDrag;
                        vx = Mathf.Lerp(vx, 0f, totalDrag * Time.fixedDeltaTime);
                    }

                    float desired = moveInput.x * moveSpeed;

                    if (inputReturnTimer > 0f)
                    {
                        inputReturnTimer -= Time.fixedDeltaTime;
                        bool aligning =
                            (Mathf.Sign(desired) == 0) ||
                            (Mathf.Sign(desired) == Mathf.Sign(vx)) ||
                            (Mathf.Sign(desired) == wallJumpKickDir);
                        if (!aligning) desired = 0f;
                    }

                    float suppress = Mathf.Lerp(
                        counterSteerSuppressionStart,
                        counterSteerSuppressionEnd,
                        Mathf.Clamp01((elapsed - wallJumpLockTime) / Mathf.Max(0.0001f, wallJumpRampTime))
                    );
                    bool opposite = (Mathf.Sign(desired) != 0) && (Mathf.Sign(desired) == -wallJumpKickDir);
                    if (opposite) desired *= (1f - suppress);

                    float steerBlend = control * wallJumpSteerLerp * Time.fixedDeltaTime;
                    float steered    = Mathf.Lerp(vx, desired, steerBlend);

                    float maxAccel = inputReturnAccel * Time.fixedDeltaTime;
                    float deltaSteer = Mathf.Clamp(steered - vx, -maxAccel, maxAccel);
                    vx += deltaSteer;
                }

                if (isGrounded)
                {
                    wallJumpControlTimer = 0f;
                    wallJumpKickDir = 0;
                    wallKickAccelTimer = 0f;
                    wallKickTargetX = 0f;
                    inputReturnTimer = 0f;
                    momentumFloorTimer = 0f;
                    kickAbsXAtLaunch = 0f;
                }
            }

            if (vy > 0f && !jumpHeld) vy *= shortHopMultiplier;
        }

        rb.linearVelocity = new Vector2(vx, vy);
        rb.gravityScale = isWallClinging ? wallClingGravityScale : normalGravityScale;
    }

    private void TryStartDash()
    {
        if (dashCooldownTicks > 0) return;

        bool groundDash = isGrounded;
        bool canAirDash = !isGrounded && allowAirDash && (availableAirDashes > 0);
        if (!(groundDash || canAirDash)) return;

        StartDash(consumeAir: !isGrounded);
    }

    private void StartDash(bool consumeAir)
    {
        int inputDir = 0;
        if (moveInput.x > 0.1f)       inputDir = 1;
        else if (moveInput.x < -0.1f) inputDir = -1;

        dashDirection = (inputDir != 0) ? inputDir : (lastMoveDir == 0 ? 1 : lastMoveDir);
        lastMoveDir   = dashDirection;

        isDashing         = true;
        dashTimer         = dashDuration;
        dashCooldownTicks = dashCooldownFrames;

        if (consumeAir)
        {
            if (availableAirDashes > 0)      availableAirDashes--;
            else if (dashReadyInAir)         dashReadyInAir = false;
            else { isDashing = false; return; }
        }

        rb.linearVelocity = new Vector2(dashSpeed * dashDirection, rb.linearVelocity.y);

        if (dashInvincibility) StartCoroutine(DashIFrames());
    }

    private System.Collections.IEnumerator DashIFrames()
    {
        isInvincible = true;
        yield return new WaitForSeconds(dashInvincibilityDuration);
        isInvincible = false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (wallCheck)
        {
            Gizmos.color = Color.blue;
            Vector3 rightPos = wallCheck.position + Vector3.right * wallCheckOffset;
            Vector3 leftPos  = wallCheck.position + Vector3.left  * wallCheckOffset;
            Gizmos.DrawWireCube(rightPos, wallCheckSize);
            Gizmos.DrawWireCube(leftPos,  wallCheckSize);
            Gizmos.DrawWireSphere(rightPos, wallCheckRadius);
            Gizmos.DrawWireSphere(leftPos,  wallCheckRadius);

            float nearR = wallCheckRadius * nearWallRadiusMultiplier;
            Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);
            Gizmos.DrawWireSphere(rightPos, nearR);
            Gizmos.DrawWireSphere(leftPos,  nearR);
        }
    }
}
