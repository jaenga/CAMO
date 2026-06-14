using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider2D))]
public class GoalZone : MonoBehaviour
{
    [SerializeField] private Transform endWalkPoint;
    [SerializeField] private GameObject endingPanel;
    [Header("Ending UI")]
    [SerializeField] private Image fadeBlack;
    [SerializeField] private GameObject endingContent;
    [SerializeField] private GameObject cctvFrame;
    [SerializeField] private GameObject endingPigeon;
    [SerializeField] private GameObject missionTitleImage;
    [SerializeField] private GameObject missionSubtitle;
    [SerializeField] private GameObject restartText;
    [Range(0f, 1f)]
    [SerializeField] private float fadeTargetAlpha = 0.9f;
    [Min(0.01f)]
    [SerializeField] private float fadeDuration = 1f;
    [Min(0f)]
    [SerializeField] private float cctvDuration = 5f;
    [Min(0f)]
    [SerializeField] private float finalTextDelay = 0.5f;

    [Header("Auto Walk")]
    [Min(0.01f)]
    [SerializeField] private float autoWalkSpeed = 2f;
    [Min(0f)]
    [SerializeField] private float stopDistance = 0.05f;
    [Min(0f)]
    [SerializeField] private float panelDelay = 0.5f;

    private bool hasStartedEnding;
    private bool isEndingPanelVisible;

    public static bool IsEndingActive { get; private set; }

    private void Awake()
    {
        IsEndingActive = false;

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();

        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        if (endingPanel != null)
        {
            endingPanel.SetActive(false);
        }

        SetFadeAlpha(0f);

        if (endingContent != null)
        {
            endingContent.SetActive(false);
        }

        SetEndingStageObjects(false, false, false);
    }

    private void Update()
    {
        if (!isEndingPanelVisible ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RestartCurrentSceneFromIntro();
        }
    }

    private void OnDestroy()
    {
        IsEndingActive = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStartedEnding ||
            !other.CompareTag("Player"))
        {
            return;
        }

        PlayerController playerController =
            other.GetComponentInParent<PlayerController>();
        Rigidbody2D playerRigidbody =
            other.GetComponentInParent<Rigidbody2D>();

        if (playerController == null ||
            playerRigidbody == null)
        {
            Debug.LogError(
                "[GoalZone] PlayerController or Rigidbody2D was not found on Player.",
                this);
            return;
        }

        if (endWalkPoint == null ||
            endingPanel == null ||
            fadeBlack == null ||
            endingContent == null ||
            cctvFrame == null ||
            endingPigeon == null ||
            missionTitleImage == null ||
            missionSubtitle == null ||
            restartText == null)
        {
            Debug.LogError(
                "[GoalZone] Ending sequence references are not fully assigned.",
                this);
            return;
        }

        hasStartedEnding = true;
        IsEndingActive = true;
        StopCameraFollow();
        StartCoroutine(
            EndingSequence(playerController, playerRigidbody));
    }

    private void StopCameraFollow()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return;
        }

        CameraFollow cameraFollow =
            mainCamera.GetComponent<CameraFollow>();

        if (cameraFollow != null)
        {
            cameraFollow.enabled = false;
        }
    }

    private IEnumerator EndingSequence(
        PlayerController playerController,
        Rigidbody2D playerRigidbody)
    {
        playerController.SetControlLocked(true);
        playerRigidbody.linearVelocity = Vector2.zero;

        Vector2 targetPosition = endWalkPoint.position;

        while (Vector2.Distance(
                   playerRigidbody.position,
                   targetPosition) > stopDistance)
        {
            float directionX =
                targetPosition.x - playerRigidbody.position.x;

            playerController.SetAutoWalkState(true, directionX);

            Vector2 nextPosition = Vector2.MoveTowards(
                playerRigidbody.position,
                targetPosition,
                autoWalkSpeed * Time.fixedDeltaTime);
            playerRigidbody.MovePosition(nextPosition);

            yield return new WaitForFixedUpdate();
        }

        playerRigidbody.position = targetPosition;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerController.SetAutoWalkState(false, 0f);

        if (panelDelay > 0f)
        {
            yield return new WaitForSeconds(panelDelay);
        }

        endingPanel.SetActive(true);
        endingContent.SetActive(true);
        SetEndingStageObjects(false, false, false);
        SetFadeAlpha(0f);

        float safeFadeDuration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;

        while (elapsed < safeFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / safeFadeDuration);
            SetFadeAlpha(
                Mathf.Lerp(0f, fadeTargetAlpha, progress));
            yield return null;
        }

        SetFadeAlpha(fadeTargetAlpha);

        // 엔딩 UI가 시작된 뒤에는 게임을 멈추고 unscaled time으로 연출합니다.
        Time.timeScale = 0f;
        SetEndingStageObjects(true, false, false);

        if (cctvDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(cctvDuration);
        }

        SetEndingStageObjects(false, true, false);
        SoundManager.Instance?.PlaySuccess();

        if (finalTextDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(finalTextDelay);
        }

        SetEndingStageObjects(false, true, true);
        isEndingPanelVisible = true;
    }

    private void SetEndingStageObjects(
        bool showCctv,
        bool showTitle,
        bool showFinalText)
    {
        bool makeCctvVisualsTransparent =
            showTitle || showFinalText;

        if (cctvFrame != null)
        {
            cctvFrame.SetActive(
                showCctv || makeCctvVisualsTransparent);
            SetGraphicAlpha(
                cctvFrame,
                showCctv ? 1f : 0f);
        }

        if (endingPigeon != null)
        {
            endingPigeon.SetActive(
                showCctv || makeCctvVisualsTransparent);
            SetGraphicAlpha(
                endingPigeon,
                showCctv ? 1f : 0f);
        }

        if (missionTitleImage != null)
        {
            missionTitleImage.SetActive(showTitle);
        }

        if (missionSubtitle != null)
        {
            missionSubtitle.SetActive(showFinalText);
        }

        if (restartText != null)
        {
            restartText.SetActive(showFinalText);
        }
    }

    private void SetGraphicAlpha(GameObject target, float alpha)
    {
        if (target == null)
        {
            return;
        }

        Graphic[] graphics =
            target.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeBlack == null)
        {
            return;
        }

        Color color = fadeBlack.color;
        color.a = Mathf.Clamp01(alpha);
        fadeBlack.color = color;
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        IntroManager.SkipNextIntro();
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void RestartCurrentSceneFromIntro()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}
