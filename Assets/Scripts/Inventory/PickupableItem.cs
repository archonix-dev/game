using UnityEngine;
using TMPro;
using Mirror;

/// <summary>
/// Компонент для предметов, которые можно подобрать в инвентарь
/// 
/// ТРЕБОВАНИЯ ДЛЯ МУЛЬТИПЛЕЕРА:
/// - GameObject должен иметь компонент NetworkIdentity для синхронизации в сети
/// - GameObject должен иметь компонент NetworkTransformReliable или NetworkTransformHybrid для синхронизации позиции/ротации
///   (для физических объектов рекомендуется NetworkTransformReliable с updateMethod = FixedUpdate)
/// 
/// Примечание: Если объект не имеет NetworkIdentity, он будет уничтожен локально через Destroy().
/// Если имеет NetworkIdentity, уничтожение происходит через NetworkServer.Destroy() для синхронизации.
/// </summary>
public class PickupableItem : MonoBehaviour, IPickupable
{
    [Header("Item Settings")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private bool canBePickedUp = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showPickupPrompt = true;
    [SerializeField] private float promptDistance = 3f;
    [SerializeField] private TextMeshPro pickupPromptText;
    [SerializeField] private GameObject pickupPromptObject;
    
    [Header("Настройки масштабирования")]
    [SerializeField] private float minScale = 0.01f;  // Минимальный размер (близко к объекту)
    [SerializeField] private float maxScale = 0.05f;  // Максимальный размер (далеко от объекта)
    [SerializeField] private float scaleSmoothTime = 0.1f; // Время плавного изменения размера
    
    private Transform playerTransform;
    private bool isPlayerInRange = false;
    private bool isPlayerLookingAt = false;
    private float currentScale = 0.01f;
    private float scaleVelocity = 0f;
    
    void Start()
    {
        InitializePickupableItem();
    }
    
    /// <summary>
    /// Инициализирует компонент PickupableItem (можно вызвать вручную для объектов, созданных в рантайме)
    /// </summary>
    public void InitializePickupableItem()
    {
        // Автоматически создаем ItemData если не назначен
        if (itemData == null)
        {
            CreateDefaultItemData();
        }
        
        // Ищем игрока
        FindPlayer();
        
        // Инициализируем систему отображения подсказки
        InitializePickupPrompt();
        
        // Убеждаемся, что тег установлен правильно
        if (gameObject.tag != "Item")
        {
            gameObject.tag = "Item";
        }
    }
    
    void Update()
    {
        if (showPickupPrompt)
        {
            UpdatePickupPrompt();
        }
    }
    
    /// <summary>
    /// Создает стандартный ItemData на основе компонентов объекта
    /// </summary>
    void CreateDefaultItemData()
    {
        string itemName = gameObject.name.Replace("(Clone)", "");
        
        // Пытаемся найти иконку в ресурсах
        Sprite icon = Resources.Load<Sprite>($"Icons/{itemName}");
        if (icon == null)
        {
            // Создаем простую иконку если не найдена
            icon = CreateSimpleIcon();
        }
        
        // Создаем ItemData в рантайме
        itemData = ScriptableObject.CreateInstance<ItemData>();
        itemData.itemName = itemName;
        itemData.description = $"Подобранный {itemName}";
        itemData.icon = icon;
        itemData.itemType = ItemType.Normal;
        
        // Настраиваем вес на основе Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            itemData.weight = rb.mass;
        }
        
        // Сохраняем тег и слой
        itemData.itemTag = gameObject.tag;
        itemData.itemLayer = gameObject.layer;
        
        // Предметы в инвентаре не разбиваются
        itemData.isBreakable = false;
        itemData.itemPrefab = gameObject;
    }
    
    /// <summary>
    /// Создает простую иконку для предмета
    /// </summary>
    Sprite CreateSimpleIcon()
    {
        // Создаем простую текстуру 32x32
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        
        // Заполняем случайным цветом
        Color itemColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = itemColor;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }
    
    /// <summary>
    /// Ищет игрока в сцене
    /// </summary>
    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            // Ищем камеру как альтернативу
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerTransform = mainCamera.transform;
            }
        }
    }
    
    /// <summary>
    /// Инициализирует систему отображения подсказки
    /// </summary>
    void InitializePickupPrompt()
    {

        if (pickupPromptObject == null)
        {
            CreatePickupPrompt();
        }
        
        // Скрываем отображение по умолчанию
        HidePickupPrompt();
    }
    
    /// <summary>
    /// Создает отображение подсказки
    /// </summary>
    void CreatePickupPrompt()
    {
        // Создаем GameObject для отображения подсказки
        pickupPromptObject = new GameObject("PickupPrompt");
        pickupPromptObject.transform.SetParent(transform);
        pickupPromptObject.transform.localPosition = Vector3.up * 2f; // Над объектом
        pickupPromptObject.transform.localScale = Vector3.one * minScale; // Устанавливаем начальный размер
        
        // Добавляем TextMeshPro
        pickupPromptText = pickupPromptObject.AddComponent<TextMeshPro>();
        pickupPromptText.fontSize = 2f;
        pickupPromptText.color = Color.white;
        pickupPromptText.alignment = TextAlignmentOptions.Center;
        pickupPromptText.sortingOrder = 10;
        
        // Устанавливаем текст подсказки
        pickupPromptText.text = GetPickupPromptText();
        
        // Настраиваем шрифт
        pickupPromptText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (pickupPromptText.font == null)
        {
            // Используем стандартный шрифт если не найден
            pickupPromptText.font = Resources.GetBuiltinResource<TMP_FontAsset>("Legacy Runtime/TextMeshPro/Fonts & Materials/LiberationSans SDF");
        }
        
    }
    
    /// <summary>
    /// Обновляет отображение подсказки
    /// </summary>
    void UpdatePickupPrompt()
    {
        if (!showPickupPrompt || itemData == null) return;
        
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }
        
        // Проверяем расстояние до игрока
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = distanceToPlayer <= promptDistance;
        
        // Показываем только если игрок в радиусе И смотрит на объект
        bool shouldShow = inRange && isPlayerLookingAt;
        
        if (shouldShow != isPlayerInRange)
        {
            isPlayerInRange = shouldShow;
            
            if (isPlayerInRange)
            {
                ShowPickupPrompt();
            }
            else
            {
                HidePickupPrompt();
            }
        }
        
        // Поворачиваем текст к игроку и обновляем размер
        if (isPlayerInRange && pickupPromptObject != null)
        {
            pickupPromptObject.transform.LookAt(playerTransform);
            // Поворачиваем на 180 градусов чтобы текст был читаемым
            pickupPromptObject.transform.Rotate(0, 180, 0);
            
            // Обновляем размер в зависимости от расстояния
            UpdatePromptScale(distanceToPlayer);
            
            // Обновляем текст подсказки
            UpdatePickupPromptText();
        }
    }
    
    /// <summary>
    /// Обновляет размер отображения подсказки в зависимости от расстояния до игрока
    /// </summary>
    void UpdatePromptScale(float distanceToPlayer)
    {
        if (pickupPromptObject == null) return;
        
        // Вычисляем целевой размер на основе расстояния
        // Чем дальше игрок, тем больше размер (но в пределах promptDistance)
        float normalizedDistance = Mathf.Clamp01(distanceToPlayer / promptDistance);
        
        // Инвертируем: далеко = большой размер, близко = маленький размер
        float targetScale = Mathf.Lerp(minScale, maxScale, normalizedDistance);
        
        // Плавно изменяем размер
        currentScale = Mathf.SmoothDamp(currentScale, targetScale, ref scaleVelocity, scaleSmoothTime);
        
        // Применяем размер
        pickupPromptObject.transform.localScale = Vector3.one * currentScale;
    }
    
    /// <summary>
    /// Обновляет текст подсказки
    /// </summary>
    void UpdatePickupPromptText()
    {
        if (pickupPromptText != null)
        {
            string newText = GetPickupPromptText();
            if (pickupPromptText.text != newText)
            {
                pickupPromptText.text = newText;
            }
        }
    }
    
    /// <summary>
    /// Показывает отображение подсказки
    /// </summary>
    void ShowPickupPrompt()
    {
        if (pickupPromptObject != null)
        {
            pickupPromptObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Скрывает отображение подсказки
    /// </summary>
    void HidePickupPrompt()
    {
        if (pickupPromptObject != null)
        {
            pickupPromptObject.SetActive(false);
            // Сбрасываем размер к минимальному при скрытии
            pickupPromptObject.transform.localScale = Vector3.one * minScale;
            currentScale = minScale;
            scaleVelocity = 0f;
        }
        isPlayerInRange = false;
    }
    
    /// <summary>
    /// Получает текст подсказки
    /// </summary>
    string GetPickupPromptText()
    {
        if (itemData == null) 
        {
            return "E - Подобрать";
        }
        
        string itemName = itemData.itemName;
        return $"E - {itemName}";
    }
    
    /// <summary>
    /// Устанавливает данные предмета
    /// </summary>
    public void SetItemData(ItemData data)
    {
        itemData = data;
    }
    
    /// <summary>
    /// Получает данные предмета
    /// </summary>
    public ItemData GetItemData()
    {
        return itemData;
    }
    
    // Реализация интерфейса IPickupable
    public InventoryItem GetInventoryItem()
    {
        if (itemData == null) return null;
        
        // Создаем InventoryItem из ItemData
        InventoryItem inventoryItem = new InventoryItem(
            itemData.itemName,
            itemData.description,
            itemData.icon,
            itemData.itemPrefab != null ? itemData.itemPrefab : gameObject
        );
        
        // Копируем дополнительные свойства из ItemData
        inventoryItem.weight = itemData.weight;
        inventoryItem.isBreakable = itemData.isBreakable;
        inventoryItem.itemTag = itemData.itemTag;
        inventoryItem.itemLayer = itemData.itemLayer;
        inventoryItem.itemType = itemData.itemType;
        inventoryItem.healthAmount = itemData.healthAmount;
        inventoryItem.maxHealthAmount = itemData.maxHealthAmount;
        inventoryItem.maxStaminaAmount = itemData.maxStaminaAmount;
        
        return inventoryItem;
    }
    
    public void OnPickedUp()
    {
        // Воспроизводим звук подбора если есть AudioSource
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Play();
        }
        
        // Уничтожаем объект (синхронизированно в мультиплеере если есть NetworkIdentity)
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        if (networkIdentity != null && networkIdentity.netId != 0)
        {
            // В мультиплеере уничтожаем через NetworkServer
            if (NetworkServer.active)
            {
                NetworkServer.Destroy(gameObject);
            }
            // Если вызывается на клиенте, сервер должен обработать уничтожение через InventorySystem
        }
        else
        {
            // Локальное уничтожение для объектов без NetworkIdentity
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Применяет эффекты предмета к игроку
    /// </summary>
    public void ApplyItemEffects()
    {
        if (itemData == null) return;
        
        // Ищем компонент здоровья игрока
        PlayerHealthStamina playerHealth = FindObjectOfType<PlayerHealthStamina>();
        if (playerHealth == null) return;
        
        switch (itemData.itemType)
        {
            case ItemType.Health:
                playerHealth.Heal(itemData.healthAmount);
                break;
                
            case ItemType.MaxHealth:
                playerHealth.IncreaseMaxHealth(itemData.maxHealthAmount);
                break;
                
            case ItemType.MaxStamina:
                playerHealth.IncreaseMaxStamina(itemData.maxStaminaAmount);
                break;
                
            case ItemType.Strength:
                // Ищем систему захвата объектов
                ObjectGrabSystem grabSystem = FindObjectOfType<ObjectGrabSystem>();
                if (grabSystem != null)
                {
                    grabSystem.AddStrengthBonus(itemData.strengthAmount);
                }
                break;
                
            case ItemType.Normal:
                // Обычные предметы не расходуются
                break;
        }
    }
    
    /// <summary>
    /// Проверяет, можно ли использовать предмет
    /// </summary>
    public bool CanUseItem()
    {
        if (itemData == null) return false;
        
        // Обычные предметы не расходуются
        if (itemData.itemType == ItemType.Normal) return false;
        
        // Проверяем, есть ли игрок
        PlayerHealthStamina playerHealth = FindObjectOfType<PlayerHealthStamina>();
        return playerHealth != null;
    }
    
    public bool CanBePickedUp()
    {
        return canBePickedUp;
    }
    
    /// <summary>
    /// Устанавливает состояние наведения игрока на объект
    /// </summary>
    public void SetPlayerLookingAt(bool looking)
    {
        isPlayerLookingAt = looking;
    }
    
    /// <summary>
    /// Проверяет, смотрит ли игрок на объект
    /// </summary>
    public bool IsPlayerLookingAt()
    {
        return isPlayerLookingAt;
    }
    
    void OnDrawGizmosSelected()
    {
        // Показываем радиус подбора
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, promptDistance);
    }
}
