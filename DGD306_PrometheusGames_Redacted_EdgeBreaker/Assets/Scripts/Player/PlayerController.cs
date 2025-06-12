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
    private Vector2 aimInput = Vector2.right;
    private bool jumpPressed;
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
            PlayerInput[] allInputs = FindObjectsOfType<PlayerInput>();
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
    private void OnJump(InputAction.CallbackContext context) => jumpPressed = true;
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

    void HandleShooting()
    {
        if (Time.time >= nextFireTime)
        {
            if (fire1Pressed)
            {
                nextFireTime = Time.time + fireRate;
                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
                bullet.GetComponent<Bullet>().SetDirection(GetAimDirection());
                fire1Pressed = false;
            }
            else if (fire2Pressed)
            {
                nextFireTime = Time.time + fireRate;
                GameObject bullet2 = Instantiate(bullet2Prefab, firePoint.position, Quaternion.identity);
                bullet2.GetComponent<Bullet>().SetDirection(GetAimDirection());
                fire2Pressed = false;
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