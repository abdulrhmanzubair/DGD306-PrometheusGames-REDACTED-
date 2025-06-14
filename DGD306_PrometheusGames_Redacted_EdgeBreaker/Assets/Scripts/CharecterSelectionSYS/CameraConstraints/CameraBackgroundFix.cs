using UnityEngine;

/// <summary>
/// Simple fix: Add this to your CoopCamera to prevent it from following players
/// too close to background edges
/// </summary>
public class CameraBackgroundFix : MonoBehaviour
{
    [Header("Background Limits")]
    [Tooltip("Y position where your background ends (top)")]
    public float backgroundTopY = 12f;

    [Tooltip("Y position where your background ends (bottom)")]
    public float backgroundBottomY = -8f;

    [Header("Side Limits (Optional)")]
    public bool useSideLimits = false;
    public float backgroundLeftX = -25f;
    public float backgroundRightX = 25f;

    [Header("Debug")]
    public bool showLimits = true;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    // This runs AFTER the CoopCameraController moves the camera
    void LateUpdate()
    {
        if (cam == null) return;

        ConstrainCameraToBackground();
    }

    void ConstrainCameraToBackground()
    {
        Vector3 pos = transform.position;
        bool modified = false;

        // Get camera viewport size
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // Constrain vertically - prevent showing above/below background
        float maxY = backgroundTopY - halfHeight;
        float minY = backgroundBottomY + halfHeight;

        if (pos.y > maxY)
        {
            pos.y = maxY;
            modified = true;
        }
        else if (pos.y < minY)
        {
            pos.y = minY;
            modified = true;
        }

        // Constrain horizontally (optional)
        if (useSideLimits)
        {
            float maxX = backgroundRightX - halfWidth;
            float minX = backgroundLeftX + halfWidth;

            if (pos.x > maxX)
            {
                pos.x = maxX;
                modified = true;
            }
            else if (pos.x < minX)
            {
                pos.x = minX;
                modified = true;
            }
        }

        if (modified)
        {
            transform.position = pos;
        }
    }

    // Public methods to adjust limits
    public void SetBackgroundLimits(float topY, float bottomY)
    {
        backgroundTopY = topY;
        backgroundBottomY = bottomY;
    }

    public void SetSideLimits(float leftX, float rightX)
    {
        backgroundLeftX = leftX;
        backgroundRightX = rightX;
        useSideLimits = true;
    }

    void OnDrawGizmos()
    {
        if (!showLimits || cam == null) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // Draw background boundaries (red)
        Gizmos.color = Color.red;

        // Top boundary
        Gizmos.DrawLine(new Vector3(-100f, backgroundTopY, 0),
                       new Vector3(100f, backgroundTopY, 0));

        // Bottom boundary  
        Gizmos.DrawLine(new Vector3(-100f, backgroundBottomY, 0),
                       new Vector3(100f, backgroundBottomY, 0));

        // Camera limits (yellow)
        Gizmos.color = Color.yellow;

        // Where camera center can actually go
        float maxCameraY = backgroundTopY - halfHeight;
        float minCameraY = backgroundBottomY + halfHeight;

        Gizmos.DrawLine(new Vector3(-100f, maxCameraY, 0),
                       new Vector3(100f, maxCameraY, 0));

        Gizmos.DrawLine(new Vector3(-100f, minCameraY, 0),
                       new Vector3(100f, minCameraY, 0));

        // Side limits if enabled
        if (useSideLimits)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(backgroundLeftX, backgroundBottomY - 5f, 0),
                           new Vector3(backgroundLeftX, backgroundTopY + 5f, 0));

            Gizmos.DrawLine(new Vector3(backgroundRightX, backgroundBottomY - 5f, 0),
                           new Vector3(backgroundRightX, backgroundTopY + 5f, 0));

            // Camera X limits
            Gizmos.color = Color.yellow;
            float maxCameraX = backgroundRightX - halfWidth;
            float minCameraX = backgroundLeftX + halfWidth;

            Gizmos.DrawLine(new Vector3(maxCameraX, backgroundBottomY - 5f, 0),
                           new Vector3(maxCameraX, backgroundTopY + 5f, 0));

            Gizmos.DrawLine(new Vector3(minCameraX, backgroundBottomY - 5f, 0),
                           new Vector3(minCameraX, backgroundTopY + 5f, 0));
        }

        // Current camera view (green)
        Gizmos.color = Color.green;
        Vector3 pos = transform.position;
        Gizmos.DrawWireCube(pos, new Vector3(halfWidth * 2, halfHeight * 2, 0));

        // Labels
#if UNITY_EDITOR
        UnityEditor.Handles.Label(new Vector3(-95f, backgroundTopY + 0.5f, 0f), "Background Top");
        UnityEditor.Handles.Label(new Vector3(-95f, maxCameraY + 0.5f, 0f), "Camera Max Y");
#endif
    }
}