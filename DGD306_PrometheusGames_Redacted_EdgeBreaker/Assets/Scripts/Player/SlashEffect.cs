using UnityEngine;

public class SlashEffect : MonoBehaviour
{
    public float lifetime = 0.3f;
    public float damage = 20f;
    public string targetTag = "Enemy";

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Slash");
        }

        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }
    }
}
