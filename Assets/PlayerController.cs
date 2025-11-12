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
    public Transform wallCheck;
    public Vector2 wallCheckSize = new Vector2(0.2f, 0.9f);
    public float wallCheckOffset = 0.35f;
    public float wallCheckRadius = 0.35f;
    public float wallSlideSpeed = 2f;
    public bool isOnWall;
    public int wallSide; // -1 = left, 1 = right, 0 = none

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
    public float wallJumpHorizontalForce = 10f;
    public float wallJumpVerticalForce = 12f;
    public float wallRegrabDelay = 0.15f;
    public float wallJumpPushOff = 0.12f;

    [Header("Wall Jump Feel")]
    public float wallCoyoteTime = 0.12f;

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

    void Awake()
    {
        controls = new PlayerControls();

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled  += ctx => moveInput = Vector2.zero;

        controls.Player.Jump.performed += _ =>
        {
            jumpPressed = true;
            jumpBufferCounter = jumpBufferTime;
        };

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
    }

    void Update()
    {
        jumpHeld = controls.Player.Jump.IsPressed();

        // ground check
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

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
        }

        // wall detection
        Vector2 basePos = wallCheck.position;
        Vector2 rightPos = basePos + Vector2.right * wallCheckOffset;
        Vector2 leftPos  = basePos + Vector2.left  * wallCheckOffset;

        bool hitRight = Physics2D.OverlapCircle(rightPos, wallCheckRadius, groundLayer);
        bool hitLeft  = Physics2D.OverlapCircle(leftPos,  wallCheckRadius, groundLayer);

        bool touchingWall = !isGrounded && (hitRight || hitLeft);

        bool allowWall = wallRegrabTimer <= 0f;
        isOnWall = touchingWall && allowWall;
        wallSide = hitRight ? 1 : (hitLeft ? -1 : 0);

        if (isOnWall)
        {
            wallCoyoteCounter = wallCoyoteTime;
            lastWallSideForCoyote = wallSide;
        }
        else
        {
            wallCoyoteCounter = Mathf.Max(0f, wallCoyoteCounter - Time.deltaTime);
        }

        if (isOnWall && wallSide != 0 && wallSide != lastWallJumpSide)
        {
            lastWallJumpSide = 0;
        }

        if (isOnWall && !wasOnWallLastFrame)
        {
            wallAttachTimer = wallAttachGrace;
            wallClingTimer  = maxWallClingTime;
            isWallClinging  = false;
        }

        if (debugWalls)
        {
            Debug.Log($"WALL touch={touchingWall} side={wallSide} grounded={isGrounded} clingTimer={wallClingTimer} wallCoyote={wallCoyoteCounter}");
        }

        if (isGrounded) coyoteTimeCounter = coyoteTime;
        else            coyoteTimeCounter = Mathf.Max(0f, coyoteTimeCounter - Time.deltaTime);

        jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
        if (wallRegrabTimer > 0f)   wallRegrabTimer   -= Time.deltaTime;

        wasOnWallLastFrame = isOnWall;
    }

    void FixedUpdate()
    {
        if (dashCooldownTicks > 0) dashCooldownTicks--;

        float vx = rb.velocity.x;
        float vy = rb.velocity.y;

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
            float speed = moveSpeed;
            if (sprintHeld && (allowAirSprint || isGrounded)) speed *= sprintMultiplier;
            vx = moveInput.x * speed;

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

                if (pressingIntoWall && snapped)
                    vx = 0;

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
                        if (vy > -wallSlideSpeed)
                            vy = -wallSlideSpeed;
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
                }
                else if (isOnWall || wallCoyoteCounter > 0f)
                {
                    int jumpWallSide = isOnWall ? wallSide : lastWallSideForCoyote;
                    bool wallJumpAllowed = (jumpWallSide != 0) && (lastWallJumpSide != jumpWallSide);

                    if (wallJumpAllowed)
                    {
                        float dir = -jumpWallSide;

                        Vector2 pos = rb.position;
                        pos.x += dir * wallJumpPushOff;
                        rb.position = pos;

                        vx = dir * wallJumpHorizontalForce;
                        vy = wallJumpVerticalForce;
                        jumpBufferCounter = 0f;
                        isWallClinging = false;
                        wallAttachTimer = 0f;
                        wallRegrabTimer = wallRegrabDelay;
                        lastWallJumpSide = jumpWallSide;

                        isOnWall = false;
                        wasOnWallLastFrame = false;
                        wallCoyoteCounter = 0f;
                    }
                    else
                    {
                        if (allowDoubleJump && extraJumpsRemaining > 0)
                        {
                            vy = jumpForce;
                            jumpBufferCounter = 0f;
                            extraJumpsRemaining--;

                            // detach from wall when using double jump in wall context
                            isOnWall = false;
                            wasOnWallLastFrame = false;
                            wallRegrabTimer = wallRegrabDelay * 0.5f;
                            isWallClinging = false;
                        }
                    }
                }
                else if (allowDoubleJump && extraJumpsRemaining > 0)
                {
                    vy = jumpForce;
                    jumpBufferCounter = 0f;
                    extraJumpsRemaining--;
                }
            }

            if (vy > 0f && !jumpHeld)
                vy *= shortHopMultiplier;
        }

        rb.velocity = new Vector2(vx, vy);

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

        rb.velocity = new Vector2(dashSpeed * dashDirection, rb.velocity.y);

        if (dashInvincibility)
            StartCoroutine(DashIFrames());
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
        }
    }
}