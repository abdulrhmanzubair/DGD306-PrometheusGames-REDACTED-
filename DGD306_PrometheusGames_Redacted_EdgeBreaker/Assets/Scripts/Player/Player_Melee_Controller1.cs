using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Melee_Controller1 : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Guard Settings")]
    public GameObject guardPrefab;
    public Transform guardSpawnPoint;
    public float guardCooldown = 5f;
    public float guardLifetime = 10f;

    [Header("Slash Attack")]
    public Transform attackPoint;
    public float dashForce = 20f;
    public float dashCooldown = 1f;
    public float dashDuration = 0.15f;

    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;

    private bool isGuardOnCooldown = false;
    private float guardCooldownTimer = 0f;
    private GameObject activeGuard;

    private float lastDashTime = -999f;
    private bool isDashing = false;
    private float dashEndTime;

    // Input system
    private PlayerInputActions inputActions;
    private bool jumpPressed;
    private bool dashPressed;
    private bool guardPressed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        inputActions = new PlayerInputActions();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Aim.performed += ctx => aimDirection = ctx.ReadValue<Vector2>();
        inputActions.Player.Aim.canceled += ctx => aimDirection = Vector2.right;

        inputActions.Player.Jump.performed += ctx => jumpPressed = true;
        inputActions.Player.Jump.canceled += ctx => jumpPressed = false;

        inputActions.Player.Dash.performed += ctx => dashPressed = true;
        inputActions.Player.Dash.canceled += ctx => dashPressed = false;

        inputActions.Player.Guard.performed += ctx => guardPressed = true;
        inputActions.Player.Guard.canceled += ctx => guardPressed = false;
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Update()
    {
        CheckGrounded();
        HandleJumping();
        HandleGuard();
        HandleDashAttack();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (!isDashing)
            Move();

        ApplyBetterJump();
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
    void Move()
    {
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);

        if (moveInput.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(moveInput.x), 1f, 1f);
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
            jumpPressed = false;
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

    void HandleGuard()
    {
        if (guardPressed && !isGuardOnCooldown && activeGuard == null)
        {
            GameObject guard = Instantiate(guardPrefab, guardSpawnPoint.position, Quaternion.identity);
            GuardBehavior behavior = guard.GetComponent<GuardBehavior>();
            behavior.SetOwner(this);
            behavior.SetLifetime(guardLifetime);
            activeGuard = guard;
            guardPressed = false;
        }

        if (isGuardOnCooldown)
        {
            guardCooldownTimer -= Time.deltaTime;
            if (guardCooldownTimer <= 0f)
                isGuardOnCooldown = false;
        }
    }

    public void OnGuardDestroyed()
    {
        if (activeGuard != null)
        {
            Destroy(activeGuard);
            activeGuard = null;
        }

        isGuardOnCooldown = true;
        guardCooldownTimer = guardCooldown;
    }

    void HandleDashAttack()
    {
        if (dashPressed && Time.time >= lastDashTime + dashCooldown)
        {
            Vector2 dashDir = aimDirection != Vector2.zero ? aimDirection : Vector2.right;
            StartDash(dashDir);
            dashPressed = false;
        }

        if (isDashing && Time.time > dashEndTime)
        {
            isDashing = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    void StartDash(Vector2 direction)
    {
        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        lastDashTime = Time.time;

        rb.linearVelocity = direction.normalized * dashForce;

        if (direction.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(direction.x), 1, 1);

        string animTrigger = "DashForward";
        if (direction.y > 0.5f) animTrigger = "DashUp";
        else if (direction.y < -0.5f) animTrigger = "DashDown";

        if (animator != null)
            animator.SetTrigger(animTrigger);
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
            animator.SetBool("isGrounded", isGrounded);

            bool isJumping = !isGrounded && rb.linearVelocity.y > 0.1f;
            animator.SetBool("isJumping", isJumping);
        }
    }
}
