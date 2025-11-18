using UnityEngine;
using UnityEngine.InputSystem; // new Input System

public class GrappleHookLauncher : MonoBehaviour
{
    [Header("Hook Settings")]
    public GameObject hookPrefab;       // prefab with GrappleHookProjectile + LineRenderer
    public float hookSpeed = 25f;
    public float maxHookDistance = 20f;
    public float hookSpawnOffset = 0.4f;   // spawn slightly in front of player

    [Header("Debug")]
    public bool debugLogs = false;

    private GrappleHookProjectile activeHook;

    void Update()
    {
        // Make sure Game view has focus when you click
        if (Mouse.current == null) return;

        // Left click: fire hook
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (debugLogs) Debug.Log("[GrappleHookLauncher] Left click detected, trying to fire hook...");
            FireHook();
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

        // enforce only one hook at a time
        if (activeHook != null)
        {
            if (debugLogs) Debug.Log("[GrappleHookLauncher] Existing hook found, destroying it before spawning a new one.");
            Destroy(activeHook.gameObject);
            activeHook = null;
        }

        // Spawn hook slightly in front of the player so it doesn't overlap the player collider
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

    // Called by hook when it destroys itself (e.g. max distance, future release)
    public void ClearHook(GrappleHookProjectile hook)
    {
        if (activeHook == hook)
            activeHook = null;
    }
}


