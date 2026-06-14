using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerCamouflageApplier : MonoBehaviour
{
    private const string OverlayObjectName = "CamouflageOverlay";

    [Tooltip("원본 외형을 표시하는 Player의 SpriteRenderer를 연결합니다.")]
    [SerializeField] private SpriteRenderer playerRenderer;

    [Tooltip("패턴을 표시할 자식 CamouflageOverlay의 SpriteRenderer입니다.")]
    [SerializeField] private SpriteRenderer overlayRenderer;

    [Header("Camouflage Mask")]
    [Tooltip("몸통 내부는 흰색/불투명, 외부는 검정/투명인 마스크입니다.")]
    [SerializeField] private Texture2D bodyMaskTexture;

    [Tooltip("검정 테두리, 눈, 입 등 드로잉 위에 유지할 라인 이미지입니다.")]
    [SerializeField] private Texture2D lineTexture;

    [SerializeField] private bool keepOriginalLine = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private Sprite camouflageSprite;
    private Texture2D camouflageTexture;
    private Sprite referencePlayerSprite;
    private bool originalRendererWasEnabled;
    private bool originalRendererWasForcedOff;
    private bool isOriginalRendererHidden;

    private void Awake()
    {
        if (playerRenderer == null)
        {
            playerRenderer = GetComponent<SpriteRenderer>();
        }

        if (playerRenderer == null)
        {
            Debug.LogError(
                "[PlayerCamouflageApplier] Player SpriteRenderer was not found.",
                this);
            return;
        }

        referencePlayerSprite = playerRenderer.sprite;
        EnsureOverlayRenderer();

        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageApplier] Player original sprite preserved.",
            this);
    }

    private void LateUpdate()
    {
        if (isOriginalRendererHidden &&
            playerRenderer != null)
        {
            playerRenderer.enabled = false;
            playerRenderer.forceRenderingOff = true;
        }
    }

    public void ApplyCamouflage(Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogWarning(
                "[PlayerCamouflageApplier] Camouflage texture is null.",
                this);
            return;
        }

        if (playerRenderer == null)
        {
            Debug.LogError(
                "[PlayerCamouflageApplier] Player SpriteRenderer is not assigned.",
                this);
            return;
        }

        Sprite playerSprite =
            referencePlayerSprite != null
                ? referencePlayerSprite
                : playerRenderer.sprite;

        if (playerSprite == null)
        {
            Debug.LogWarning(
                "[PlayerCamouflageApplier] Player Sprite is not assigned.",
                this);
            return;
        }

        if (bodyMaskTexture == null)
        {
            Debug.LogWarning(
                "[PlayerCamouflageApplier] Body Mask Texture is not assigned. Camouflage was not applied.",
                this);
            return;
        }

        EnsureOverlayRenderer();

        if (overlayRenderer == null)
        {
            Debug.LogError(
                "[PlayerCamouflageApplier] Camouflage Overlay could not be created.",
                this);
            return;
        }

        ReleaseGeneratedCamouflage();

        // 마스크와 라인 텍스처는 기본 정지 프레임 기준입니다.
        // 걷기 중간 프레임이 남아 있어도 같은 기준 이미지로 합성합니다.
        playerRenderer.sprite = playerSprite;
        Texture2D playerTexture = playerSprite.texture;

        // Player, Body Mask, Line Texture는 Read/Write Enabled가 필요합니다.
        // 픽셀 아트에는 Filter Mode Point, Compression None을 권장합니다.
        try
        {
            camouflageTexture = CreateMaskedCamouflageTexture(
                texture,
                playerSprite,
                playerTexture);
        }
        catch (UnityException exception)
        {
            Debug.LogError(
                "[PlayerCamouflageApplier] Camouflage textures must have Read/Write Enabled. " +
                "Filter Mode Point and Compression None are recommended.\n" +
                exception.Message,
                this);
            ReleaseGeneratedCamouflage();
            return;
        }

        camouflageSprite = Sprite.Create(
            camouflageTexture,
            playerSprite.rect,
            GetNormalizedPivot(playerSprite),
            playerSprite.pixelsPerUnit);
        camouflageSprite.name = "PlayerCamouflageSprite";

        overlayRenderer.sprite = camouflageSprite;
        overlayRenderer.color = Color.white;
        MatchOverlayToPlayer();
        overlayRenderer.gameObject.SetActive(true);
        HideOriginalRenderer();

        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageApplier] Camouflage texture applied to overlay.",
            this);
        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageApplier] Player original sprite hidden while camouflage is active.",
            this);
    }

    private Texture2D CreateMaskedCamouflageTexture(
        Texture2D drawingTexture,
        Sprite playerSprite,
        Texture2D playerTexture)
    {
        Texture2D resultTexture = new Texture2D(
            playerTexture.width,
            playerTexture.height,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "PlayerCamouflageTexture"
        };

        Color32[] resultPixels =
            new Color32[playerTexture.width * playerTexture.height];
        Rect spriteRect = playerSprite.rect;
        int startX = Mathf.RoundToInt(spriteRect.x);
        int startY = Mathf.RoundToInt(spriteRect.y);
        int spriteWidth = Mathf.RoundToInt(spriteRect.width);
        int spriteHeight = Mathf.RoundToInt(spriteRect.height);

        for (int localY = 0; localY < spriteHeight; localY++)
        {
            float v = (localY + 0.5f) / spriteHeight;
            int textureY = startY + localY;

            for (int localX = 0; localX < spriteWidth; localX++)
            {
                float u = (localX + 0.5f) / spriteWidth;
                int textureX = startX + localX;
                int resultIndex =
                    textureY * playerTexture.width + textureX;
                Color originalColor =
                    playerTexture.GetPixel(textureX, textureY);

                if (originalColor.a <= 0.01f)
                {
                    resultPixels[resultIndex] = Color.clear;
                    continue;
                }

                bool isProtectedEye = IsProtectedEyePixel(
                    textureX,
                    textureY,
                    playerTexture.width,
                    playerTexture.height);
                bool isBodyPixel = !isProtectedEye &&
                    (IsColoredBodyPixel(originalColor) ||
                     IsBodyMaskPixelWithEdgeExpansion(
                         textureX,
                         textureY,
                         playerTexture.width,
                         playerTexture.height,
                         originalColor));
                Color resultColor = originalColor;

                if (isBodyPixel)
                {
                    Color drawingColor =
                        SampleTextureByUv(drawingTexture, u, v);

                    if (drawingColor.a > 0.01f)
                    {
                        drawingColor.a *= originalColor.a;
                        resultColor = drawingColor;
                    }
                }

                if (keepOriginalLine &&
                    lineTexture != null)
                {
                    Color lineColor = SampleTextureBySourcePixel(
                        lineTexture,
                        textureX,
                        textureY,
                        playerTexture.width,
                        playerTexture.height);

                    if (lineColor.a > 0.01f &&
                        (!isBodyPixel ||
                         IsDarkLinePixel(lineColor)))
                    {
                        resultColor = lineColor;
                    }
                }

                resultPixels[resultIndex] = resultColor;
            }
        }

        resultTexture.SetPixels32(resultPixels);
        resultTexture.Apply();
        return resultTexture;
    }

    private bool IsBodyMaskPixelWithEdgeExpansion(
        int textureX,
        int textureY,
        int referenceWidth,
        int referenceHeight,
        Color originalColor)
    {
        Color maskColor = SampleTextureBySourcePixel(
                    bodyMaskTexture,
                    textureX,
                    textureY,
                    referenceWidth,
                    referenceHeight);

        if (IsBodyMaskPixel(maskColor))
        {
            return true;
        }

        float originalBrightness = Mathf.Max(
            originalColor.r,
            Mathf.Max(originalColor.g, originalColor.b));

        // Keep the dark outline intact while filling a one-pixel mask gap.
        if (originalBrightness <= 0.12f)
        {
            return false;
        }

        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 &&
                    offsetY == 0)
                {
                    continue;
                }

                Color neighborMask = SampleTextureBySourcePixel(
                    bodyMaskTexture,
                    textureX + offsetX,
                    textureY + offsetY,
                    referenceWidth,
                    referenceHeight);

                if (IsBodyMaskPixel(neighborMask))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsProtectedEyePixel(
        int textureX,
        int textureY,
        int referenceWidth,
        int referenceHeight)
    {
        if (lineTexture == null)
        {
            return false;
        }

        const int eyePadding = 2;

        for (int offsetY = -eyePadding;
             offsetY <= eyePadding;
             offsetY++)
        {
            for (int offsetX = -eyePadding;
                 offsetX <= eyePadding;
                 offsetX++)
            {
                Color detailColor = SampleTextureBySourcePixel(
                    lineTexture,
                    textureX + offsetX,
                    textureY + offsetY,
                    referenceWidth,
                    referenceHeight);
                float minimumChannel = Mathf.Min(
                    detailColor.r,
                    Mathf.Min(detailColor.g, detailColor.b));

                if (detailColor.a > 0.5f &&
                    minimumChannel >= 0.7f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsColoredBodyPixel(Color color)
    {
        float maximumChannel = Mathf.Max(
            color.r,
            Mathf.Max(color.g, color.b));
        float minimumChannel = Mathf.Min(
            color.r,
            Mathf.Min(color.g, color.b));

        return color.a > 0.01f &&
               maximumChannel > 0.04f &&
               maximumChannel - minimumChannel > 0.025f;
    }

    private bool IsDarkLinePixel(Color color)
    {
        float brightness = Mathf.Max(
            color.r,
            Mathf.Max(color.g, color.b));

        return color.a > 0.01f &&
               brightness <= 0.05f;
    }

    private Color SampleTextureByUv(
        Texture2D source,
        float u,
        float v)
    {
        int x = Mathf.Clamp(
            Mathf.FloorToInt(u * source.width),
            0,
            source.width - 1);
        int y = Mathf.Clamp(
            Mathf.FloorToInt(v * source.height),
            0,
            source.height - 1);

        return source.GetPixel(x, y);
    }

    private Color SampleTextureBySourcePixel(
        Texture2D source,
        int sourceX,
        int sourceY,
        int referenceWidth,
        int referenceHeight)
    {
        int clampedX = Mathf.Clamp(
            sourceX,
            0,
            Mathf.Max(referenceWidth - 1, 0));
        int clampedY = Mathf.Clamp(
            sourceY,
            0,
            Mathf.Max(referenceHeight - 1, 0));
        float u = (clampedX + 0.5f) /
                  Mathf.Max(referenceWidth, 1);
        float v = (clampedY + 0.5f) /
                  Mathf.Max(referenceHeight, 1);

        return SampleTextureByUv(source, u, v);
    }

    private bool IsBodyMaskPixel(Color maskColor)
    {
        float brightness = Mathf.Max(
            maskColor.r,
            Mathf.Max(maskColor.g, maskColor.b));

        // 투명 마스크와 불투명 흑백 마스크를 모두 지원합니다.
        return maskColor.a > 0.01f &&
               brightness >= 0.5f;
    }

    private Vector2 GetNormalizedPivot(Sprite sprite)
    {
        return new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);
    }

    public void ResetCamouflage()
    {
        RestoreOriginalRenderer();

        if (overlayRenderer != null)
        {
            overlayRenderer.color = Color.white;
            overlayRenderer.sprite = null;
            overlayRenderer.gameObject.SetActive(false);
        }

        ReleaseGeneratedCamouflage();

        GameplayDebug.Log(enableDebugLogs,
            "[PlayerCamouflageApplier] Player camouflage reset.",
            this);
    }

    private void HideOriginalRenderer()
    {
        if (playerRenderer == null ||
            isOriginalRendererHidden)
        {
            return;
        }

        originalRendererWasEnabled = playerRenderer.enabled;
        originalRendererWasForcedOff =
            playerRenderer.forceRenderingOff;
        playerRenderer.enabled = false;
        playerRenderer.forceRenderingOff = true;
        isOriginalRendererHidden = true;
    }

    private void RestoreOriginalRenderer()
    {
        if (playerRenderer == null ||
            !isOriginalRendererHidden)
        {
            return;
        }

        playerRenderer.forceRenderingOff =
            originalRendererWasForcedOff;
        playerRenderer.enabled = originalRendererWasEnabled;
        isOriginalRendererHidden = false;
    }

    public IEnumerator FadeOutCamouflage(
        float duration,
        bool revealOriginalDuringFade = false)
    {
        if (overlayRenderer == null ||
            !overlayRenderer.gameObject.activeSelf)
        {
            ResetCamouflage();
            yield break;
        }

        float safeDuration = Mathf.Max(duration, 0f);

        if (safeDuration <= 0f)
        {
            ResetCamouflage();
            yield break;
        }

        Color originalColor = overlayRenderer.color;
        float elapsed = 0f;

        if (revealOriginalDuringFade)
        {
            if (playerRenderer != null &&
                referencePlayerSprite != null)
            {
                playerRenderer.sprite = referencePlayerSprite;
            }

            RestoreOriginalRenderer();
        }

        while (elapsed < safeDuration)
        {
            if (!revealOriginalDuringFade)
            {
                // 일반 위장 해제에서는 서로 다른 애니메이션 프레임이
                // 겹쳐 보이지 않도록 페이드가 끝날 때까지 원본을 숨깁니다.
                HideOriginalRenderer();
            }

            float progress = Mathf.Clamp01(elapsed / safeDuration);
            Color fadedColor = originalColor;
            fadedColor.a = originalColor.a *
                           (1f - Mathf.SmoothStep(0f, 1f, progress));
            overlayRenderer.color = fadedColor;

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetCamouflage();
    }

    private void EnsureOverlayRenderer()
    {
        if (overlayRenderer == null)
        {
            Transform existingOverlay = transform.Find(OverlayObjectName);

            if (existingOverlay != null)
            {
                overlayRenderer =
                    existingOverlay.GetComponent<SpriteRenderer>();
            }
        }

        if (overlayRenderer == null)
        {
            GameObject overlayObject = new GameObject(OverlayObjectName);
            overlayObject.transform.SetParent(transform, false);
            overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();

            GameplayDebug.Log(enableDebugLogs,
                "[PlayerCamouflageApplier] Camouflage overlay created.",
                this);
        }

        overlayRenderer.transform.localPosition = Vector3.zero;
        overlayRenderer.transform.localRotation = Quaternion.identity;
        overlayRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = playerRenderer.sortingOrder + 1;
        overlayRenderer.color = Color.white;
        overlayRenderer.gameObject.SetActive(false);
    }

    private void MatchOverlayToPlayer()
    {
        overlayRenderer.transform.localPosition = Vector3.zero;
        overlayRenderer.transform.localRotation = Quaternion.identity;
        overlayRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = playerRenderer.sortingOrder + 1;

        if (playerRenderer.sprite == null ||
            overlayRenderer.sprite == null)
        {
            overlayRenderer.transform.localScale = Vector3.one;
            return;
        }

        Vector2 playerSize = playerRenderer.sprite.bounds.size;
        Vector2 overlaySize = overlayRenderer.sprite.bounds.size;

        float scaleX = overlaySize.x > 0f
            ? playerSize.x / overlaySize.x
            : 1f;
        float scaleY = overlaySize.y > 0f
            ? playerSize.y / overlaySize.y
            : 1f;

        overlayRenderer.transform.localScale =
            new Vector3(scaleX, scaleY, 1f);
    }

    private void OnDestroy()
    {
        RestoreOriginalRenderer();
        ReleaseGeneratedCamouflage();
    }

    private void ReleaseGeneratedCamouflage()
    {
        if (camouflageSprite != null)
        {
            Destroy(camouflageSprite);
            camouflageSprite = null;
        }

        if (camouflageTexture != null)
        {
            Destroy(camouflageTexture);
            camouflageTexture = null;
        }
    }
}
