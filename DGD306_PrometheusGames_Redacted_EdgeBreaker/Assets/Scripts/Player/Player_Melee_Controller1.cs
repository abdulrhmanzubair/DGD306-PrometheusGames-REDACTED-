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

    // FLEXIBLE INPUT INTEGRATION
    private FlexibleInputInjector flexInjector;
    private SimpleFlexibleInput flexInput;

    // Original components
    private PlayerDeviceInfo deviceInfo;
    private InputDevice assignedDevice;

    // Input states
    private bool jumpPressed;
    private bool dashPressed;
    private bool guardPressed;

    public int PlayerIndex { get; set; }
    private Keyboard keyboard;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        keyboard = Keyboard.current;
    }

    void Start()
    {
        // Check for flexible input components (added by AutoFlexibleInputSetup)
        flexInjector = GetComponent<FlexibleInputInjector>();
        flexInput = GetComponent<SimpleFlexibleInput>();

        if (flexInjector != null || flexInput != null)
        {
            Debug.Log($"MeleeController - FLEXIBLE INPUT ACTIVE for Player {PlayerIndex}");
        }
        else
        {
            // Fallback to original device system
            deviceInfo = GetComponent<PlayerDeviceInfo>();
            if (deviceInfo != null)
            {
                assignedDevice = deviceInfo.AssignedDevice;
                PlayerIndex = deviceInfo.PlayerIndex;
                Debug.Log($"MeleeController - Device: {assignedDevice?.name}, PlayerIndex: {PlayerIndex}");
            }
        }
    }

    void Update()
    {
        HandleInput();
        CheckGrounded();
        HandleJumping();
        HandleGuard();
        HandleDashAttack();
        UpdateAnimator();
    }

    void HandleInput()
    {
        // PRIORITY 1: Check for flexible input
        if (flexInjector != null)
        {
            moveInput = flexInjector.GetMoveInput();
            aimDirection = flexInjector.GetAimInput();

            jumpPressed = flexInjector.GetJumpPressed();
            dashPressed = flexInjector.GetAction2Pressed();
            guardPressed = flexInjector.GetAction1Pressed();

            return; // Use flexible input, skip original handling
        }

        // PRIORITY 2: Check SimpleFlexibleInput
        if (flexInput != null)
        {
            moveInput = flexInput.moveInput;
            aimDirection = flexInput.aimInput;

            jumpPressed = flexInput.jumpPressed;
            dashPressed = flexInput.action2Pressed;
            guardPressed = flexInput.action1Pressed;

            return; // Use simple flexible input
        }

        // FALLBACK: Original input handling
        HandleOriginalInput();
    }

    void HandleOriginalInput()
    {
        if (assignedDevice is Gamepad gamepad)
        {
            HandleGamepadInput(gamepad);
        }
        else if (assignedDevice is Keyboard || keyboard != null)
        {
            HandleKeyboardInput();
        }
    }

    void HandleGamepadInput(Gamepad gamepad)
    {
        Vector2 leftStick = gamepad.leftStick.ReadValue();
        Vector2 dpad = gamepad.dpad.ReadValue();
        moveInput = leftStick.magnitude > 0.1f ? leftStick : dpad;

        Vector2 rightStick = gamepad.rightStick.ReadValue();
        if (rightStick.magnitude > 0.1f)
            aimDirection = rightStick;
        else if (moveInput.magnitude > 0.1f)
            aimDirection = moveInput;

        if (gamepad.buttonSouth.wasPressedThisFrame)
            jumpPressed = true;
        if (gamepad.buttonSouth.wasReleasedThisFrame)
            jumpPressed = false;

        if (gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame)
            dashPressed = true;
        if (gamepad.buttonWest.wasReleasedThisFrame || gamepad.buttonNorth.wasReleasedThisFrame)
            dashPressed = false;

        if (gamepad.rightShoulder.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame)
            guardPressed = true;
        if (gamepad.rightShoulder.wasReleasedThisFrame || gamepad.buttonEast.wasReleasedThisFrame)
            guardPressed = false;
    }

    void HandleKeyboardInput()
    {
        if (keyboard == null) return;

        Vector2 keyboardMove = Vector2.zero;

        // Default to WASD for Player 0, Arrow Keys for Player 1
        if (PlayerIndex == 0)
        {
            if (keyboard.aKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.dKey.isPressed) keyboardMove.x += 1;
            if (keyboard.wKey.isPressed) keyboardMove.y += 1;
            if (keyboard.sKey.isPressed) keyboardMove.y -= 1;

            if (keyboard.spaceKey.wasPressedThisFrame)
                jumpPressed = true;
            if (keyboard.spaceKey.wasReleasedThisFrame)
                jumpPressed = false;

            if (keyboard.leftShiftKey.wasPressedThisFrame)
                dashPressed = true;
            if (keyboard.leftShiftKey.wasReleasedThisFrame)
                dashPressed = false;

            if (keyboard.leftCtrlKey.wasPressedThisFrame)
                guardPressed = true;
            if (keyboard.leftCtrlKey.wasReleasedThisFrame)
                guardPressed = false;
        }
        else
        {
            if (keyboard.leftArrowKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.rightArrowKey.isPressed) keyboardMove.x += 1;
            if (keyboard.upArrowKey.isPressed) keyboardMove.y += 1;
            if (keyboard.downArrowKey.isPressed) keyboardMove.y -= 1;

            if (keyboard.enterKey.wasPressedThisFrame)
                jumpPressed = true;
            if (keyboard.enterKey.wasReleasedThisFrame)
                jumpPressed = false;

            if (keyboard.rightShiftKey.wasPressedThisFrame)
                dashPressed = true;
            if (keyboard.rightShiftKey.wasReleasedThisFrame)
                dashPressed = false;

            if (keyboard.rightCtrlKey.wasPressedThisFrame)
                guardPressed = true;
            if (keyboard.rightCtrlKey.wasReleasedThisFrame)
                guardPressed = false;
        }

        moveInput = keyboardMove;
        if (moveInput.magnitude > 0.1f)
            aimDirection = moveInput;
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
        Debug.Log($"MeleeController initialized - PlayerIndex: {playerIndex}");
    }

    // Rest of your existing methods stay the same...
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
            if (behavior != null)
            {
                behavior.SetOwner(this);
                behavior.SetLifetime(guardLifetime);
            }
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
            Vector2 dashDir = aimDirection.magnitude > 0.1f ? aimDirection : Vector2.right;
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