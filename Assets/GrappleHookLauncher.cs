using UnityEngine;
using UnityEngine.InputSystem; // new Input System

public class GrappleHookLauncher : MonoBehaviour
{
    [Header("Hook Settings")]
    public GameObject hookPrefab;       // prefab with GrappleHookProjectile + LineRenderer
    public float hookSpeed = 25f;
    public float maxHookDistance = 20f;

    [Header("Debug")]
    public bool debugLogs = true;

    private GrappleHookProjectile activeHook;

    void Update()
    {
        // Make sure Game view has focus when you click
        if (Mouse.current == null)
        {
            if (debugLogs)
                Debug.LogWarning("[GrappleHookLauncher] Mouse.current is null. Check that the Input System is set up and you're in the Game view.");
            return;
        }

        // Left click: fire new hook
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

        // Use distance from camera to player so the world point is on the correct plane
        float zDist = Mathf.Abs(cam.transform.position.z - playerPos.y);
        Vector3 mouseWorld3 = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, zDist));
        Vector2 mouseWorld = mouseWorld3;

        Vector2 dir = mouseWorld - playerPos;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        dir.Normalize();

        if (debugLogs)
        {
            Debug.Log($"[GrappleHookLauncher] Firing hook. playerPos={playerPos}, mouseWorld={mouseWorld}, dir={dir}");
        }

        GameObject hookObj = Instantiate(hookPrefab, playerPos, Quaternion.identity);
        activeHook = hookObj.GetComponent<GrappleHookProjectile>();

        if (activeHook != null)
        {
            activeHook.Init(transform, dir, hookSpeed, maxHookDistance);
        }
        else
        {
            Debug.LogError("[GrappleHookLauncher] Spawned hookPrefab, but it has NO GrappleHookProjectile component!");
        }
    }

    // Called by hook when it dies / finishes
    public void ClearHook(GrappleHookProjectile hook)
    {
        if (activeHook == hook)
            activeHook = null;
    }
}

