using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class GrappleHookLauncher : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Button used to fire / hold the grapple (e.g. left mouse).")]
    public InputActionReference fireAction;   // Button

    [Header("Hook")]
    [Tooltip("Prefab that has a GrappleHookProjectile on it.")]
    public GrappleHookProjectile hookPrefab;
    [Tooltip("Where the hook spawns from. If null, uses the player position.")]
    public Transform firePoint;
    [Tooltip("Initial speed of the hook projectile.")]
    public float hookSpeed = 30f;
    [Tooltip("Maximum distance the hook can travel before despawning.")]
    public float maxHookDistance = 20f;

    [Header("Leash")]
    [Tooltip("Minimum rope length so you are never nailed to a tiny point under your feet.")]
    public float minRopeLength = 2f;

    [Tooltip("Allow this fraction of slack beyond rope length before spring/bungee kicks in.")]
    [Range(0f, 0.5f)]
    public float slackFraction = 0.05f;

    [Header("Leash Bungee")]
    [Tooltip("Spring-like pull strength when the rope is past its max length in leash mode.")]
    public float leashSpringStrength = 80f;

    [Tooltip("How strongly sideways velocity is damped in leash mode.")]
    public float leashTangentialDamping = 6f;

    [Tooltip("Max speed the leash is allowed to pull the player with.")]
    public float leashMaxPullSpeed = 20f;

    [Header("Swing / Overhead detection")]
    [Tooltip("Anchor must be at least this much higher than the player to count as overhead (pendulum). Should usually match PlayerController.grappleSwingOverheadOffset.")]
    public float swingOverheadOffset = 0.25f;

    [Tooltip("Log useful debug info and draw gizmos.")]
    public bool debugGrapple = false;

    private Rigidbody2D rb;
    private GrappleHookProjectile activeHook;

    private bool isTetherActive = false;
    private Vector2 anchor;
    private float ropeLength;

    public bool IsTetherActive => isTetherActive;

    // Used by PlayerController to know if the rope is taut.
    public bool IsOverSlack { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        isTetherActive = false;
        activeHook = null;
        IsOverSlack = false;
    }

    void OnEnable()
    {
        if (fireAction != null)
        {
            fireAction.action.Enable();
            fireAction.action.performed += OnFirePerformed;
            fireAction.action.canceled  += OnFireCanceled;
        }
    }

    void OnDisable()
    {
        if (fireAction != null)
        {
            fireAction.action.performed -= OnFirePerformed;
            fireAction.action.canceled  -= OnFireCanceled;
            fireAction.action.Disable();
        }
    }

    // ---------------- Input ----------------

    void OnFirePerformed(InputAction.CallbackContext ctx)
    {
        // Don't spawn another if one is already out
        if (activeHook != null) return;
        ShootHook();
    }

    void OnFireCanceled(InputAction.CallbackContext ctx)
    {
        // Release / retract
        if (activeHook != null)
        {
            activeHook.DestroySelfPublic(); // calls ClearHook inside
        }
    }

    // ---------------- Firing / Hook spawn ----------------

    void ShootHook()
    {
        if (hookPrefab == null)
        {
            Debug.LogWarning("[GrappleHookLauncher] No hookPrefab assigned.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[GrappleHookLauncher] No Camera.main for aiming.");
            return;
        }

        // Aim from player to mouse
        Vector2 screenPos;
        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
        }
        else
        {
            // Fallback: aim straight right
            screenPos = cam.WorldToScreenPoint(transform.position + Vector3.right);
        }

        float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 worldMouse = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
        Vector2 dir = (Vector2)(worldMouse - transform.position);
        if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right;
        dir.Normalize();

        Vector2 spawnPos = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;

        GrappleHookProjectile hook = Instantiate(hookPrefab, spawnPos, Quaternion.identity);
        hook.Init(transform, dir, hookSpeed, maxHookDistance);
        activeHook = hook;

        if (debugGrapple)
            Debug.Log($"[GrappleHookLauncher] Hook spawned dir={dir}, speed={hookSpeed}");
    }

    // ---------------- Called by GrappleHookProjectile ----------------

    // Called when the projectile latches
    public void OnHookLatched(GrappleHookProjectile hook)
    {
        if (hook != activeHook) return;   // ignore stray hooks

        anchor = hook.transform.position;

        // Use distance, but enforce a minimum so you aren't locked in place
        ropeLength = Mathf.Max(Vector2.Distance(rb.position, anchor), minRopeLength);

        isTetherActive = true;
        IsOverSlack = false;

        if (debugGrapple)
            Debug.Log($"[GrappleHookLauncher] Latched at {anchor}, ropeLength={ropeLength:0.00}");
    }

    // Called when the projectile dies / is cleared
    public void ClearHook(GrappleHookProjectile hook)
    {
        if (hook != activeHook) return;

        activeHook = null;
        isTetherActive = false;
        IsOverSlack = false;

        if (debugGrapple)
            Debug.Log("[GrappleHookLauncher] Hook cleared (destroyed or released).");
    }

    // ---------------- Leash / swing enforcement ----------------

    void FixedUpdate()
    {
        // No hook → nothing to do
        if (!isTetherActive || activeHook == null)
        {
            IsOverSlack = false;
            return;
        }

        // Follow moving anchor (e.g., moving platform)
        anchor = activeHook.transform.position;

        Vector2 pos   = rb.position;
        Vector2 delta = pos - anchor;
        float dist    = delta.magnitude;
        if (dist <= 0f)
        {
            IsOverSlack = false;
            return;
        }

        Vector2 dirOut = delta / dist;

        // "Hard" rope length including a bit of slack before any constraint kicks in
        float slackLimit = ropeLength * (1f + slackFraction);
        float stretch    = dist - slackLimit;

        // If we haven't stretched past slackLimit, leash/swing do nothing
        if (stretch <= 0f)
        {
            IsOverSlack = false;
            return;
        }

        IsOverSlack = true;

        bool anchorOverhead = anchor.y > rb.position.y + swingOverheadOffset;
        Vector2 vel = rb.linearVelocity;

        if (anchorOverhead)
        {
            // -------- Pendulum / swing mode --------
            // Treat rope as basically rigid once taut.

            // Clamp player onto a circle of radius ropeLength.
            rb.position = anchor + dirOut * ropeLength;

            // Kill only outward radial velocity; keep tangential for a smooth swing.
            float vRad = Vector2.Dot(vel, dirOut);
            if (vRad > 0f)
            {
                vel -= vRad * dirOut;
            }

            rb.linearVelocity = vel;

            if (debugGrapple)
                Debug.DrawLine(anchor, rb.position, Color.cyan, 0.03f);
        }
        else
        {
            // -------- Leash / bungee mode --------
            // Here we DON'T teleport the player; we just apply forces
            // so it feels like a springy leash instead of a rubber band glitch.

            float vRad = Vector2.Dot(vel, dirOut);
            Vector2 vRadial = vRad * dirOut;
            Vector2 vTan    = vel - vRadial;

            // Spring force pulling back toward the anchor when stretched past slackLimit
            // F = -k * stretch along the rope.
            rb.AddForce(-dirOut * (leashSpringStrength * stretch), ForceMode2D.Force);

            // Tangential damping so you don't orbit around the anchor forever.
            rb.AddForce(-vTan * leashTangentialDamping, ForceMode2D.Force);

            // Optional: lightly resist further outward motion so it doesn't blow up.
            if (vRad > 0f)
            {
                rb.AddForce(-vRadial * 0.5f * leashTangentialDamping, ForceMode2D.Force);
            }

            // Clamp overall speed so the leash can't yeet the player.
            vel = rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed > leashMaxPullSpeed)
            {
                rb.linearVelocity = vel / speed * leashMaxPullSpeed;
            }

            if (debugGrapple)
                Debug.DrawLine(anchor, rb.position, Color.magenta, 0.03f);
        }
    }

    public Vector2 GetAnchorPosition()
    {
        return anchor;
    }
}

