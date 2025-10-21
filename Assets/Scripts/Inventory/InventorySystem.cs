using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Основная система инвентаря с хотбаром
/// </summary>
public class InventorySystem : MonoBehaviour
{
    [Header("Hotbar Settings")]
    [SerializeField] private int hotbarSlots = 3;
    [SerializeField] private Canvas hotbarCanvas;
    [SerializeField] private Transform hotbarParent;
    [SerializeField] private GameObject slotPrefab;
    
    [Header("Hand Display")]
    [SerializeField] private Transform handTransform; // Точка в руке для отображения предмета
    [SerializeField] private Vector3 handOffset = Vector3.zero;
    [SerializeField] private Vector3 handRotation = Vector3.zero;
    
    [Header("Pickup Settings")]
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField] private LayerMask pickupLayerMask = -1;
    
    [Header("Drop Settings")]
    [SerializeField] private float dropForce = 5f;
    [SerializeField] private Transform dropPoint; // Точка выброса предметов
    
    [Header("Item Name Display")]
    [SerializeField] private Text itemNameText; // Текст для отображения названия предмета
    [SerializeField] private float displayDuration = 3f; // Время отображения названия
    [SerializeField] private float fadeDuration = 3f; // Время исчезновения
    
    [Header("Default Drop Settings")]
    [SerializeField] private string defaultDropTag = "Untagged"; // Тег по умолчанию для выброшенных предметов
    [SerializeField] private int defaultDropLayer = 0; // Слой по умолчанию для выброшенных предметов
    
    [Header("Grab System Reference")]
    [SerializeField] private PickupableGrabSystem grabSystem; // Ссылка на систему захвата
    
    private InventorySlot[] hotbarSlotsArray;
    private int currentSelectedSlot = 0;
    private GameObject currentHandItem;
    private Camera playerCamera;
    private PickupableItem currentLookingAtPickupable;
    private Coroutine itemNameDisplayCoroutine;
    private bool isDraggingItem = false;
    
    // События для UI
    public System.Action<int> OnSlotChanged;
    public System.Action<InventoryItem> OnItemPickedUp;
    public System.Action<InventoryItem> OnItemDropped;
    
    // События для перетаскивания
    public System.Action OnDragStarted;
    public System.Action OnDragEnded;

    private bool isDraggingItemplus = false;
    
    void Start()
    {
        InitializeInventory();
        playerCamera = Camera.main;
        
        // Автоматически находим систему захвата если не назначена
        if (grabSystem == null)
        {
            grabSystem = FindObjectOfType<PickupableGrabSystem>();
        }
        
        if (dropPoint == null)
        {
            GameObject dropPointObj = new GameObject("DropPoint");
            dropPointObj.transform.SetParent(transform);
            dropPointObj.transform.localPosition = new Vector3(0, 0, 1f);
            dropPoint = dropPointObj.transform;
        }
        
        // Создаем текст для отображения названия предмета если не назначен
        if (itemNameText == null)
        {
            CreateItemNameDisplay();
        }
        
        // Инициализируем предметы в руке
        InitializeHandItems();
    }
    
    void Update()
    {
        HandleInput();
        UpdateHandDisplay();
        CheckForPickupableObjects();

        if(Input.GetMouseButton(0))
        {
            isDraggingItemplus = true;
        }
        else
        {
            isDraggingItemplus = false;
        }
    }
    
    /// <summary>
    /// Инициализирует систему инвентаря
    /// </summary>
    void InitializeInventory()
    {
        // Создаем UI если не назначен
        if (hotbarCanvas == null)
        {
            CreateHotbarUI();
        }
        
        hotbarSlotsArray = new InventorySlot[hotbarSlots];
        
        // Создаем слоты хотбара
        for (int i = 0; i < hotbarSlots; i++)
        {
            GameObject slotObj;
            
            if (slotPrefab != null)
            {
                slotObj = Instantiate(slotPrefab, hotbarParent);
            }
            else
            {
                // Создаем простой слот если префаб не назначен
                slotObj = CreateSimpleSlot();
                slotObj.transform.SetParent(hotbarParent);
            }
            
            InventorySlot slot = slotObj.GetComponent<InventorySlot>();
            if (slot == null)
            {
                slot = slotObj.AddComponent<InventorySlot>();
            }
            
            // Добавляем компонент перетаскивания
            InventoryDragDrop dragDrop = slotObj.GetComponent<InventoryDragDrop>();
            if (dragDrop == null)
            {
                dragDrop = slotObj.AddComponent<InventoryDragDrop>();
            }
            
            // Добавляем EventTrigger для обработки событий мыши
            UnityEngine.EventSystems.EventTrigger eventTrigger = slotObj.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = slotObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }
            
            hotbarSlotsArray[i] = slot;
            slotObj.name = $"HotbarSlot_{i}";
        }
        
        // Выбираем первый слот
        SelectSlot(0);
    }
    
    /// <summary>
    /// Создает UI для хотбара
    /// </summary>
    void CreateHotbarUI()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("HotbarCanvas");
        hotbarCanvas = canvasObj.AddComponent<Canvas>();
        hotbarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hotbarCanvas.sortingOrder = 100;
        
        // Добавляем CanvasScaler
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Добавляем GraphicRaycaster
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем панель для хотбара
        GameObject panelObj = new GameObject("HotbarPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 50);
        panelRect.sizeDelta = new Vector2(300, 100);
        
        // Добавляем фон панели
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.3f);
        
        // Создаем HorizontalLayoutGroup
        HorizontalLayoutGroup layoutGroup = panelObj.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        
        hotbarParent = panelObj.transform;
    }
    
    /// <summary>
    /// Создает простой слот
    /// </summary>
    GameObject CreateSimpleSlot()
    {
        GameObject slot = new GameObject("Slot");
        
        // Основной фон слота
        Image slotImage = slot.AddComponent<Image>();
        slotImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Рамка выделения
        GameObject frameObj = new GameObject("SelectedFrame");
        frameObj.transform.SetParent(slot.transform, false);
        Image frameImage = frameObj.AddComponent<Image>();
        frameImage.color = new Color(1f, 1f, 0f, 0.8f);
        frameImage.enabled = false;
        
        RectTransform frameRect = frameObj.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        
        // Иконка предмета
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slot.transform, false);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        
        return slot;
    }
    
    /// <summary>
    /// Создает UI для отображения названия предмета
    /// </summary>
    void CreateItemNameDisplay()
    {
        // Создаем Canvas для текста если его нет
        Canvas textCanvas = FindObjectOfType<Canvas>();
        if (textCanvas == null)
        {
            GameObject canvasObj = new GameObject("ItemNameCanvas");
            textCanvas = canvasObj.AddComponent<Canvas>();
            textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            textCanvas.sortingOrder = 200; // Выше хотбара
        }
        
        // Создаем GameObject для текста
        GameObject textObj = new GameObject("ItemNameText");
        textObj.transform.SetParent(textCanvas.transform, false);
        
        // Добавляем RectTransform
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0, 100); // Над хотбаром
        textRect.sizeDelta = new Vector2(400, 50);
        
        // Добавляем Text
        itemNameText = textObj.AddComponent<Text>();
        itemNameText.text = "";
        itemNameText.fontSize = 24;
        itemNameText.color = Color.white;
        itemNameText.alignment = TextAnchor.MiddleCenter;
        itemNameText.fontStyle = FontStyle.Bold;
        
        // Скрываем по умолчанию
        itemNameText.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Инициализирует предметы в руке - настраивает позицию, поворот и отключает физику
    /// </summary>
    void InitializeHandItems()
    {
        if (handTransform == null) return;
        
        // Настраиваем все дочерние объекты
        for (int i = 0; i < handTransform.childCount; i++)
        {
            GameObject child = handTransform.GetChild(i).gameObject;
            
            // Устанавливаем позицию и поворот
            child.transform.localPosition = handOffset;
            child.transform.localRotation = Quaternion.Euler(handRotation);
            
            // Отключаем физику
            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            
            // Отключаем коллайдеры
            Collider[] colliders = child.GetComponents<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }
            
            // Скрываем по умолчанию
            child.SetActive(false);
        }
    }
    
    /// <summary>
    /// Обрабатывает ввод пользователя
    /// </summary>
    void HandleInput()
    {
        // Переключение слотов клавишами 1, 2, 3
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SelectSlot(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SelectSlot(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            SelectSlot(2);
        
        // Переключение слотов колесиком мыши
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f)
        {
            SelectSlot((currentSelectedSlot + 1) % hotbarSlots);
        }
        else if (scroll < -0.01f)
        {
            SelectSlot((currentSelectedSlot - 1 + hotbarSlots) % hotbarSlots);
        }
        
        // Подбор предметов на E (только если не перетаскиваем)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isDraggingItem)
            {
                TryUseDraggedItem();
            }
            else
            {
                TryPickupItem();
            }
        }
        
        // Выбрасывание предметов на Q
        if (Input.GetKeyDown(KeyCode.Q))
        {
            TryDropItem();
        }
        
        // Использование предмета на ЛКМ
        if (Input.GetMouseButtonDown(0))
        {
            TryUseItem();
        }
    }
    
    /// <summary>
    /// Выбирает слот хотбара
    /// </summary>
    void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hotbarSlots) return;
        
        // Убираем выделение с предыдущего слота
        if (currentSelectedSlot >= 0 && currentSelectedSlot < hotbarSlots)
        {
            hotbarSlotsArray[currentSelectedSlot].SetSelected(false);
        }
        
        currentSelectedSlot = slotIndex;
        hotbarSlotsArray[currentSelectedSlot].SetSelected(true);
        
        // Отображаем название предмета если слот не пустой
        DisplayItemName();
        
        OnSlotChanged?.Invoke(currentSelectedSlot);
    }
    
    /// <summary>
    /// Проверяет, на какие подбираемые объекты смотрит игрок
    /// </summary>
    void CheckForPickupableObjects()
    {
        if (playerCamera == null) return;
        
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
        // Убираем подсказку с предыдущего объекта
        if (currentLookingAtPickupable != null)
        {
            // Проверяем, что объект не уничтожен
            if (currentLookingAtPickupable.gameObject == null)
            {
                currentLookingAtPickupable = null;
            }
            else
            {
                currentLookingAtPickupable.SetPlayerLookingAt(false);
                currentLookingAtPickupable = null;
            }
        }
        
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayerMask))
        {
            // Проверяем, есть ли компонент для подбора
            PickupableItem pickupableItem = hit.collider.GetComponent<PickupableItem>();
            if (pickupableItem != null)
            {
                currentLookingAtPickupable = pickupableItem;
                pickupableItem.SetPlayerLookingAt(true);
            }
        }
    }

    void TryPickupItem()
    {
        if (currentLookingAtPickupable != null)
        {

            if (currentLookingAtPickupable.CompareTag("Item") && !isDraggingItemplus)
            {
                IPickupable pickupable = currentLookingAtPickupable.GetComponent<IPickupable>();
                if (pickupable != null)
                {
                    InventoryItem item = pickupable.GetInventoryItem();
                    if (item != null && AddItemToInventory(item))
                    {
                        pickupable.OnPickedUp();
                        OnItemPickedUp?.Invoke(item);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Пытается выбросить предмет
    /// </summary>
    void TryDropItem()
    {
        InventorySlot currentSlot = hotbarSlotsArray[currentSelectedSlot];
        if (currentSlot.IsEmpty) return;
        
        InventoryItem item = currentSlot.RemoveItem();
        if (item != null)
        {
            // Останавливаем отображение названия предмета и скрываем текст
            if (itemNameDisplayCoroutine != null)
            {
                StopCoroutine(itemNameDisplayCoroutine);
                itemNameDisplayCoroutine = null;
            }
            if (itemNameText != null)
            {
                itemNameText.gameObject.SetActive(false);
            }
            
            DropItem(item);
            OnItemDropped?.Invoke(item);
        }
    }
    
    /// <summary>
    /// Пытается использовать предмет
    /// </summary>
    void TryUseItem()
    {
        InventorySlot currentSlot = hotbarSlotsArray[currentSelectedSlot];
        if (currentSlot.IsEmpty) return;
        
        InventoryItem item = currentSlot.Item;
        if (item.canBeUsed)
        {
            UseItem(item);
        }
    }
    void UseItem(InventoryItem item)
    {
    }
    
    /// <summary>
    /// Устанавливает состояние перетаскивания
    /// </summary>
    public void SetDraggingState(bool dragging)
    {
        isDraggingItem = dragging;
        if (dragging)
        {
            OnDragStarted?.Invoke();
        }
        else
        {
            OnDragEnded?.Invoke();
        }
    }
    
    /// <summary>
    /// Проверяет, идет ли перетаскивание
    /// </summary>
    public bool IsDragging()
    {
        return isDraggingItem;
    }
    
    /// <summary>
    /// Пытается использовать перетаскиваемый предмет
    /// </summary>
    void TryUseDraggedItem()
    {
        // Находим перетаскиваемый предмет
        InventoryDragDrop dragDrop = FindObjectOfType<InventoryDragDrop>();
        if (dragDrop == null) return;
        
        // Получаем слот с перетаскиваемым предметом
        InventorySlot draggedSlot = dragDrop.GetComponent<InventorySlot>();
        if (draggedSlot == null || draggedSlot.IsEmpty) return;
        
        InventoryItem item = draggedSlot.Item;
        
        // Проверяем, можно ли использовать предмет
        if (item != null && CanUseDraggedItem(item))
        {
            UseDraggedItem(item, draggedSlot);
        }
    }
    
    /// <summary>
    /// Проверяет, можно ли использовать перетаскиваемый предмет
    /// </summary>
    bool CanUseDraggedItem(InventoryItem item)
    {
        // Создаем временный PickupableItem для проверки
        GameObject tempObject = new GameObject("TempPickupable");
        PickupableItem tempPickupable = tempObject.AddComponent<PickupableItem>();
        
        // Создаем ItemData из InventoryItem
        ItemData tempItemData = ScriptableObject.CreateInstance<ItemData>();
        tempItemData.itemName = item.itemName;
        tempItemData.description = item.description;
        tempItemData.icon = item.icon;
        tempItemData.itemType = ItemType.Normal; // По умолчанию
        
        // Определяем тип предмета по названию
        DetermineItemTypeFromName(item.itemName, tempItemData);
        
        tempPickupable.SetItemData(tempItemData);
        
        bool canUse = tempPickupable.CanUseItem();
        
        // Очищаем временные объекты
        DestroyImmediate(tempObject);
        
        return canUse;
    }
    
    /// <summary>
    /// Использует перетаскиваемый предмет
    /// </summary>
    void UseDraggedItem(InventoryItem item, InventorySlot slot)
    {
        // Создаем временный PickupableItem для применения эффектов
        GameObject tempObject = new GameObject("TempPickupable");
        PickupableItem tempPickupable = tempObject.AddComponent<PickupableItem>();
        
        // Создаем ItemData из InventoryItem
        ItemData tempItemData = ScriptableObject.CreateInstance<ItemData>();
        tempItemData.itemName = item.itemName;
        tempItemData.description = item.description;
        tempItemData.icon = item.icon;
        tempItemData.itemType = ItemType.Normal; // По умолчанию
        
        // Определяем тип предмета и эффекты
        DetermineItemTypeFromName(item.itemName, tempItemData);
        
        tempPickupable.SetItemData(tempItemData);
        
        // Применяем эффекты
        tempPickupable.ApplyItemEffects();
        
        // Если предмет расходуемый, удаляем его из инвентаря
        if (tempItemData.IsConsumable())
        {
            slot.ClearSlot();
        }
        
        // Очищаем временные объекты
        DestroyImmediate(tempObject);
    }
    
    /// <summary>
    /// Определяет тип предмета по его названию
    /// </summary>
    void DetermineItemTypeFromName(string itemName, ItemData itemData)
    {
        string lowerName = itemName.ToLower();
        
        // Проверяем на предметы лечения
        if (lowerName.Contains("health") || lowerName.Contains("лечение") || 
            lowerName.Contains("heal") || lowerName.Contains("аптечка") ||
            lowerName.Contains("зелье") || lowerName.Contains("potion"))
        {
            itemData.itemType = ItemType.Health;
            itemData.healthAmount = 25f; // Значение по умолчанию
        }
        // Проверяем на предметы увеличения максимального здоровья
        else if (lowerName.Contains("maxhealth") || lowerName.Contains("здоровье") ||
                 lowerName.Contains("max_health") || lowerName.Contains("сердце") ||
                 lowerName.Contains("heart") || lowerName.Contains("vitality"))
        {
            itemData.itemType = ItemType.MaxHealth;
            itemData.maxHealthAmount = 10f; // Значение по умолчанию
        }
        // Проверяем на предметы увеличения максимальной стамины
        else if (lowerName.Contains("stamina") || lowerName.Contains("стамина") ||
                 lowerName.Contains("max_stamina") || lowerName.Contains("энергия") ||
                 lowerName.Contains("energy") || lowerName.Contains("endurance"))
        {
            itemData.itemType = ItemType.MaxStamina;
            itemData.maxStaminaAmount = 10f; // Значение по умолчанию
        }
        // Проверяем на предметы увеличения силы хвата
        else if (lowerName.Contains("strength") || lowerName.Contains("сила") ||
                 lowerName.Contains("power") || lowerName.Contains("мощность") ||
                 lowerName.Contains("grip") || lowerName.Contains("хват") ||
                 lowerName.Contains("muscle") || lowerName.Contains("мышца"))
        {
            itemData.itemType = ItemType.Strength;
            itemData.strengthAmount = 2f; // Значение по умолчанию (будет умножено на 0.25 в ObjectGrabSystem)
        }
        // По умолчанию обычный предмет
        else
        {
            itemData.itemType = ItemType.Normal;
        }
    }
    /// <summary>
    /// Добавляет предмет в инвентарь
    /// </summary>
    public bool AddItemToInventory(InventoryItem item)
    {
        // Ищем пустой слот
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (hotbarSlotsArray[i].IsEmpty)
            {
                hotbarSlotsArray[i].AddItem(item);
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Выбрасывает предмет в мир
    /// </summary>
    void DropItem(InventoryItem item)
    {
        GameObject droppedItem;
        
        if (item.itemPrefab != null)
        {
            // Используем префаб предмета
            droppedItem = Instantiate(item.itemPrefab, dropPoint.position, dropPoint.rotation);
        }
        else
        {
            // Создаем простой куб если нет префаба
            droppedItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            droppedItem.name = item.itemName;
            droppedItem.transform.position = dropPoint.position;
            droppedItem.transform.rotation = dropPoint.rotation;
            
            // Устанавливаем цвет на основе категории
            Renderer renderer = droppedItem.GetComponent<Renderer>();
            if (renderer != null)
            {
                switch (item.category)
                {
                    case ItemCategory.Tool:
                        renderer.material.color = Color.blue;
                        break;
                    case ItemCategory.Weapon:
                        renderer.material.color = Color.red;
                        break;
                    case ItemCategory.Material:
                        renderer.material.color = Color.green;
                        break;
                    default:
                        renderer.material.color = Color.white;
                        break;
                }
            }
        }
        
        // Устанавливаем точное название предмета
        droppedItem.name = item.itemName;
        
        // Добавляем физику если её нет
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = droppedItem.AddComponent<Rigidbody>();
        }
        rb.mass = item.weight;
        
        // Применяем силу выброса
        Vector3 dropDirection = dropPoint.forward;
        rb.AddForce(dropDirection * dropForce, ForceMode.Impulse);
        
        // Применяем тег и слой - принудительно устанавливаем тег "Item" для предметов инвентаря
        droppedItem.tag = "Item";
        // Устанавливаем слой "Item" (обычно это слой 6, но можно настроить)
        droppedItem.layer = LayerMask.NameToLayer("Item");
        
        // Добавляем компонент для повторного подбора
        PickupableItem pickupable = droppedItem.GetComponent<PickupableItem>();
        if (pickupable == null)
        {
            pickupable = droppedItem.AddComponent<PickupableItem>();
        }
        
        // Создаем ItemData для сохранения всех свойств
        ItemData droppedItemData = ScriptableObject.CreateInstance<ItemData>();
        droppedItemData.itemName = item.itemName;
        droppedItemData.description = item.description;
        droppedItemData.icon = item.icon;
        droppedItemData.itemPrefab = item.itemPrefab;
        droppedItemData.weight = item.weight;
        droppedItemData.isBreakable = item.isBreakable;
        droppedItemData.itemTag = item.itemTag;
        droppedItemData.itemLayer = item.itemLayer;
        droppedItemData.itemType = item.itemType; // Сохраняем оригинальный тип предмета
        
        // Копируем эффекты предмета из InventoryItem
        droppedItemData.healthAmount = item.healthAmount;
        droppedItemData.maxHealthAmount = item.maxHealthAmount;
        droppedItemData.maxStaminaAmount = item.maxStaminaAmount;
        
        pickupable.SetItemData(droppedItemData);
    }
    /// <summary>
    /// Обновляет отображение предмета в руке
    /// </summary>
    void UpdateHandDisplay()
    {
        if (handTransform == null) return;
        
        InventorySlot currentSlot = hotbarSlotsArray[currentSelectedSlot];
        
        // Скрываем все дочерние объекты
        for (int i = 0; i < handTransform.childCount; i++)
        {
            handTransform.GetChild(i).gameObject.SetActive(false);
        }
        
        // Показываем предмет если слот не пустой
        if (!currentSlot.IsEmpty)
        {
            InventoryItem item = currentSlot.Item;
            string itemName = item.itemName;
            
            // Ищем дочерний объект с таким же названием
            Transform foundItem = handTransform.Find(itemName);
            if (foundItem != null)
            {
                foundItem.gameObject.SetActive(true);
                currentHandItem = foundItem.gameObject;
            }
            else
            {
                // Если не найден - скрываем все
                currentHandItem = null;
            }
        }
        else
        {
            currentHandItem = null;
        }
    }
    
    // Публичные методы для получения информации
    public InventoryItem GetCurrentItem()
    {
        return hotbarSlotsArray[currentSelectedSlot].Item;
    }
    
    public int GetCurrentSlotIndex()
    {
        return currentSelectedSlot;
    }
    
    public bool IsCurrentSlotEmpty()
    {
        return hotbarSlotsArray[currentSelectedSlot].IsEmpty;
    }
    
    public InventorySlot GetSlot(int index)
    {
        if (index >= 0 && index < hotbarSlots)
            return hotbarSlotsArray[index];
        return null;
    }
    
    /// <summary>
    /// Отображает название предмета в выбранном слоте
    /// </summary>
    void DisplayItemName()
    {
        if (itemNameText == null) return;
        
        // Останавливаем предыдущую корутину если она запущена
        if (itemNameDisplayCoroutine != null)
        {
            StopCoroutine(itemNameDisplayCoroutine);
            itemNameDisplayCoroutine = null;
        }
        
        InventorySlot currentSlot = hotbarSlotsArray[currentSelectedSlot];
        
        // Если слот пустой - скрываем текст
        if (currentSlot.IsEmpty)
        {
            itemNameText.gameObject.SetActive(false);
            return;
        }
        
        // Показываем название предмета
        InventoryItem item = currentSlot.Item;
        itemNameText.text = item.itemName;
        itemNameText.color = Color.white; // Сбрасываем прозрачность
        itemNameText.gameObject.SetActive(true);
        
        // Запускаем корутину для анимации исчезновения
        itemNameDisplayCoroutine = StartCoroutine(ItemNameDisplayCoroutine());
    }
    
    /// <summary>
    /// Корутина для отображения названия предмета с анимацией исчезновения
    /// </summary>
    IEnumerator ItemNameDisplayCoroutine()
    {
        // Ждем время отображения
        yield return new WaitForSeconds(displayDuration);
        
        // Анимация исчезновения
        Color startColor = itemNameText.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            itemNameText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
        
        // Скрываем текст после анимации
        itemNameText.gameObject.SetActive(false);
        itemNameDisplayCoroutine = null;
    }
}
