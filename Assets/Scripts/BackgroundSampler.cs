using UnityEngine;

public class BackgroundSampler : MonoBehaviour
{
    [Header("Capture Target")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera captureCamera;

    [Header("Capture Settings")]
    [Min(1)]
    public int sampleSize = 64;

    private void Awake()
    {
        if (captureCamera == null)
        {
            captureCamera = Camera.main;
        }

        if (player == null)
        {
            Debug.LogError("[BackgroundSampler] Player Transform is not assigned.", this);
        }

        if (captureCamera == null)
        {
            Debug.LogError("[BackgroundSampler] Capture Camera is not assigned.", this);
        }
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

        Debug.Log("[BackgroundSampler] Background capture started.", this);

        Vector3 screenPosition =
            captureCamera.WorldToScreenPoint(player.position);

        Debug.Log(
            $"[BackgroundSampler] Player screen position: {screenPosition}",
            this);

        if (screenPosition.z < 0f)
        {
            Debug.LogWarning(
                "[BackgroundSampler] Player is behind the capture Camera.",
                this);
            return null;
        }

        Rect cameraRect = captureCamera.pixelRect;
        int captureSize = Mathf.Clamp(
            sampleSize,
            1,
            Mathf.FloorToInt(Mathf.Min(cameraRect.width, cameraRect.height)));
        float halfSize = captureSize * 0.5f;
        int minX = Mathf.CeilToInt(cameraRect.xMin);
        int minY = Mathf.CeilToInt(cameraRect.yMin);
        int maxX = Mathf.FloorToInt(cameraRect.xMax) - captureSize;
        int maxY = Mathf.FloorToInt(cameraRect.yMax) - captureSize;
        int captureX = Mathf.Clamp(
            Mathf.RoundToInt(screenPosition.x - halfSize),
            minX,
            maxX);
        int captureY = Mathf.Clamp(
            Mathf.RoundToInt(screenPosition.y - halfSize),
            minY,
            maxY);

        Texture2D capturedTexture = new Texture2D(
            captureSize,
            captureSize,
            TextureFormat.RGBA32,
            false);

        capturedTexture.ReadPixels(
            new Rect(captureX, captureY, captureSize, captureSize),
            0,
            0);
        capturedTexture.Apply();
        capturedTexture.name = "RuntimeBackgroundAnswer";

        Debug.Log(
            $"[BackgroundSampler] Captured answer texture size: {capturedTexture.width}x{capturedTexture.height}",
            this);

        return capturedTexture;
    }
}
