using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Отдельный слот инвентаря
/// </summary>
public class InventorySlot : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image selectedFrame;
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    
    private InventoryItem item;
    private bool isSelected = false;
    private bool isEmpty = true;
    
    public InventoryItem Item => item;
    public bool IsEmpty => isEmpty;
    public bool IsSelected => isSelected;
    
    void Awake()
    {
        // Автоматически находим компоненты если не назначены
        if (iconImage == null)
            iconImage = transform.Find("Icon")?.GetComponent<Image>();
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (selectedFrame == null)
            selectedFrame = transform.Find("SelectedFrame")?.GetComponent<Image>();
        
        ClearSlot();
    }
    
    /// <summary>
    /// Добавляет предмет в слот (только 1 предмет на слот)
    /// </summary>
    public bool AddItem(InventoryItem newItem)
    {
        if (isEmpty)
        {
            item = newItem;
            isEmpty = false;
            UpdateVisuals();
            return true;
        }
        
        return false; // Слот уже занят
    }
    
    /// <summary>
    /// Удаляет предмет из слота
    /// </summary>
    public InventoryItem RemoveItem()
    {
        if (isEmpty)
            return null;
        
        InventoryItem removedItem = item;
        ClearSlot();
        return removedItem;
    }
    
    /// <summary>
    /// Очищает слот
    /// </summary>
    public void ClearSlot()
    {
        item = null;
        isEmpty = true;
        UpdateVisuals();
    }
    
    /// <summary>
    /// Устанавливает выбранное состояние слота
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisuals();
    }
    
    /// <summary>
    /// Обновляет визуальное представление слота
    /// </summary>
    private void UpdateVisuals()
    {
        if (isEmpty)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = emptyColor;
            }
            
            if (backgroundImage != null)
                backgroundImage.color = isSelected ? selectedColor : normalColor;
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = item.icon;
                iconImage.color = Color.white;
            }
            
            if (backgroundImage != null)
                backgroundImage.color = isSelected ? selectedColor : normalColor;
        }
        
        if (selectedFrame != null)
            selectedFrame.gameObject.SetActive(isSelected);
    }
    
    /// <summary>
    /// Проверяет, можно ли добавить предмет в этот слот
    /// </summary>
    public bool CanAddItem(InventoryItem newItem)
    {
        return isEmpty; // Можно добавить только если слот пустой
    }
}
