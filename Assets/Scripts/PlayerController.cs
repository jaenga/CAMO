using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 8f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rigidbody2D;
    private float horizontalInput;
    private bool isGrounded;
    private Vector3 originalScale;

    private void Awake()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // Ground Check가 지정되어 있으면 작은 원 범위 안의 Ground 레이어를 검사합니다.
        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(
                         groundCheck.position,
                         groundCheckRadius,
                         groundLayer) != null;

        ReadMovementInput();

        // Space 키 입력과 실제 점프 성공 여부를 Console에서 확인합니다.
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (isGrounded)
            {
                rigidbody2D.linearVelocity = new Vector2(
                    rigidbody2D.linearVelocity.x,
                    jumpForce);

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
        transform.localScale = new Vector3(
            Mathf.Abs(originalScale.x) * direction,
            originalScale.y,
            originalScale.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        // Scene 뷰에서 실제 바닥 감지 범위를 확인할 수 있습니다.
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
