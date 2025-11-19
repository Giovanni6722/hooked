using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(DistanceJoint2D))]
public class GrappleHookLauncher : MonoBehaviour
{
    [Header("Hook Settings")]
    public GameObject hookPrefab;       // prefab with GrappleHookProjectile + LineRenderer
    public float hookSpeed = 25f;
    public float maxHookDistance = 20f;
    public float hookSpawnOffset = 0.4f;   // spawn slightly in front of player

    [Header("Swing / Tether")]
    public float ropeMinLength = 1.5f;     // shortest allowed rope
    public float ropeMaxLength = 20f;      // safety max in case things go wild
    public float reelSpeed    = 8f;        // how fast W/S moves you along the rope

    [Header("Debug")]
    public bool debugLogs = false;

    private GrappleHookProjectile activeHook;
    private DistanceJoint2D playerJoint;
    private Rigidbody2D rb;
    private float currentRopeLength = 0f;
    private bool isTetherActive = false;
    private bool isGrounded = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        playerJoint = GetComponent<DistanceJoint2D>();
        if (playerJoint == null)
            playerJoint = gameObject.AddComponent<DistanceJoint2D>();

        // Configure like a rope
        playerJoint.autoConfigureDistance = false;
        playerJoint.enableCollision = true;
        playerJoint.maxDistanceOnly = false;
        playerJoint.enabled = false;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // Left click: fire hook
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (debugLogs) Debug.Log("[GrappleHookLauncher] Left click detected, trying to fire hook...");
            FireHook();
        }

        // While tethered and airborne: reel in/out
        if (isTetherActive && playerJoint.enabled && !isGrounded)
        {
            float reelInput = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) reelInput += 1f;
                if (Keyboard.current.sKey.isPressed) reelInput -= 1f;
            }

            if (Gamepad.current != null)
            {
                float stickY = Gamepad.current.leftStick.ReadValue().y;
                if (Mathf.Abs(stickY) > 0.1f)
                {
                    reelInput += stickY;
                }
            }

            if (Mathf.Abs(reelInput) > 0.01f)
            {
                currentRopeLength -= reelInput * reelSpeed * Time.deltaTime;
                currentRopeLength = Mathf.Clamp(currentRopeLength, ropeMinLength, ropeMaxLength);
                playerJoint.distance = currentRopeLength;
            }
        }
    }

    void FireHook()
    {
        if (hookPrefab == null)
        {
            Debug.LogError("[GrappleHookLauncher] hookPrefab is NOT assigned in the Inspector!");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[GrappleHookLauncher] No Camera.main found in the scene. Tag your main camera as MainCamera.");
            return;
        }

        Vector2 playerPos = transform.position;
        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        float zDist = Mathf.Abs(cam.transform.position.z - playerPos.y);
        Vector3 mouseWorld3 = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, zDist));
        Vector2 mouseWorld = mouseWorld3;

        Vector2 dir = mouseWorld - playerPos;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        dir.Normalize();

        // disable existing tether
        if (isTetherActive && playerJoint != null)
        {
            playerJoint.enabled = false;
            isTetherActive = false;
        }

        // only one hook at a time
        if (activeHook != null)
        {
            if (debugLogs) Debug.Log("[GrappleHookLauncher] Existing hook found, destroying it before spawning a new one.");
            Destroy(activeHook.gameObject);
            activeHook = null;
        }

        Vector2 spawnPos = playerPos + dir * hookSpawnOffset;

        GameObject hookObj = Instantiate(hookPrefab, spawnPos, Quaternion.identity);
        activeHook = hookObj.GetComponent<GrappleHookProjectile>();

        if (activeHook != null)
        {
            activeHook.Init(transform, dir, hookSpeed, maxHookDistance);
            if (debugLogs) Debug.Log("[GrappleHookLauncher] Hook spawned and initialized.");
        }
        else
        {
            Debug.LogError("[GrappleHookLauncher] Spawned hookPrefab, but it has NO GrappleHookProjectile component!");
        }
    }

    // called by hook when it latches
    public void OnHookLatched(GrappleHookProjectile hook)
    {
        if (hook == null) return;
        if (activeHook != hook)
            activeHook = hook;

        Vector2 playerPos = rb.position;
        Vector2 anchorPos = hook.transform.position;

        float dist = Vector2.Distance(playerPos, anchorPos);
        currentRopeLength = Mathf.Clamp(dist, ropeMinLength, ropeMaxLength);

        playerJoint.enabled = true;
        playerJoint.autoConfigureDistance = false;
        playerJoint.connectedBody = null;
        playerJoint.connectedAnchor = anchorPos;
        playerJoint.distance = currentRopeLength;

        isTetherActive = true;

        if (debugLogs)
        {
            Debug.Log($"[GrappleHookLauncher] Tether active. Anchor at {anchorPos}, initial rope length {currentRopeLength}");
        }
    }

    // called by hook when it destroys itself
    public void ClearHook(GrappleHookProjectile hook)
    {
        if (activeHook == hook)
            activeHook = null;

        if (isTetherActive)
        {
            isTetherActive = false;
            if (playerJoint != null)
                playerJoint.enabled = false;
        }
    }

    // from PlayerController
    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    public bool IsTetherActive => isTetherActive;

    public Vector2 GetAnchorPosition()
    {
        if (!isTetherActive) return rb.position;
        return playerJoint.connectedAnchor;
    }
}