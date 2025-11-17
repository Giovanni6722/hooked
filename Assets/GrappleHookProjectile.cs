// Assets/Scripts/GrappleHookProjectile.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrappleHookProjectile : MonoBehaviour
{
    [Header("Latching")]
    [Tooltip("Hook is allowed to latch onto anything exept the player and anything on the notGrapplableLayerMask.")]
    public LayerMask latchMask = ~0; // Default to everything
    public float maxDistance = 15f;
    private bool isLatched = false;
    private Transform player;
    private Vector2 direction;
    private float speed;
    private float distanceTravelled;

    private LineRenderer rope;
    private GrappleHookLauncher launcher;

    public void Init(Transform playerTransform, Vector2 dir, float hookSpeed, float maxDist)
    {
        player = playerTransform;
        direction = dir.normalized;
        speed = hookSpeed;
        maxDistance = maxDist;

        launcher = player.GetComponent<GrappleHookLauncher>();

        rope = GetComponent<LineRenderer>();
        if (rope == null) {rope = gameObject.AddComponent<LineRenderer>();}

        rope.useWorldSpace = true;
        rope.positionCount = 2;
        rope.startWidth = 0.08f;
        rope.endWidth   = 0.08f;

        // Make sure it's actually visible
        if (rope.material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            rope.material = new Material(shader);
        }
        rope.startColor = Color.white;
        rope.endColor   = Color.white;

        Debug.Log("[GrappleHookProjectile] Init called, hook spawned.");
    }

    void Update()
    {
        if (player == null)
        {
            DestroySelf();
            return;
        }

        if(!isLatched)
        {
            float step = speed * Time.deltaTime;
            Vector2 currentPos = transform.position;
            Vector2 targetPos  = currentPos + direction * step;

            transform.position = targetPos;
            distanceTravelled += step;

            if (distanceTravelled >= maxDistance)
            {
                Debug.Log("[GrappleHookProjectile] Max distance reached, destroying hook.");
                DestroySelf();
            }
        }

        // Update rope endpoints
        UpdateRope();

        // Debug line as backup visual
        Debug.DrawLine(player.position, transform.position, Color.yellow);
    }

    void UpdateRope()
    {
        if (rope == null || player == null) return;

        rope.SetPosition(0, player.position);
        rope.SetPosition(1, transform.position);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        LatchTarget(other);
    }

    void LatchTarget(Collider2D other)
    {
        if (isLatched) return;                                              // Already latched fuh
        if (other.isTrigger) return;                                        // Ignore triggers fuh
        if (other.transform == player) return;                              // Don't latch onto the player fuh
        if ((latchMask.value & (1 << other.gameObject.layer)) == 0) return; // Layer not allowed fuh
        isLatched = true;                                                   // Mark as latched fuh
        Vector2 contactPoint = other.ClosestPoint(transform.position);      // Get contact point fuh
        transform.position = contactPoint;                                  // Snap hook to contact point fuh

        Debug.Log($"[GrappleHookProjectile] Latched onto {other.name} at {contactPoint}."); // Log fuh
    }

    void DestroySelf()
    {
        if (launcher != null)
        {
            launcher.ClearHook(this);
        }
        Destroy(gameObject);
    }
}
