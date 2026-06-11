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
    [SerializeField] private Texture2D targetPattern;
    [SerializeField] private GameObject drawingPanel;
    [SerializeField] private GameObject gate;
    private Color brushColor = Color.green;
    private int brushSize = 2;

    private Vector2Int previousPixel;
    private bool hasPreviousPixel;
    private readonly List<Color[]> undoHistory = new List<Color[]>();

    private void Start()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();

        texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        rawImage.texture = texture;
        FillCanvasWhite();

        if (brushSizeSlider != null)
        {
            brushSizeSlider.minValue = 1;
            brushSizeSlider.maxValue = 10;
            brushSizeSlider.wholeNumbers = true;
            brushSizeSlider.value = brushSize;
            brushSizeSlider.onValueChanged.AddListener(SetBrushSize);
        }
    }

    private void OnDestroy()
    {
        if (brushSizeSlider != null)
        {
            brushSizeSlider.onValueChanged.RemoveListener(SetBrushSize);
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

    public void SetGreen()
    {
        brushColor = Color.green;
    }

    public void SetBrown()
    {
        brushColor = new Color32(150, 75, 0, 255);
    }

    public void SetPink()
    {
        brushColor = new Color32(255, 105, 180, 255);
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

    public void SubmitDrawing()
    {
        if (texture == null)
        {
            Debug.LogWarning("[DrawingTest] Drawing texture has not been initialized.", this);
            HideDrawingPanel();
            return;
        }

        if (targetPattern == null)
        {
            Debug.LogWarning("[DrawingTest] Target Pattern is not assigned.", this);
            HideDrawingPanel();
            return;
        }

        Texture2D downsampledDrawing = DownsampleTexture(texture);
        Texture2D downsampledTarget = DownsampleTexture(targetPattern);

        float totalScore = 0f;

        for (int region = 0; region < 4; region++)
        {
            Color drawingAverage = GetRegionAverageColor(downsampledDrawing, region);
            Color targetAverage = GetRegionAverageColor(downsampledTarget, region);
            totalScore += CalculateColorSimilarity(drawingAverage, targetAverage);
        }

        Destroy(downsampledDrawing);
        Destroy(downsampledTarget);

        float similarityScore = Mathf.Clamp(totalScore / 4f, 0f, 100f);
        string result = GetSimilarityResult(similarityScore);

        Debug.Log(
            $"[DrawingTest] Similarity Score: {similarityScore:F2} / 100 - {result}",
            this);

        if (result != "Fail")
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
        }
        else
        {
            Debug.Log("[DrawingTest] Submission failed. Gate remains closed.", this);
        }

        HideDrawingPanel();
    }

    public void ClearCanvas()
    {
        if (texture == null)
        {
            return;
        }

        SaveUndoState();
        FillCanvasWhite();
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

    private float CalculateColorSimilarity(Color first, Color second)
    {
        float redDifference = first.r - second.r;
        float greenDifference = first.g - second.g;
        float blueDifference = first.b - second.b;
        float colorDistance = Mathf.Sqrt(
            redDifference * redDifference +
            greenDifference * greenDifference +
            blueDifference * blueDifference);
        float normalizedDistance = colorDistance / Mathf.Sqrt(3f);

        return (1f - normalizedDistance) * 100f;
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

    private void FillCanvasWhite()
    {
        Color[] pixels = new Color[TextureSize * TextureSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
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
