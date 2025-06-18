using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Enhanced melee controller optimized for isolated input system
/// Each player gets completely independent gamepad + keyboard controls
/// Features bulletproof Genji-style dash slash and guard system
/// </summary>
public class Player_Melee_Controller1 : MonoBehaviour
{
    public int PlayerIndex { get; set; }

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
    private Vector2 aimDirection = Vector2.right;

    // ISOLATED INPUT SYSTEM
    private FlexibleInputInjector inputInjector;
    private bool hasIsolatedInput = false;

    // Input states
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool guardPressed;
    private bool attackPressed;

    // Fallback input (only used if isolated system fails)
    private SimpleFlexibleInput flexInput;
    private PlayerDeviceInfo deviceInfo;
    private InputDevice assignedDevice;
    private Keyboard keyboard;

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
        // PRIORITY: Always check for isolated input components first (they may be added later)
        CheckForIsolatedInputComponents();

        // Initialize health system
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
            Debug.Log($"🗡️ Melee Player {PlayerIndex} - ISOLATED INPUT ACTIVE ({inputInjector.GetCurrentInputMethod()})");
            return;
        }

        // Check for simple flexible input (medium priority)
        flexInput = GetComponent<SimpleFlexibleInput>();
        if (flexInput != null)
        {
            Debug.Log($"🗡️ Melee Player {PlayerIndex} - FLEXIBLE INPUT ACTIVE");
            return;
        }

        // Fallback to original device system (lowest priority)
        deviceInfo = GetComponent<PlayerDeviceInfo>();
        if (deviceInfo != null)
        {
            assignedDevice = deviceInfo.AssignedDevice;
            PlayerIndex = deviceInfo.PlayerIndex;
            Debug.Log($"🗡️ Melee Player {PlayerIndex} - DEVICE INPUT: {assignedDevice?.name}");
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
        aimDirection = inputInjector.GetAimInput();

        // Map isolated input to melee actions
        jumpPressed = inputInjector.GetJumpPressed();
        guardPressed = inputInjector.GetAction1Pressed();  // Action1 = Guard
        dashPressed = inputInjector.GetAction2Pressed();   // Action2 = Regular Dash
        attackPressed = inputInjector.GetAction1Pressed(); // Action1 also triggers Genji Slash

        // Ensure aim direction is valid
        if (aimDirection.magnitude < 0.1f && moveInput.magnitude > 0.1f)
        {
            aimDirection = moveInput;
        }
        else if (aimDirection.magnitude < 0.1f)
        {
            aimDirection = Vector2.right; // Default facing direction
        }

        // Debug: Show input activity
        if (moveInput.magnitude > 0.1f || guardPressed || dashPressed || attackPressed || jumpPressed)
        {
            Debug.Log($"🗡️ Melee P{PlayerIndex} ({inputInjector.GetCurrentInputMethod()}): Move={moveInput:F1}, Guard={guardPressed}, Dash={dashPressed}, Attack={attackPressed}");
        }
    }

    void HandleFlexibleInput()
    {
        moveInput = flexInput.moveInput;
        aimDirection = flexInput.aimInput;
        jumpPressed = flexInput.jumpPressed;
        guardPressed = flexInput.action1Pressed;
        dashPressed = flexInput.action2Pressed;
        attackPressed = flexInput.action1Pressed;

        if (aimDirection.magnitude < 0.1f && moveInput.magnitude > 0.1f)
        {
            aimDirection = moveInput;
        }
        else if (aimDirection.magnitude < 0.1f)
        {
            aimDirection = Vector2.right;
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
        guardPressed = gamepad.buttonNorth.wasPressedThisFrame;  // Y button - GUARD
        attackPressed = gamepad.buttonWest.wasPressedThisFrame; // X button - GENJI SLASH
    }

    void HandleKeyboardInput()
    {
        if (keyboard == null) return;

        Vector2 keyboardMove = Vector2.zero;

        // Player-specific keyboard controls
        if (PlayerIndex == 0)
        {
            // Player 0: WASD + Space/Ctrl/Shift/F
            if (keyboard.aKey.isPressed) keyboardMove.x -= 1;
            if (keyboard.dKey.isPressed) keyboardMove.x += 1;
            if (keyboard.wKey.isPressed) keyboardMove.y += 1;
            if (keyboard.sKey.isPressed) keyboardMove.y -= 1;

            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            dashPressed = keyboard.leftShiftKey.wasPressedThisFrame;
            guardPressed = keyboard.leftCtrlKey.wasPressedThisFrame;
            attackPressed = keyboard.fKey.wasPressedThisFrame;
        }
        else if (PlayerIndex == 1)
        {
            // Player 1: Arrow Keys + Enter/RCtrl/RShift/Numpad0
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

    // Debug info
    void OnGUI()
    {
        if (Application.isEditor && hasIsolatedInput)
        {
            float yOffset = PlayerIndex * 25 + 450;

            string inputInfo = inputInjector != null ? inputInjector.GetCurrentInputMethod() : "Unknown";
            GUI.Label(new Rect(10, yOffset, 300, 20),
                $"Melee P{PlayerIndex}: {inputInfo} | Move: {moveInput:F1}");
        }
    }

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

        // Input status
        if (hasIsolatedInput)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.3f);
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