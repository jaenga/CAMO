using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorEyedropper : MonoBehaviour
{
    [SerializeField] private FlexibleColorPicker colorPicker;
    [SerializeField] private GameObject gameplayUI;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject drawingPanel;
    [SerializeField] private DrawingTest drawingCanvas;
    [SerializeField] private Button eyedropperButton;
    [SerializeField] private bool enableDebugLogs;

    private GameObject inputBlocker;
    private bool isSampling;

    public static bool IsAnySampling { get; private set; }

    private void Awake()
    {
        if (colorPicker == null)
        {
            colorPicker =
                FindFirstObjectByType<FlexibleColorPicker>(
                    FindObjectsInactive.Include);
        }

        if (drawingCanvas == null)
        {
            drawingCanvas =
                FindFirstObjectByType<DrawingTest>(
                    FindObjectsInactive.Include);
        }

        if (drawingPanel == null &&
            drawingCanvas != null &&
            drawingCanvas.transform.parent != null)
        {
            drawingPanel =
                drawingCanvas.transform.parent.gameObject;
        }

        EnsureInputBlocker();

        if (eyedropperButton != null)
        {
            eyedropperButton.onClick.AddListener(
                BeginEyedropperMode);
        }
        else
        {
            Debug.LogWarning(
                "[ColorEyedropper] Eyedropper Button is not assigned. " +
                "Connect the manually created button in the Inspector.",
                this);
        }
    }

    private void OnDestroy()
    {
        EndEyedropperMode();

        if (eyedropperButton != null)
        {
            eyedropperButton.onClick.RemoveListener(
                BeginEyedropperMode);
        }
    }

    private void LateUpdate()
    {
        if (eyedropperButton == null)
        {
            return;
        }

        bool shouldShowButton =
            (gameplayUI == null ||
             gameplayUI.activeInHierarchy) &&
            (gameOverPanel == null ||
             !gameOverPanel.activeInHierarchy);

        if (!shouldShowButton && isSampling)
        {
            EndEyedropperMode();
        }

        if (eyedropperButton.gameObject.activeSelf !=
            shouldShowButton)
        {
            eyedropperButton.gameObject.SetActive(
                shouldShowButton);
        }
    }

    public void BeginEyedropperMode()
    {
        if (isSampling)
        {
            return;
        }

        if (colorPicker == null)
        {
            Debug.LogError(
                "[ColorEyedropper] FlexibleColorPicker is not assigned.",
                this);
            return;
        }

        if (inputBlocker == null)
        {
            EnsureInputBlocker();
        }

        if (inputBlocker == null)
        {
            return;
        }

        isSampling = true;
        IsAnySampling = true;
        inputBlocker.SetActive(true);
        inputBlocker.transform.SetAsLastSibling();

        GameplayDebug.Log(enableDebugLogs,
            "[ColorEyedropper] Eyedropper mode started. Click a screen pixel.",
            this);
    }

    public void SampleAtScreenPosition(Vector2 screenPosition)
    {
        if (!isSampling)
        {
            return;
        }

        if (inputBlocker != null)
        {
            inputBlocker.SetActive(false);
        }

        StartCoroutine(SampleScreenPixel(screenPosition));
    }

    private IEnumerator SampleScreenPixel(Vector2 screenPosition)
    {
        yield return new WaitForEndOfFrame();

        Texture2D screenshot =
            ScreenCapture.CaptureScreenshotAsTexture();

        if (screenshot == null)
        {
            EndEyedropperMode();
            yield break;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt(
                screenPosition.x *
                screenshot.width /
                Mathf.Max(Screen.width, 1)),
            0,
            screenshot.width - 1);
        int y = Mathf.Clamp(
            Mathf.FloorToInt(
                screenPosition.y *
                screenshot.height /
                Mathf.Max(Screen.height, 1)),
            0,
            screenshot.height - 1);
        Color sampledColor = screenshot.GetPixel(x, y);
        sampledColor.a = colorPicker.color.a;

        colorPicker.color = sampledColor;
        Destroy(screenshot);
        EndEyedropperMode();

        GameplayDebug.Log(enableDebugLogs,
            $"[ColorEyedropper] Sampled screen color: {sampledColor}.",
            this);
    }

    private void EndEyedropperMode()
    {
        isSampling = false;
        IsAnySampling = false;

        if (inputBlocker != null)
        {
            inputBlocker.SetActive(false);
        }
    }

    private void EnsureInputBlocker()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError(
                "[ColorEyedropper] Parent Canvas was not found.",
                this);
            return;
        }

        inputBlocker = new GameObject(
            "EyedropperInputBlocker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ColorEyedropperClickCatcher));
        inputBlocker.layer = gameObject.layer;
        inputBlocker.transform.SetParent(
            canvas.transform,
            false);

        RectTransform blockerRect =
            inputBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blockerImage = inputBlocker.GetComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
        blockerImage.raycastTarget = true;

        inputBlocker
            .GetComponent<ColorEyedropperClickCatcher>()
            .Initialize(this);
        inputBlocker.SetActive(false);
    }

}

public class ColorEyedropperClickCatcher :
    MonoBehaviour,
    IPointerClickHandler
{
    private ColorEyedropper controller;

    public void Initialize(ColorEyedropper eyedropper)
    {
        controller = eyedropper;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        controller?.SampleAtScreenPosition(eventData.position);
    }
}
