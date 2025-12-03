using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Скрипт для плавного изменения Scale Image при наведении мыши и клике
/// </summary>
public class DropdownHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Настройки цветов")]
    [Tooltip("Цвет Image в обычном состоянии")]
    public Color normalImageColor = Color.black;

    [Tooltip("Цвет Image при наведении")]
    public Color hoverImageColor = new Color32(62, 169, 78, 255);

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

        SetOutlineState(isHover);
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

