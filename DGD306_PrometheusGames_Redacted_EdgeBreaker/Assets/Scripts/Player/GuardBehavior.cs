using UnityEngine;

public class GuardBehavior : MonoBehaviour
{
    private Player_Melee_Controller1 owner;
    private float lifetime = 5f;
    private float timer = 0f;

    public Vector3 followOffset = new Vector3(1f, 0.5f, 0f);
    public int maxHealth = 3;
    private int currentHealth;

    public void SetOwner(Player_Melee_Controller1 player)
    {
        owner = player;
    }

    public void SetLifetime(float duration)
    {
        lifetime = duration;
        timer = 0f;
    }

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (owner != null)
        {
            FollowPlayer();
        }

        if (timer >= lifetime)
        {
            BreakGuard();
        }
    }

    void FollowPlayer()
    {
        float direction = Mathf.Sign(owner.transform.localScale.x);
        Vector3 targetPos = owner.transform.position + new Vector3(followOffset.x * direction, followOffset.y, 0f);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("EnemyProjectile") || other.CompareTag("Enemy"))
        {
            TakeDamage(1); // You can adjust damage values
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            BreakGuard();
        }
    }

    void BreakGuard()
    {
        if (owner != null)
        {
            owner.OnGuardDestroyed(); // Notify player to start cooldown
        }

        Destroy(gameObject);
    }
}
