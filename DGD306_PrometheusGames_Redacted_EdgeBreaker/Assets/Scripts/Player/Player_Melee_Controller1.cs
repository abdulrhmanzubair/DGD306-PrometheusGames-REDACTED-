using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enhanced melee controller with proper damage dealing and receiving
/// </summary>
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
    public GameObject slashEffectPrefab; // Use EnhancedSlashEffect prefab
    public float dashForce = 20f;
    public float dashCooldown = 1f;
    public float dashDuration = 0.15f;
    public float slashDamage = 25f;
    public float slashRange = 2f;
    public LayerMask enemyLayers = -1;

    [Header("Combat Audio")]
    public AudioClip[] slashSounds;
    public AudioClip dashSound;
    [Range(0f, 1f)] public float slashVolume = 0.6f;
    [Range(0f, 1f)] public float dashVolume = 0.5f;

    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    private Rigidbody2D rb;
    private Animator animator;
    private AudioSource audioSource;
    private PlayerHealthSystem healthSystem;
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
    private bool attackPressed;

    public int PlayerIndex { get; set; }
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

        // Set health system player index
        if (healthSystem != null)
        {
            healthSystem.PlayerIndex = PlayerIndex;
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
        HandleGuard();
        HandleDashAttack();
        HandleMeleeAttack();
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
            attackPressed = flexInjector.GetAction1Pressed(); // Use action1 for attack instead

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
            attackPressed = flexInput.action1Pressed;

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

        if (gamepad.rightTrigger.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame)
            attackPressed = true;
        if (gamepad.rightTrigger.wasReleasedThisFrame || gamepad.leftShoulder.wasReleasedThisFrame)
            attackPressed = false;
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

            if (keyboard.fKey.wasPressedThisFrame)
                attackPressed = true;
            if (keyboard.fKey.wasReleasedThisFrame)
                attackPressed = false;
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

            if (keyboard.numpad0Key.wasPressedThisFrame)
                attackPressed = true;
            if (keyboard.numpad0Key.wasReleasedThisFrame)
                attackPressed = false;
        }

        moveInput = keyboardMove;
        if (moveInput.magnitude > 0.1f)
            aimDirection = moveInput;
    }

    void FixedUpdate()
    {
        if (!isDashing && (healthSystem == null || healthSystem.IsAlive))
            Move();
        ApplyBetterJump();
    }

    public void Initialize(int playerIndex)
    {
        PlayerIndex = playerIndex;
        Debug.Log($"MeleeController initialized - PlayerIndex: {playerIndex}");

        // Set health system player index
        if (healthSystem != null)
        {
            healthSystem.PlayerIndex = PlayerIndex;
        }
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

        // Play dash sound
        if (dashSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dashSound, dashVolume);
        }

        string animTrigger = "DashForward";
        if (direction.y > 0.5f) animTrigger = "DashUp";
        else if (direction.y < -0.5f) animTrigger = "DashDown";

        if (animator != null)
            animator.SetTrigger(animTrigger);
    }

    void HandleMeleeAttack()
    {
        if (attackPressed)
        {
            PerformSlashAttack();
            attackPressed = false;
        }
    }

    void PerformSlashAttack()
    {
        Debug.Log($"Player {PlayerIndex} performing slash attack!");

        // Play slash sound
        if (slashSounds.Length > 0 && audioSource != null)
        {
            AudioClip slashClip = slashSounds[Random.Range(0, slashSounds.Length)];
            audioSource.PlayOneShot(slashClip, slashVolume);
        }

        // Create slash effect
        Vector3 slashPosition = attackPoint != null ? attackPoint.position : transform.position;
        Vector2 attackDirection = aimDirection.magnitude > 0.1f ? aimDirection : Vector2.right;

        if (slashEffectPrefab != null)
        {
            GameObject slashEffect = Instantiate(slashEffectPrefab, slashPosition, Quaternion.identity);

            // Configure the enhanced slash effect
            EnhancedSlashEffect slashScript = slashEffect.GetComponent<EnhancedSlashEffect>();
            if (slashScript != null)
            {
                slashScript.SetDamage(slashDamage);
                slashScript.AddTargetTag("Enemy");
            }

            // Orient slash effect based on attack direction
            float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
            slashEffect.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        else
        {
            // Fallback: direct damage detection
            PerformDirectSlashDamage(slashPosition, attackDirection);
        }

        // Trigger attack animation
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    void PerformDirectSlashDamage(Vector3 attackPosition, Vector2 attackDirection)
    {
        // Use circle cast for slash damage
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPosition, slashRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            // Check if it's an enemy
            if (enemy.CompareTag("Enemy"))
            {
                // Deal damage
                IDamageable damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(slashDamage);
                    Debug.Log($"Slash hit {enemy.name} for {slashDamage} damage!");
                }

                // Apply knockback
                Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    Vector2 knockbackDirection = (enemy.transform.position - transform.position).normalized;
                    enemyRb.AddForce(knockbackDirection * 5f, ForceMode2D.Impulse);
                }
            }
        }
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

            // Set guard state
            animator.SetBool("hasGuard", activeGuard != null);

            // Set invulnerability state
            if (healthSystem != null)
            {
                animator.SetBool("isInvulnerable", healthSystem.IsInvulnerable);
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

        // Take damage from enemy contact (if no guard)
        if (other.CompareTag("Enemy") && activeGuard == null)
        {
            if (healthSystem != null)
            {
                float contactDamage = 15f; // Default contact damage

                // Try to get specific enemy damage
                UniversalEnemyHealth enemyHealth = other.GetComponent<UniversalEnemyHealth>();
                if (enemyHealth != null)
                {
                    // Could have contact damage property on enemy
                    contactDamage = 10f; // Basic enemy contact damage
                }

                healthSystem.TakeDamage(contactDamage);
                Debug.Log($"Player {PlayerIndex} hit by enemy contact for {contactDamage} damage!");

                // Push player away from enemy
                Vector2 pushDirection = (transform.position - other.transform.position).normalized;
                if (rb != null)
                {
                    rb.AddForce(pushDirection * 8f, ForceMode2D.Impulse);
                }
            }
        }
    }

    // Public methods for external access
    public bool IsGuardActive()
    {
        return activeGuard != null;
    }

    public bool IsGuardOnCooldown()
    {
        return isGuardOnCooldown;
    }

    public float GetGuardCooldownRemaining()
    {
        return isGuardOnCooldown ? guardCooldownTimer : 0f;
    }

    public bool IsDashing()
    {
        return isDashing;
    }

    public PlayerHealthSystem GetHealthSystem()
    {
        return healthSystem;
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw attack range
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, slashRange);
        }

        // Draw guard spawn point
        if (guardSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(guardSpawnPoint.position, Vector3.one * 0.3f);
        }

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
    }
}