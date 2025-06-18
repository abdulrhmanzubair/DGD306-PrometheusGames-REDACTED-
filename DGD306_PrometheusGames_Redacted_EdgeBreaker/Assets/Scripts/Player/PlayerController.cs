using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enhanced player controller (Gunner) optimized for isolated input system
/// Each player gets completely independent gamepad + keyboard controls
/// </summary>
public class PlayerController : MonoBehaviour
{
    public int PlayerIndex { get; set; }

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

    [Header("Combat Audio")]
    public AudioClip[] shootSounds;
    public AudioClip grenadeThrowSound;
    [Range(0f, 1f)] public float shootVolume = 0.6f;
    [Range(0f, 1f)] public float grenadeVolume = 0.5f;

    // Core components
    private Rigidbody2D rb;
    private Animator animator;
    private AudioSource audioSource;
    private PlayerHealthSystem healthSystem;
    private bool isGrounded;
    private float nextFireTime;
    private float nextGrenadeTime;

    // ISOLATED INPUT SYSTEM
    private FlexibleInputInjector inputInjector;
    private bool hasIsolatedInput = false;

    // Input states
    private Vector2 moveInput;
    private Vector2 aimInput = Vector2.right;
    private bool jumpPressed;
    private bool jumpRequested;
    private bool fire1Pressed;
    private bool fire2Pressed;

    // Fallback input (only used if isolated system fails)
    private SimpleFlexibleInput flexInput;
    private PlayerDeviceInfo deviceInfo;
    private InputDevice assignedDevice;
    private Keyboard keyboard;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        keyboard = Keyboard.current;

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.3f;

        // Get health system
        healthSystem = GetComponent<PlayerHealthSystem>();
        if (healthSystem == null)
        {
            Debug.LogWarning($"No PlayerHealthSystem found on {gameObject.name}! Adding one...");
            healthSystem = gameObject.AddComponent<PlayerHealthSystem>();
        }
    }

    void Start()
    {
        // PRIORITY: Always check for isolated input components first (they may be added later)
        CheckForIsolatedInputComponents();

        if (groundCheck == null)
        {
            Debug.LogError("Ground Check Transform is not assigned!");
        }

        // Set health system player index
        if (healthSystem != null)
        {
            healthSystem.PlayerIndex = PlayerIndex;
        }
    }

    void CheckForIsolatedInputComponents()
    {
        // Check for isolated input injector (highest priority)
        inputInjector = GetComponent<FlexibleInputInjector>();
        if (inputInjector != null)
        {
            hasIsolatedInput = true;
            Debug.Log($"🎮 Gunner Player {PlayerIndex} - ISOLATED INPUT ACTIVE ({inputInjector.GetCurrentInputMethod()})");
            return;
        }

        // Check for simple flexible input (medium priority)
        flexInput = GetComponent<SimpleFlexibleInput>();
        if (flexInput != null)
        {
            Debug.Log($"🎮 Gunner Player {PlayerIndex} - FLEXIBLE INPUT ACTIVE");
            return;
        }

        // Fallback to original device system (lowest priority)
        deviceInfo = GetComponent<PlayerDeviceInfo>();
        if (deviceInfo != null)
        {
            assignedDevice = deviceInfo.AssignedDevice;
            PlayerIndex = deviceInfo.PlayerIndex;
            Debug.Log($"🎮 Gunner Player {PlayerIndex} - DEVICE INPUT: {assignedDevice?.name}");
        }
    }

    void Update()
    {
        // Don't accept input if dead
        if (healthSystem != null && !healthSystem.IsAlive)
        {
            return;
        }

        HandleInput();
        CheckGrounded();
        HandleJumping();
        HandleShooting();
        UpdateGrenadeCooldownUI();
        UpdateAnimator();
    }

    void HandleInput()
    {
        // Reset input states
        jumpRequested = false;
        fire1Pressed = false;
        fire2Pressed = false;

        // DYNAMIC DETECTION: Re-check for isolated input components if not found yet
        if (!hasIsolatedInput && inputInjector == null)
        {
            CheckForIsolatedInputComponents();
        }

        // PRIORITY 1: Use isolated input injector
        if (hasIsolatedInput && inputInjector != null)
        {
            HandleIsolatedInput();
            return;
        }

        // PRIORITY 2: Use simple flexible input
        if (flexInput != null)
        {
            HandleFlexibleInput();
            return;
        }

        // FALLBACK: Original input handling
        HandleFallbackInput();
    }

    void HandleIsolatedInput()
    {
        // Get completely isolated input for this player
        moveInput = inputInjector.GetMoveInput();
        aimInput = inputInjector.GetAimInput();

        // Handle jump
        jumpPressed = inputInjector.GetJumpHeld();
        if (inputInjector.GetJumpPressed())
        {
            jumpRequested = true;
        }

        // Handle shooting (Action1 = Fire, Action2 = Grenade)
        fire1Pressed = inputInjector.GetAction1Pressed();
        fire2Pressed = inputInjector.GetAction2Pressed();

        // Ensure aim direction is valid
        if (aimInput.magnitude < 0.1f && moveInput.magnitude > 0.1f)
        {
            aimInput = moveInput;
        }
        else if (aimInput.magnitude < 0.1f)
        {
            aimInput = Vector2.right; // Default facing direction
        }

        // Debug: Show input activity
        if (moveInput.magnitude > 0.1f || fire1Pressed || fire2Pressed || jumpRequested)
        {
            Debug.Log($"🎮 Gunner P{PlayerIndex} ({inputInjector.GetCurrentInputMethod()}): Move={moveInput:F1}, Fire={fire1Pressed}, Jump={jumpRequested}");
        }
    }

    void HandleFlexibleInput()
    {
        moveInput = flexInput.moveInput;
        aimInput = flexInput.aimInput;

        jumpPressed = flexInput.jumpHeld;
        if (flexInput.jumpPressed)
        {
            jumpRequested = true;
        }

        fire1Pressed = flexInput.action1Pressed;
        fire2Pressed = flexInput.action2Pressed;

        if (aimInput.magnitude < 0.1f && moveInput.magnitude > 0.1f)
        {
            aimInput = moveInput;
        }
        else if (aimInput.magnitude < 0.1f)
        {
            aimInput = Vector2.right;
        }
    }

    void HandleFallbackInput()
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
            aimInput = rightStick;
        else if (moveInput.magnitude > 0.1f)
            aimInput = moveInput;

        jumpPressed = gamepad.buttonSouth.isPressed;
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            jumpRequested = true;
        }

        fire1Pressed = gamepad.rightTrigger.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame;
        fire2Pressed = gamepad.rightShoulder.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame;
    }

    void HandleKeyboardInput()
    {
        if (keyboard == null) return;

        Vector2 keyboardMove = Vector2.zero;

        // Player-specific keyboard controls
        if (PlayerIndex == 0)
        {
            // Player 0: WASD + Space/Ctrl/Shift
            if (keyboard.aKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.dKey.isPressed) keyboardMove.x += 1;
            if (keyboard.wKey.isPressed) keyboardMove.y += 1;
            if (keyboard.sKey.isPressed) keyboardMove.y -= 1;

            jumpPressed = keyboard.spaceKey.isPressed;
            if (keyboard.spaceKey.wasPressedThisFrame)
                jumpRequested = true;

            fire1Pressed = keyboard.leftCtrlKey.wasPressedThisFrame;
            fire2Pressed = keyboard.leftShiftKey.wasPressedThisFrame;
        }
        else if (PlayerIndex == 1)
        {
            // Player 1: Arrow Keys + Enter/RCtrl/RShift
            if (keyboard.leftArrowKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.rightArrowKey.isPressed) keyboardMove.x += 1;
            if (keyboard.upArrowKey.isPressed) keyboardMove.y += 1;
            if (keyboard.downArrowKey.isPressed) keyboardMove.y -= 1;

            jumpPressed = keyboard.enterKey.isPressed;
            if (keyboard.enterKey.wasPressedThisFrame)
                jumpRequested = true;

            fire1Pressed = keyboard.rightCtrlKey.wasPressedThisFrame;
            fire2Pressed = keyboard.rightShiftKey.wasPressedThisFrame;
        }

        moveInput = keyboardMove;
        if (moveInput.magnitude > 0.1f)
            aimInput = moveInput;
    }

    public void Initialize(int playerIndex)
    {
        PlayerIndex = playerIndex;
        Debug.Log($"GunnerController initialized - PlayerIndex: {playerIndex}");

        // Set health system player index
        if (healthSystem != null)
        {
            healthSystem.PlayerIndex = PlayerIndex;
        }
    }

    void FixedUpdate()
    {
        if (healthSystem == null || healthSystem.IsAlive)
        {
            Move();
            ApplyBetterJump();
        }
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
        if (jumpRequested && isGrounded)
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
        // Primary fire (bullets)
        if (Time.time >= nextFireTime && fire1Pressed)
        {
            nextFireTime = Time.time + fireRate;

            // Play shoot sound
            if (shootSounds.Length > 0 && audioSource != null)
            {
                AudioClip shootClip = shootSounds[Random.Range(0, shootSounds.Length)];
                audioSource.PlayOneShot(shootClip, shootVolume);
            }

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.SetDirection(GetAimDirection());
                bulletScript.SetShooter("Player");
            }
        }

        // Secondary fire (grenades)
        if (Time.time >= nextGrenadeTime && fire2Pressed)
        {
            ThrowGrenade();
            nextGrenadeTime = Time.time + grenadeCooldown;
        }
    }

    void ThrowGrenade()
    {
        if (grenadePrefab == null) return;

        // Play grenade throw sound
        if (grenadeThrowSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(grenadeThrowSound, grenadeVolume);
        }

        Vector2 throwDirection = GetAimDirection();
        GameObject grenade = Instantiate(grenadePrefab, firePoint.position, Quaternion.identity);
        if (grenade == null) return;

        Grenade grenadeScript = grenade.GetComponent<Grenade>();
        if (grenadeScript != null)
        {
            grenadeScript.Launch(throwDirection, grenadeForce);
        }
        else
        {
            Rigidbody2D grenadeRb = grenade.GetComponent<Rigidbody2D>();
            if (grenadeRb != null)
            {
                grenadeRb.linearVelocity = throwDirection * grenadeForce;
            }
        }
    }

    void UpdateGrenadeCooldownUI()
    {
        float timeRemaining = nextGrenadeTime - Time.time;
        bool isOnCooldown = timeRemaining > 0f;

        if (grenadeCooldownFill != null)
        {
            if (isOnCooldown)
            {
                float fillAmount = timeRemaining / grenadeCooldown;
                grenadeCooldownFill.fillAmount = fillAmount;
                grenadeCooldownFill.color = cooldownColor;
            }
            else
            {
                grenadeCooldownFill.fillAmount = 1f;
                grenadeCooldownFill.color = readyColor;
            }
        }

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

    // Handle taking damage from enemies
    void OnTriggerEnter2D(Collider2D other)
    {
        // Take damage from enemy projectiles
        if (other.CompareTag("EnemyProjectile"))
        {
            Bullet bullet = other.GetComponent<Bullet>();
            if (bullet != null && healthSystem != null)
            {
                healthSystem.TakeDamage(bullet.damage);
                Debug.Log($"Player {PlayerIndex} hit by enemy bullet for {bullet.damage} damage!");
            }
        }

        // Take damage from enemy contact
        if (other.CompareTag("Enemy"))
        {
            if (healthSystem != null)
            {
                float contactDamage = 10f;

                // Try to get specific enemy damage
                UniversalEnemyHealth enemyHealth = other.GetComponent<UniversalEnemyHealth>();
                if (enemyHealth != null)
                {
                    contactDamage = 8f; // Basic enemy contact damage
                }

                healthSystem.TakeDamage(contactDamage);
                Debug.Log($"Player {PlayerIndex} hit by enemy contact for {contactDamage} damage!");

                // Push player away from enemy
                Vector2 pushDirection = (transform.position - other.transform.position).normalized;
                if (rb != null)
                {
                    rb.AddForce(pushDirection * 6f, ForceMode2D.Impulse);
                }
            }
        }
    }

    // Public methods
    public bool IsGrenadeReady() => Time.time >= nextGrenadeTime;
    public float GetGrenadeCooldownRemaining() => Mathf.Max(0f, nextGrenadeTime - Time.time);
    public PlayerHealthSystem GetHealthSystem() => healthSystem;

    Vector2 GetAimDirection() => aimInput != Vector2.zero ? aimInput.normalized : (Vector2)transform.right;

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
            animator.SetBool("isGrounded", isGrounded);
            animator.SetBool("isJumping", !isGrounded && rb.linearVelocity.y > 0.1f);

            // Set invulnerability state
            if (healthSystem != null)
            {
                animator.SetBool("isInvulnerable", healthSystem.IsInvulnerable);
            }
        }
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw health status
        if (healthSystem != null)
        {
            if (!healthSystem.IsAlive)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 2f);
            }
            else if (healthSystem.IsInvulnerable)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }

        // Draw input status
        if (hasIsolatedInput)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.3f);
        }
    }
}