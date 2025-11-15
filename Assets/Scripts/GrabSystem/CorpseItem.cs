using UnityEngine;

/// <summary>
/// Компонент для трупов игроков. Идентифицирует объект как труп и хранит информацию об игроке.
/// </summary>
public class CorpseItem : MonoBehaviour
{
    [Header("Corpse Info")]
    [Tooltip("Никнейм игрока, которому принадлежит этот труп")]
    [SerializeField] private string playerName = "Unknown Player";
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showCorpsePrompt = true;
    [SerializeField] private float promptDistance = 3f;
    [SerializeField] private TMPro.TextMeshPro corpsePromptText;
    [SerializeField] private GameObject corpsePromptObject;
    
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
        // Ищем игрока
        FindPlayer();
        
        // Инициализируем систему отображения подсказки
        InitializeCorpsePrompt();
    }
    
    void Update()
    {
        if (showCorpsePrompt)
        {
            UpdateCorpsePrompt();
        }
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
    void InitializeCorpsePrompt()
    {
        if (corpsePromptObject == null)
        {
            CreateCorpsePrompt();
        }
        
        // Скрываем отображение по умолчанию
        HideCorpsePrompt();
    }
    
    /// <summary>
    /// Создает отображение подсказки
    /// </summary>
    void CreateCorpsePrompt()
    {
        // Создаем GameObject для отображения подсказки
        corpsePromptObject = new GameObject("CorpsePrompt");
        corpsePromptObject.transform.SetParent(transform);
        corpsePromptObject.transform.localPosition = Vector3.up * 2f; // Над объектом
        corpsePromptObject.transform.localScale = Vector3.one * minScale; // Устанавливаем начальный размер
        
        // Добавляем TextMeshPro
        corpsePromptText = corpsePromptObject.AddComponent<TMPro.TextMeshPro>();
        corpsePromptText.fontSize = 2f;
        corpsePromptText.color = Color.white;
        corpsePromptText.alignment = TMPro.TextAlignmentOptions.Center;
        corpsePromptText.sortingOrder = 10;
        
        // Устанавливаем текст подсказки
        corpsePromptText.text = GetCorpsePromptText();
        
        // Настраиваем шрифт
        corpsePromptText.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (corpsePromptText.font == null)
        {
            // Используем стандартный шрифт если не найден
            corpsePromptText.font = Resources.GetBuiltinResource<TMPro.TMP_FontAsset>("Legacy Runtime/TextMeshPro/Fonts & Materials/LiberationSans SDF");
        }
    }
    
    /// <summary>
    /// Обновляет отображение подсказки
    /// </summary>
    void UpdateCorpsePrompt()
    {
        if (!showCorpsePrompt) return;
        
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
                ShowCorpsePrompt();
            }
            else
            {
                HideCorpsePrompt();
            }
        }
        
        // Поворачиваем текст к игроку и обновляем размер
        if (isPlayerInRange && corpsePromptObject != null)
        {
            corpsePromptObject.transform.LookAt(playerTransform);
            // Поворачиваем на 180 градусов чтобы текст был читаемым
            corpsePromptObject.transform.Rotate(0, 180, 0);
            
            // Обновляем размер в зависимости от расстояния
            UpdatePromptScale(distanceToPlayer);
            
            // Обновляем текст подсказки
            UpdateCorpsePromptText();
        }
    }
    
    /// <summary>
    /// Обновляет размер отображения подсказки в зависимости от расстояния до игрока
    /// </summary>
    void UpdatePromptScale(float distanceToPlayer)
    {
        if (corpsePromptObject == null) return;
        
        // Вычисляем целевой размер на основе расстояния
        // Чем дальше игрок, тем больше размер (но в пределах promptDistance)
        float normalizedDistance = Mathf.Clamp01(distanceToPlayer / promptDistance);
        
        // Инвертируем: далеко = большой размер, близко = маленький размер
        float targetScale = Mathf.Lerp(minScale, maxScale, normalizedDistance);
        
        // Плавно изменяем размер
        currentScale = Mathf.SmoothDamp(currentScale, targetScale, ref scaleVelocity, scaleSmoothTime);
        
        // Применяем размер
        corpsePromptObject.transform.localScale = Vector3.one * currentScale;
    }
    
    /// <summary>
    /// Обновляет текст подсказки
    /// </summary>
    void UpdateCorpsePromptText()
    {
        if (corpsePromptText != null)
        {
            string newText = GetCorpsePromptText();
            if (corpsePromptText.text != newText)
            {
                corpsePromptText.text = newText;
            }
        }
    }
    
    /// <summary>
    /// Показывает отображение подсказки
    /// </summary>
    void ShowCorpsePrompt()
    {
        if (corpsePromptObject != null)
        {
            corpsePromptObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Скрывает отображение подсказки
    /// </summary>
    void HideCorpsePrompt()
    {
        if (corpsePromptObject != null)
        {
            corpsePromptObject.SetActive(false);
            // Сбрасываем размер к минимальному при скрытии
            corpsePromptObject.transform.localScale = Vector3.one * minScale;
            currentScale = minScale;
            scaleVelocity = 0f;
        }
        isPlayerInRange = false;
    }
    
    /// <summary>
    /// Получает текст подсказки
    /// </summary>
    string GetCorpsePromptText()
    {
        return $"Труп: {playerName}";
    }
    
    /// <summary>
    /// Устанавливает никнейм игрока, которому принадлежит этот труп
    /// </summary>
    public void SetPlayerName(string name)
    {
        playerName = name;
        Debug.Log($"[CorpseItem] Никнейм игрока установлен: {playerName}");
        
        // Обновляем текст подсказки если она уже создана
        if (corpsePromptText != null)
        {
            corpsePromptText.text = GetCorpsePromptText();
        }
    }
    
    /// <summary>
    /// Получает никнейм игрока, которому принадлежит этот труп
    /// </summary>
    public string GetPlayerName()
    {
        return playerName;
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
        // Показываем радиус подсказки
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, promptDistance);
    }
}

