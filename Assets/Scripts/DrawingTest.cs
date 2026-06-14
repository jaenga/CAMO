using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class DrawingTest : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int TextureSize = 128;
    private const int ComparisonSize = 32;
    private const int MaxUndoCount = 10;

    private Texture2D texture;
    private RawImage rawImage;
    private RectTransform rectTransform;
    [SerializeField] private Slider brushSizeSlider;
    [SerializeField] private FlexibleColorPicker colorPicker;
    [SerializeField] private Texture2D targetPattern;
    [SerializeField] private GameObject drawingPanel;
    [SerializeField] private GameObject gate;
    [SerializeField] private PredatorController predatorController;
    [SerializeField] private RawImage chameleonPreviewImage;
    [SerializeField] private Image chameleonLineImage;
    [Range(0.05f, 1.5f)]
    [SerializeField] private float colorTolerance = 0.5f;
    private Color brushColor = new Color32(255, 105, 180, 255);
    private int brushSize = 2;

    private Vector2Int previousPixel;
    private bool hasPreviousPixel;
    private bool isCanvasMirrored;
    private Texture2D runtimeAnswerTexture;
    private readonly List<Color[]> undoHistory = new List<Color[]>();

    private void Awake()
    {
        EnsureCanvasReferences(false);
    }

    private void Start()
    {
        EnsureCanvasReferences(true);

        texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        if (rawImage != null)
        {
            rawImage.texture = texture;
        }

        FillCanvasTransparent();

        if (chameleonPreviewImage != null)
        {
            chameleonPreviewImage.gameObject.SetActive(false);
        }

        if (colorPicker != null)
        {
            brushColor = colorPicker.color;

            Debug.Log(
                $"[DrawingTest] FlexibleColorPicker connected. Initial brush color: {brushColor}",
                this);
        }
        else
        {
            Debug.LogWarning(
                $"[DrawingTest] FlexibleColorPicker is not assigned. Default Pink brush color will be used: {brushColor}",
                this);
        }

        if (brushSizeSlider != null)
        {
            brushSizeSlider.minValue = 1;
            brushSizeSlider.maxValue = 10;
            brushSizeSlider.wholeNumbers = true;
            brushSizeSlider.value = brushSize;
            brushSizeSlider.onValueChanged.AddListener(SetBrushSize);
        }
    }

    private void Update()
    {
        if (colorPicker == null)
        {
            return;
        }

        Color pickerColor = colorPicker.color;

        if (pickerColor == brushColor)
        {
            return;
        }

        SetBrushColor(pickerColor);
    }

    private void OnDestroy()
    {
        if (brushSizeSlider != null)
        {
            brushSizeSlider.onValueChanged.RemoveListener(SetBrushSize);
        }

        if (runtimeAnswerTexture != null)
        {
            Destroy(runtimeAnswerTexture);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SaveUndoState();
        hasPreviousPixel = false;
        Draw(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Draw(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        hasPreviousPixel = false;
    }

    public void ToggleCanvasPreviewMode()
    {
        if (chameleonPreviewImage == null)
        {
            Debug.LogWarning(
                "[DrawingTest] Chameleon Preview Image is not assigned.",
                this);
            return;
        }

        GameObject previewObject = chameleonPreviewImage.gameObject;
        previewObject.SetActive(!previewObject.activeSelf);
    }

    public void SetPreviewFacingDirection(bool isFacingRight)
    {
        SetCanvasMirrored(!isFacingRight);

        Debug.Log(
            $"[DrawingTest] Canvas preview facing direction: {(isFacingRight ? "Right" : "Left")}.",
            this);
    }

    public void SetCanvasMirrored(bool mirrored)
    {
        isCanvasMirrored = mirrored;
        EnsureCanvasReferences(true);

        // DrawingCanvas 자체는 반전하지 않고 표시 UV와 입력 좌표를 함께 반전합니다.
        if (rectTransform != null)
        {
            Vector3 canvasScale = rectTransform.localScale;
            canvasScale.x = Mathf.Abs(canvasScale.x);
            rectTransform.localScale = canvasScale;
        }

        if (rawImage != null)
        {
            rawImage.uvRect = isCanvasMirrored
                ? new Rect(1f, 0f, -1f, 1f)
                : new Rect(0f, 0f, 1f, 1f);
        }

        if (chameleonLineImage != null)
        {
            RectTransform lineRectTransform =
                chameleonLineImage.rectTransform != null
                    ? chameleonLineImage.rectTransform
                    : chameleonLineImage.GetComponent<RectTransform>();

            SetHorizontalMirrored(
                lineRectTransform,
                isCanvasMirrored);
        }

        if (chameleonPreviewImage != null)
        {
            RectTransform previewRectTransform =
                chameleonPreviewImage.rectTransform != null
                    ? chameleonPreviewImage.rectTransform
                    : chameleonPreviewImage.GetComponent<RectTransform>();

            SetHorizontalMirrored(
                previewRectTransform,
                isCanvasMirrored);
        }
    }

    private void EnsureCanvasReferences(bool logWarnings)
    {
        if (rawImage == null)
        {
            rawImage = GetComponent<RawImage>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (chameleonLineImage == null)
        {
            chameleonLineImage = FindLineImage();
        }

        if (chameleonPreviewImage == null)
        {
            chameleonPreviewImage = FindPreviewImage();
        }

        if (!logWarnings)
        {
            return;
        }

        if (rawImage == null)
        {
            Debug.LogWarning(
                "[DrawingTest] DrawingCanvas RawImage was not found. Drawing preview mirroring was skipped.",
                this);
        }

        if (rectTransform == null)
        {
            Debug.LogWarning(
                "[DrawingTest] DrawingCanvas RectTransform was not found. Canvas transform mirroring was skipped.",
                this);
        }

        if (chameleonLineImage == null)
        {
            Debug.LogWarning(
                "[DrawingTest] ChameleonLineImage was not found. Line image mirroring was skipped.",
                this);
        }

        if (chameleonPreviewImage == null)
        {
            Debug.LogWarning(
                "[DrawingTest] ChameleonPreviewImage was not found. Optional preview mirroring was skipped.",
                this);
        }
    }

    private Image FindLineImage()
    {
        Transform lineTransform = transform.Find("ChameleonLineImage");

        if (lineTransform == null && transform.parent != null)
        {
            lineTransform =
                transform.parent.Find("ChameleonLineImage");
        }

        if (lineTransform != null)
        {
            Image directImage = lineTransform.GetComponent<Image>();

            if (directImage != null)
            {
                return directImage;
            }
        }

        Image[] childImages = GetComponentsInChildren<Image>(true);

        foreach (Image childImage in childImages)
        {
            if (childImage != null &&
                childImage.name == "ChameleonLineImage")
            {
                return childImage;
            }
        }

        return null;
    }

    private RawImage FindPreviewImage()
    {
        Transform previewTransform =
            transform.Find("ChameleonPreviewImage");

        if (previewTransform == null && transform.parent != null)
        {
            previewTransform =
                transform.parent.Find("ChameleonPreviewImage");
        }

        if (previewTransform != null)
        {
            RawImage directPreview =
                previewTransform.GetComponent<RawImage>();

            if (directPreview != null)
            {
                return directPreview;
            }
        }

        Transform searchRoot =
            transform.parent != null ? transform.parent : transform;
        RawImage[] childRawImages =
            searchRoot.GetComponentsInChildren<RawImage>(true);

        foreach (RawImage childRawImage in childRawImages)
        {
            if (childRawImage != null &&
                childRawImage.name == "ChameleonPreviewImage")
            {
                return childRawImage;
            }
        }

        return null;
    }

    private void SetHorizontalMirrored(
        RectTransform target,
        bool mirrored)
    {
        if (target == null)
        {
            return;
        }

        Vector3 scale = target.localScale;
        scale.x = Mathf.Abs(scale.x) *
                  (mirrored ? -1f : 1f);
        target.localScale = scale;
    }

    public void SetSmallBrush()
    {
        brushSize = 2;
    }

    public void SetBigBrush()
    {
        brushSize = 5;
    }

    public void SetBrushSize(float value)
    {
        int previousBrushSize = brushSize;
        brushSize = Mathf.Clamp(Mathf.RoundToInt(value), 1, 10);

        Debug.Log(
            $"[DrawingTest] Slider Value: {value:F2}, Brush Size: {previousBrushSize} -> {brushSize}",
            this);
    }

    private void SetBrushColor(Color color)
    {
        brushColor = color;

        Debug.Log(
            $"[DrawingTest] Brush color updated from FlexibleColorPicker: {brushColor}",
            this);
    }

    public void SetAnswerTexture(Texture2D newAnswer)
    {
        if (newAnswer == null)
        {
            Debug.LogWarning(
                "[DrawingTest] New answer texture is null. Existing answer texture was kept.",
                this);
            return;
        }

        if (runtimeAnswerTexture != null &&
            runtimeAnswerTexture != newAnswer)
        {
            Destroy(runtimeAnswerTexture);
        }

        runtimeAnswerTexture = newAnswer;
        targetPattern = newAnswer;

        Debug.Log(
            $"[DrawingTest] Answer texture updated: {newAnswer.width}x{newAnswer.height}",
            this);
    }

    public Texture2D GetCurrentDrawingTexture()
    {
        if (texture == null)
        {
            Debug.LogWarning(
                "[DrawingTest] Current drawing texture is not initialized.",
                this);
            return null;
        }

        Debug.Log(
            $"[DrawingTest] Current drawing texture requested: {texture.width}x{texture.height}",
            this);

        return texture;
    }

    public void SubmitDrawing()
    {
        if (!TryEvaluateCamouflage(
                out float similarityScore,
                out string result))
        {
            HideDrawingPanel();
            return;
        }

        bool isSuccess =
            result == "Perfect" ||
            result == "Success";

        if (isSuccess)
        {
            if (gate != null)
            {
                gate.SetActive(false);
                Debug.Log(
                    $"[DrawingTest] Gate '{gate.name}' opened after result: {result}.",
                    gate);
            }
            else
            {
                Debug.LogWarning("[DrawingTest] Gate is not assigned.", this);
            }

            if (predatorController != null)
            {
                predatorController.ResolveCamouflageSubmission(true);
            }
            else
            {
                Debug.LogWarning("[DrawingTest] PredatorController is not assigned.", this);
            }
        }
        else
        {
            Debug.Log(
                $"[DrawingTest] Submission result '{result}' treated as failure. Gate remains closed.",
                this);

            if (predatorController != null)
            {
                predatorController.ResolveCamouflageSubmission(false);
            }
            else
            {
                Debug.LogWarning("[DrawingTest] PredatorController is not assigned.", this);
            }
        }

        HideDrawingPanel();
    }

    public bool TryEvaluateCamouflage(
        out float similarityScore,
        out string result)
    {
        similarityScore = 0f;
        result = "Fail";

        if (texture == null)
        {
            Debug.LogWarning(
                "[DrawingTest] Drawing texture has not been initialized.",
                this);
            return false;
        }

        if (targetPattern == null)
        {
            Debug.LogWarning(
                "[DrawingTest] Target Pattern is not assigned.",
                this);
            return false;
        }

        Color drawingAverageColor = CalculateAverageColor(texture);
        Debug.Log(
            $"[DrawingTest] Drawing average color: {drawingAverageColor}",
            this);

        Texture2D downsampledDrawing = DownsampleTexture(texture);
        Texture2D downsampledTarget = DownsampleTexture(targetPattern);
        float totalScore = 0f;

        for (int region = 0; region < 4; region++)
        {
            Color drawingAverage =
                GetRegionAverageColor(downsampledDrawing, region);
            Color targetAverage =
                GetRegionAverageColor(downsampledTarget, region);
            totalScore +=
                CalculateColorSimilarity(drawingAverage, targetAverage);
        }

        Destroy(downsampledDrawing);
        Destroy(downsampledTarget);

        similarityScore = Mathf.Clamp(totalScore / 4f, 0f, 100f);
        result = GetSimilarityResult(similarityScore);
        bool isSuccess =
            result == "Perfect" ||
            result == "Success";

        Debug.Log(
            $"[DrawingTest] Similarity Score: {similarityScore:F2} / 100 - {result}. Color tolerance: {colorTolerance:F2}. Success: {isSuccess}",
            this);

        return true;
    }

    public void ClearCanvas()
    {
        if (texture == null)
        {
            return;
        }

        SaveUndoState();
        FillCanvasTransparent();
    }

    public void Undo()
    {
        if (texture == null || undoHistory.Count == 0)
        {
            return;
        }

        int lastIndex = undoHistory.Count - 1;
        Color[] previousPixels = undoHistory[lastIndex];
        undoHistory.RemoveAt(lastIndex);

        texture.SetPixels(previousPixels);
        texture.Apply();
        hasPreviousPixel = false;
    }

    private void SaveUndoState()
    {
        if (texture == null)
        {
            return;
        }

        if (undoHistory.Count >= MaxUndoCount)
        {
            undoHistory.RemoveAt(0);
        }

        undoHistory.Add(texture.GetPixels());
    }

    private void HideDrawingPanel()
    {
        if (drawingPanel == null)
        {
            return;
        }

        drawingPanel.SetActive(false);
        Debug.Log($"[DrawingTest] Drawing Panel '{drawingPanel.name}' hidden.", this);
    }

    private Texture2D DownsampleTexture(Texture source)
    {
        RenderTexture temporaryRenderTexture = RenderTexture.GetTemporary(
            ComparisonSize,
            ComparisonSize,
            0,
            RenderTextureFormat.ARGB32);
        RenderTexture previousRenderTexture = RenderTexture.active;

        Graphics.Blit(source, temporaryRenderTexture);
        RenderTexture.active = temporaryRenderTexture;

        Texture2D downsampledTexture = new Texture2D(
            ComparisonSize,
            ComparisonSize,
            TextureFormat.RGBA32,
            false);
        downsampledTexture.ReadPixels(
            new Rect(0, 0, ComparisonSize, ComparisonSize),
            0,
            0);
        downsampledTexture.Apply();

        RenderTexture.active = previousRenderTexture;
        RenderTexture.ReleaseTemporary(temporaryRenderTexture);

        return downsampledTexture;
    }

    private Color GetRegionAverageColor(Texture2D source, int region)
    {
        int halfSize = ComparisonSize / 2;
        int startX = region % 2 * halfSize;
        int startY = region / 2 * halfSize;
        Color totalColor = Color.black;

        for (int x = startX; x < startX + halfSize; x++)
        {
            for (int y = startY; y < startY + halfSize; y++)
            {
                totalColor += source.GetPixel(x, y);
            }
        }

        return totalColor / (halfSize * halfSize);
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

    private float CalculateColorSimilarity(Color first, Color second)
    {
        float redDifference = first.r - second.r;
        float greenDifference = first.g - second.g;
        float blueDifference = first.b - second.b;
        float colorDistance = Mathf.Sqrt(
            redDifference * redDifference +
            greenDifference * greenDifference +
            blueDifference * blueDifference);
        float safeTolerance = Mathf.Clamp(
            colorTolerance,
            0.01f,
            Mathf.Sqrt(3f) - 0.01f);

        if (colorDistance <= safeTolerance)
        {
            float toleratedDifference = colorDistance / safeTolerance;
            return Mathf.Lerp(100f, 70f, toleratedDifference);
        }

        float excessDifference = Mathf.InverseLerp(
            safeTolerance,
            Mathf.Sqrt(3f),
            colorDistance);

        return Mathf.Lerp(70f, 0f, excessDifference);
    }

    private string GetSimilarityResult(float similarityScore)
    {
        if (similarityScore >= 80f)
        {
            return "Perfect";
        }

        if (similarityScore >= 60f)
        {
            return "Success";
        }

        if (similarityScore >= 40f)
        {
            return "Danger";
        }

        return "Fail";
    }

    private void FillCanvasTransparent()
    {
        Color[] pixels = new Color[TextureSize * TextureSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        hasPreviousPixel = false;
    }

    private void Draw(PointerEventData eventData)
    {
        if (!TryGetTexturePixel(eventData, out Vector2Int currentPixel))
        {
            hasPreviousPixel = false;
            return;
        }

        if (hasPreviousPixel)
        {
            DrawLine(previousPixel, currentPixel);
        }
        else
        {
            DrawBrush(currentPixel.x, currentPixel.y);
        }

        previousPixel = currentPixel;
        hasPreviousPixel = true;
        texture.Apply();
    }

    private bool TryGetTexturePixel(PointerEventData eventData, out Vector2Int pixel)
    {
        pixel = default;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;

        if (!rect.Contains(localPoint))
        {
            return false;
        }

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        if (isCanvasMirrored)
        {
            normalizedX = 1f - normalizedX;
        }

        int x = Mathf.FloorToInt(normalizedX * TextureSize);
        int y = Mathf.FloorToInt(normalizedY * TextureSize);

        if (x < 0 || x >= TextureSize || y < 0 || y >= TextureSize)
        {
            return false;
        }

        pixel = new Vector2Int(x, y);
        return true;
    }

    private void DrawLine(Vector2Int from, Vector2Int to)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));

            DrawBrush(x, y);
        }
    }

    private void DrawBrush(int centerX, int centerY)
    {
        for (int x = -brushSize; x <= brushSize; x++)
        {
            for (int y = -brushSize; y <= brushSize; y++)
            {
                int pixelX = centerX + x;
                int pixelY = centerY + y;

                if (pixelX >= 0 && pixelX < TextureSize &&
                    pixelY >= 0 && pixelY < TextureSize)
                {
                    texture.SetPixel(pixelX, pixelY, brushColor);
                }
            }
        }
    }
}
