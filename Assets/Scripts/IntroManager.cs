using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class IntroManager : MonoBehaviour
{
    private static bool skipNextIntro;

    [Header("Panels")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject gameplayUI;

    [Header("Intro UI")]
    [SerializeField] private TMP_Text pressAnyKeyText;
    [SerializeField] private CanvasGroup logoCanvasGroup;
    [SerializeField] private IntroPigeonFlyBy introPigeon;

    [Header("Camera")]
    [SerializeField] private Transform introCameraStart;
    [SerializeField] private Transform gameplayCameraTarget;

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float blinkInterval = 0.5f;
    [Min(0f)]
    [SerializeField] private float introHoldDuration = 2f;
    [Min(0.01f)]
    [SerializeField] private float logoFadeDuration = 2.5f;

    private bool hasStartedTransition;
    private float nextBlinkTime;
    private Camera mainCamera;
    private CameraFollow cameraFollow;

    public static void SkipNextIntro()
    {
        skipNextIntro = true;
    }

    private void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera != null)
        {
            cameraFollow = mainCamera.GetComponent<CameraFollow>();

            if (cameraFollow != null)
            {
                cameraFollow.enabled = false;
            }

            if (introCameraStart != null)
            {
                mainCamera.transform.SetPositionAndRotation(
                    introCameraStart.position,
                    introCameraStart.rotation);
            }
        }

        if (skipNextIntro)
        {
            skipNextIntro = false;
            StartGameplayImmediately();
            return;
        }

        if (introPanel != null)
        {
            introPanel.SetActive(true);
        }

        if (gameplayUI != null)
        {
            gameplayUI.SetActive(false);
        }

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 1f;
        }

        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(true);
        }

        if (introPigeon != null)
        {
            introPigeon.gameObject.SetActive(false);
        }

        nextBlinkTime =
            Time.unscaledTime + Mathf.Max(blinkInterval, 0.01f);
    }

    private void StartGameplayImmediately()
    {
        hasStartedTransition = true;

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 0f;
        }

        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(false);
        }

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        if (gameplayUI != null)
        {
            gameplayUI.SetActive(true);
        }

        if (mainCamera != null &&
            gameplayCameraTarget != null)
        {
            mainCamera.transform.SetPositionAndRotation(
                gameplayCameraTarget.position,
                gameplayCameraTarget.rotation);
        }

        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
        }
    }

    private void Update()
    {
        if (hasStartedTransition)
        {
            return;
        }

        UpdatePressAnyKeyBlink();

        // 이 프로젝트는 New Input System 전용이므로 Input.anyKeyDown과 같은 동작을 사용합니다.
        if (Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame)
        {
            hasStartedTransition = true;
            StartCoroutine(TransitionToGameplay());
        }
    }

    private void UpdatePressAnyKeyBlink()
    {
        if (pressAnyKeyText == null ||
            Time.unscaledTime < nextBlinkTime)
        {
            return;
        }

        pressAnyKeyText.gameObject.SetActive(
            !pressAnyKeyText.gameObject.activeSelf);
        nextBlinkTime =
            Time.unscaledTime + Mathf.Max(blinkInterval, 0.01f);
    }

    private IEnumerator TransitionToGameplay()
    {
        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(false);
        }

        introPigeon?.Play(mainCamera);

        float holdDuration = Mathf.Max(introHoldDuration, 0f);
        float fadeDuration = Mathf.Max(logoFadeDuration, 0.01f);
        float duration = Mathf.Max(
            holdDuration + fadeDuration,
            introPigeon != null
                ? introPigeon.FlightDuration
                : 0.01f);
        float elapsed = 0f;
        float startAlpha =
            logoCanvasGroup != null ? logoCanvasGroup.alpha : 1f;
        Vector3 cameraStartPosition =
            mainCamera != null
                ? mainCamera.transform.position
                : Vector3.zero;
        Quaternion cameraStartRotation =
            mainCamera != null
                ? mainCamera.transform.rotation
                : Quaternion.identity;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (logoCanvasGroup != null)
            {
                float fadeProgress = Mathf.Clamp01(
                    (elapsed - holdDuration) / fadeDuration);
                logoCanvasGroup.alpha =
                    Mathf.Lerp(startAlpha, 0f, fadeProgress);
            }

            if (mainCamera != null &&
                gameplayCameraTarget != null)
            {
                mainCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(
                        cameraStartPosition,
                        gameplayCameraTarget.position,
                        progress),
                    Quaternion.Slerp(
                        cameraStartRotation,
                        gameplayCameraTarget.rotation,
                        progress));
            }

            yield return null;
        }

        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 0f;
        }

        if (mainCamera != null &&
            gameplayCameraTarget != null)
        {
            mainCamera.transform.SetPositionAndRotation(
                gameplayCameraTarget.position,
                gameplayCameraTarget.rotation);
        }

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        introPigeon?.Stop();

        if (gameplayUI != null)
        {
            gameplayUI.SetActive(true);
        }

        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
        }
    }
}
