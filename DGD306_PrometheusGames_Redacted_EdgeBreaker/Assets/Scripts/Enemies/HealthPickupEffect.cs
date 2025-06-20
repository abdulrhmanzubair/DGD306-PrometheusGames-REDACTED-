using UnityEngine;
using System.Collections;

/// <summary>
/// Simple visual effect for health pickup collection
/// Creates floating text and particle effects
/// </summary>
public class HealthPickupEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    public float effectDuration = 2f;
    public float floatHeight = 2f;
    public float fadeSpeed = 2f;

    [Header("Components")]
    public ParticleSystem healParticles;
    public GameObject floatingTextPrefab;

    void Start()
    {
        StartCoroutine(PlayEffect());
    }

    IEnumerator PlayEffect()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * floatHeight;

        // Play particles
        if (healParticles != null)
        {
            healParticles.Play();
        }

        // Create floating text
        if (floatingTextPrefab != null)
        {
            GameObject floatingText = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);

            // Try to set text
            UnityEngine.UI.Text textComponent = floatingText.GetComponent<UnityEngine.UI.Text>();
            if (textComponent != null)
            {
                textComponent.text = "+HEALTH";
                textComponent.color = Color.green;
            }

            Destroy(floatingText, effectDuration);
        }

        // Float upward and fade
        float timer = 0f;
        while (timer < effectDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / effectDuration;

            // Move upward
            transform.position = Vector3.Lerp(startPos, endPos, progress);

            yield return null;
        }

        // Destroy effect
        Destroy(gameObject);
    }
}