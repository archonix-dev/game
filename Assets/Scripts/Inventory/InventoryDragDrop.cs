using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Система перетаскивания предметов в инвентаре
/// </summary>
public class InventoryDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("Drag Settings")]
    [SerializeField] private Canvas dragCanvas;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private float dragAlpha = 0.6f;
    
    private InventorySlot sourceSlot;
    private GameObject dragObject;
    private CanvasGroup dragCanvasGroup;
    private Vector3 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;
    
    void Start()
    {
        sourceSlot = GetComponent<InventorySlot>();
        
        // Находим Canvas если не назначен
        if (dragCanvas == null)
        {
            dragCanvas = GetComponentInParent<Canvas>();
            if (dragCanvas == null)
            {
                dragCanvas = FindObjectOfType<Canvas>();
            }
        }
        
        if (graphicRaycaster == null && dragCanvas != null)
        {
            graphicRaycaster = dragCanvas.GetComponent<GraphicRaycaster>();
        }
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (sourceSlot.IsEmpty) return;
        
        // Создаем объект для перетаскивания
        dragObject = new GameObject("DragObject");
        dragObject.transform.SetParent(dragCanvas.transform, false);
        
        // Копируем визуальные компоненты
        Image dragImage = dragObject.AddComponent<Image>();
        Image sourceImage = sourceSlot.transform.Find("Icon")?.GetComponent<Image>();
        
        if (sourceImage != null)
        {
            dragImage.sprite = sourceImage.sprite;
            dragImage.color = sourceImage.color;
        }
        
        // Настраиваем размер
        RectTransform dragRect = dragObject.GetComponent<RectTransform>();
        RectTransform sourceRect = sourceSlot.GetComponent<RectTransform>();
        dragRect.sizeDelta = sourceRect.sizeDelta;
        
        // Добавляем CanvasGroup для прозрачности
        dragCanvasGroup = dragObject.AddComponent<CanvasGroup>();
        dragCanvasGroup.alpha = dragAlpha;
        dragCanvasGroup.blocksRaycasts = false;
        
        // Запоминаем оригинальную позицию
        originalPosition = sourceSlot.transform.position;
        originalParent = sourceSlot.transform.parent;
        originalSiblingIndex = sourceSlot.transform.GetSiblingIndex();
        
        // Устанавливаем позицию под курсором
        dragRect.position = eventData.position;
        
        // Делаем слот полупрозрачным
        CanvasGroup slotCanvasGroup = sourceSlot.GetComponent<CanvasGroup>();
        if (slotCanvasGroup == null)
        {
            slotCanvasGroup = sourceSlot.gameObject.AddComponent<CanvasGroup>();
        }
        slotCanvasGroup.alpha = 0.5f;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (dragObject == null) return;
        
        // Обновляем позицию объекта перетаскивания
        dragObject.transform.position = eventData.position;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragObject == null) return;
        
        // Восстанавливаем прозрачность слота
        CanvasGroup slotCanvasGroup = sourceSlot.GetComponent<CanvasGroup>();
        if (slotCanvasGroup != null)
        {
            slotCanvasGroup.alpha = 1f;
        }
        
        // Уничтожаем объект перетаскивания
        Destroy(dragObject);
        dragObject = null;
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        // Находим источник перетаскивания
        InventoryDragDrop sourceDragDrop = eventData.pointerDrag?.GetComponent<InventoryDragDrop>();
        if (sourceDragDrop == null) return;
        
        InventorySlot targetSlot = GetComponent<InventorySlot>();
        InventorySlot sourceSlot = sourceDragDrop.sourceSlot;
        
        if (targetSlot == null || sourceSlot == null) return;
        
        // Выполняем обмен предметами
        SwapItems(sourceSlot, targetSlot);
    }
    
    /// <summary>
    /// Обменивает предметы между слотами
    /// </summary>
    void SwapItems(InventorySlot slot1, InventorySlot slot2)
    {
        if (slot1.IsEmpty && slot2.IsEmpty) return;
        
        // Если один из слотов пустой - просто перемещаем предмет
        if (slot1.IsEmpty)
        {
            InventoryItem item = slot2.Item;
            slot2.ClearSlot();
            slot1.AddItem(item);
        }
        else if (slot2.IsEmpty)
        {
            InventoryItem item = slot1.Item;
            slot1.ClearSlot();
            slot2.AddItem(item);
        }
        else
        {
            // Оба слота заняты - обмениваем предметы
            InventoryItem item1 = slot1.Item;
            InventoryItem item2 = slot2.Item;
            
            slot1.ClearSlot();
            slot2.ClearSlot();
            
            slot1.AddItem(item2);
            slot2.AddItem(item1);
        }
        
    }
    
    /// <summary>
    /// Проверяет, можно ли поместить предмет в целевой слот
    /// </summary>
    bool CanDropItem(InventorySlot targetSlot, InventoryItem item)
    {
        return targetSlot.IsEmpty; // Можно поместить только в пустой слот
    }
}
