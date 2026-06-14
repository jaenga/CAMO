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

    private Sprite camouflageSprite;
    private Texture2D camouflageTexture;

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

        EnsureOverlayRenderer();

        Debug.Log(
            "[PlayerCamouflageApplier] Player original sprite preserved.",
            this);
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

        if (playerRenderer.sprite == null)
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

        Sprite playerSprite = playerRenderer.sprite;
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

        Debug.Log(
            "[PlayerCamouflageApplier] Camouflage texture applied to overlay.",
            this);
        Debug.Log(
            "[PlayerCamouflageApplier] Player original sprite preserved.",
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

                Color maskColor =
                    SampleTextureByUv(bodyMaskTexture, u, v);
                Color resultColor = originalColor;

                if (IsBodyMaskPixel(maskColor))
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
                    Color lineColor =
                        SampleTextureByUv(lineTexture, u, v);

                    if (lineColor.a > 0.01f)
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
        if (overlayRenderer != null)
        {
            overlayRenderer.color = Color.white;
            overlayRenderer.sprite = null;
            overlayRenderer.gameObject.SetActive(false);
        }

        ReleaseGeneratedCamouflage();

        Debug.Log(
            "[PlayerCamouflageApplier] Player camouflage reset.",
            this);
    }

    public IEnumerator FadeOutCamouflage(float duration)
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

        while (elapsed < safeDuration)
        {
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

            Debug.Log(
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
