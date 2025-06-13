using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    [Header("Animation Settings")]
    public float duration = 1f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Scale Settings")]
    public float maxScale = 2f;

    private SpriteRenderer spriteRenderer;
    private float timer = 0f;
    private Vector3 initialScale;
    private Color initialColor;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            initialScale = transform.localScale;
            initialColor = spriteRenderer.color;
        }

        // Auto-destroy after duration
        Destroy(gameObject, duration);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (spriteRenderer != null)
        {
            // Animate scale
            float scaleMultiplier = scaleCurve.Evaluate(progress) * maxScale;
            transform.localScale = initialScale * scaleMultiplier;

            // Animate alpha
            Color newColor = initialColor;
            newColor.a = initialColor.a * alphaCurve.Evaluate(progress);
            spriteRenderer.color = newColor;
        }
    }
}