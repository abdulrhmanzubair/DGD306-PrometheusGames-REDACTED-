using UnityEngine;

/// <summary>
/// Temporary debugging script to help diagnose guard protection issues
/// Add this to your guard prefab temporarily to see what's happening
/// </summary>
public class GuardDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    public bool visualizeDetection = true;
    public Color detectionColor = Color.red;

    private Collider2D guardCollider;
    private GuardBehavior guardBehavior;

    void Start()
    {
        guardCollider = GetComponent<Collider2D>();
        guardBehavior = GetComponent<GuardBehavior>();

        if (enableDebugLogs)
        {
            Debug.Log("=== GUARD DEBUG INFO ===");
            Debug.Log($"Guard Collider: {guardCollider}");
            Debug.Log($"Is Trigger: {guardCollider?.isTrigger}");
            Debug.Log($"Guard Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
            Debug.Log($"Guard Behavior: {guardBehavior}");

            // Check what layers this collider can interact with
            Debug.Log("=== COLLISION MATRIX CHECK ===");
            for (int i = 0; i < 32; i++)
            {
                if (!Physics2D.GetIgnoreLayerCollision(gameObject.layer, i))
                {
                    Debug.Log($"Can collide with layer {i}: {LayerMask.LayerToName(i)}");
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (enableDebugLogs)
        {
            // Only log important collisions (projectiles and enemies)
            if (other.CompareTag("Projectiles") || other.CompareTag("EnemyProjectile") || other.CompareTag("Enemy"))
            {
                Debug.Log($"🛡️ GUARD TRIGGER ENTER: {other.name}");
                Debug.Log($"   - Tag: {other.tag}");
                Debug.Log($"   - Layer: {other.gameObject.layer} ({LayerMask.LayerToName(other.gameObject.layer)})");
                Debug.Log($"   - Is EnemyProjectile: {other.CompareTag("EnemyProjectile")}");
                Debug.Log($"   - Is Projectiles: {other.CompareTag("Projectiles")}");
                Debug.Log($"   - Is Enemy: {other.CompareTag("Enemy")}");

                // Check for Bullet component
                Bullet bullet = other.GetComponent<Bullet>();
                if (bullet != null)
                {
                    Debug.Log($"   - Bullet Damage: {bullet.damage}");
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"🛡️ GUARD TRIGGER EXIT: {other.name}");
        }
    }

    void Update()
    {
        if (visualizeDetection)
        {
            // Find nearby projectiles
            Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, 3f);

            foreach (Collider2D obj in nearbyObjects)
            {
                if (obj.CompareTag("EnemyProjectile"))
                {
                    Debug.DrawLine(transform.position, obj.transform.position, Color.red, 0.1f);
                }
              
                else if (obj.CompareTag("Player"))
                {
                    Debug.DrawLine(transform.position, obj.transform.position, Color.green, 0.1f);
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (visualizeDetection)
        {
            // Draw guard detection area
            Gizmos.color = new Color(detectionColor.r, detectionColor.g, detectionColor.b, 0.3f);

            if (guardCollider != null)
            {
                Gizmos.DrawCube(transform.position, guardCollider.bounds.size);
            }
            else
            {
                Gizmos.DrawSphere(transform.position, 1f);
            }

            // Draw protection radius
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }

    // Call this method to test guard collision manually
    [ContextMenu("Test Guard Collision")]
    void TestGuardCollision()
    {
        Debug.Log("=== TESTING GUARD COLLISION ===");

        // Find nearby projectiles manually
        GameObject[] projectiles = GameObject.FindGameObjectsWithTag("EnemyProjectile");
        Debug.Log($"Found {projectiles.Length} enemy projectiles in scene");

        foreach (GameObject proj in projectiles)
        {
            float distance = Vector2.Distance(transform.position, proj.transform.position);
            Debug.Log($"Projectile {proj.name} distance: {distance}");

            if (distance < 2f)
            {
                Debug.Log($"Projectile {proj.name} is close enough - should be blocked!");
            }
        }
    }
}