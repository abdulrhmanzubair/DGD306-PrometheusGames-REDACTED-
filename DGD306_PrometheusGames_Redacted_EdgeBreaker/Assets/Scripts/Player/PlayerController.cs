using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private float nextFireTime;

    private Vector2 moveInput;
    private Vector2 aimInput;
    private bool jumpPressed;
    private bool fire1Pressed;
    private bool fire2Pressed;

    private PlayerInputActions inputActions;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        inputActions = new PlayerInputActions();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Aim.performed += ctx => aimInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Aim.canceled += ctx => aimInput = Vector2.right;

        inputActions.Player.Jump.performed += ctx => jumpPressed = true;
        inputActions.Player.Jump.canceled += ctx => jumpPressed = false;

        inputActions.Player.Fire1.performed += ctx => fire1Pressed = true;
        inputActions.Player.Fire1.canceled += ctx => fire1Pressed = false;

        inputActions.Player.Fire2.performed += ctx => fire2Pressed = true;
        inputActions.Player.Fire2.canceled += ctx => fire2Pressed = false;
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        CheckGrounded();
        HandleJumping();
        HandleShooting();
        UpdateAnimator();
    }

    public void Initialize(int playerIndex)
    {
        // Set up input scheme
        GetComponent<PlayerInput>().SwitchCurrentControlScheme(
            playerIndex == 0 ? "Player1" : "Player2",
            Keyboard.current,
            Gamepad.current
        );

       
    }
    void FixedUpdate()
    {
        Move();
        ApplyBetterJump();
    }

    void Move()
    {
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);

        if (moveInput.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(moveInput.x), 1, 1);
    }

    void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
    }

    void HandleJumping()
    {
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    void ApplyBetterJump()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpPressed)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void HandleShooting()
    {
        if (Time.time >= nextFireTime)
        {
            if (fire1Pressed)
            {
                nextFireTime = Time.time + fireRate;
                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
                bullet.GetComponent<Bullet>().SetDirection(GetAimDirection());
            }
            else if (fire2Pressed)
            {
                nextFireTime = Time.time + fireRate;
                GameObject bullet2 = Instantiate(bullet2Prefab, firePoint.position, Quaternion.identity);
                bullet2.GetComponent<Bullet>().SetDirection(GetAimDirection());
            }
        }
    }

    Vector2 GetAimDirection()
    {
        return aimInput != Vector2.zero ? aimInput.normalized : (Vector2)transform.right;
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
            animator.SetBool("isGrounded", isGrounded);
            animator.SetBool("isJumping", !isGrounded && rb.linearVelocity.y > 0.1f);
        }
    }
}
