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

    // Updated input system - use PlayerInput component
    private PlayerInput playerInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool guardPressed;

    public int PlayerIndex { get; set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        // Get the PlayerInput component that was set up by the spawner
        playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            // If no PlayerInput on this object, find the one with matching index
            PlayerInput[] allInputs = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
            foreach (var input in allInputs)
            {
                if (input.playerIndex == PlayerIndex)
                {
                    playerInput = input;
                    break;
                }
            }
        }

        if (playerInput != null)
        {
            Debug.Log($"MeleeController - PlayerIndex: {PlayerIndex}, PlayerInput found with index: {playerInput.playerIndex}");

            // Set up input callbacks using the PlayerInput's actions
            var moveAction = playerInput.actions["Move"];
            var aimAction = playerInput.actions["Aim"];
            var jumpAction = playerInput.actions["Jump"];
            var dashAction = playerInput.actions["Dash"];
            var guardAction = playerInput.actions["Guard"];

            if (moveAction != null)
            {
                moveAction.performed += OnMove;
                moveAction.canceled += OnMoveCancel;
            }

            if (aimAction != null)
            {
                aimAction.performed += OnAim;
                aimAction.canceled += OnAimCancel;
            }

            if (jumpAction != null)
            {
                jumpAction.performed += OnJump;
                jumpAction.canceled += OnJumpCancel;
            }

            if (dashAction != null)
            {
                dashAction.performed += OnDash;
                dashAction.canceled += OnDashCancel;
            }

            if (guardAction != null)
            {
                guardAction.performed += OnGuard;
                guardAction.canceled += OnGuardCancel;
            }
        }
        else
        {
            Debug.LogError($"MeleeController - No PlayerInput found for PlayerIndex: {PlayerIndex}");
        }
    }

    void OnDestroy()
    {
        // Clean up input callbacks
        if (playerInput != null)
        {
            var moveAction = playerInput.actions["Move"];
            var aimAction = playerInput.actions["Aim"];
            var jumpAction = playerInput.actions["Jump"];
            var dashAction = playerInput.actions["Dash"];
            var guardAction = playerInput.actions["Guard"];

            if (moveAction != null)
            {
                moveAction.performed -= OnMove;
                moveAction.canceled -= OnMoveCancel;
            }

            if (aimAction != null)
            {
                aimAction.performed -= OnAim;
                aimAction.canceled -= OnAimCancel;
            }

            if (jumpAction != null)
            {
                jumpAction.performed -= OnJump;
                jumpAction.canceled -= OnJumpCancel;
            }

            if (dashAction != null)
            {
                dashAction.performed -= OnDash;
                dashAction.canceled -= OnDashCancel;
            }

            if (guardAction != null)
            {
                guardAction.performed -= OnGuard;
                guardAction.canceled -= OnGuardCancel;
            }
        }
    }

    // Input callback methods
    private void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnMoveCancel(InputAction.CallbackContext context) => moveInput = Vector2.zero;
    private void OnAim(InputAction.CallbackContext context) => aimDirection = context.ReadValue<Vector2>();
    private void OnAimCancel(InputAction.CallbackContext context) => aimDirection = Vector2.right;
    private void OnJump(InputAction.CallbackContext context) => jumpPressed = true;
    private void OnJumpCancel(InputAction.CallbackContext context) => jumpPressed = false;
    private void OnDash(InputAction.CallbackContext context) => dashPressed = true;
    private void OnDashCancel(InputAction.CallbackContext context) => dashPressed = false;
    private void OnGuard(InputAction.CallbackContext context) => guardPressed = true;
    private void OnGuardCancel(InputAction.CallbackContext context) => guardPressed = false;

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
        PlayerIndex = playerIndex;
        Debug.Log($"MeleeController initialized with PlayerIndex: {playerIndex}");
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