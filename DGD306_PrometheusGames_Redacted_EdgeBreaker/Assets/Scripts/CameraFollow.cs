using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Rigidbody2D targetRb;       // Reference to the player's Rigidbody2D
    public Vector3 offset = new Vector3(0f, 1.5f, -10f);
    public float smoothSpeed = 5f;

    [Header("Bounds (Optional)")]
    public bool useBounds = false;
    public Vector2 minBounds;
    public Vector2 maxBounds;

    void LateUpdate()
    {
        if (!targetRb) return;

        Vector3 desiredPosition = (Vector3)targetRb.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (useBounds)
        {
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minBounds.x, maxBounds.x);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minBounds.y, maxBounds.y);
        }

        transform.position = smoothedPosition;
    }
}
