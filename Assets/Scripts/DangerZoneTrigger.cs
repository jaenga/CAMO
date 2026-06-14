using UnityEngine;

public class DangerZoneTrigger : MonoBehaviour
{
    [SerializeField] private bool enableDebugLogs;
    [SerializeField] private PredatorController predatorController;

    // 같은 DangerZone이 한 번만 작동했는지 저장합니다.
    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger에 들어온 오브젝트를 Console에서 확인합니다.
        GameplayDebug.Log(enableDebugLogs,
            $"[DangerZoneTrigger] DangerZone entered - Object: '{other.name}', Tag: '{other.tag}'",
            this);

        // 이미 작동했거나 Player가 아닌 오브젝트라면 무시합니다.
        if (hasTriggered || !other.CompareTag("Player"))
        {
            return;
        }

        hasTriggered = true;

        if (predatorController == null)
        {
            Debug.LogError(
                "[DangerZoneTrigger] PredatorController is not assigned.",
                this);
            return;
        }

        predatorController.StartChase();
        GameplayDebug.Log(enableDebugLogs, "[DangerZoneTrigger] Predator chase started.", this);
    }
}
