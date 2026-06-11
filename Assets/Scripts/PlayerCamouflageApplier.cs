using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerCamouflageApplier : MonoBehaviour
{
    private const string OverlayObjectName = "CamouflageOverlay";

    [Tooltip("원본 외형을 표시하는 Player의 SpriteRenderer를 연결합니다.")]
    [SerializeField] private SpriteRenderer playerRenderer;

    [Tooltip("패턴을 표시할 자식 CamouflageOverlay의 SpriteRenderer입니다.")]
    [SerializeField] private SpriteRenderer overlayRenderer;

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

        EnsureOverlayRenderer();

        if (overlayRenderer == null)
        {
            Debug.LogError(
                "[PlayerCamouflageApplier] Camouflage Overlay could not be created.",
                this);
            return;
        }

        ReleaseGeneratedCamouflage();

        // 드로잉 원본이 바뀌어도 적용된 외형이 유지되도록 별도 Texture를 만듭니다.
        camouflageTexture = new Texture2D(
            texture.width,
            texture.height,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = texture.filterMode,
            wrapMode = TextureWrapMode.Clamp,
            name = "PlayerCamouflageTexture"
        };
        camouflageTexture.SetPixels32(texture.GetPixels32());
        camouflageTexture.Apply();

        camouflageSprite = Sprite.Create(
            camouflageTexture,
            new Rect(
                0f,
                0f,
                camouflageTexture.width,
                camouflageTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        camouflageSprite.name = "PlayerCamouflageSprite";

        overlayRenderer.sprite = camouflageSprite;
        MatchOverlayToPlayer();
        overlayRenderer.gameObject.SetActive(true);

        Debug.Log(
            "[PlayerCamouflageApplier] Camouflage texture applied to overlay.",
            this);
        Debug.Log(
            "[PlayerCamouflageApplier] Player original sprite preserved.",
            this);
    }

    public void ResetCamouflage()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.sprite = null;
            overlayRenderer.gameObject.SetActive(false);
        }

        ReleaseGeneratedCamouflage();

        Debug.Log(
            "[PlayerCamouflageApplier] Player camouflage reset.",
            this);
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
