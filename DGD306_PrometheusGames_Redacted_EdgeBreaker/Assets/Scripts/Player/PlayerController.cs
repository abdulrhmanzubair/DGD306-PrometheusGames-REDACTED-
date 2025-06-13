using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public int PlayerIndex { get; set; }
    private PlayerInput playerInput;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public GameObject grenadePrefab; // Changed from bullet2Prefab
    public Transform firePoint;
    public float fireRate = 0.2f;

    [Header("Grenade Settings")]
    public float grenadeCooldown = 3f;
    public float grenadeForce = 12f; // Forward launch force

    [Header("UI Elements")]
    public UnityEngine.UI.Image grenadeCooldownFill; // Radial fill image
    public UnityEngine.UI.Text grenadeCooldownText; // Text showing seconds remaining
    public Color cooldownColor = Color.red;
    public Color readyColor = Color.green;

    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private float nextFireTime;
    private float nextGrenadeTime; // Add grenade cooldown timer

    private Vector2 moveInput;
    private Vector2 aimInput = Vector2.right;
    private bool jumpPressed;
    private bool jumpRequested; // Add a separate jump request flag
    private bool fire1Pressed;
    private bool fire2Pressed;

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
            Debug.Log($"GunnerController - PlayerIndex: {PlayerIndex}, PlayerInput found with index: {playerInput.playerIndex}");

            // Set up input callbacks using the PlayerInput's actions
            var moveAction = playerInput.actions["Move"];
            var aimAction = playerInput.actions["Aim"];
            var jumpAction = playerInput.actions["Jump"];
            var fire1Action = playerInput.actions["Fire1"];
            var fire2Action = playerInput.actions["Fire2"];

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

            if (fire1Action != null)
            {
                fire1Action.performed += OnFire1;
                fire1Action.canceled += OnFire1Cancel;
            }

            if (fire2Action != null)
            {
                fire2Action.performed += OnFire2;
                fire2Action.canceled += OnFire2Cancel;
            }
        }
        else
        {
            Debug.LogError($"GunnerController - No PlayerInput found for PlayerIndex: {PlayerIndex}");
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

    void OnDestroy()
    {
        // Clean up input callbacks
        if (playerInput != null)
        {
            var moveAction = playerInput.actions["Move"];
            var aimAction = playerInput.actions["Aim"];
            var jumpAction = playerInput.actions["Jump"];
            var fire1Action = playerInput.actions["Fire1"];
            var fire2Action = playerInput.actions["Fire2"];

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

            if (fire1Action != null)
            {
                fire1Action.performed -= OnFire1;
                fire1Action.canceled -= OnFire1Cancel;
            }

            if (fire2Action != null)
            {
                fire2Action.performed -= OnFire2;
                fire2Action.canceled -= OnFire2Cancel;
            }
        }
    }

    // Input callback methods
    private void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnMoveCancel(InputAction.CallbackContext context) => moveInput = Vector2.zero;
    private void OnAim(InputAction.CallbackContext context) => aimInput = context.ReadValue<Vector2>();
    private void OnAimCancel(InputAction.CallbackContext context) => aimInput = Vector2.right;
    private void OnJump(InputAction.CallbackContext context)
    {
        jumpPressed = true;
        jumpRequested = true; // Set jump request when button is pressed
    }
    private void OnJumpCancel(InputAction.CallbackContext context) => jumpPressed = false;
    private void OnFire1(InputAction.CallbackContext context) => fire1Pressed = true;
    private void OnFire1Cancel(InputAction.CallbackContext context) => fire1Pressed = false;
    private void OnFire2(InputAction.CallbackContext context) => fire2Pressed = true;
    private void OnFire2Cancel(InputAction.CallbackContext context) => fire2Pressed = false;

    void Update()
    {
        CheckGrounded();
        HandleJumping();
        HandleShooting();
        UpdateGrenadeCooldownUI();
        UpdateAnimator();
    }

    public void Initialize(int playerIndex)
    {
        PlayerIndex = playerIndex;
        Debug.Log($"GunnerController initialized with PlayerIndex: {playerIndex}");
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