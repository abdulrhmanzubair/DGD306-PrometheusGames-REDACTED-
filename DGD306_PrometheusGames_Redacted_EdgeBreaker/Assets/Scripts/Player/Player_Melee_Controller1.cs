using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Bulletproof melee controller with reliable Genji-style dash slash
/// Based on proven Unity 2D melee combat patterns
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

    [Header("Genji Dash Slash - Reliable System")]
    public Transform attackPoint;
    public GameObject slashEffectPrefab;
    public float dashDistance = 6f;
    public float dashSpeed = 20f;
    public float dashDuration = 0.3f;
    public float slashDamage = 35f;
    public float slashWidth = 2.5f;
    public float dashCooldown = 2f;
    public LayerMask enemyLayers = -1;

    [Header("Regular Attacks")]
    public float slashRange = 2f;
    public float regularDashForce = 15f;
    public float regularDashCooldown = 1f;
    public float regularDashDuration = 0.15f;

    [Header("Combat Audio")]
    public AudioClip[] slashSounds;
    public AudioClip dashSound;
    public AudioClip dashSlashSound;
    [Range(0f, 1f)] public float slashVolume = 0.6f;
    [Range(0f, 1f)] public float dashVolume = 0.5f;

    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    // Core components
    private Rigidbody2D rb;
    private Animator animator;
    private AudioSource audioSource;
    private PlayerHealthSystem healthSystem;
    private bool isGrounded;
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;

    // Guard system
    private bool isGuardOnCooldown = false;
    private float guardCooldownTimer = 0f;
    private GameObject activeGuard;

    // Genji dash slash system - Bulletproof implementation
    private bool isDashSlashing = false;
    private float dashSlashStartTime;
    private float lastDashSlashTime = -999f;
    private Vector2 dashStartPosition;
    private Vector2 dashTargetPosition;
    private Vector2 dashDirection;
    private HashSet<GameObject> hitTargetsThisDash = new HashSet<GameObject>();

    // Regular dash system
    private float lastRegularDashTime = -999f;
    private bool isRegularDashing = false;
    private float regularDashEndTime;

    // Input integration
    private FlexibleInputInjector flexInjector;
    private SimpleFlexibleInput flexInput;
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

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.3f;

        // Setup health system
        healthSystem = GetComponent<PlayerHealthSystem>();
        if (healthSystem == null)
        {
            Debug.LogWarning($"No PlayerHealthSystem found on {gameObject.name}! Adding one...");
            healthSystem = gameObject.AddComponent<PlayerHealthSystem>();
        }
    }

    void Start()
    {
        // Check for flexible input integration
        flexInjector = GetComponent<FlexibleInputInjector>();
        flexInput = GetComponent<SimpleFlexibleInput>();

        if (flexInjector != null || flexInput != null)
        {
            Debug.Log($"MeleeController - FLEXIBLE INPUT ACTIVE for Player {PlayerIndex}");
        }
        else
        {
            // Fallback to device system
            deviceInfo = GetComponent<PlayerDeviceInfo>();
            if (deviceInfo != null)
            {
                assignedDevice = deviceInfo.AssignedDevice;
                PlayerIndex = deviceInfo.PlayerIndex;
                Debug.Log($"MeleeController - Device: {assignedDevice?.name}, PlayerIndex: {PlayerIndex}");
            }
        }

        // Initialize health system
        if (healthSystem != null)
        {
            healthSystem.PlayerIndex = PlayerIndex;
        }
    }

    void Update()
    {
        // Skip input if dead
        if (healthSystem != null && !healthSystem.IsAlive)
        {
            return;
        }

        HandleInput();
        CheckGrounded();
        HandleJumping();
        HandleGuard();
        HandleGenjiDashSlash();
        HandleRegularDash();
        UpdateAnimator();
    }

    void HandleInput()
    {
        // Reset input states
        jumpPressed = dashPressed = guardPressed = attackPressed = false;

        // Priority 1: Flexible input injector
        if (flexInjector != null)
        {
            moveInput = flexInjector.GetMoveInput();
            aimDirection = flexInjector.GetAimInput();
            jumpPressed = flexInjector.GetJumpPressed();
            dashPressed = flexInjector.GetAction2Pressed();
            guardPressed = flexInjector.GetAction1Pressed();
            attackPressed = flexInjector.GetAction1Pressed();
            return;
        }

        // Priority 2: Simple flexible input
        if (flexInput != null)
        {
            moveInput = flexInput.moveInput;
            aimDirection = flexInput.aimInput;
            jumpPressed = flexInput.jumpPressed;
            dashPressed = flexInput.action2Pressed;
            guardPressed = flexInput.action1Pressed;
            attackPressed = flexInput.action1Pressed;
            return;
        }

        // Fallback: Direct input handling
        HandleDirectInput();
    }

    void HandleDirectInput()
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
        // Movement
        Vector2 leftStick = gamepad.leftStick.ReadValue();
        Vector2 dpad = gamepad.dpad.ReadValue();
        moveInput = leftStick.magnitude > 0.1f ? leftStick : dpad;

        // Aiming
        Vector2 rightStick = gamepad.rightStick.ReadValue();
        if (rightStick.magnitude > 0.1f)
            aimDirection = rightStick;
        else if (moveInput.magnitude > 0.1f)
            aimDirection = moveInput;

        // Buttons
        jumpPressed = gamepad.buttonSouth.wasPressedThisFrame; // A button
        dashPressed = gamepad.buttonEast.wasPressedThisFrame;  // B button (regular dash)

        // SWAPPED CONTROLS:
        guardPressed = gamepad.buttonNorth.wasPressedThisFrame;  // Y button (Triangle) - GUARD
        attackPressed = gamepad.buttonWest.wasPressedThisFrame; // X button (Square) - GENJI SLASH
    }

    void HandleKeyboardInput()
    {
        if (keyboard == null) return;

        Vector2 keyboardMove = Vector2.zero;

        if (PlayerIndex == 0)
        {
            // Player 1 - WASD
            if (keyboard.aKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.dKey.isPressed) keyboardMove.x += 1;
            if (keyboard.wKey.isPressed) keyboardMove.y += 1;
            if (keyboard.sKey.isPressed) keyboardMove.y -= 1;

            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            dashPressed = keyboard.leftShiftKey.wasPressedThisFrame;
            guardPressed = keyboard.leftCtrlKey.wasPressedThisFrame;
            attackPressed = keyboard.fKey.wasPressedThisFrame;
        }
        else
        {
            // Player 2 - Arrow keys
            if (keyboard.leftArrowKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.rightArrowKey.isPressed) keyboardMove.x += 1;
            if (keyboard.upArrowKey.isPressed) keyboardMove.y += 1;
            if (keyboard.downArrowKey.isPressed) keyboardMove.y -= 1;

            jumpPressed = keyboard.enterKey.wasPressedThisFrame;
            dashPressed = keyboard.rightShiftKey.wasPressedThisFrame;
            guardPressed = keyboard.rightCtrlKey.wasPressedThisFrame;
            attackPressed = keyboard.numpad0Key.wasPressedThisFrame;
        }

        moveInput = keyboardMove;
        if (moveInput.magnitude > 0.1f)
            aimDirection = moveInput;
    }

    void FixedUpdate()
    {
        if (!isDashSlashing && !isRegularDashing && (healthSystem == null || healthSystem.IsAlive))
            Move();

        ApplyBetterJump();

        // Handle dash slash movement in physics update
        if (isDashSlashing)
        {
            UpdateDashSlashMovement();
        }
    }

    public void Initialize(int playerIndex)
    {
        PlayerIndex = playerIndex;
        Debug.Log($"MeleeController initialized - PlayerIndex: {playerIndex}");

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
        if (isGuardOnCooldown)
        {
            guardCooldownTimer -= Time.deltaTime;
            if (guardCooldownTimer <= 0f)
            {
                isGuardOnCooldown = false;
            }
        }

        if (guardPressed && !isGuardOnCooldown && activeGuard == null)
        {
            SpawnGuard();
        }
    }

    void SpawnGuard()
    {
        if (guardPrefab == null || guardSpawnPoint == null) return;

        GameObject guard = Instantiate(guardPrefab, guardSpawnPoint.position, Quaternion.identity);
        GuardBehavior behavior = guard.GetComponent<GuardBehavior>();
        if (behavior != null)
        {
            behavior.SetOwner(this);
            behavior.SetLifetime(guardLifetime);
        }
        activeGuard = guard;
    }

    public void OnGuardDestroyed()
    {
        activeGuard = null;
        isGuardOnCooldown = true;
        guardCooldownTimer = guardCooldown;
    }

    // BULLETPROOF GENJI DASH SLASH
    void HandleGenjiDashSlash()
    {
        // Start dash slash
        if (attackPressed && CanStartDashSlash())
        {
            StartDashSlash();
        }

        // Continue dash slash
        if (isDashSlashing)
        {
            ContinueDashSlash();
        }
    }

    bool CanStartDashSlash()
    {
        return !isDashSlashing &&
               !isRegularDashing &&
               Time.time >= lastDashSlashTime + dashCooldown;
    }

    void StartDashSlash()
    {
        // Calculate dash direction
        dashDirection = aimDirection.magnitude > 0.1f ? aimDirection.normalized : Vector2.right;

        // Set facing direction
        if (dashDirection.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(dashDirection.x), 1, 1);

        // Setup dash parameters
        dashStartPosition = transform.position;
        dashTargetPosition = dashStartPosition + dashDirection * dashDistance;

        // Initialize dash state
        isDashSlashing = true;
        dashSlashStartTime = Time.time;
        lastDashSlashTime = Time.time;
        hitTargetsThisDash.Clear();

        // Audio feedback
        PlayDashSlashSound();

        // Animation
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        Debug.Log($"Player {PlayerIndex} started Genji dash slash!");
    }

    void ContinueDashSlash()
    {
        float elapsed = Time.time - dashSlashStartTime;

        // Check if dash is complete
        if (elapsed >= dashDuration)
        {
            EndDashSlash();
            return;
        }

        // Continuous damage detection during dash
        PerformDashSlashDamage();
    }

    void UpdateDashSlashMovement()
    {
        if (!isDashSlashing) return;

        float elapsed = Time.time - dashSlashStartTime;
        float progress = elapsed / dashDuration;
        progress = Mathf.Clamp01(progress);

        // Smooth dash movement
        Vector2 currentPosition = Vector2.Lerp(dashStartPosition, dashTargetPosition, progress);
        rb.MovePosition(currentPosition);
    }

    void PerformDashSlashDamage()
    {
        // Create attack area
        Vector2 currentPos = transform.position;
        Vector2 boxSize = new Vector2(slashWidth, slashWidth);

        // Find all potential targets
        Collider2D[] potentialTargets = Physics2D.OverlapBoxAll(currentPos, boxSize, 0f, enemyLayers);

        foreach (Collider2D target in potentialTargets)
        {
            // Validate target
            if (!IsValidTarget(target)) continue;

            // Prevent multiple hits per dash
            if (hitTargetsThisDash.Contains(target.gameObject)) continue;

            // Deal damage
            DealDamageToTarget(target);

            // Mark as hit
            hitTargetsThisDash.Add(target.gameObject);
        }
    }

    bool IsValidTarget(Collider2D target)
    {
        return target != null &&
               target.gameObject != gameObject &&
               target.CompareTag("Enemy");
    }

    void DealDamageToTarget(Collider2D target)
    {
        // Try IDamageable interface first
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(slashDamage);
            Debug.Log($"Genji dash hit {target.name} for {slashDamage} damage!");
        }
        else
        {
            // Fallback to UniversalEnemyHealth
            UniversalEnemyHealth enemyHealth = target.GetComponent<UniversalEnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(slashDamage);
                Debug.Log($"Genji dash hit {target.name} via UniversalEnemyHealth!");
            }
        }

        // Apply effects
        ApplyKnockback(target);
        SpawnHitEffect(target.transform.position);
        PlayHitSound();
    }

    void ApplyKnockback(Collider2D target)
    {
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 knockbackDir = dashDirection;
            targetRb.AddForce(knockbackDir * 8f, ForceMode2D.Impulse);
        }
    }

    void SpawnHitEffect(Vector3 position)
    {
        if (slashEffectPrefab != null)
        {
            GameObject effect = Instantiate(slashEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 1f);
        }
    }

    void PlayDashSlashSound()
    {
        if (dashSlashSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dashSlashSound, dashVolume);
        }
        else if (slashSounds.Length > 0 && audioSource != null)
        {
            AudioClip sound = slashSounds[Random.Range(0, slashSounds.Length)];
            audioSource.PlayOneShot(sound, slashVolume);
        }
    }

    void PlayHitSound()
    {
        if (slashSounds.Length > 0 && audioSource != null)
        {
            AudioClip sound = slashSounds[Random.Range(0, slashSounds.Length)];
            audioSource.PlayOneShot(sound, slashVolume * 0.7f);
        }
    }

    void EndDashSlash()
    {
        isDashSlashing = false;
        Debug.Log($"Player {PlayerIndex} Genji dash completed - hit {hitTargetsThisDash.Count} enemies");
    }

    // REGULAR DASH (Non-damage)
    void HandleRegularDash()
    {
        if (dashPressed && CanStartRegularDash())
        {
            StartRegularDash();
        }

        if (isRegularDashing && Time.time > regularDashEndTime)
        {
            isRegularDashing = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    bool CanStartRegularDash()
    {
        return !isDashSlashing &&
               !isRegularDashing &&
               Time.time >= lastRegularDashTime + regularDashCooldown;
    }

    void StartRegularDash()
    {
        Vector2 dashDir = aimDirection.magnitude > 0.1f ? aimDirection : Vector2.right;

        isRegularDashing = true;
        regularDashEndTime = Time.time + regularDashDuration;
        lastRegularDashTime = Time.time;

        rb.linearVelocity = dashDir.normalized * regularDashForce;

        if (dashDir.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(dashDir.x), 1, 1);

        if (dashSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dashSound, dashVolume);
        }

        if (animator != null)
            animator.SetTrigger("Dash");
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("xVelocity", Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
            animator.SetBool("isGrounded", isGrounded);
            animator.SetBool("isJumping", !isGrounded && rb.linearVelocity.y > 0.1f);
            animator.SetBool("hasGuard", activeGuard != null);

            if (healthSystem != null)
            {
                animator.SetBool("isInvulnerable", healthSystem.IsInvulnerable);
            }
        }
    }

    // Damage handling
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("EnemyProjectile"))
        {
            Bullet bullet = other.GetComponent<Bullet>();
            if (bullet != null && healthSystem != null)
            {
                healthSystem.TakeDamage(bullet.damage);
            }
        }

        // No contact damage during dash slash (invincible frames)
        if (other.CompareTag("Enemy") && activeGuard == null && !isDashSlashing)
        {
            if (healthSystem != null)
            {
                float contactDamage = 15f;
                healthSystem.TakeDamage(contactDamage);

                Vector2 pushDirection = (transform.position - other.transform.position).normalized;
                if (rb != null)
                {
                    rb.AddForce(pushDirection * 8f, ForceMode2D.Impulse);
                }
            }
        }
    }

    // Public accessors
    public bool IsGuardActive() => activeGuard != null;
    public bool IsGuardOnCooldown() => isGuardOnCooldown;
    public float GetGuardCooldownRemaining() => isGuardOnCooldown ? guardCooldownTimer : 0f;
    public bool IsDashing() => isRegularDashing;
    public bool IsDashSlashing() => isDashSlashing;
    public PlayerHealthSystem GetHealthSystem() => healthSystem;

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Dash slash visualization
        if (isDashSlashing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(slashWidth, slashWidth, 0));
            Gizmos.DrawLine(dashStartPosition, dashTargetPosition);
        }
        else
        {
            Vector3 dashDir = aimDirection.magnitude > 0.1f ? (Vector3)aimDirection : Vector3.right;
            Vector3 dashEnd = transform.position + dashDir.normalized * dashDistance;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, dashEnd);
            Gizmos.DrawWireCube(transform.position, new Vector3(slashWidth, slashWidth, 0));
        }

        // Guard visualization
        if (guardSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(guardSpawnPoint.position, Vector3.one * 0.3f);
        }

        // Cooldown indicators
        if (isGuardOnCooldown)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }

        if (Time.time < lastDashSlashTime + dashCooldown)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
        }
    }
}