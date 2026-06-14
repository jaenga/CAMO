using UnityEngine;

public class HideZoneTrigger : MonoBehaviour
{
    [SerializeField] private bool enableDebugLogs;
    [SerializeField] private PredatorEventManager predatorEventManager;

    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || !other.CompareTag("Player"))
        {
            return;
        }

        hasTriggered = true;

        if (predatorEventManager == null)
        {
            predatorEventManager =
                FindFirstObjectByType<PredatorEventManager>();
        }

        if (predatorEventManager == null)
        {
            Debug.LogError(
                "[HideZoneTrigger] PredatorEventManager is not assigned.",
                this);
            return;
        }

        predatorEventManager.StartPredatorEvent();
        GameplayDebug.Log(enableDebugLogs,
            $"[HideZoneTrigger] Predator event requested by '{name}'.",
            this);
    }
}
