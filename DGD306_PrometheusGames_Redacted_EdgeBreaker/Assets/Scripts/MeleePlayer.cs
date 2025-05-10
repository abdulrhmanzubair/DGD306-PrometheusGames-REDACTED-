using UnityEngine;

public class MeleePlayer : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 movement;

    [Header("Melee Attack")]
    public float attackRange = 1.2f;
    public float attackCooldown = 0.5f;
    public int attackDamage = 1;
    public LayerMask enemyLayers;
    public Transform attackPoint;
    private float attackTimer;

    [Header("Shield Ability")]
    public float shieldDuration = 2f;
    public float shieldCooldown = 5f;
    private float shieldTimer;
    private bool isShieldActive = false;

    [Header("Health")]
    public int maxHealth = 5;
    private int currentHealth;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        HandleMovement();
        HandleAttack();
        HandleShield();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = movement * moveSpeed;
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        movement = new Vector2(moveX, moveY).normalized;
    }

    void HandleAttack()
    {
        attackTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Z) && attackTimer <= 0f)
        {
            Attack();
            attackTimer = attackCooldown;
        }
    }

    void Attack()
    {
        // Detect enemies in range
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);
        foreach (Collider2D enemy in hits)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(attackDamage);
            }
        }
    }

    void HandleShield()
    {
        if (Input.GetKeyDown(KeyCode.X) && shieldTimer <= 0f && !isShieldActive)
        {
            ActivateShield();
        }

        if (isShieldActive)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f)
            {
                DeactivateShield();
            }
        }
    }

    void ActivateShield()
    {
        isShieldActive = true;
        shieldTimer = shieldDuration;
        // Optional: Add shield visual or sound here
    }

    void DeactivateShield()
    {
        isShieldActive = false;
        shieldTimer = shieldCooldown; // Start cooldown timer
        // Optional: Disable shield visual
    }

    public void TakeDamage(float damage)
    {
        if (isShieldActive) return;

        currentHealth -= Mathf.RoundToInt(damage);
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Placeholder death logic
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}
