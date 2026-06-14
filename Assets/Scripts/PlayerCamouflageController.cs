using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerCamouflageController : MonoBehaviour
{
    private enum CamouflageModeType
    {
        None,
        Free,
        Timed
    }

    [Header("Camouflage UI")]
    [Tooltip("위장 그림을 그릴 DrawingPanel 오브젝트를 연결합니다.")]
    public GameObject drawingPanel;

    [Tooltip("Timed Camouflage의 남은 시간을 표시할 TMP Text를 연결합니다.")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("Timed Camouflage가 진행되는 동안 점멸할 경고 이미지를 연결합니다.")]
    [SerializeField] private Image timedWarningImage;

    [Min(0.1f)]
    [Tooltip("경고 이미지가 한 번 사라졌다 나타나는 데 걸리는 시간입니다.")]
    [SerializeField] private float warningPulseDuration = 1.2f;

    [Header("Drawing")]
    [Tooltip("SubmitDrawing()을 가진 DrawingTest 컴포넌트를 연결합니다.")]
    [SerializeField] private DrawingTest drawingManager;

    [Tooltip("Timed Camouflage 시작 전에 배경을 캡처할 BackgroundSampler를 연결합니다.")]
    [SerializeField] private BackgroundSampler backgroundSampler;

    [Tooltip("Submit된 그림을 Player 외형에 적용할 컴포넌트를 연결합니다.")]
    [SerializeField] private PlayerCamouflageApplier camouflageApplier;

    [Tooltip("포식자 이벤트 드로잉을 관리할 PredatorEventManager입니다.")]
    [SerializeField] private PredatorEventManager predatorEventManager;

    [Header("Camouflage Freeze")]
    [Min(0f)]
    [SerializeField] private float camouflageFreezeDuration = 5f;

    [Min(0f)]
    [SerializeField] private float camouflageFadeDuration = 1.5f;

    [Tooltip("위장 판정 중 비활성화할 PlayerController를 연결합니다.")]
    [SerializeField] private MonoBehaviour playerMovementController;

    [Tooltip("위장 판정 중 정지할 Player Animator를 연결합니다.")]
    [SerializeField] private Animator playerAnimator;

    [Header("Predator")]
    [Tooltip("Timed Camouflage 중 추격 속도를 낮출 Predator를 연결합니다.")]
    [SerializeField] private PredatorController predator;

    [Header("Timer")]
    [Min(0.1f)]
    public float camouflageTimeLimit = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private CamouflageModeType currentMode = CamouflageModeType.None;
    private bool isCamouflageMode;
    private bool isTimedCamouflageActive;
    private bool isDangerCamouflageAvailable;
    private bool hasSubmitted;
    private bool isTimerRunning;
    private bool isPuzzleInteractionActive;
    private bool suppressInputUntilEReleased;
    private Coroutine timedCamouflageCoroutine;
    private Coroutine camouflageFreezeCoroutine;
    private float remainingTime;
    private Color warningImageColor = Color.white;

    private void Start()
    {
        if (predatorEventManager == null)
        {
            predatorEventManager =
                FindFirstObjectByType<PredatorEventManager>();
        }

        ResetCamouflageState();
        SetDrawingPanelActive(false);
        ClearTimerText();
        InitializeWarningImage();

        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageController] Ready. Free camouflage is available.",
            this);
    }

    private void OnDisable()
    {
        SetWarningImageActive(false);
    }

    private void Update()
    {
        HandleCamouflageInput();

        if (currentMode != CamouflageModeType.Timed ||
            !isTimerRunning ||
            hasSubmitted)
        {
            return;
        }

        // Timed Camouflage는 패널을 닫아도 계속 진행됩니다.
        remainingTime = Mathf.Max(remainingTime - Time.deltaTime, 0f);
        UpdateWarningImagePulse();

        if (remainingTime <= 0f)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageController] Timer expired auto submit.",
                this);
            SubmitCamouflage(true);
        }
    }

    private void HandleCamouflageInput()
    {
        if (GameOverUIController.IsGameOverActive ||
            GoalZone.IsEndingActive)
        {
            SetDrawingPanelActive(false);
            return;
        }

        if (predatorEventManager != null &&
            predatorEventManager.IsEventActive)
        {
            if (predatorEventManager.IsDrawingActive &&
                Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                SoundManager.Instance?.PlayButton();
                predatorEventManager.ToggleDrawingPanel();
            }

            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        // 퍼즐 완료에 사용한 E 입력이 곧바로 위장 시작으로 이어지는 것을 막습니다.
        if (suppressInputUntilEReleased)
        {
            if (!Keyboard.current.eKey.isPressed)
            {
                suppressInputUntilEReleased = false;
            }

            return;
        }

        if (isPuzzleInteractionActive ||
            !Keyboard.current.rKey.wasPressedThisFrame)
        {
            return;
        }

        GameplayDebug.Log(enableDebugLogs, "[PlayerCamouflageController] R key pressed.", this);
        SoundManager.Instance?.PlayButton();

        if (!isCamouflageMode)
        {
            EnterCamouflageMode();
            return;
        }

        if (drawingPanel == null)
        {
            Debug.LogError(
                "[PlayerCamouflageController] Drawing Panel is not assigned.",
                this);
            return;
        }

        // 진행 중인 모드는 유지하고 DrawingPanel 표시만 전환합니다.
        bool shouldShowPanel = !drawingPanel.activeSelf;
        SetDrawingPanelActive(shouldShowPanel);

        GameplayDebug.Log(enableDebugLogs,
            $"[PlayerCamouflageController] Drawing Panel toggled: {(shouldShowPanel ? "Open" : "Closed")}.",
            this);
    }

    public void EnableTimedCamouflage()
    {
        if (currentMode == CamouflageModeType.Free)
        {
            // Free 모드는 제출하지 않고 UI와 상태만 종료합니다.
            SetDrawingPanelActive(false);
            ClearTimerText();
            SetWarningImageActive(false);

            isCamouflageMode = false;
            isTimerRunning = false;
            hasSubmitted = false;
            remainingTime = 0f;
            currentMode = CamouflageModeType.None;

            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageController] Active Free camouflage cleared before enabling Timed camouflage.",
                this);
        }

        isDangerCamouflageAvailable = true;
        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageController] Timed camouflage enabled.",
            this);
    }

    public void SetPuzzleInteractionActive(bool isActive)
    {
        isPuzzleInteractionActive = isActive;
    }

    public void SuppressCamouflageInputUntilEReleased()
    {
        suppressInputUntilEReleased = true;
    }

    public void EnterCamouflageMode()
    {
        if (predatorEventManager != null &&
            predatorEventManager.IsEventActive)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageController] Free camouflage is unavailable during a Predator event.",
                this);
            return;
        }

        if (isCamouflageMode)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageController] Camouflage mode is already active.",
                this);
            return;
        }

        if (drawingPanel == null)
        {
            Debug.LogError(
                "[PlayerCamouflageController] Cannot enter camouflage mode because Drawing Panel is not assigned.",
                this);
            return;
        }

        isCamouflageMode = true;
        hasSubmitted = false;
        UpdateDrawingPreviewDirection();

        if (isDangerCamouflageAvailable)
        {
            StartTimedCamouflage();
        }
        else
        {
            SetDrawingPanelActive(true);
            StartFreeCamouflage();
        }
    }

    private void StartFreeCamouflage()
    {
        currentMode = CamouflageModeType.Free;
        isTimerRunning = false;
        remainingTime = 0f;
        ClearTimerText();
        SetWarningImageActive(false);

        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageController] Free camouflage started.",
            this);
    }

    private void StartTimedCamouflage()
    {
        currentMode = CamouflageModeType.Timed;
        isTimedCamouflageActive = true;
        isTimerRunning = false;
        remainingTime = 0f;
        SetDrawingPanelActive(false);
        ClearTimerText();
        SetWarningImageActive(false);

        StopTimedCamouflageCoroutine();
        timedCamouflageCoroutine =
            StartCoroutine(StartTimedCamouflageRoutine());
    }

    private IEnumerator StartTimedCamouflageRoutine()
    {
        // DrawingPanel이 꺼진 화면을 끝까지 렌더링한 뒤 캡처합니다.
        yield return new WaitForEndOfFrame();

        if (!isCamouflageMode ||
            currentMode != CamouflageModeType.Timed ||
            hasSubmitted)
        {
            timedCamouflageCoroutine = null;
            yield break;
        }

        if (backgroundSampler != null)
        {
            Texture2D capturedAnswer =
                backgroundSampler.CaptureBackgroundAroundPlayer();

            if (capturedAnswer != null)
            {
                if (drawingManager != null)
                {
                    drawingManager.SetAnswerTexture(capturedAnswer);
                }
                else
                {
                    Destroy(capturedAnswer);
                    Debug.LogError(
                        "[PlayerCamouflageController] Drawing Manager is not assigned.",
                        this);
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "[PlayerCamouflageController] BackgroundSampler is not assigned. Existing answer texture will be used.",
                this);
        }

        SetDrawingPanelActive(true);
        isTimerRunning = true;
        remainingTime = camouflageTimeLimit;
        SetWarningImageActive(true);

        if (predator != null)
        {
            predator.SetCamouflageSlowMode(true);
        }
        else
        {
            Debug.LogWarning(
                "[PlayerCamouflageController] PredatorController is not assigned.",
                this);
        }

        timedCamouflageCoroutine = null;

        GameplayDebug.Log(enableDebugLogs,
            $"[PlayerCamouflageController] Timed camouflage started after background capture. Time limit: {camouflageTimeLimit:F1} seconds.",
            this);
    }

    // Submit 버튼의 OnClick에는 DrawingTest.SubmitDrawing 대신 이 메서드를 연결합니다.
    public void ManualSubmit()
    {
        GameplayDebug.Log(enableDebugLogs, "[PlayerCamouflageController] Manual Submit requested.", this);
        SoundManager.Instance?.PlaySubmit();

        if (predatorEventManager != null &&
            predatorEventManager.IsEventActive)
        {
            predatorEventManager.SubmitEventDrawing();
            return;
        }

        SubmitCamouflage(false);
    }

    public void ExitCamouflageMode()
    {
        ManualSubmit();
    }

    public void ForceCancelCamouflage()
    {
        bool hadActiveCamouflage =
            isCamouflageMode ||
            isTimedCamouflageActive ||
            isTimerRunning ||
            isDangerCamouflageAvailable ||
            camouflageFreezeCoroutine != null;

        StopTimedCamouflageCoroutine();
        StopCamouflageFreeze();

        if (camouflageApplier != null)
        {
            camouflageApplier.ResetCamouflage();
        }

        isCamouflageMode = false;
        isTimedCamouflageActive = false;
        isTimerRunning = false;
        isDangerCamouflageAvailable = false;
        isPuzzleInteractionActive = false;
        suppressInputUntilEReleased = false;
        hasSubmitted = true;
        remainingTime = 0f;
        currentMode = CamouflageModeType.None;

        SetDrawingPanelActive(false);
        ClearTimerText();
        SetWarningImageActive(false);

        if (predator != null)
        {
            predator.SetCamouflageSlowMode(false);
        }

        if (hadActiveCamouflage)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageController] Camouflage force cancelled.",
                this);
        }
    }

    private void SubmitCamouflage(bool isAutomatic)
    {
        if (!isCamouflageMode)
        {
            Debug.LogWarning(
                "[PlayerCamouflageController] Submit ignored because camouflage mode is not active.",
                this);
            return;
        }

        if (hasSubmitted)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageController] Duplicate Submit ignored.",
                this);
            return;
        }

        CamouflageModeType submittedMode = currentMode;
        StopTimedCamouflageCoroutine();
        hasSubmitted = true;
        isTimerRunning = false;

        bool shouldEvaluateCamouflage =
            predator != null &&
            predator.IsChasingOrThreatening;

        GameplayDebug.Log(enableDebugLogs,
            shouldEvaluateCamouflage
                ? "[PlayerCamouflageController] Combat camouflage submitted. Evaluating result."
                : "[PlayerCamouflageController] Free camouflage submitted. Pattern applied without evaluation.",
            this);

        if (drawingManager != null)
        {
            Texture2D drawingTexture =
                drawingManager.GetCurrentDrawingTexture();

            if (camouflageApplier != null)
            {
                camouflageApplier.ApplyCamouflage(drawingTexture);
                StartCamouflageFreeze();
            }
            else
            {
                Debug.LogWarning(
                    "[PlayerCamouflageController] PlayerCamouflageApplier is not assigned.",
                    this);
            }

            if (shouldEvaluateCamouflage)
            {
                drawingManager.SubmitDrawing();
            }
        }
        else
        {
            Debug.LogError(
                "[PlayerCamouflageController] Drawing Manager is not assigned.",
                this);
        }

        if (submittedMode == CamouflageModeType.Timed)
        {
            isTimedCamouflageActive = false;
            isDangerCamouflageAvailable = false;

            if (predator != null)
            {
                predator.SetCamouflageSlowMode(false);
            }

            GameplayDebug.Log(enableDebugLogs,
                $"[PlayerCamouflageController] Timed camouflage submitted ({(isAutomatic ? "Auto" : "Manual")}).",
                this);
        }

        SetDrawingPanelActive(false);
        ClearTimerText();
        SetWarningImageActive(false);
        isCamouflageMode = false;
        remainingTime = 0f;
        currentMode = CamouflageModeType.None;
    }

    private void ResetCamouflageState()
    {
        currentMode = CamouflageModeType.None;
        isCamouflageMode = false;
        isTimedCamouflageActive = false;
        isDangerCamouflageAvailable = false;
        hasSubmitted = false;
        isTimerRunning = false;
        isPuzzleInteractionActive = false;
        suppressInputUntilEReleased = false;
        remainingTime = 0f;
    }

    private void StopTimedCamouflageCoroutine()
    {
        if (timedCamouflageCoroutine == null)
        {
            return;
        }

        StopCoroutine(timedCamouflageCoroutine);
        timedCamouflageCoroutine = null;
    }

    private void StartCamouflageFreeze()
    {
        StopCamouflageFreeze();
        camouflageFreezeCoroutine =
            StartCoroutine(CamouflageFreezeRoutine());
    }

    private IEnumerator CamouflageFreezeRoutine()
    {
        SetPlayerFreezeState(true);

        yield return new WaitForSeconds(camouflageFreezeDuration);

        if (camouflageApplier != null)
        {
            yield return camouflageApplier.FadeOutCamouflage(
                camouflageFadeDuration);
        }

        SetPlayerFreezeState(false);
        camouflageFreezeCoroutine = null;
    }

    private void StopCamouflageFreeze()
    {
        if (camouflageFreezeCoroutine != null)
        {
            StopCoroutine(camouflageFreezeCoroutine);
            camouflageFreezeCoroutine = null;
        }

        SetPlayerFreezeState(false);
    }

    private void SetPlayerFreezeState(bool shouldFreeze)
    {
        PlayerController playerController =
            playerMovementController as PlayerController;

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.SetMovementLocked(shouldFreeze);
            return;
        }

        if (shouldFreeze)
        {
            Rigidbody2D playerRigidbody = GetPlayerRigidbody();

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
            }
        }

        if (playerAnimator != null)
        {
            playerAnimator.enabled = !shouldFreeze;
        }

        if (playerMovementController != null)
        {
            playerMovementController.enabled = !shouldFreeze;
        }
    }

    private Rigidbody2D GetPlayerRigidbody()
    {
        if (playerMovementController != null)
        {
            Rigidbody2D movementRigidbody =
                playerMovementController.GetComponent<Rigidbody2D>();

            if (movementRigidbody != null)
            {
                return movementRigidbody;
            }
        }

        if (playerAnimator != null)
        {
            return playerAnimator.GetComponent<Rigidbody2D>();
        }

        return null;
    }

    private void SetDrawingPanelActive(bool isActive)
    {
        if (drawingPanel != null)
        {
            drawingPanel.SetActive(isActive);
        }
    }

    private void UpdateDrawingPreviewDirection()
    {
        if (drawingManager == null)
        {
            return;
        }

        PlayerController playerController =
            GetComponent<PlayerController>();

        if (playerController != null)
        {
            drawingManager.SetPreviewFacingDirection(
                playerController.IsFacingRight);
        }
    }

    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            timerText.text = $"Time: {remainingTime:F1}";
        }
    }

    private void ClearTimerText()
    {
        if (timerText != null)
        {
            timerText.text = string.Empty;
        }
    }

    private void InitializeWarningImage()
    {
        if (timedWarningImage == null)
        {
            return;
        }

        warningImageColor = timedWarningImage.color;
        SetWarningImageActive(false);
    }

    private void UpdateWarningImagePulse()
    {
        if (timedWarningImage == null)
        {
            return;
        }

        float duration = Mathf.Max(warningPulseDuration, 0.1f);
        float pulse = Mathf.PingPong(Time.unscaledTime * 2f / duration, 1f);
        float alpha = Mathf.SmoothStep(0f, warningImageColor.a, pulse);
        Color color = warningImageColor;
        color.a = alpha;
        timedWarningImage.color = color;
    }

    private void SetWarningImageActive(bool isActive)
    {
        if (timedWarningImage == null)
        {
            return;
        }

        Color color = warningImageColor;
        color.a = isActive ? warningImageColor.a : 0f;
        timedWarningImage.color = color;
        timedWarningImage.gameObject.SetActive(isActive);
    }
}
