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
    public LayerMask groundLayer; // we'll use this for walls too
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
    public float wallCheckOffset = 0.35f;    // how far to the side we check
    public float wallSlideSpeed = 2f;
    public bool isOnWall;
    public int wallSide;                     // -1 = left, 1 = right, 0 = none

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

    void Awake()
    {
        controls = new PlayerControls();

        // Movement axis
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled  += ctx => moveInput = Vector2.zero;

        // Jump press (buffer)
        controls.Player.Jump.performed += _ =>
        {
            jumpPressed = true;
            jumpBufferCounter = jumpBufferTime;
        };

        // Sprint (dash)
        controls.Player.Sprint.started  += _ => { sprintHeld = true;  TryStartDash(); };
        controls.Player.Sprint.canceled += _ =>  sprintHeld = false;
    }

    void OnEnable()  => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        jumpHeld = controls.Player.Jump.IsPressed();

        // --- Ground check ---
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // --- Refill on ground ---
        if (isGrounded)
        {
            availableAirDashes  = dashCount;
            extraJumpsRemaining = maxExtraJumps;
            dashReadyInAir      = true;
            dashCooldownTicks   = 0;
        }

        // --- WALL DETECTION (overlap box, super reliable) ---
        // we check BOTH sides, using the same groundLayer (because walls == ground in your scene)
        bool hitRight = Physics2D.OverlapBox(
            wallCheck.position + Vector3.right * wallCheckOffset,
            wallCheckSize,
            0f,
            groundLayer
        );
        bool hitLeft = Physics2D.OverlapBox(
            wallCheck.position + Vector3.left * wallCheckOffset,
            wallCheckSize,
            0f,
            groundLayer
        );

        bool touchingWall = !isGrounded && (hitRight || hitLeft);
        isOnWall = touchingWall;
        wallSide = hitRight ? 1 : (hitLeft ? -1 : 0);

        //wall debug
        if (debugWalls)
        {
            Debug.Log($"WALL touch={touchingWall} hitR={hitRight} hitL={hitLeft} side={wallSide} grounded={isGrounded}");
        }

        // --- Coyote & jump buffer ---
        if (isGrounded) coyoteTimeCounter = coyoteTime;
        else            coyoteTimeCounter = Mathf.Max(0f, coyoteTimeCounter - Time.deltaTime);

        jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (dashCooldownTicks > 0) dashCooldownTicks--;

        float vx = rb.velocity.x;
        float vy = rb.velocity.y;

        // ───────────────── DASH BRANCH ─────────────────
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
        // ───────────────── NORMAL BRANCH ─────────────────
        else
        {
            // base movement
            float speed = moveSpeed;
            if (sprintHeld && (allowAirSprint || isGrounded)) speed *= sprintMultiplier;
            vx = moveInput.x * speed;

            // are we pressing into wall this frame?
            bool pressingIntoWall =
                (wallSide == 1 && moveInput.x > 0.1f) ||
                (wallSide == -1 && moveInput.x < -0.1f);

            // --- WALL BEHAVIOR ---
            if (isOnWall)
            {
                // don't let us keep pushing into wall forever
                if (pressingIntoWall)
                    vx = 0;

                // force slide speed (since friction is 0)
                if (vy > -wallSlideSpeed)
                    vy = -wallSlideSpeed;
            }

            // --- JUMP LOGIC ---
            bool wantJump = jumpBufferCounter > 0f;

            if (wantJump)
            {
                // ground / coyote
                if (coyoteTimeCounter > 0f)
                {
                    vy = jumpForce;
                    jumpBufferCounter = 0f;
                    coyoteTimeCounter = 0f;
                }
                // air / double
                else if (allowDoubleJump && extraJumpsRemaining > 0)
                {
                    vy = jumpForce;
                    jumpBufferCounter = 0f;
                    extraJumpsRemaining--;
                }
                // wall jump
                else if (isOnWall)
                {
                    float wallJumpDir = -wallSide;
                    vx = wallJumpDir * moveSpeed * 1.2f;
                    vy = jumpForce;
                    jumpBufferCounter = 0f;
                }
            }

            // short hop
            if (vy > 0f && !jumpHeld)
                vy *= shortHopMultiplier;
        }

        rb.velocity = new Vector2(vx, vy);
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
            // draw the overlap boxes on both sides so you can see range
            Vector3 rightPos = wallCheck.position + Vector3.right * wallCheckOffset;
            Vector3 leftPos  = wallCheck.position + Vector3.left  * wallCheckOffset;
            Gizmos.DrawWireCube(rightPos, wallCheckSize);
            Gizmos.DrawWireCube(leftPos,  wallCheckSize);
        }
    }
}