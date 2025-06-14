using UnityEngine;

/// <summary>
/// Add this to your CoopCamera to create "dead zones" where camera stops following players
/// when they get too close to background edges
/// </summary>
public class CameraDeadZone : MonoBehaviour
{
    [Header("Dead Zone Settings")]
    [Tooltip("Stop following player vertically when they get close to background top")]
    public bool useTopDeadZone = true;
    [Tooltip("Background top Y position")]
    public float backgroundTopY = 12f;
    [Tooltip("How close to background top before camera stops following")]
    public float topDeadZoneDistance = 3f;

    [Tooltip("Stop following player vertically when they get close to background bottom")]
    public bool useBottomDeadZone = false;
    [Tooltip("Background bottom Y position")]
    public float backgroundBottomY = -8f;
    [Tooltip("How close to background bottom before camera stops following")]
    public float bottomDeadZoneDistance = 2f;

    [Header("Side Dead Zones (Optional)")]
    public bool useSideDeadZones = false;
    public float leftBoundary = -25f;
    public float rightBoundary = 25f;
    public float sideDeadZoneDistance = 3f;

    [Header("Debug")]
    public bool showDeadZones = true;

    private CoopCameraController coopCamera;
    private Camera cam;

    void Start()
    {
        coopCamera = GetComponent<CoopCameraController>();
        cam = GetComponent<Camera>();

        if (coopCamera == null)
        {
            Debug.LogError("CameraDeadZone: No CoopCameraController found on this GameObject!");
        }
    }

    void LateUpdate()
    {
        if (coopCamera == null || cam == null) return;

        ApplyDeadZoneConstraints();
    }

    void ApplyDeadZoneConstraints()
    {
        Vector3 currentPos = transform.position;
        Vector3 constrainedPos = currentPos;

        // Get camera viewport size
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // Apply top dead zone
        if (useTopDeadZone)
        {
            // Calculate the maximum Y position for camera center to prevent showing above background
            float maxCameraY = backgroundTopY - halfHeight;

            // Create dead zone - stop following player when they get too close to top
            float deadZoneStartY = backgroundTopY - topDeadZoneDistance;

            // If camera would go into dead zone area, constrain it
            if (constrainedPos.y > maxCameraY)
            {
                constrainedPos.y = maxCameraY;
            }
        }

        // Apply bottom dead zone
        if (useBottomDeadZone)
        {
            float minCameraY = backgroundBottomY + halfHeight;
            float deadZoneStartY = backgroundBottomY + bottomDeadZoneDistance;

            if (constrainedPos.y < minCameraY)
            {
                constrainedPos.y = minCameraY;
            }
        }

        // Apply side dead zones
        if (useSideDeadZones)
        {
            float maxCameraX = rightBoundary - halfWidth;
            float minCameraX = leftBoundary + halfWidth;

            if (constrainedPos.x > maxCameraX)
            {
                constrainedPos.x = maxCameraX;
            }
            else if (constrainedPos.x < minCameraX)
            {
                constrainedPos.x = minCameraX;
            }
        }

        // Apply the constraints
        if (constrainedPos != currentPos)
        {
            transform.position = constrainedPos;
        }
    }

    // Public methods to adjust dead zones at runtime
    public void SetTopDeadZone(float backgroundTop, float deadZoneDistance)
    {
        backgroundTopY = backgroundTop;
        topDeadZoneDistance = deadZoneDistance;
        useTopDeadZone = true;
    }

    public void SetBottomDeadZone(float backgroundBottom, float deadZoneDistance)
    {
        backgroundBottomY = backgroundBottom;
        bottomDeadZoneDistance = deadZoneDistance;
        useBottomDeadZone = true;
    }

    public void DisableTopDeadZone()
    {
        useTopDeadZone = false;
    }

    public void DisableBottomDeadZone()
    {
        useBottomDeadZone = false;
    }

    void OnDrawGizmos()
    {
        if (!showDeadZones || cam == null) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // Draw background limits
        Gizmos.color = Color.red;

        if (useTopDeadZone)
        {
            // Background top line
            Gizmos.DrawLine(new Vector3(-100f, backgroundTopY, 0),
                           new Vector3(100f, backgroundTopY, 0));

            // Camera constraint line (where camera center stops)
            float maxCameraY = backgroundTopY - halfHeight;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(-100f, maxCameraY, 0),
                           new Vector3(100f, maxCameraY, 0));

            // Dead zone area
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Transparent red
            Vector3 deadZoneCenter = new Vector3(0f, backgroundTopY - (topDeadZoneDistance / 2f), 0f);
            Vector3 deadZoneSize = new Vector3(200f, topDeadZoneDistance, 0f);
            Gizmos.DrawCube(deadZoneCenter, deadZoneSize);
        }

        if (useBottomDeadZone)
        {
            // Background bottom line
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(-100f, backgroundBottomY, 0),
                           new Vector3(100f, backgroundBottomY, 0));

            // Camera constraint line
            float minCameraY = backgroundBottomY + halfHeight;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(-100f, minCameraY, 0),
                           new Vector3(100f, minCameraY, 0));

            // Dead zone area
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Vector3 deadZoneCenter = new Vector3(0f, backgroundBottomY + (bottomDeadZoneDistance / 2f), 0f);
            Vector3 deadZoneSize = new Vector3(200f, bottomDeadZoneDistance, 0f);
            Gizmos.DrawCube(deadZoneCenter, deadZoneSize);
        }

        // Draw current camera viewport
        Gizmos.color = Color.green;
        Vector3 pos = transform.position;
        Gizmos.DrawWireCube(pos, new Vector3(halfWidth * 2, halfHeight * 2, 0));

        // Labels
#if UNITY_EDITOR
        if (useTopDeadZone)
        {
            UnityEditor.Handles.Label(new Vector3(-95f, backgroundTopY + 0.5f, 0f), "Background Top");
            float maxCameraY = backgroundTopY - halfHeight;
            UnityEditor.Handles.Label(new Vector3(-95f, maxCameraY + 0.5f, 0f), "Camera Stops Here");
        }
#endif
    }
}