using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class OneWayTopTrigger : MonoBehaviour
{
    [SerializeField] private bool enableDebugLogs;
    private BoxCollider2D platformTrigger;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        platformTrigger = GetComponent<BoxCollider2D>();

        if (!platformTrigger.isTrigger)
        {
            Debug.LogWarning(
                "[OneWayTopTrigger] BoxCollider2D was changed to a Trigger.",
                this);
            platformTrigger.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        LogTriggerState("Enter", other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody2D playerRigidbody = other.attachedRigidbody;
        PlayerController playerController =
            other.GetComponentInParent<PlayerController>();

        if (playerRigidbody == null || playerController == null)
        {
            return;
        }

        Bounds platformBounds = platformTrigger.bounds;
        Bounds playerBounds = other.bounds;
        LogTriggerState("Stay", other);

        if (playerRigidbody.linearVelocity.y > 0f)
        {
            playerController.SetExternalGrounded(false);
            return;
        }

        float verticalCorrection =
            platformBounds.max.y - playerBounds.min.y;

        playerRigidbody.position +=
            Vector2.up * verticalCorrection;
        playerRigidbody.linearVelocity = new Vector2(
            playerRigidbody.linearVelocity.x,
            0f);
        playerController.SetExternalGrounded(true);
    }

    private void LogTriggerState(string triggerEvent, Collider2D other)
    {
        Rigidbody2D playerRigidbody = other.attachedRigidbody;
        float velocityY = playerRigidbody != null
            ? playerRigidbody.linearVelocity.y
            : 0f;
        float platformTopY = platformTrigger.bounds.max.y;
        Vector3 playerPosition = playerRigidbody != null
            ? (Vector3)playerRigidbody.position
            : other.transform.position;

        GameplayDebug.Log(enableDebugLogs,
            $"[OneWayTopTrigger] {triggerEvent} - Player: {other.name}, " +
            $"Velocity Y: {velocityY:F2}, Platform Top Y: {platformTopY:F2}, " +
            $"Player Position: {playerPosition}",
            this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        PlayerController playerController =
            other.GetComponentInParent<PlayerController>();

        if (playerController != null)
        {
            playerController.SetExternalGrounded(false);
        }
    }
}
