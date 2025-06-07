using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public Rigidbody2D rb;
    public Animator animator;

    private Vector2 movement;
    private bool isGrounded;

    void Update()
    {
        // Get input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // Normalize diagonal movement
        movement = new Vector2(moveX, moveY).normalized * moveSpeed;

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // Update animation
        if (animator != null)
        {
            animator.SetFloat("Speed", movement.magnitude);
        }
    }

    void FixedUpdate()
    {
        // Apply movement
        rb.linearVelocity = new Vector2(movement.x, rb.linearVelocity.y);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
