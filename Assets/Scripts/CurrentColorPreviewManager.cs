using UnityEngine;
using UnityEngine.UI;

public class CurrentColorPreviewManager : MonoBehaviour
{
    [SerializeField] private FlexibleColorPicker colorPicker;
    [SerializeField] private Image currentColorPreview;

    private void Awake()
    {
        ResolveReferences();
        RefreshPreview();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (colorPicker != null)
        {
            colorPicker.onColorChange.AddListener(
                UpdatePreviewColor);
        }

        RefreshPreview();
    }

    private void OnDisable()
    {
        if (colorPicker != null)
        {
            colorPicker.onColorChange.RemoveListener(
                UpdatePreviewColor);
        }
    }

    private void ResolveReferences()
    {
        if (currentColorPreview == null)
        {
            currentColorPreview = GetComponent<Image>();
        }

        if (colorPicker == null)
        {
            colorPicker = FindFirstObjectByType<FlexibleColorPicker>(
                FindObjectsInactive.Include);
        }
    }

    private void RefreshPreview()
    {
        if (colorPicker == null ||
            currentColorPreview == null)
        {
            return;
        }

        UpdatePreviewColor(colorPicker.color);
    }

    private void UpdatePreviewColor(Color color)
    {
        if (currentColorPreview != null)
        {
            currentColorPreview.color = color;
        }
    }
}
