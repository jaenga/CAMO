using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundSampler : MonoBehaviour
{
    [Header("Capture Target")]
    [SerializeField] private Transform player;
    [Tooltip("실제 카멜레온 본체의 SpriteRenderer를 연결합니다. 비어 있으면 Player 하위에서 자동 탐색합니다.")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Camera captureCamera;

    [Header("Capture Settings")]
    [Min(1)]
    public int sampleSize = 64;
    [SerializeField] private LayerMask captureLayerMask = 193;
    [SerializeField] private Vector2 captureCenterOffset = Vector2.zero;
    [Min(0.01f)]
    [SerializeField] private float captureSizeMultiplier = 1f;
    [Min(0.01f)]
    [SerializeField] private float captureWidthMultiplier = 1f;
    [Min(0.01f)]
    [SerializeField] private float captureHeightMultiplier = 1f;

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
        else
        {
            TryResolvePlayerSpriteRenderer();
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
        if (captureCamera == null ||
            !TryResolvePlayerSpriteRenderer())
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
        if (!TryGetCaptureBounds(
                out _,
                out Vector2 captureWorldSize))
        {
            return;
        }

        int height = Mathf.Max(sampleSize, 1);
        float captureAspect =
            captureWorldSize.x / Mathf.Max(captureWorldSize.y, 0.0001f);
        int width = Mathf.Max(
            Mathf.RoundToInt(height * captureAspect),
            1);

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

        if (captureCamera == null ||
            !TryGetCaptureBounds(
                out Vector3 captureCenter,
                out _))
        {
            return false;
        }

        SynchronizeCaptureCamera(false);
        EnsureCaptureRenderTexture();

        if (captureRenderTexture == null)
        {
            return false;
        }

        viewportPosition =
            captureCamera.WorldToViewportPoint(captureCenter);

        if (viewportPosition.z < 0f)
        {
            return false;
        }

        captureRect = new Rect(
            0f,
            0f,
            captureRenderTexture.width,
            captureRenderTexture.height);
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

        if (!TryGetCaptureBounds(
                out Vector3 captureCenter,
                out Vector2 captureWorldSize))
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = FindMainCameraForSynchronization();
        }

        Vector3 capturePosition =
            captureCamera.transform.position;
        captureCamera.transform.position = new Vector3(
            captureCenter.x,
            captureCenter.y,
            capturePosition.z);

        if (captureCamera.orthographic)
        {
            captureCamera.orthographicSize =
                Mathf.Max(captureWorldSize.y * 0.5f, 0.01f);
        }

        if (logState && debugLogCapture)
        {
            Debug.Log(
                $"[BackgroundSampler] Capture camera position: {captureCamera.transform.position}, Sprite bounds center: {captureCenter}, Capture world size: {captureWorldSize}",
                this);
        }
    }

    private bool TryResolvePlayerSpriteRenderer()
    {
        if (playerSpriteRenderer != null)
        {
            return true;
        }

        if (player == null)
        {
            return false;
        }

        SpriteRenderer directRenderer =
            player.GetComponent<SpriteRenderer>();

        if (IsBasePlayerRenderer(directRenderer))
        {
            playerSpriteRenderer = directRenderer;
            return true;
        }

        SpriteRenderer[] childRenderers =
            player.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer childRenderer in childRenderers)
        {
            if (IsBasePlayerRenderer(childRenderer))
            {
                playerSpriteRenderer = childRenderer;
                return true;
            }
        }

        // 이름으로 본체를 구분할 수 없는 경우 GetComponentInChildren 결과를 사용합니다.
        playerSpriteRenderer =
            player.GetComponentInChildren<SpriteRenderer>(true);

        if (playerSpriteRenderer == null)
        {
            Debug.LogWarning(
                "[BackgroundSampler] Player SpriteRenderer was not assigned and could not be found under Player.",
                this);
            return false;
        }

        return true;
    }

    private bool IsBasePlayerRenderer(SpriteRenderer renderer)
    {
        return renderer != null &&
               renderer.sprite != null &&
               renderer.gameObject.name != "CamouflageOverlay";
    }

    private bool TryGetCaptureBounds(
        out Vector3 captureCenter,
        out Vector2 captureWorldSize)
    {
        captureCenter = default;
        captureWorldSize = default;

        if (!TryResolvePlayerSpriteRenderer() ||
            playerSpriteRenderer.sprite == null)
        {
            return false;
        }

        Bounds spriteBounds = playerSpriteRenderer.bounds;
        captureCenter = spriteBounds.center +
                        (Vector3)captureCenterOffset;

        float sizeMultiplier =
            Mathf.Max(captureSizeMultiplier, 0.01f);
        float widthMultiplier =
            Mathf.Max(captureWidthMultiplier, 0.01f);
        float heightMultiplier =
            Mathf.Max(captureHeightMultiplier, 0.01f);

        captureWorldSize = new Vector2(
            Mathf.Max(
                spriteBounds.size.x *
                sizeMultiplier *
                widthMultiplier,
                0.01f),
            Mathf.Max(
                spriteBounds.size.y *
                sizeMultiplier *
                heightMultiplier,
                0.01f));

        return true;
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
