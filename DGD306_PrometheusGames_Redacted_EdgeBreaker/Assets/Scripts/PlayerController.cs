using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public GameObject bullet2Prefab;
    public Transform firePoint;
    public float fireRate = 0.2f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private float nextFireTime;
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;

    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        HandleInput();
        CheckGrounded();
        HandleJumping();
        HandleShooting();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        Move();
        ApplyBetterJump();
    }

    void HandleInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float aimY = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(moveX, 0f).normalized;

        // Update aim direction
        Vector2 rawAim = new Vector2(moveX, aimY);
        if (rawAim != Vector2.zero)
            aimDirection = rawAim.normalized;

        // Flip sprite
        if (moveX != 0)
            transform.localScale = new Vector3(Mathf.Sign(moveX), 1, 1);
    }

    void Move()
    {
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }

    void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
    }

    void HandleJumping()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }
    void ApplyBetterJump()
    {
        if (rb.linearVelocity.y < 0)
        {
            // Faster fall
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            // Short hop if jump button released early
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }


    void HandleShooting()
    {
        if (Time.time >= nextFireTime)
        {
            if (Input.GetButton("Fire1"))
            {
                nextFireTime = Time.time + fireRate;
                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
                bullet.GetComponent<Bullet>().SetDirection(aimDirection);
            }
            else if (Input.GetButton("Fire2"))
            {
                nextFireTime = Time.time + fireRate;
                GameObject bullet2 = Instantiate(bullet2Prefab, firePoint.position, Quaternion.identity);
                bullet2.GetComponent<Bullet>().SetDirection(aimDirection);
            }
        }
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            float xVel = Mathf.Abs(rb.linearVelocity.x);
            float yVel = rb.linearVelocity.y;

            animator.SetFloat("xVelocity", xVel);
            animator.SetFloat("yVelocity", yVel);
            animator.SetBool("isGrounded", isGrounded);

            // Jumping logic: true if not grounded and upward velocity
            bool isJumping = !isGrounded && yVel > 0.1f;
            animator.SetBool("isJumping", isJumping);
        }
    }
}
