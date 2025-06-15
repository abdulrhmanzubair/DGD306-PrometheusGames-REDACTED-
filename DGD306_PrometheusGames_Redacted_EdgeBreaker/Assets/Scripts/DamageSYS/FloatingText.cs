using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating text component for damage numbers and other floating UI text
/// </summary>
public class FloatingText : MonoBehaviour
{
    [Header("Movement Settings")]
    public float floatSpeed = 2f;
    public float lifetime = 1.5f;
    public Vector2 randomOffset = new Vector2(0.5f, 0.5f);

    [Header("Animation")]
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public float maxScale = 1.2f;

    [Header("Colors")]
    public Color damageColor = Color.red;
    public Color healColor = Color.green;
    public Color criticalColor = Color.yellow;

    private Text textComponent;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float timer = 0f;
    private Vector3 originalScale;
    private Color originalColor;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        textComponent = GetComponent<Text>();
        if (textComponent == null)
        {
            Debug.LogError("FloatingText requires a Text component!");
            Destroy(gameObject);
            return;
        }

        // Store original values
        startPosition = transform.position;
        originalScale = transform.localScale;
        originalColor = textComponent.color;

        // Calculate target position with random offset
        Vector2 randomDir = new Vector2(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(0f, randomOffset.y)
        );
        targetPosition = startPosition + (Vector3)randomDir + Vector3.up * floatSpeed;

        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (textComponent == null) return;

        timer += Time.deltaTime;
        float normalizedTime = timer / lifetime;

        // Move upward
        transform.position = Vector3.Lerp(startPosition, targetPosition, normalizedTime);

        // Scale animation
        float scaleMultiplier = scaleCurve.Evaluate(normalizedTime) * maxScale;
        transform.localScale = originalScale * scaleMultiplier;

        // Alpha animation
        float alpha = alphaCurve.Evaluate(normalizedTime);
        Color color = textComponent.color;
        color.a = alpha;
        textComponent.color = color;
    }

    public void SetText(string text)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
        }
    }

    public void SetDamageText(int damage, bool isCritical = false)
    {
        if (textComponent != null)
        {
            textComponent.text = $"-{damage}";
            textComponent.color = isCritical ? criticalColor : damageColor;

            if (isCritical)
            {
                // Make critical hits bigger and last longer
                maxScale = 1.5f;
                lifetime *= 1.3f;
            }
        }
    }

    public void SetHealText(int healAmount)
    {
        if (textComponent != null)
        {
            textComponent.text = $"+{healAmount}";
            textComponent.color = healColor;
        }
    }

    public void SetCustomText(string text, Color color)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
            textComponent.color = color;
        }
    }
}