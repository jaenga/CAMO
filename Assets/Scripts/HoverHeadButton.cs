using UnityEngine;
using UnityEngine.EventSystems;

public class HoverHeadButton :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private GameObject hoverBubble;
    [Min(1f)]
    [SerializeField] private float hoverScaleMultiplier = 1.08f;
    [Min(0f)]
    [SerializeField] private float scaleLerpSpeed = 12f;

    private Vector3 baseScale;
    private bool isHovered;

    private void Awake()
    {
        baseScale = transform.localScale;

        if (hoverBubble != null)
        {
            hoverBubble.SetActive(false);
        }
    }

    private void OnEnable()
    {
        isHovered = false;

        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        transform.localScale = baseScale;

        if (hoverBubble != null)
        {
            hoverBubble.SetActive(false);
        }
    }

    private void Update()
    {
        Vector3 targetScale = isHovered
            ? baseScale * hoverScaleMultiplier
            : baseScale;

        if (scaleLerpSpeed <= 0f)
        {
            transform.localScale = targetScale;
            return;
        }

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            scaleLerpSpeed * Time.unscaledDeltaTime);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        if (hoverBubble != null)
        {
            hoverBubble.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (hoverBubble != null)
        {
            hoverBubble.SetActive(false);
        }
    }
}
