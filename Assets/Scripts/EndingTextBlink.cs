using UnityEngine;
using UnityEngine.UI;

public class EndingTextBlink : MonoBehaviour
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Graphic[] synchronizedGraphics;
    [Min(0.05f)]
    [SerializeField] private float blinkInterval = 0.5f;
    [Tooltip("0이면 계속 깜빡입니다.")]
    [Min(0f)]
    [SerializeField] private float blinkDuration;
    [SerializeField] private bool remainVisibleAfterBlink = true;

    private float originalAlpha = 1f;
    private float[] synchronizedOriginalAlphas;
    private float elapsed;
    private float intervalElapsed;
    private bool isVisible = true;

    private void Awake()
    {
        ResolveGraphic();
        RememberOriginalAlpha();
    }

    private void OnEnable()
    {
        ResolveGraphic();
        RememberOriginalAlpha();
        elapsed = 0f;
        intervalElapsed = 0f;
        SetVisible(true);
    }

    private void OnDisable()
    {
        SetVisible(true);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        elapsed += deltaTime;
        intervalElapsed += deltaTime;

        if (blinkDuration > 0f && elapsed >= blinkDuration)
        {
            SetVisible(remainVisibleAfterBlink);
            enabled = false;
            return;
        }

        if (intervalElapsed < blinkInterval)
        {
            return;
        }

        intervalElapsed %= blinkInterval;
        SetVisible(!isVisible);
    }

    private void ResolveGraphic()
    {
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }
    }

    private void RememberOriginalAlpha()
    {
        if (targetGraphic != null && targetGraphic.color.a > 0f)
        {
            originalAlpha = targetGraphic.color.a;
        }

        if (synchronizedGraphics == null)
        {
            synchronizedOriginalAlphas = null;
            return;
        }

        synchronizedOriginalAlphas =
            new float[synchronizedGraphics.Length];

        for (int i = 0; i < synchronizedGraphics.Length; i++)
        {
            Graphic graphic = synchronizedGraphics[i];
            synchronizedOriginalAlphas[i] =
                graphic != null && graphic.color.a > 0f
                    ? graphic.color.a
                    : 1f;
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (targetGraphic == null)
        {
            return;
        }

        Color color = targetGraphic.color;
        color.a = visible ? originalAlpha : 0f;
        targetGraphic.color = color;

        if (synchronizedGraphics == null)
        {
            return;
        }

        for (int i = 0; i < synchronizedGraphics.Length; i++)
        {
            Graphic graphic = synchronizedGraphics[i];

            if (graphic == null)
            {
                continue;
            }

            Color synchronizedColor = graphic.color;
            synchronizedColor.a = visible
                ? synchronizedOriginalAlphas[i]
                : 0f;
            graphic.color = synchronizedColor;
        }
    }
}
