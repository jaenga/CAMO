using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RecentColorManager : MonoBehaviour
{
    private const int MaxRecentColors = 6;

    [SerializeField] private FlexibleColorPicker colorPicker;
    [SerializeField] private Button[] recentColorButtons;
    [SerializeField] private Image[] recentColorImages;
    [Range(0.001f, 0.1f)]
    [SerializeField] private float colorComparisonTolerance = 0.01f;

    private readonly List<Color> recentColors = new List<Color>();
    private UnityAction[] buttonListeners;

    private void Awake()
    {
        ResolveReferences();
        RegisterButtonListeners();
        RefreshSlots();
    }

    private void OnDestroy()
    {
        UnregisterButtonListeners();
    }

    public void RecordColor(Color color)
    {
        int duplicateIndex = FindMatchingColorIndex(color);

        if (duplicateIndex >= 0)
        {
            recentColors.RemoveAt(duplicateIndex);
        }

        recentColors.Insert(0, color);

        if (recentColors.Count > MaxRecentColors)
        {
            recentColors.RemoveRange(
                MaxRecentColors,
                recentColors.Count - MaxRecentColors);
        }

        RefreshSlots();
    }

    private void SelectRecentColor(int index)
    {
        if (colorPicker == null ||
            index < 0 ||
            index >= recentColors.Count)
        {
            return;
        }

        Color selectedColor = recentColors[index];
        colorPicker.color = selectedColor;
        RecordColor(selectedColor);
    }

    private int FindMatchingColorIndex(Color color)
    {
        for (int i = 0; i < recentColors.Count; i++)
        {
            if (ColorsApproximatelyEqual(recentColors[i], color))
            {
                return i;
            }
        }

        return -1;
    }

    private bool ColorsApproximatelyEqual(Color first, Color second)
    {
        return Mathf.Abs(first.r - second.r) <= colorComparisonTolerance &&
               Mathf.Abs(first.g - second.g) <= colorComparisonTolerance &&
               Mathf.Abs(first.b - second.b) <= colorComparisonTolerance &&
               Mathf.Abs(first.a - second.a) <= colorComparisonTolerance;
    }

    private void RefreshSlots()
    {
        int slotCount = Mathf.Min(
            MaxRecentColors,
            recentColorImages != null ? recentColorImages.Length : 0);

        for (int i = 0; i < slotCount; i++)
        {
            Image slotImage = recentColorImages[i];

            if (slotImage == null)
            {
                continue;
            }

            bool hasColor = i < recentColors.Count;
            slotImage.color = hasColor
                ? recentColors[i]
                : Color.clear;

            if (recentColorButtons != null &&
                i < recentColorButtons.Length &&
                recentColorButtons[i] != null)
            {
                recentColorButtons[i].interactable = hasColor;
            }
        }
    }

    private void ResolveReferences()
    {
        if (colorPicker == null)
        {
            colorPicker = GetComponentInParent<FlexibleColorPicker>(true);
        }

        if (colorPicker == null)
        {
            colorPicker = FindFirstObjectByType<FlexibleColorPicker>(
                FindObjectsInactive.Include);
        }

        if (recentColorButtons == null ||
            recentColorButtons.Length != MaxRecentColors)
        {
            recentColorButtons = new Button[MaxRecentColors];
        }

        if (recentColorImages == null ||
            recentColorImages.Length != MaxRecentColors)
        {
            recentColorImages = new Image[MaxRecentColors];
        }

        for (int i = 0; i < MaxRecentColors; i++)
        {
            Transform slot = transform.Find($"RecentColor_{i}");

            if (slot == null)
            {
                continue;
            }

            if (recentColorButtons[i] == null)
            {
                recentColorButtons[i] = slot.GetComponent<Button>();
            }

            if (recentColorImages[i] == null)
            {
                recentColorImages[i] = slot.GetComponent<Image>();
            }
        }
    }

    private void RegisterButtonListeners()
    {
        if (recentColorButtons == null)
        {
            return;
        }

        buttonListeners = new UnityAction[MaxRecentColors];
        int count = Mathf.Min(
            MaxRecentColors,
            recentColorButtons.Length);

        for (int i = 0; i < count; i++)
        {
            Button button = recentColorButtons[i];

            if (button == null)
            {
                continue;
            }

            int slotIndex = i;
            buttonListeners[i] =
                () => SelectRecentColor(slotIndex);
            button.onClick.AddListener(buttonListeners[i]);
        }
    }

    private void UnregisterButtonListeners()
    {
        if (recentColorButtons == null)
        {
            return;
        }

        if (buttonListeners == null)
        {
            return;
        }

        int count = Mathf.Min(
            recentColorButtons.Length,
            buttonListeners.Length);

        for (int i = 0; i < count; i++)
        {
            if (recentColorButtons[i] != null &&
                buttonListeners[i] != null)
            {
                recentColorButtons[i].onClick.RemoveListener(
                    buttonListeners[i]);
            }
        }
    }
}
