using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PredatorEventManager : MonoBehaviour
{
    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float minHideCountdownTime = 5f;
    [Min(0f)]
    [SerializeField] private float maxHideCountdownTime = 10f;
    [Min(0.1f)]
    [SerializeField] private float drawingLimitTime = 30f;
    [Min(0f)]
    [FormerlySerializedAs("postDrawingFreezeTime")]
    [SerializeField] private float resultAnimationDuration = 5f;
    [Min(0f)]
    [SerializeField] private float camouflageFadeDuration = 1.5f;
    [Min(0f)]
    [SerializeField] private float predatorSearchTime = 5f;

    [Header("Drawing")]
    [SerializeField] private GameObject drawingPanel;
    [SerializeField] private DrawingTest drawingManager;
    [SerializeField] private BackgroundSampler backgroundSampler;
    [SerializeField] private PlayerCamouflageApplier camouflageApplier;

    [Header("Player")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCamouflageController
        playerCamouflageController;

    [Header("Predator")]
    [SerializeField] private PredatorController predator;
    [Min(0f)]
    [SerializeField] private float spawnHorizontalOffset = 8f;
    [SerializeField] private float spawnYOffset;
    [SerializeField] private bool spawnFromRandomSide = true;
    [Min(0f)]
    [SerializeField] private float predatorMoveSpeed = 2f;
    [Min(0f)]
    [SerializeField] private float predatorMoveRange = 2f;
    [Min(0f)]
    [SerializeField] private float predatorMinimumDistance = 2.5f;

    [Header("UI")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private TMP_Text drawingTimerText;
    [SerializeField] private GameOverUIController gameOverUIController;

    [Header("Development")]
    [SerializeField] private bool enableDeveloperResultShortcuts = true;
    [SerializeField] private bool enableDebugLogs;

    private Coroutine eventCoroutine;
    private bool isEventActive;
    private bool isDrawingActive;
    private bool submitRequested;
    private bool? developerForcedResult;

    public bool IsEventActive => isEventActive;
    public bool IsDrawingActive => isDrawingActive;

    private void Awake()
    {
        if (playerController == null &&
            playerCamouflageController != null)
        {
            playerController =
                playerCamouflageController.GetComponent<PlayerController>();
        }

        if (playerCamouflageController == null &&
            playerController != null)
        {
            playerCamouflageController =
                playerController.GetComponent<PlayerCamouflageController>();
        }

        SetDrawingPanelActive(false);
        SetTextVisible(warningText, false);
        SetTextVisible(drawingTimerText, false);

        if (predator != null)
        {
            predator.Hide();
        }
    }

    private void Update()
    {
        if (!enableDeveloperResultShortcuts ||
            !isDrawingActive ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame ||
            Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            ForceDeveloperResult(true);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame ||
                 Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            ForceDeveloperResult(false);
        }
    }

    public bool StartPredatorEvent()
    {
        if (isEventActive)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorEventManager] Event start ignored because an event is already active.",
                this);
            return false;
        }

        eventCoroutine = StartCoroutine(PredatorEventRoutine());
        return true;
    }

    public void SubmitEventDrawing()
    {
        if (!isEventActive || !isDrawingActive)
        {
            Debug.LogWarning(
                "[PredatorEventManager] Submit ignored because event drawing is not active.",
                this);
            return;
        }

        submitRequested = true;
    }

    public void ToggleDrawingPanel()
    {
        if (!isEventActive || !isDrawingActive)
        {
            return;
        }

        if (drawingPanel == null)
        {
            Debug.LogWarning(
                "[PredatorEventManager] Drawing Panel is not assigned.",
                this);
            return;
        }

        bool shouldShowPanel = !drawingPanel.activeSelf;
        SetDrawingPanelActive(shouldShowPanel);

        GameplayDebug.Log(enableDebugLogs,
            $"[PredatorEventManager] Event Drawing Panel toggled: {(shouldShowPanel ? "Open" : "Closed")}. Drawing data and timer were preserved.",
            this);
    }

    public void CancelEvent()
    {
        if (eventCoroutine != null)
        {
            StopCoroutine(eventCoroutine);
            eventCoroutine = null;
        }

        ResetEventPresentation();
    }

    private IEnumerator PredatorEventRoutine()
    {
        isEventActive = true;
        submitRequested = false;
        developerForcedResult = null;

        Debug.Log("Predator Event Started", this);

        SetDrawingPanelActive(false);
        SetTextVisible(drawingTimerText, false);

        if (playerCamouflageController != null)
        {
            playerCamouflageController.ForceCancelCamouflage();
        }

        if (predator != null)
        {
            predator.Hide();
        }

        float countdownDuration = Random.Range(
            Mathf.Min(minHideCountdownTime, maxHideCountdownTime),
            Mathf.Max(minHideCountdownTime, maxHideCountdownTime));
        float countdownRemaining = countdownDuration;

        SetTextVisible(warningText, true);

        while (countdownRemaining > 0f)
        {
            if (warningText != null)
            {
                warningText.text =
                    $"포식자 접근 중\n{countdownRemaining:F1}";
            }

            countdownRemaining -= Time.deltaTime;
            yield return null;
        }

        SetTextVisible(warningText, false);
        SetDrawingPanelActive(false);

        yield return new WaitForEndOfFrame();

        CaptureAnswerTexture();
        UpdateDrawingPreviewDirection();

        if (playerController != null)
        {
            playerController.SetMovementLocked(true);
        }

        isDrawingActive = true;
        submitRequested = false;
        SetDrawingPanelActive(true);
        SetTextVisible(drawingTimerText, true);

        float drawingTimeRemaining = drawingLimitTime;

        while (!submitRequested && drawingTimeRemaining > 0f)
        {
            if (drawingTimerText != null)
            {
                drawingTimerText.text =
                    $"Time: {drawingTimeRemaining:F1}";
            }

            drawingTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        isDrawingActive = false;
        SetDrawingPanelActive(false);
        SetTextVisible(drawingTimerText, false);

        Texture2D drawingTexture = drawingManager != null
            ? drawingManager.GetCurrentDrawingTexture()
            : null;

        if (camouflageApplier != null)
        {
            camouflageApplier.ApplyCamouflage(drawingTexture);
        }
        else
        {
            Debug.LogWarning(
                "[PredatorEventManager] PlayerCamouflageApplier is not assigned.",
                this);
        }

        if (developerForcedResult.HasValue)
        {
            yield return CompleteDeveloperForcedResult(
                developerForcedResult.Value);
            yield break;
        }

        if (predator != null)
        {
            Vector3 spawnPosition =
                CalculatePredatorSpawnPosition(out float spawnSide);
            Vector3 searchTarget =
                CalculatePredatorSearchTarget(spawnSide);
            float moveDirectionX = -spawnSide;

            predator.ShowAtPosition(spawnPosition);
            predator.SetFacingDirection(moveDirectionX);
            predator.PlayWalk();

            yield return RunPredatorSearch(searchTarget);
        }
        else
        {
            Debug.LogWarning(
                "[PredatorEventManager] PredatorController is not assigned.",
                this);
        }

        bool isSuccess = EvaluateCamouflage();

        if (predator != null)
        {
            KeepPredatorAwayFromPlayer();

            if (isSuccess)
            {
                predator.PlayStop();
                GameplayDebug.Log(enableDebugLogs,
                    "[PredatorEventManager] Camouflage succeeded. Holding the predator stop animation.",
                    this);
            }
            else
            {
                predator.PlayFly();
                GameplayDebug.Log(enableDebugLogs,
                    "[PredatorEventManager] Camouflage failed. Holding the predator detection animation before Game Over.",
                    this);
            }
        }

        if (resultAnimationDuration > 0f)
        {
            yield return new WaitForSeconds(resultAnimationDuration);
        }

        if (camouflageApplier != null)
        {
            yield return camouflageApplier.FadeOutCamouflage(
                camouflageFadeDuration);
        }

        // 위장 오버레이가 완전히 사라진 뒤에만 이동을 다시 허용합니다.
        if (playerController != null)
        {
            playerController.SetMovementLocked(false);
        }

        if (predator != null)
        {
            predator.Hide();
        }

        eventCoroutine = null;
        isEventActive = false;

        if (!isSuccess)
        {
            HandleGameOver();
        }
    }

    private void ForceDeveloperResult(bool isSuccess)
    {
        developerForcedResult = isSuccess;
        submitRequested = true;

        GameplayDebug.Log(enableDebugLogs,
            $"[PredatorEventManager] Developer shortcut forced an immediate {(isSuccess ? "success" : "failure")} result.",
            this);
    }

    private IEnumerator CompleteDeveloperForcedResult(bool isSuccess)
    {
        developerForcedResult = null;
        isDrawingActive = false;
        SetDrawingPanelActive(false);
        SetTextVisible(drawingTimerText, false);

        if (resultAnimationDuration > 0f)
        {
            yield return new WaitForSeconds(resultAnimationDuration);
        }

        if (camouflageApplier != null)
        {
            yield return camouflageApplier.FadeOutCamouflage(
                camouflageFadeDuration);
        }

        if (isSuccess)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorEventManager] Developer shortcut completed the predator event as a success.",
                this);

            if (playerController != null)
            {
                playerController.SetMovementLocked(false);
            }

            if (predator != null)
            {
                predator.Hide();
            }
        }
        else
        {
            HandleGameOver();
        }

        eventCoroutine = null;
        isEventActive = false;
    }

    private Vector3 CalculatePredatorSpawnPosition(out float spawnSide)
    {
        Transform playerTransform = GetPlayerTransform();

        if (spawnFromRandomSide)
        {
            spawnSide = Random.value < 0.5f ? -1f : 1f;
        }
        else
        {
            spawnSide = -1f;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning(
                "[PredatorEventManager] Player Transform was not found. Predator will use the world origin as its spawn reference.",
                this);
            return new Vector3(
                spawnSide * spawnHorizontalOffset,
                spawnYOffset,
                predator != null ? predator.transform.position.z : 0f);
        }

        Vector3 playerPosition = playerTransform.position;
        return new Vector3(
            playerPosition.x + spawnSide * spawnHorizontalOffset,
            playerPosition.y + spawnYOffset,
            predator != null
                ? predator.transform.position.z
                : playerPosition.z);
    }

    private Vector3 CalculatePredatorSearchTarget(float spawnSide)
    {
        Transform playerTransform = GetPlayerTransform();

        if (playerTransform == null)
        {
            return predator != null
                ? predator.transform.position
                : Vector3.zero;
        }

        Vector3 playerPosition = playerTransform.position;
        float nearDistance = Mathf.Max(
            predatorMoveRange,
            predatorMinimumDistance);

        return new Vector3(
            playerPosition.x + spawnSide * nearDistance,
            playerPosition.y + spawnYOffset,
            predator != null
                ? predator.transform.position.z
                : playerPosition.z);
    }

    private IEnumerator RunPredatorSearch(Vector3 searchTarget)
    {
        float elapsed = 0f;
        float previousX = predator.transform.position.x;
        Transform playerTransform = GetPlayerTransform();
        float patrolSide =
            playerTransform != null &&
            searchTarget.x < playerTransform.position.x
                ? -1f
                : 1f;
        float nearDistance = Mathf.Max(
            predatorMoveRange,
            predatorMinimumDistance);
        float farDistance = Mathf.Max(
            spawnHorizontalOffset,
            nearDistance);
        bool movingToNearPoint = true;

        while (elapsed < predatorSearchTime)
        {
            if (Vector3.Distance(
                    predator.transform.position,
                    searchTarget) <= 0.05f &&
                playerTransform != null)
            {
                movingToNearPoint = !movingToNearPoint;
                Vector3 playerPosition = playerTransform.position;
                float targetDistance =
                    movingToNearPoint
                        ? nearDistance
                        : farDistance;

                searchTarget = new Vector3(
                    playerPosition.x +
                    patrolSide * targetDistance,
                    playerPosition.y + spawnYOffset,
                    predator.transform.position.z);
            }

            Vector3 nextPosition = Vector3.MoveTowards(
                predator.transform.position,
                searchTarget,
                predatorMoveSpeed * Time.deltaTime);
            nextPosition = ClampPredatorDistance(
                nextPosition,
                playerTransform,
                patrolSide);
            float moveDirectionX = nextPosition.x - previousX;

            predator.transform.position = nextPosition;
            predator.SetFacingDirection(moveDirectionX);
            previousX = nextPosition.x;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void KeepPredatorAwayFromPlayer()
    {
        Transform playerTransform = GetPlayerTransform();

        if (predator == null ||
            playerTransform == null)
        {
            return;
        }

        float side = predator.transform.position.x <
                     playerTransform.position.x
            ? -1f
            : 1f;

        predator.transform.position = ClampPredatorDistance(
            predator.transform.position,
            playerTransform,
            side);
        predator.SetFacingDirection(
            playerTransform.position.x -
            predator.transform.position.x);
    }

    private Vector3 ClampPredatorDistance(
        Vector3 predatorPosition,
        Transform playerTransform,
        float side)
    {
        if (playerTransform == null)
        {
            return predatorPosition;
        }

        float safeSide = side < 0f ? -1f : 1f;
        float minimumDistance =
            Mathf.Max(predatorMinimumDistance, 0f);
        float boundaryX =
            playerTransform.position.x +
            safeSide * minimumDistance;

        predatorPosition.x = safeSide < 0f
            ? Mathf.Min(predatorPosition.x, boundaryX)
            : Mathf.Max(predatorPosition.x, boundaryX);

        return predatorPosition;
    }

    private Transform GetPlayerTransform()
    {
        if (playerController != null)
        {
            return playerController.transform;
        }

        if (playerCamouflageController != null)
        {
            return playerCamouflageController.transform;
        }

        return null;
    }

    private void CaptureAnswerTexture()
    {
        if (backgroundSampler == null)
        {
            Debug.LogWarning(
                "[PredatorEventManager] BackgroundSampler is not assigned. Existing answer texture will be used.",
                this);
            return;
        }

        Texture2D capturedAnswer =
            backgroundSampler.CaptureBackgroundAroundPlayer();

        if (capturedAnswer == null)
        {
            return;
        }

        if (drawingManager != null)
        {
            drawingManager.SetAnswerTexture(capturedAnswer);
        }
        else
        {
            Destroy(capturedAnswer);
            Debug.LogError(
                "[PredatorEventManager] Drawing Manager is not assigned.",
                this);
        }
    }

    private void UpdateDrawingPreviewDirection()
    {
        if (drawingManager == null)
        {
            return;
        }

        PlayerController directionSource = playerController;

        if (directionSource == null &&
            playerCamouflageController != null)
        {
            directionSource =
                playerCamouflageController.GetComponent<PlayerController>();
        }

        if (directionSource == null)
        {
            Debug.LogWarning(
                "[PredatorEventManager] PlayerController was not found. Canvas preview direction was not updated.",
                this);
            return;
        }

        drawingManager.SetPreviewFacingDirection(
            directionSource.IsFacingRight);
    }

    private bool EvaluateCamouflage()
    {
        if (drawingManager == null)
        {
            Debug.LogError(
                "[PredatorEventManager] Drawing Manager is not assigned.",
                this);
            return false;
        }

        if (!drawingManager.TryEvaluateCamouflage(
                out float score,
                out string result))
        {
            return false;
        }

        bool isSuccess =
            result == "Perfect" ||
            result == "Success";

        GameplayDebug.Log(enableDebugLogs,
            $"[PredatorEventManager] Delayed camouflage result: {result}, Score: {score:F2}, Success: {isSuccess}.",
            this);

        return isSuccess;
    }

    private void HandleGameOver()
    {
        if (gameOverUIController == null)
        {
            gameOverUIController =
                FindFirstObjectByType<GameOverUIController>();
        }

        if (gameOverUIController != null)
        {
            gameOverUIController.ShowGameOver();
            return;
        }

        Debug.LogError(
            "[PredatorEventManager] GameOverUIController was not found.",
            this);
    }

    private void ResetEventPresentation()
    {
        isEventActive = false;
        isDrawingActive = false;
        submitRequested = false;
        developerForcedResult = null;
        SetDrawingPanelActive(false);
        SetTextVisible(warningText, false);
        SetTextVisible(drawingTimerText, false);

        if (camouflageApplier != null)
        {
            camouflageApplier.ResetCamouflage();
        }

        if (playerController != null)
        {
            playerController.SetMovementLocked(false);
        }

        if (predator != null)
        {
            predator.Hide();
        }
    }

    private void SetDrawingPanelActive(bool isActive)
    {
        if (drawingPanel != null)
        {
            drawingPanel.SetActive(isActive);
        }
    }

    private void SetTextVisible(TMP_Text text, bool isVisible)
    {
        if (text == null)
        {
            return;
        }

        text.text = string.Empty;
        text.gameObject.SetActive(isVisible);
    }

    private void OnDisable()
    {
        if (isEventActive)
        {
            CancelEvent();
        }
    }
}
