using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUIController : MonoBehaviour
{
    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Image spotlight;
    [SerializeField] private GameObject bubbleChameleon;
    [SerializeField] private GameObject bubblePigeon;
    [SerializeField] private TMP_Text bubbleChameleonText;
    [SerializeField] private TMP_Text bubblePigeonText;

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

    private Coroutine spotlightCoroutine;

    private void Awake()
    {
        ResolveBubbleText();
        SetBubbleText();
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
        if (chameleonHeadButton != null)
        {
            chameleonHeadButton.onClick.RemoveListener(RestartCurrentScene);
        }

        if (pigeonHeadButton != null)
        {
            pigeonHeadButton.onClick.RemoveListener(LoadHomeScene);
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
        SetBubblesActive(false);
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

        SetSpotlightAlpha(0f);

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
                SetSpotlightAlpha(isLit ? 1f : 0f);
                nextFlickerTime = elapsed + interval;
            }

            yield return null;
        }

        SetSpotlightAlpha(1f);
        spotlightCoroutine = null;
    }

    private void SetSpotlightAlpha(float alpha)
    {
        Color color = spotlight.color;
        color.a = alpha;
        spotlight.color = color;
    }

    private void ResolveBubbleText()
    {
        if (bubbleChameleonText == null &&
            bubbleChameleon != null)
        {
            bubbleChameleonText =
                bubbleChameleon.GetComponentInChildren<TMP_Text>(true);
        }

        if (bubblePigeonText == null &&
            bubblePigeon != null)
        {
            bubblePigeonText =
                bubblePigeon.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void SetBubbleText()
    {
        if (bubbleChameleonText != null)
        {
            bubbleChameleonText.text = "다시 속여보자";
        }

        if (bubblePigeonText != null)
        {
            bubblePigeonText.text = "오늘은 여기까지";
        }
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
