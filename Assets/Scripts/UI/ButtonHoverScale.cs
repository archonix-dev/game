using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Скрипт для плавного изменения Scale Image при наведении мыши и клике
/// </summary>
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Настройки цветов")]
    [Tooltip("Цвет Image в обычном состоянии")]
    public Color normalImageColor = Color.black;

    [Tooltip("Цвет Image при наведении")]
    public Color hoverImageColor = new Color32(62, 169, 78, 255);

    [Header("Обводка")]
    [Tooltip("Outline, который выключаем/включаем при наведении")]
    public Outline targetOutline;
    
    private Button button;
    private Image targetImage;
    
    void Start()
    {
        targetImage = GetComponent<Image>();
        button = GetComponent<Button>();
        
        if (targetOutline == null)
        {
            targetOutline = GetComponent<Outline>();
        }

        if (targetImage != null)
        {
            targetImage.color = normalImageColor;
        }
        SetOutlineState(false);
    }
    
    /// <summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage == null) return;
        ApplyHoverVisuals(true);
    }
    
    /// <summary>
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

        SetOutlineState(isHover);
    }

    private void SetOutlineState(bool state)
    {
        if (targetOutline != null)
        {
            targetOutline.enabled = state;
        }
    }
    
    /// <summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button != null && !button.interactable)
        {
            return;
        }

        ApplyHoverVisuals(true);
    }
}

