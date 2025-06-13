using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public int PlayerIndex { get; set; }

    // DIRECT INPUT ONLY - no PlayerInput system
    private PlayerDeviceInfo deviceInfo;
    private InputDevice assignedDevice;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public GameObject grenadePrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;

    [Header("Grenade Settings")]
    public float grenadeCooldown = 3f;
    public float grenadeForce = 12f;

    [Header("UI Elements")]
    public UnityEngine.UI.Image grenadeCooldownFill;
    public UnityEngine.UI.Text grenadeCooldownText;
    public Color cooldownColor = Color.red;
    public Color readyColor = Color.green;

    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private float nextFireTime;
    private float nextGrenadeTime;

    // Input states
    private Vector2 moveInput;
    private Vector2 aimInput = Vector2.right;
    private bool jumpPressed;
    private bool jumpRequested;
    private bool fire1Pressed;
    private bool fire2Pressed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        // Get device info from the spawner
        deviceInfo = GetComponent<PlayerDeviceInfo>();
        if (deviceInfo != null)
        {
            assignedDevice = deviceInfo.AssignedDevice;
            PlayerIndex = deviceInfo.PlayerIndex;
            Debug.Log($"GunnerController - DIRECT INPUT - PlayerIndex: {PlayerIndex}, Device: {assignedDevice?.name ?? "None"}");
        }
        else
        {
            Debug.LogError($"GunnerController - No PlayerDeviceInfo found!");
        }

        // Debug ground check setup
        if (groundCheck == null)
        {
            Debug.LogError("Ground Check Transform is not assigned!");
        }
        else
        {
            Debug.Log($"Ground Check assigned. Player pos: {transform.position}, Ground check pos: {groundCheck.position}");
        }
    }

    void Update()
    {
        // Handle DIRECT device input only
        HandleDirectDeviceInput();

        CheckGrounded();
        HandleJumping();
        HandleShooting();
        UpdateGrenadeCooldownUI();
        UpdateAnimator();
    }

    void HandleDirectDeviceInput()
    {
        if (assignedDevice == null) return;

        // Handle gamepad input
        if (assignedDevice is Gamepad gamepad)
        {
            // Movement
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 dpad = gamepad.dpad.ReadValue();
            moveInput = leftStick.magnitude > 0.1f ? leftStick : dpad;

            // Aim direction (use right stick or default to move direction)
            Vector2 rightStick = gamepad.rightStick.ReadValue();
            if (rightStick.magnitude > 0.1f)
                aimInput = rightStick;
            else if (moveInput.magnitude > 0.1f)
                aimInput = moveInput;

            // Jump
            if (gamepad.buttonSouth.wasPressedThisFrame) // A button
            {
                jumpPressed = true;
                jumpRequested = true;
            }
            if (gamepad.buttonSouth.wasReleasedThisFrame)
                jumpPressed = false;

            // Fire1 (bullets)
            if (gamepad.rightTrigger.wasPressedThisFrame) // Right Trigger
                fire1Pressed = true;
            if (gamepad.rightTrigger.wasReleasedThisFrame)
                fire1Pressed = false;

            // Fire2 (grenades)
            if (gamepad.rightShoulder.wasPressedThisFrame) // Right Bumper
                fire2Pressed = true;
            if (gamepad.rightShoulder.wasReleasedThisFrame)
                fire2Pressed = false;
        }
        // Handle keyboard input - different keys for each player
        else if (assignedDevice is Keyboard keyboard)
        {
            if (PlayerIndex == 0) // Player 1 keyboard controls
            {
                // Movement
                Vector2 keyboardMove = Vector2.zero;
                if (keyboard.aKey.isPressed) keyboardMove.x -= 1;
                if (keyboard.dKey.isPressed) keyboardMove.x += 1;
                if (keyboard.wKey.isPressed) keyboardMove.y += 1;
                if (keyboard.sKey.isPressed) keyboardMove.y -= 1;
                moveInput = keyboardMove;

                // Aim direction follows movement
                if (moveInput.magnitude > 0.1f)
                    aimInput = moveInput;

                // Jump
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    jumpPressed = true;
                    jumpRequested = true;
                }
                if (keyboard.spaceKey.wasReleasedThisFrame)
                    jumpPressed = false;

                // Fire1 (bullets)
                if (keyboard.leftCtrlKey.wasPressedThisFrame)
                    fire1Pressed = true;
                if (keyboard.leftCtrlKey.wasReleasedThisFrame)
                    fire1Pressed = false;

                // Fire2 (grenades)
                if (keyboard.leftShiftKey.wasPressedThisFrame)
                    fire2Pressed = true;
                if (keyboard.leftShiftKey.wasReleasedThisFrame)
                    fire2Pressed = false;
            }
            else // Player 2 keyboard controls (different keys)
            {
                // Movement
                Vector2 keyboardMove = Vector2.zero;
                if (keyboard.leftArrowKey.isPressed) keyboardMove.x -= 1;
                if (keyboard.rightArrowKey.isPressed) keyboardMove.x += 1;
                if (keyboard.upArrowKey.isPressed) keyboardMove.y += 1;
                if (keyboard.downArrowKey.isPressed) keyboardMove.y -= 1;
                moveInput = keyboardMove;

                // Aim direction follows movement
                if (moveInput.magnitude > 0.1f)
                    aimInput = moveInput;

                // Jump
                if (keyboard.enterKey.wasPressedThisFrame)
                {
                    jumpPressed = true;
                    jumpRequested = true;
                }
                if (keyboard.enterKey.wasReleasedThisFrame)
                    jumpPressed = false;

                // Fire1 (bullets)
                if (keyboard.rightCtrlKey.wasPressedThisFrame)
                    fire1Pressed = true;
                if (keyboard.rightCtrlKey.wasReleasedThisFrame)
                    fire1Pressed = false;

                // Fire2 (grenades)
                if (keyboard.rightShiftKey.wasPressedThisFrame)
                    fire2Pressed = true;
                if (keyboard.rightShiftKey.wasReleasedThisFrame)
                    fire2Pressed = false;
            }
        }
    }

    public void Initialize(int playerIndex)
    {
        PlayerIndex = playerIndex;
        Debug.Log($"GunnerController initialized with PlayerIndex: {playerIndex} - DIRECT INPUT MODE");
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

        // Debug ground detection
        if (jumpRequested)
        {
            Debug.Log($"Ground check: isGrounded = {isGrounded}, groundCheck pos: {groundCheck.position}");
            Debug.Log($"Checking layer mask: {groundLayer.value}, overlapping objects: {Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer)}");
        }
    }

    void HandleJumping()
    {
        // Use jumpRequested for more reliable jump detection
        if (jumpRequested && isGrounded)
        {
            Debug.Log($"Jump executed! Current velocity: {rb.linearVelocity}, jumpForce: {jumpForce}");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            Debug.Log($"After jump velocity: {rb.linearVelocity}");
            jumpRequested = false; // Clear the jump request after using it
        }

        // Clear jump request if we've been in air too long (prevents stuck jump requests)
        if (!isGrounded && jumpRequested)
        {
            jumpRequested = false;
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
        // Handle regular bullet shooting
        if (Time.time >= nextFireTime && fire1Pressed)
        {
            nextFireTime = Time.time + fireRate;
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            bullet.GetComponent<Bullet>().SetDirection(GetAimDirection());
            fire1Pressed = false;
        }

        // Handle grenade throwing
        if (Time.time >= nextGrenadeTime && fire2Pressed)
        {
            ThrowGrenade();
            nextGrenadeTime = Time.time + grenadeCooldown;
            fire2Pressed = false;
        }
    }

    void ThrowGrenade()
    {
        if (grenadePrefab == null)
        {
            Debug.LogWarning("Grenade prefab is not assigned!");
            return;
        }

        // Launch grenade straight forward in aim direction
        Vector2 throwDirection = GetAimDirection();

        // Spawn grenade
        GameObject grenade = Instantiate(grenadePrefab, firePoint.position, Quaternion.identity);

        if (grenade == null)
        {
            Debug.LogError("Failed to instantiate grenade!");
            return;
        }

        Grenade grenadeScript = grenade.GetComponent<Grenade>();
        if (grenadeScript != null)
        {
            grenadeScript.Launch(throwDirection, grenadeForce);
        }
        else
        {
            Debug.LogWarning("Grenade prefab doesn't have Grenade script! Using fallback method.");
            // Fallback if no Grenade script
            Rigidbody2D grenadeRb = grenade.GetComponent<Rigidbody2D>();
            if (grenadeRb != null)
            {
                grenadeRb.linearVelocity = throwDirection * grenadeForce;
            }
            else
            {
                Debug.LogError("Grenade prefab has no Rigidbody2D component!");
            }
        }

        Debug.Log($"Grenade launched! Next grenade available in {grenadeCooldown} seconds.");
    }

    void UpdateGrenadeCooldownUI()
    {
        float timeRemaining = nextGrenadeTime - Time.time;
        bool isOnCooldown = timeRemaining > 0f;

        // Update the radial fill image
        if (grenadeCooldownFill != null)
        {
            if (isOnCooldown)
            {
                // Show cooldown progress (fill decreases as cooldown reduces)
                float fillAmount = timeRemaining / grenadeCooldown;
                grenadeCooldownFill.fillAmount = fillAmount;
                grenadeCooldownFill.color = cooldownColor;
            }
            else
            {
                // Grenade is ready
                grenadeCooldownFill.fillAmount = 1f;
                grenadeCooldownFill.color = readyColor;
            }
        }

        // Update the text
        if (grenadeCooldownText != null)
        {
            if (isOnCooldown)
            {
                grenadeCooldownText.text = $"{timeRemaining:F1}s";
                grenadeCooldownText.color = cooldownColor;
            }
            else
            {
                grenadeCooldownText.text = "READY";
                grenadeCooldownText.color = readyColor;
            }
        }
    }

    // Public method to check if grenade is ready (useful for other UI elements)
    public bool IsGrenadeReady()
    {
        return Time.time >= nextGrenadeTime;
    }

    // Get remaining cooldown time
    public float GetGrenadeCooldownRemaining()
    {
        return Mathf.Max(0f, nextGrenadeTime - Time.time);
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