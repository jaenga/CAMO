using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUIController : MonoBehaviour
{
    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Image spotlight;
    [SerializeField] private GameObject bubbleChameleon;
    [SerializeField] private GameObject bubblePigeon;

    [Header("Head Buttons")]
    [SerializeField] private Button chameleonHeadButton;
    [SerializeField] private Button pigeonHeadButton;

    [Header("Scene")]
    public string homeSceneName = "MainScene";

    [Header("Spotlight")]
    [Min(0.1f)]
    [SerializeField] private float spotlightDuration = 3f;
    [Min(0.01f)]
    [SerializeField] private float spotlightFlickerInterval = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float spotlightFlickerMinAlpha;
    [Range(0f, 1f)]
    [SerializeField] private float spotlightFlickerMaxAlpha = 1f;

    private Coroutine spotlightCoroutine;

    public static bool IsGameOverActive { get; private set; }

    private void Awake()
    {
        IsGameOverActive = false;
        DisableBubbleRaycasts(bubbleChameleon);
        DisableBubbleRaycasts(bubblePigeon);
        SetBubblesActive(false);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (chameleonHeadButton != null)
        {
            chameleonHeadButton.onClick.AddListener(RestartCurrentScene);
        }

        if (pigeonHeadButton != null)
        {
            pigeonHeadButton.onClick.AddListener(LoadHomeScene);
        }
    }

    private void OnDestroy()
    {
        IsGameOverActive = false;

        if (chameleonHeadButton != null)
        {
            chameleonHeadButton.onClick.RemoveListener(RestartCurrentScene);
        }

        if (pigeonHeadButton != null)
        {
            pigeonHeadButton.onClick.RemoveListener(LoadHomeScene);
        }
    }

    private void Update()
    {
        if (!IsGameOverActive ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RestartCurrentSceneFromIntro();
        }
    }

    public void ShowGameOver()
    {
        if (gameOverPanel == null)
        {
            Debug.LogError(
                "[GameOverUIController] Game Over Panel is not assigned.",
                this);
            return;
        }

        gameOverPanel.SetActive(true);
        IsGameOverActive = true;
        SetBubblesActive(false);
        SoundManager.Instance?.PlayGameOver();
        Time.timeScale = 0f;

        if (spotlightCoroutine != null)
        {
            StopCoroutine(spotlightCoroutine);
        }

        spotlightCoroutine = StartCoroutine(PlaySpotlightFlicker());
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

    public void LoadHomeScene()
    {
        if (string.IsNullOrWhiteSpace(homeSceneName))
        {
            Debug.LogError(
                "[GameOverUIController] Home Scene Name is empty.",
                this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    private IEnumerator PlaySpotlightFlicker()
    {
        if (spotlight == null)
        {
            yield break;
        }

        float minAlpha = Mathf.Min(
            spotlightFlickerMinAlpha,
            spotlightFlickerMaxAlpha);
        float maxAlpha = Mathf.Max(
            spotlightFlickerMinAlpha,
            spotlightFlickerMaxAlpha);

        SetSpotlightAlpha(minAlpha);

        float duration = Mathf.Max(spotlightDuration, 0.1f);
        float interval = Mathf.Max(spotlightFlickerInterval, 0.01f);
        float elapsed = 0f;
        float nextFlickerTime = 0f;
        bool isLit = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= nextFlickerTime)
            {
                isLit = !isLit;
                SetSpotlightAlpha(isLit ? maxAlpha : minAlpha);
                nextFlickerTime = elapsed + interval;
            }

            yield return null;
        }

        SetSpotlightAlpha(maxAlpha);
        spotlightCoroutine = null;
    }

    private void SetSpotlightAlpha(float alpha)
    {
        Color color = spotlight.color;
        color.a = alpha;
        spotlight.color = color;
    }

    private void SetBubblesActive(bool active)
    {
        if (bubbleChameleon != null)
        {
            bubbleChameleon.SetActive(active);
        }

        if (bubblePigeon != null)
        {
            bubblePigeon.SetActive(active);
        }
    }

    private void DisableBubbleRaycasts(GameObject bubble)
    {
        if (bubble == null)
        {
            return;
        }

        Graphic[] graphics =
            bubble.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }
    }
}
