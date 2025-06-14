using UnityEngine;

/// <summary>
/// Add this component alongside CoopCameraController to limit camera movement
/// without using rigid bounds - more flexible and responsive to level design
/// </summary>
public class CameraConstraints : MonoBehaviour
{
    [Header("Movement Limits")]
    public bool constrainHorizontal = true;
    public bool constrainVertical = true;

    [Header("Constraint Settings")]
    public float leftLimit = -20f;
    public float rightLimit = 20f;
    public float bottomLimit = -10f;
    public float topLimit = 10f;

    [Header("Background Limits")]
    [Tooltip("Prevent camera from showing above background")]
    public bool useBackgroundTopLimit = true;
    [Tooltip("Y position where background ends (top edge)")]
    public float backgroundTopY = 10f;
    [Tooltip("Prevent camera from showing below background")]
    public bool useBackgroundBottomLimit = false;
    [Tooltip("Y position where background ends (bottom edge)")]
    public float backgroundBottomY = -10f;

    [Header("Auto Background Detection")]
    public bool autoDetectBackground = false;
    [Tooltip("Tag of your background GameObject")]
    public string backgroundTag = "Background";
    [Tooltip("Name of your background GameObject (if no tag)")]
    public string backgroundName = "Background";
    [Tooltip("Buffer to prevent showing edge pixels")]
    public float backgroundBuffer = 0.5f;

    [Header("Edge Behavior")]
    [Range(0f, 1f)]
    public float edgeSoftness = 0.1f;
    public bool allowTemporaryOvershoot = true;

    [Header("Auto-Detection")]
    public bool autoDetectLimits = false;
    public string groundTag = "Ground";
    public string boundaryTag = "Boundary";

    [Header("Debug")]
    public bool showConstraintLines = true;
    public bool showBackgroundLimits = true;

    private Camera cam;
    private CoopCameraController coopCamera;
    private bool backgroundLimitsDetected = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        coopCamera = GetComponent<CoopCameraController>();

        if (autoDetectBackground)
        {
            DetectBackgroundLimits();
        }

        if (autoDetectLimits)
        {
            DetectLevelLimits();
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;

        ApplyConstraints();
    }

    void ApplyConstraints()
    {
        Vector3 pos = transform.position;
        bool modified = false;

        // Get camera viewport size
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // Apply horizontal constraints
        if (constrainHorizontal)
        {
            float minX = leftLimit + halfWidth;
            float maxX = rightLimit - halfWidth;

            if (pos.x < minX)
            {
                pos.x = allowTemporaryOvershoot ?
                    Mathf.Lerp(pos.x, minX, edgeSoftness * 10f * Time.deltaTime) : minX;
                modified = true;
            }
            else if (pos.x > maxX)
            {
                pos.x = allowTemporaryOvershoot ?
                    Mathf.Lerp(pos.x, maxX, edgeSoftness * 10f * Time.deltaTime) : maxX;
                modified = true;
            }
        }

        // Apply vertical constraints (including background limits)
        if (constrainVertical)
        {
            float minY = bottomLimit + halfHeight;
            float maxY = topLimit - halfHeight;

            // Override with background limits if enabled
            if (useBackgroundTopLimit)
            {
                float backgroundMaxY = backgroundTopY - halfHeight;
                maxY = Mathf.Min(maxY, backgroundMaxY);
            }

            if (useBackgroundBottomLimit)
            {
                float backgroundMinY = backgroundBottomY + halfHeight;
                minY = Mathf.Max(minY, backgroundMinY);
            }

            if (pos.y < minY)
            {
                pos.y = allowTemporaryOvershoot ?
                    Mathf.Lerp(pos.y, minY, edgeSoftness * 10f * Time.deltaTime) : minY;
                modified = true;
            }
            else if (pos.y > maxY)
            {
                pos.y = allowTemporaryOvershoot ?
                    Mathf.Lerp(pos.y, maxY, edgeSoftness * 10f * Time.deltaTime) : maxY;
                modified = true;
            }
        }

        if (modified)
        {
            transform.position = pos;
        }
    }

    void DetectBackgroundLimits()
    {
        GameObject background = FindBackgroundObject();

        if (background == null)
        {
            Debug.LogWarning("CameraConstraints: Background object not found for auto-detection!");
            return;
        }

        // Get the sprite renderer bounds
        SpriteRenderer spriteRenderer = background.GetComponent<SpriteRenderer>();
        Renderer renderer = spriteRenderer != null ? spriteRenderer : background.GetComponent<Renderer>();

        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            backgroundTopY = bounds.max.y - backgroundBuffer;
            backgroundBottomY = bounds.min.y + backgroundBuffer;
            backgroundLimitsDetected = true;

            Debug.Log($"Background limits auto-detected - Top: {backgroundTopY}, Bottom: {backgroundBottomY}");
        }
        else
        {
            Debug.LogWarning("CameraConstraints: No renderer found on background object!");
        }
    }

    GameObject FindBackgroundObject()
    {
        // Try to find by tag first
        GameObject background = GameObject.FindGameObjectWithTag(backgroundTag);
        if (background != null)
        {
            Debug.Log($"Found background by tag: {backgroundTag}");
            return background;
        }

        // Try to find by name
        background = GameObject.Find(backgroundName);
        if (background != null)
        {
            Debug.Log($"Found background by name: {backgroundName}");
            return background;
        }

        // Try to find any object with "background" in the name (case insensitive)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("background"))
            {
                Debug.Log($"Found background by partial name match: {obj.name}");
                return obj;
            }
        }

        return null;
    }

    void DetectLevelLimits()
    {
        // Find all ground/boundary objects
        GameObject[] groundObjects = GameObject.FindGameObjectsWithTag(groundTag);
        GameObject[] boundaryObjects = GameObject.FindGameObjectsWithTag(boundaryTag);

        if (groundObjects.Length == 0 && boundaryObjects.Length == 0)
        {
            Debug.LogWarning("No ground or boundary objects found for auto-detection!");
            return;
        }

        // Calculate level bounds from all found objects
        Bounds levelBounds = new Bounds();
        bool boundsInitialized = false;

        foreach (GameObject obj in groundObjects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (!boundsInitialized)
                {
                    levelBounds = renderer.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    levelBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        foreach (GameObject obj in boundaryObjects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (!boundsInitialized)
                {
                    levelBounds = renderer.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    levelBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (boundsInitialized)
        {
            leftLimit = levelBounds.min.x;
            rightLimit = levelBounds.max.x;
            bottomLimit = levelBounds.min.y;
            topLimit = levelBounds.max.y;

            Debug.Log($"Auto-detected level limits: Left={leftLimit}, Right={rightLimit}, Bottom={bottomLimit}, Top={topLimit}");
        }
    }

    // Public methods to adjust constraints at runtime
    public void SetHorizontalLimits(float left, float right)
    {
        leftLimit = left;
        rightLimit = right;
        constrainHorizontal = true;
    }

    public void SetVerticalLimits(float bottom, float top)
    {
        bottomLimit = bottom;
        topLimit = top;
        constrainVertical = true;
    }

    public void SetBackgroundLimits(float topY, float bottomY = float.MinValue)
    {
        backgroundTopY = topY;
        useBackgroundTopLimit = true;

        if (bottomY != float.MinValue)
        {
            backgroundBottomY = bottomY;
            useBackgroundBottomLimit = true;
        }

        Debug.Log($"Background limits set - Top: {backgroundTopY}, Bottom: {backgroundBottomY}");
    }

    public void DisableHorizontalConstraints()
    {
        constrainHorizontal = false;
    }

    public void DisableVerticalConstraints()
    {
        constrainVertical = false;
    }

    public void DisableBackgroundLimits()
    {
        useBackgroundTopLimit = false;
        useBackgroundBottomLimit = false;
    }

    // Method to manually refresh background detection
    public void RefreshBackgroundDetection()
    {
        if (autoDetectBackground)
        {
            DetectBackgroundLimits();
        }
    }

    void OnDrawGizmos()
    {
        if (!showConstraintLines && !showBackgroundLimits) return;

        if (showConstraintLines)
        {
            // Draw regular constraint lines
            Gizmos.color = Color.red;

            if (constrainHorizontal)
            {
                // Left limit
                Gizmos.DrawLine(new Vector3(leftLimit, bottomLimit - 5f, 0),
                               new Vector3(leftLimit, topLimit + 5f, 0));

                // Right limit  
                Gizmos.DrawLine(new Vector3(rightLimit, bottomLimit - 5f, 0),
                               new Vector3(rightLimit, topLimit + 5f, 0));
            }

            if (constrainVertical)
            {
                // Bottom limit
                Gizmos.DrawLine(new Vector3(leftLimit - 5f, bottomLimit, 0),
                               new Vector3(rightLimit + 5f, bottomLimit, 0));

                // Top limit
                Gizmos.DrawLine(new Vector3(leftLimit - 5f, topLimit, 0),
                               new Vector3(rightLimit + 5f, topLimit, 0));
            }
        }

        // Draw background limits
        if (showBackgroundLimits)
        {
            if (useBackgroundTopLimit)
            {
                // Background top limit
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(new Vector3(-100f, backgroundTopY, 0),
                               new Vector3(100f, backgroundTopY, 0));

                // Effective camera limit (where camera center can go)
                if (cam != null)
                {
                    float effectiveTopLimit = backgroundTopY - cam.orthographicSize;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(new Vector3(-100f, effectiveTopLimit, 0),
                                   new Vector3(100f, effectiveTopLimit, 0));
                }
            }

            if (useBackgroundBottomLimit)
            {
                // Background bottom limit
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(new Vector3(-100f, backgroundBottomY, 0),
                               new Vector3(100f, backgroundBottomY, 0));

                // Effective camera limit
                if (cam != null)
                {
                    float effectiveBottomLimit = backgroundBottomY + cam.orthographicSize;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(new Vector3(-100f, effectiveBottomLimit, 0),
                                   new Vector3(100f, effectiveBottomLimit, 0));
                }
            }
        }

        // Draw current camera viewport
        if (cam != null)
        {
            Gizmos.color = Color.green;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 pos = transform.position;

            Gizmos.DrawWireCube(pos, new Vector3(halfWidth * 2, halfHeight * 2, 0));
        }

        // Draw labels in editor
#if UNITY_EDITOR
        if (showBackgroundLimits && useBackgroundTopLimit)
        {
            UnityEditor.Handles.Label(new Vector3(-95f, backgroundTopY + 0.5f, 0f), "Background Top");

            if (cam != null)
            {
                float effectiveLimit = backgroundTopY - cam.orthographicSize;
                UnityEditor.Handles.Label(new Vector3(-95f, effectiveLimit + 0.5f, 0f), "Camera Top Limit");
            }
        }
#endif
    }
}