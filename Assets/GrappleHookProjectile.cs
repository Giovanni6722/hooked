// Assets/Scripts/GrappleHookProjectile.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrappleHookProjectile : MonoBehaviour
{
    private Transform player;
    private Vector2 direction;
    private float speed;
    private float maxDistance;
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
        if (rope == null)
        {
            rope = gameObject.AddComponent<LineRenderer>();
        }

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

        // Move forward
        float step = speed * Time.deltaTime;
        Vector2 currentPos = transform.position;
        Vector2 targetPos  = currentPos + direction * step;

        transform.position = targetPos;
        distanceTravelled += step;

        // Update rope endpoints
        UpdateRope();

        // Debug line as backup visual
        Debug.DrawLine(player.position, transform.position, Color.yellow);

        if (distanceTravelled >= maxDistance)
        {
            Debug.Log("[GrappleHookProjectile] Max distance reached, destroying hook.");
            DestroySelf();
        }
    }

    void UpdateRope()
    {
        if (rope == null || player == null) return;

        rope.SetPosition(0, player.position);
        rope.SetPosition(1, transform.position);
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
