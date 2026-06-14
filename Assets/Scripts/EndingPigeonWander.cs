using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class EndingPigeonWander : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float moveDistance = 4f;
    [Min(0.01f)]
    [SerializeField] private float moveSpeed = 4f;

    [Header("CCTV Clipping")]
    [SerializeField] private RectTransform cctvFrame;
    [Tooltip("CCTV 프레임 안쪽 여백입니다. X=Left, Y=Right, Z=Top, W=Bottom")]
    [SerializeField] private Vector4 screenInsets =
        new Vector4(2f, 2f, 4f, 5f);

    [Header("UI Animation")]
    [SerializeField] private Image pigeonImage;
    [SerializeField] private Sprite[] animationFrames;
    [Min(0.01f)]
    [SerializeField] private float frameInterval = 0.15f;

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private float direction = -1f;
    private float originalScaleX;
    private float frameTimer;
    private int frameIndex;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        EnsureCctvMask();
        pigeonImage = pigeonImage != null
            ? pigeonImage
            : GetComponent<Image>();
        startPosition = rectTransform.anchoredPosition;
        originalScaleX = Mathf.Abs(rectTransform.localScale.x);

        Animator animator = GetComponent<Animator>();

        if (animator != null)
        {
            // Predator 비행 애니메이션은 localScale을 키프레임으로 덮어써
            // 방향 전환 반전을 취소하므로 UI 프레임 애니메이션만 사용합니다.
            animator.enabled = false;
        }

        ShowPigeon();
    }

    private void EnsureCctvMask()
    {
        if (cctvFrame == null)
        {
            Transform frameTransform =
                transform.parent?.Find("CCTVFrame");
            cctvFrame =
                frameTransform as RectTransform;
        }

        if (cctvFrame == null ||
            rectTransform == null ||
            rectTransform.parent == null)
        {
            return;
        }

        Transform existingMask =
            rectTransform.parent.Find("CCTVScreenMask");
        RectTransform maskRect;

        if (existingMask != null)
        {
            maskRect = existingMask as RectTransform;
        }
        else
        {
            GameObject maskObject = new GameObject(
                "CCTVScreenMask",
                typeof(RectTransform),
                typeof(RectMask2D));
            maskRect = maskObject.GetComponent<RectTransform>();
            maskRect.SetParent(rectTransform.parent, false);
        }

        if (maskRect == null)
        {
            return;
        }

        maskRect.anchorMin = cctvFrame.anchorMin;
        maskRect.anchorMax = cctvFrame.anchorMax;
        maskRect.pivot = cctvFrame.pivot;
        maskRect.localScale = cctvFrame.localScale;
        maskRect.localRotation = cctvFrame.localRotation;

        float left = Mathf.Max(screenInsets.x, 0f);
        float right = Mathf.Max(screenInsets.y, 0f);
        float top = Mathf.Max(screenInsets.z, 0f);
        float bottom = Mathf.Max(screenInsets.w, 0f);
        Vector2 frameSize = cctvFrame.sizeDelta;

        maskRect.sizeDelta = new Vector2(
            Mathf.Max(frameSize.x - left - right, 0f),
            Mathf.Max(frameSize.y - top - bottom, 0f));
        maskRect.anchoredPosition =
            cctvFrame.anchoredPosition +
            new Vector2(
                (left - right) * 0.5f,
                (bottom - top) * 0.5f);

        int frameSiblingIndex = cctvFrame.GetSiblingIndex();
        maskRect.SetSiblingIndex(frameSiblingIndex);
        rectTransform.SetParent(maskRect, true);
    }

    private void OnEnable()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        startPosition = rectTransform.anchoredPosition;
        direction = -1f;
        frameTimer = 0f;
        frameIndex = 0;
        ShowPigeon();
        ApplyFacingDirection();
    }

    private void Update()
    {
        UpdateUiAnimation();

        float minX = startPosition.x - moveDistance;
        float maxX = startPosition.x + moveDistance;
        Vector2 position = rectTransform.anchoredPosition;
        position.x += direction * moveSpeed *
                      Time.unscaledDeltaTime;

        if (position.x >= maxX)
        {
            position.x = maxX;
            direction = -1f;
            ApplyFacingDirection();
        }
        else if (position.x <= minX)
        {
            position.x = minX;
            direction = 1f;
            ApplyFacingDirection();
        }

        rectTransform.anchoredPosition = position;
    }

    private void LateUpdate()
    {
        ApplyFacingDirection();
    }

    private void ShowPigeon()
    {
        if (pigeonImage == null)
        {
            return;
        }

        Color color = pigeonImage.color;
        color.a = 1f;
        pigeonImage.color = color;

        if (animationFrames != null &&
            animationFrames.Length > 0 &&
            animationFrames[0] != null)
        {
            pigeonImage.sprite = animationFrames[0];
        }
    }

    private void UpdateUiAnimation()
    {
        if (pigeonImage == null ||
            animationFrames == null ||
            animationFrames.Length == 0)
        {
            return;
        }

        frameTimer += Time.unscaledDeltaTime;

        if (frameTimer < frameInterval)
        {
            return;
        }

        frameTimer %= frameInterval;
        frameIndex = (frameIndex + 1) % animationFrames.Length;

        if (animationFrames[frameIndex] != null)
        {
            pigeonImage.sprite = animationFrames[frameIndex];
        }
    }

    private void ApplyFacingDirection()
    {
        Vector3 scale = rectTransform.localScale;
        scale.x = direction < 0f
            ? originalScaleX
            : -originalScaleX;
        rectTransform.localScale = scale;
    }
}
