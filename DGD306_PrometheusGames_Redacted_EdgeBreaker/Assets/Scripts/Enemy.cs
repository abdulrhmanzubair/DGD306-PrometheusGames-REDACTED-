using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    public float health = 50f;

    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage!");

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Play death animation, etc.
        Destroy(gameObject);
    }
}
