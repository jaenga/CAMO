using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    private const float GroundCheckDistance = 0.1f;
    private const float GroundCheckHeight = 0.05f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 8f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private Vector2 groundCheckBoxSize = new Vector2(0.6f, 0.2f);
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rigidbody2D;
    private Collider2D playerCollider;
    private Animator animator;
    private float horizontalInput;
    private bool isGrounded;
    private bool isExternallyGrounded;
    private bool isMovementLocked;
    private bool isControlLocked;
    private bool isFacingRight = true;
    private Vector3 originalScale;

    public bool IsFacingRight => isFacingRight;

    private void Awake()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        originalScale = transform.localScale;
        isFacingRight = originalScale.x >= 0f;
    }

    private void Update()
    {
        isGrounded = CheckGrounded() || isExternallyGrounded;

        if (isMovementLocked ||
            isControlLocked)
        {
            horizontalInput = 0f;
            return;
        }

        ReadMovementInput();

        if (animator != null)
        {
            animator.SetBool(
                "isMoving",
                !Mathf.Approximately(horizontalInput, 0f));
        }

        // Space 키 입력과 실제 점프 성공 여부를 Console에서 확인합니다.
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (isGrounded)
            {
                rigidbody2D.linearVelocity = new Vector2(
                    rigidbody2D.linearVelocity.x,
                    jumpForce);
                SoundManager.Instance?.PlayJump();

                Debug.Log("[PlayerController] Jump button pressed: Jump", this);
            }
            else
            {
                Debug.Log("[PlayerController] Jump button pressed: Jump blocked (not grounded)", this);
            }
        }

        FlipCharacter();
    }

    private void FixedUpdate()
    {
        if (isMovementLocked)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            return;
        }

        if (isControlLocked)
        {
            return;
        }

        // 물리 이동은 일정한 간격으로 실행되는 FixedUpdate에서 적용합니다.
        rigidbody2D.linearVelocity = new Vector2(
            horizontalInput * moveSpeed,
            rigidbody2D.linearVelocity.y);
    }

    private void ReadMovementInput()
    {
        horizontalInput = 0f;

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.aKey.wasPressedThisFrame ||
            Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            Debug.Log("[PlayerController] A / Left Arrow button pressed: Move Left", this);
        }

        if (Keyboard.current.dKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            Debug.Log("[PlayerController] D / Right Arrow button pressed: Move Right", this);
        }

        // A 또는 왼쪽 방향키를 누르면 왼쪽으로 이동합니다.
        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            horizontalInput -= 1f;
        }

        // D 또는 오른쪽 방향키를 누르면 오른쪽으로 이동합니다.
        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            horizontalInput += 1f;
        }
    }

    private void FlipCharacter()
    {
        if (Mathf.Approximately(horizontalInput, 0f))
        {
            return;
        }

        // 이동 방향에 맞춰 원래 크기를 유지하면서 X축 방향만 반전합니다.
        float direction = horizontalInput > 0f ? 1f : -1f;
        isFacingRight = horizontalInput > 0f;
        transform.localScale = new Vector3(
            Mathf.Abs(originalScale.x) * direction,
            originalScale.y,
            originalScale.z);
    }

    public void SetExternalGrounded(bool value)
    {
        isExternallyGrounded = value;
    }

    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        horizontalInput = 0f;

        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            if (locked)
            {
                animator.SetBool("isMoving", false);
            }

            animator.enabled = !locked;
        }
    }

    public void SetControlLocked(bool locked)
    {
        isControlLocked = locked;
        horizontalInput = 0f;

        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            animator.SetBool("isMoving", false);
        }
    }

    public void SetAutoWalkState(bool isWalking, float directionX)
    {
        if (!Mathf.Approximately(directionX, 0f))
        {
            SetFacingDirection(directionX);
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.SetBool("isMoving", isWalking);
        }
    }

    private void SetFacingDirection(float directionX)
    {
        float direction = directionX >= 0f ? 1f : -1f;
        isFacingRight = directionX >= 0f;
        transform.localScale = new Vector3(
            Mathf.Abs(originalScale.x) * direction,
            originalScale.y,
            originalScale.z);
    }

    private bool CheckGrounded()
    {
        if (playerCollider == null)
        {
            return false;
        }

        Bounds bounds = playerCollider.bounds;
        Vector2 castSize = new Vector2(
            bounds.size.x * 0.8f,
            GroundCheckHeight);
        float castDistance = bounds.extents.y + GroundCheckDistance;

        RaycastHit2D hit = Physics2D.BoxCast(
            bounds.center,
            castSize,
            0f,
            Vector2.down,
            castDistance,
            groundLayer);

        Debug.DrawRay(
            bounds.center,
            Vector2.down * castDistance,
            hit.collider != null ? Color.green : Color.red);

        return hit.collider != null;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D colliderToDraw =
            playerCollider != null
                ? playerCollider
                : GetComponent<Collider2D>();

        if (colliderToDraw == null)
        {
            return;
        }

        Bounds bounds = colliderToDraw.bounds;
        Vector2 castSize = new Vector2(
            bounds.size.x * 0.8f,
            GroundCheckHeight);
        float castDistance = bounds.extents.y + GroundCheckDistance;
        Vector3 castEnd = bounds.center + Vector3.down * castDistance;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(bounds.center, castEnd);
        Gizmos.DrawWireCube(castEnd, castSize);
    }
}
