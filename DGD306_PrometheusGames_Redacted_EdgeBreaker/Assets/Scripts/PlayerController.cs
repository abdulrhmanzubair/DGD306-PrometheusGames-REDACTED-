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
    private Vector2 aimDirection = Vector2.right; // default right

    void Awake()
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
    }

    void HandleInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float aimY = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(moveX, 0).normalized;

        // Update aim direction based on input
        Vector2 rawAim = new Vector2(moveX, aimY);
        if (rawAim != Vector2.zero)
        {
            aimDirection = rawAim.normalized;
        }

        // Flip sprite based on movement
        if (moveX != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(moveX), 1, 1);
        }
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

    void HandleShooting()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            bullet.GetComponent<Bullet>().SetDirection(aimDirection);
        }

        if (Input.GetButton("Fire2") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;

            GameObject bullet2 = Instantiate(bullet2Prefab, firePoint.position, Quaternion.identity);
            bullet2.GetComponent<Bullet>().SetDirection(aimDirection);
        }
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveInput.x));
            animator.SetBool("isGrounded", isGrounded);
            animator.SetFloat("AimX", aimDirection.x);
            animator.SetFloat("AimY", aimDirection.y);
        }
    }
}