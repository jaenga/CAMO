using UnityEngine;
using UnityEngine.InputSystem;

public class PuzzleEventTrigger : MonoBehaviour
{
    [Header("Predator Event")]
    [Tooltip("퍼즐 완료 후 추격을 시작할 PredatorController를 연결합니다.")]
    public PredatorController predator;

    [Tooltip("Timed Camouflage를 활성화할 PlayerCamouflageController를 연결합니다.")]
    public PlayerCamouflageController playerCamouflageController;

    // Player가 퍼즐의 Trigger 범위 안에 있는지 저장합니다.
    private bool isPlayerNear;

    // 같은 퍼즐이 여러 번 완료되는 것을 막습니다.
    private bool isPuzzleCompleted;

    private void Update()
    {
        if (!isPlayerNear ||
            isPuzzleCompleted ||
            Keyboard.current == null)
        {
            return;
        }

        // Player가 근처에 있을 때 E 키를 누르면 퍼즐을 완료합니다.
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log(
                "[PuzzleEventTrigger] E key pressed near the puzzle.",
                this);
            CompletePuzzle();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        isPlayerNear = true;

        if (playerCamouflageController == null)
        {
            playerCamouflageController =
                other.GetComponent<PlayerCamouflageController>();
        }

        if (playerCamouflageController != null)
        {
            playerCamouflageController.SetPuzzleInteractionActive(true);
        }

        Debug.Log(
            $"[PuzzleEventTrigger] Player '{other.name}' entered the puzzle area.",
            this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        isPlayerNear = false;

        if (playerCamouflageController != null)
        {
            playerCamouflageController.SetPuzzleInteractionActive(false);
        }

        Debug.Log(
            $"[PuzzleEventTrigger] Player '{other.name}' left the puzzle area.",
            this);
    }

    // 나중에 퍼즐 UI나 다른 완료 조건에서도 호출할 수 있는 공개 메서드입니다.
    public void CompletePuzzle()
    {
        if (isPuzzleCompleted)
        {
            Debug.Log(
                "[PuzzleEventTrigger] CompletePuzzle ignored because this puzzle is already completed.",
                this);
            return;
        }

        isPuzzleCompleted = true;
        Debug.Log("[PuzzleEventTrigger] Puzzle completed.", this);

        if (predator == null)
        {
            Debug.LogError(
                "[PuzzleEventTrigger] PredatorController is not assigned.",
                this);
        }
        else
        {
            predator.StartChase(true);
            Debug.Log("[PuzzleEventTrigger] Predator chase started.", this);
        }

        if (playerCamouflageController == null)
        {
            Debug.LogError(
                "[PuzzleEventTrigger] PlayerCamouflageController is not assigned.",
                this);
        }
        else
        {
            playerCamouflageController.SetPuzzleInteractionActive(false);
            playerCamouflageController.SuppressCamouflageInputUntilEReleased();
            playerCamouflageController.EnableTimedCamouflage();
        }
    }
}
