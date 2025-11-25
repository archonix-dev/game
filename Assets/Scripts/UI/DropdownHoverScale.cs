using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Аналог ButtonHoverScale, но для Dropdown: меняет цвета Image, текста и Outline при наведении.
/// </summary>
public class DropdownHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Настройки цветов")]
    [Tooltip("Цвет Image в обычном состоянии")]
    public Color normalImageColor = Color.black;

    [Tooltip("Цвет Image при наведении")]
    public Color hoverImageColor = new Color32(0, 255, 39, 255);

    [Tooltip("Элементы текста, которым будем менять цвет")]
    public List<Graphic> targetTexts = new List<Graphic>();

    [Tooltip("Цвет текста в обычном состоянии")]
    public Color normalTextColor = Color.white;

    [Tooltip("Цвет текста при наведении")]
    public Color hoverTextColor = Color.black;

    [Header("Обводка")]
    [Tooltip("Outline, который выключаем/включаем при наведении")]
    public Outline targetOutline;

    private Dropdown dropdown;
    private Image targetImage;

    void Start()
    {
        targetImage = GetComponent<Image>();
        dropdown = GetComponent<Dropdown>();

        if (targetOutline == null)
        {
            targetOutline = GetComponent<Outline>();
        }

        if (targetImage != null)
        {
            targetImage.color = normalImageColor;
        }

        SetTextColors(normalTextColor);
        SetOutlineState(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable() || targetImage == null) return;
        ApplyHoverVisuals(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsInteractable() || targetImage == null) return;
        ApplyHoverVisuals(false);
    }

    private void ApplyHoverVisuals(bool isHover)
    {
        if (targetImage != null)
        {
            targetImage.color = isHover ? hoverImageColor : normalImageColor;
        }

        SetTextColors(isHover ? hoverTextColor : normalTextColor);
        SetOutlineState(isHover);
    }

    private void SetTextColors(Color color)
    {
        if (targetTexts == null) return;
        for (int i = 0; i < targetTexts.Count; i++)
        {
            if (targetTexts[i] != null)
            {
                targetTexts[i].color = color;
            }
        }
    }

    private void SetOutlineState(bool state)
    {
        if (targetOutline != null)
        {
            targetOutline.enabled = state;
        }
    }

    private bool IsInteractable()
    {
        return dropdown == null || dropdown.interactable;
    }
}

