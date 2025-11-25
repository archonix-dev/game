using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Скрипт для плавного изменения Scale Image при наведении мыши
/// </summary>
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
    
    private Button button;
    private Image targetImage;
    
    void Start()
    {
        targetImage = GetComponent<Image>();
        // Получаем компонент Button
        button = GetComponent<Button>();
        
        // Получаем Outline, если не задан вручную
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
    
    /// <summary>
    /// Вызывается при наведении мыши на кнопку
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage == null) return;
        ApplyHoverVisuals(true);
    }
    
    /// <summary>
    /// Вызывается при убирании мыши с кнопки
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetImage == null) return;
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
    
    /// <summary>
    /// Вызывается при нажатии на кнопку
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // Проверяем, что кнопка не заблокирована
        if (button != null && !button.interactable)
        {
            return;
        }

        // При клике фиксируем визуальное состояние как наведённое
        ApplyHoverVisuals(true);
    }
}

