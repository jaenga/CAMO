using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundSampler : MonoBehaviour
{
    [Header("Capture Target")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera captureCamera;

    [Header("Capture Settings")]
    [Min(1)]
    public int sampleSize = 64;
    [SerializeField] private LayerMask captureLayerMask = 193;

    [Header("Debug Preview")]
    [SerializeField] private RawImage answerPreviewImage;
    [SerializeField] private bool previewEnabled = true;
    [Min(0f)]
    [SerializeField] private float previewUpdateInterval = 0.25f;
    [SerializeField] private bool debugLogCapture;

    private Texture2D previewTexture;
    private RenderTexture captureRenderTexture;
    private Coroutine previewCoroutine;
    private bool isUsingMainCameraFallback;
    private bool hasLoggedMainCameraWarning;
    private Camera mainCamera;

    private void Awake()
    {
        if (captureCamera == null)
        {
            captureCamera = Camera.main;
            isUsingMainCameraFallback = true;

            if (captureCamera != null)
            {
                Debug.LogWarning(
                    $"[BackgroundSampler] Capture Camera was not assigned. Falling back to Camera.main: {captureCamera.name}",
                    this);
            }
        }

        mainCamera = FindMainCameraForSynchronization();

        if (player == null)
        {
            Debug.LogError("[BackgroundSampler] Player Transform is not assigned.", this);
        }

        if (captureCamera == null)
        {
            Debug.LogError("[BackgroundSampler] Capture Camera is not assigned.", this);
        }
    }

    private void OnEnable()
    {
        StartPreview();
    }

    private void OnDisable()
    {
        StopPreview();
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
        {
            Destroy(previewTexture);
            previewTexture = null;
        }

        ReleaseCaptureRenderTexture();
    }

    // WaitForEndOfFrame 이후 호출해야 현재 화면을 정확히 읽을 수 있습니다.
    public Texture2D CaptureBackgroundAroundPlayer()
    {
        if (player == null || captureCamera == null)
        {
            Debug.LogError(
                "[BackgroundSampler] Background capture failed because required references are missing.",
                this);
            return null;
        }

        if (debugLogCapture)
        {
            Debug.Log("[BackgroundSampler] Background capture started.", this);
        }

        if (!TryGetCaptureRect(
                out Rect captureRect,
                out Vector3 viewportPosition))
        {
            if (viewportPosition.z < 0f)
            {
                Debug.LogWarning(
                    "[BackgroundSampler] Player is behind the capture Camera.",
                    this);
            }

            return null;
        }

        if (debugLogCapture)
        {
            Debug.Log(
                $"[BackgroundSampler] Player viewport position: {viewportPosition}",
                this);
        }

        Texture2D capturedTexture = new Texture2D(
            Mathf.RoundToInt(captureRect.width),
            Mathf.RoundToInt(captureRect.height),
            TextureFormat.RGBA32,
            false);

        CaptureIntoTexture(capturedTexture, captureRect);
        capturedTexture.name = "RuntimeBackgroundAnswer";

        if (answerPreviewImage != null)
        {
            answerPreviewImage.texture = capturedTexture;
        }

        Color averageColor = CalculateAverageColor(capturedTexture);

        if (debugLogCapture)
        {
            Debug.Log(
                $"[BackgroundSampler] Captured answer texture size: {capturedTexture.width}x{capturedTexture.height}",
                this);
            Debug.Log(
                $"[BackgroundSampler] Captured average color: {averageColor}",
                this);
        }

        return capturedTexture;
    }

    private void StartPreview()
    {
        if (!previewEnabled ||
            answerPreviewImage == null ||
            previewCoroutine != null)
        {
            return;
        }

        previewCoroutine = StartCoroutine(UpdatePreviewRoutine());
    }

    private void StopPreview()
    {
        if (previewCoroutine == null)
        {
            return;
        }

        StopCoroutine(previewCoroutine);
        previewCoroutine = null;
    }

    private IEnumerator UpdatePreviewRoutine()
    {
        float nextUpdateTime = 0f;

        while (previewEnabled && answerPreviewImage != null)
        {
            yield return new WaitForEndOfFrame();

            if (Time.unscaledTime < nextUpdateTime ||
                !TryGetCaptureRect(out Rect captureRect, out _))
            {
                continue;
            }

            UpdatePreviewTexture(captureRect);
            nextUpdateTime =
                Time.unscaledTime + Mathf.Max(0f, previewUpdateInterval);
        }

        previewCoroutine = null;
    }

    private void UpdatePreviewTexture(Rect captureRect)
    {
        int width = Mathf.RoundToInt(captureRect.width);
        int height = Mathf.RoundToInt(captureRect.height);

        if (previewTexture == null ||
            previewTexture.width != width ||
            previewTexture.height != height)
        {
            if (previewTexture != null)
            {
                Destroy(previewTexture);
            }

            previewTexture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "RuntimeBackgroundPreview"
            };
        }

        CaptureIntoTexture(previewTexture, captureRect);
        answerPreviewImage.texture = previewTexture;
    }

    private void CaptureIntoTexture(
        Texture2D destination,
        Rect captureRect)
    {
        SynchronizeCaptureCamera(true);
        EnsureCaptureRenderTexture();

        RenderTexture previousTarget = captureCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            captureCamera.cullingMask = captureLayerMask.value;
            captureCamera.targetTexture = captureRenderTexture;

            if (debugLogCapture)
            {
                Debug.Log(
                    $"[BackgroundSampler] Capture camera used: {captureCamera.name}",
                    this);
                Debug.Log(
                    $"[BackgroundSampler] Capture camera culling mask: {captureCamera.cullingMask}",
                    this);
            }

            if ((isUsingMainCameraFallback ||
                 captureCamera.gameObject.name == "Main Camera") &&
                !hasLoggedMainCameraWarning)
            {
                Debug.LogWarning(
                    $"[BackgroundSampler] Main Camera is being used for capture: {captureCamera.name}",
                    this);
                hasLoggedMainCameraWarning = true;
            }

            captureCamera.Render();

            RenderTexture.active = captureRenderTexture;
            destination.ReadPixels(captureRect, 0, 0);
            destination.Apply();
        }
        finally
        {
            captureCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
        }
    }

    private void EnsureCaptureRenderTexture()
    {
        int width = Mathf.Max(captureCamera.pixelWidth, 1);
        int height = Mathf.Max(captureCamera.pixelHeight, 1);

        if (captureRenderTexture != null &&
            captureRenderTexture.width == width &&
            captureRenderTexture.height == height)
        {
            return;
        }

        ReleaseCaptureRenderTexture();

        captureRenderTexture = new RenderTexture(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32)
        {
            name = "BackgroundCaptureRenderTexture"
        };
        captureRenderTexture.Create();
    }

    private void ReleaseCaptureRenderTexture()
    {
        if (captureRenderTexture == null)
        {
            return;
        }

        captureRenderTexture.Release();
        Destroy(captureRenderTexture);
        captureRenderTexture = null;
    }

    private bool TryGetCaptureRect(
        out Rect captureRect,
        out Vector3 viewportPosition)
    {
        captureRect = default;
        viewportPosition = default;

        if (player == null || captureCamera == null)
        {
            return false;
        }

        SynchronizeCaptureCamera(false);
        EnsureCaptureRenderTexture();
        viewportPosition =
            captureCamera.WorldToViewportPoint(player.position);

        if (viewportPosition.z < 0f)
        {
            return false;
        }

        int captureSize = Mathf.Clamp(
            sampleSize,
            1,
            Mathf.Min(
                captureRenderTexture.width,
                captureRenderTexture.height));
        float halfSize = captureSize * 0.5f;
        float playerPixelX =
            viewportPosition.x * captureRenderTexture.width;
        float playerPixelY =
            viewportPosition.y * captureRenderTexture.height;
        int maxX = captureRenderTexture.width - captureSize;
        int maxY = captureRenderTexture.height - captureSize;
        int captureX = Mathf.Clamp(
            Mathf.RoundToInt(playerPixelX - halfSize),
            0,
            maxX);
        int captureY = Mathf.Clamp(
            Mathf.RoundToInt(playerPixelY - halfSize),
            0,
            maxY);

        captureRect =
            new Rect(captureX, captureY, captureSize, captureSize);
        return true;
    }

    private Camera FindMainCameraForSynchronization()
    {
        GameObject mainCameraObject = GameObject.Find("Main Camera");

        if (mainCameraObject != null)
        {
            Camera namedMainCamera =
                mainCameraObject.GetComponent<Camera>();

            if (namedMainCamera != null &&
                namedMainCamera != captureCamera)
            {
                return namedMainCamera;
            }
        }

        Camera taggedMainCamera = Camera.main;
        return taggedMainCamera != captureCamera
            ? taggedMainCamera
            : null;
    }

    private void SynchronizeCaptureCamera(bool logState)
    {
        if (captureCamera == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = FindMainCameraForSynchronization();
        }

        if (mainCamera != null)
        {
            Vector3 capturePosition =
                captureCamera.transform.position;
            Vector3 mainPosition =
                mainCamera.transform.position;

            captureCamera.transform.position = new Vector3(
                mainPosition.x,
                mainPosition.y,
                capturePosition.z);

            if (captureCamera.orthographic &&
                mainCamera.orthographic)
            {
                captureCamera.orthographicSize =
                    mainCamera.orthographicSize;
            }
        }

        if (logState && debugLogCapture)
        {
            string playerPosition =
                player != null
                    ? player.position.ToString()
                    : "Missing";

            Debug.Log(
                $"[BackgroundSampler] Capture camera position: {captureCamera.transform.position}, Player position: {playerPosition}",
                this);
        }
    }

    private Color CalculateAverageColor(Texture2D source)
    {
        Color32[] pixels = source.GetPixels32();

        if (pixels.Length == 0)
        {
            return Color.clear;
        }

        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;

        foreach (Color32 pixel in pixels)
        {
            red += pixel.r;
            green += pixel.g;
            blue += pixel.b;
            alpha += pixel.a;
        }

        float scale = 1f / (pixels.Length * 255f);
        return new Color(
            red * scale,
            green * scale,
            blue * scale,
            alpha * scale);
    }
}
