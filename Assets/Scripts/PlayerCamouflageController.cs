using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Drawing")]
    [Tooltip("SubmitDrawing()을 가진 DrawingTest 컴포넌트를 연결합니다.")]
    [SerializeField] private DrawingTest drawingManager;

    [Tooltip("Timed Camouflage 시작 전에 배경을 캡처할 BackgroundSampler를 연결합니다.")]
    [SerializeField] private BackgroundSampler backgroundSampler;

    [Tooltip("Submit된 그림을 Player 외형에 적용할 컴포넌트를 연결합니다.")]
    [SerializeField] private PlayerCamouflageApplier camouflageApplier;

    [Header("Predator")]
    [Tooltip("Timed Camouflage 중 추격 속도를 낮출 Predator를 연결합니다.")]
    [SerializeField] private PredatorController predator;

    [Header("Timer")]
    [Min(0.1f)]
    public float camouflageTimeLimit = 12f;

    private CamouflageModeType currentMode = CamouflageModeType.None;
    private bool isCamouflageMode;
    private bool isTimedCamouflageActive;
    private bool isDangerCamouflageAvailable;
    private bool hasSubmitted;
    private bool isTimerRunning;
    private bool isPuzzleInteractionActive;
    private bool suppressInputUntilEReleased;
    private Coroutine timedCamouflageCoroutine;
    private float remainingTime;

    private void Start()
    {
        ResetCamouflageState();
        SetDrawingPanelActive(false);
        ClearTimerText();

        Debug.Log(
            "[PlayerCamouflageController] Ready. Free camouflage is available.",
            this);
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
        UpdateTimerText();

        if (remainingTime <= 0f)
        {
            Debug.Log(
                "[PlayerCamouflageController] Timer expired auto submit.",
                this);
            SubmitCamouflage(true);
        }
    }

    private void HandleCamouflageInput()
    {
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
            !Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }

        Debug.Log("[PlayerCamouflageController] E key pressed.", this);

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

        Debug.Log(
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

            isCamouflageMode = false;
            isTimerRunning = false;
            hasSubmitted = false;
            remainingTime = 0f;
            currentMode = CamouflageModeType.None;

            Debug.Log(
                "[PlayerCamouflageController] Active Free camouflage cleared before enabling Timed camouflage.",
                this);
        }

        isDangerCamouflageAvailable = true;
        Debug.Log(
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
        if (isCamouflageMode)
        {
            Debug.Log(
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

        Debug.Log(
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
        UpdateTimerText();

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

        Debug.Log(
            $"[PlayerCamouflageController] Timed camouflage started after background capture. Time limit: {camouflageTimeLimit:F1} seconds.",
            this);
    }

    // Submit 버튼의 OnClick에는 DrawingTest.SubmitDrawing 대신 이 메서드를 연결합니다.
    public void ManualSubmit()
    {
        Debug.Log("[PlayerCamouflageController] Manual Submit requested.", this);
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
            isDangerCamouflageAvailable;

        StopTimedCamouflageCoroutine();

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

        if (predator != null)
        {
            predator.SetCamouflageSlowMode(false);
        }

        if (hadActiveCamouflage)
        {
            Debug.Log(
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
            Debug.Log(
                "[PlayerCamouflageController] Duplicate Submit ignored.",
                this);
            return;
        }

        CamouflageModeType submittedMode = currentMode;
        StopTimedCamouflageCoroutine();
        hasSubmitted = true;
        isTimerRunning = false;

        if (drawingManager != null)
        {
            drawingManager.SubmitDrawing();

            // Fail로 Attack이 발생해 ForceCancel된 경우에는 외형을 적용하지 않습니다.
            if (isCamouflageMode &&
                currentMode == submittedMode)
            {
                Texture2D drawingTexture =
                    drawingManager.GetCurrentDrawingTexture();

                if (camouflageApplier != null)
                {
                    camouflageApplier.ApplyCamouflage(drawingTexture);
                }
                else
                {
                    Debug.LogWarning(
                        "[PlayerCamouflageController] PlayerCamouflageApplier is not assigned.",
                        this);
                }
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

            Debug.Log(
                $"[PlayerCamouflageController] Timed camouflage submitted ({(isAutomatic ? "Auto" : "Manual")}).",
                this);
        }
        else
        {
            Debug.Log(
                "[PlayerCamouflageController] Free camouflage submitted.",
                this);
        }

        SetDrawingPanelActive(false);
        ClearTimerText();
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

    private void SetDrawingPanelActive(bool isActive)
    {
        if (drawingPanel != null)
        {
            drawingPanel.SetActive(isActive);
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
}
