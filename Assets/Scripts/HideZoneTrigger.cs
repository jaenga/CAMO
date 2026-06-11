using UnityEngine;

public class HideZoneTrigger : MonoBehaviour
{
    [SerializeField] private GameObject drawingPanel;

    // 같은 HideZone이 한 번만 작동하도록 상태를 저장합니다.
    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(
            $"[HideZoneTrigger] Trigger entered - Object: '{other.name}', Tag: '{other.tag}'",
            this);

        // Player가 아닌 오브젝트와의 접촉은 무시합니다.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (hasTriggered)
        {
            Debug.Log("[HideZoneTrigger] This HideZone has already been triggered.", this);
            return;
        }

        hasTriggered = true;

        // Inspector에 연결한 DrawingPanel을 화면에 표시합니다.
        if (drawingPanel != null)
        {
            drawingPanel.SetActive(true);
            Debug.Log(
                $"[HideZoneTrigger] Drawing Panel '{drawingPanel.name}' activated.",
                drawingPanel);
        }
        else
        {
            Debug.LogError("[HideZoneTrigger] Drawing Panel is not assigned.", this);
        }

        // PlayerController를 비활성화하여 추가 이동 입력을 막습니다.
        PlayerController playerController = other.GetComponent<PlayerController>();

        if (playerController != null)
        {
            playerController.enabled = false;
            Debug.Log("[HideZoneTrigger] PlayerController disabled.", playerController);
        }
        else
        {
            Debug.LogWarning(
                "[HideZoneTrigger] PlayerController was not found on the Player.",
                other);
        }

        // 남아 있는 이동 속도를 제거하여 플레이어를 즉시 멈춥니다.
        Rigidbody2D playerRigidbody = other.attachedRigidbody;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            Debug.Log("[HideZoneTrigger] Player velocity reset to zero.", playerRigidbody);
        }
        else
        {
            Debug.LogWarning(
                "[HideZoneTrigger] Rigidbody2D was not found on the Player.",
                other);
        }
    }
}
