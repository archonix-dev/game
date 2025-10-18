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
    
    private InventorySlot[] hotbarSlotsArray;
    private int currentSelectedSlot = 0;
    private GameObject currentHandItem;
    private Camera playerCamera;
    private PickupableItem currentLookingAtPickupable;
    private Coroutine itemNameDisplayCoroutine;
    
    // События для UI
    public System.Action<int> OnSlotChanged;
    public System.Action<InventoryItem> OnItemPickedUp;
    public System.Action<InventoryItem> OnItemDropped;
    
    void Start()
    {
        InitializeInventory();
        playerCamera = Camera.main;
        
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
    }
    
    void Update()
    {
        HandleInput();
        UpdateHandDisplay();
        CheckForPickupableObjects();
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
        
        // Подбор предметов на E
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPickupItem();
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
            currentLookingAtPickupable.SetPlayerLookingAt(false);
            currentLookingAtPickupable = null;
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
    
    /// <summary>
    /// Пытается подобрать предмет
    /// </summary>
    void TryPickupItem()
    {
        if (currentLookingAtPickupable != null)
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
        
        // Применяем тег и слой
        string tagToApply = !string.IsNullOrEmpty(item.itemTag) ? item.itemTag : defaultDropTag;
        int layerToApply = item.itemLayer != 0 ? item.itemLayer : defaultDropLayer;
        
        droppedItem.tag = tagToApply;
        droppedItem.layer = layerToApply;
        
        // Добавляем компонент для повторного подбора
        PickupableItem pickupable = droppedItem.GetComponent<PickupableItem>();
        if (pickupable == null)
        {
            pickupable = droppedItem.AddComponent<PickupableItem>();
        }
        pickupable.SetInventoryItem(item);
        
    }
    /// <summary>
    /// Обновляет отображение предмета в руке
    /// </summary>
    void UpdateHandDisplay()
    {
        InventorySlot currentSlot = hotbarSlotsArray[currentSelectedSlot];
        
        // Удаляем предыдущий предмет из руки
        if (currentHandItem != null)
        {
            Destroy(currentHandItem);
            currentHandItem = null;
        }
        
        // Показываем новый предмет если слот не пустой
        if (!currentSlot.IsEmpty && handTransform != null)
        {
            InventoryItem item = currentSlot.Item;
            if (item.itemPrefab != null)
            {
                currentHandItem = Instantiate(item.itemPrefab, handTransform);
                currentHandItem.transform.localPosition = handOffset;
                currentHandItem.transform.localRotation = Quaternion.Euler(handRotation);
                
                // Отключаем физику для предмета в руке
                Rigidbody rb = currentHandItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                
                // Отключаем коллайдеры
                Collider[] colliders = currentHandItem.GetComponents<Collider>();
                foreach (Collider col in colliders)
                {
                    col.enabled = false;
                }
            }
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
