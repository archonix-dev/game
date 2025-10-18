using UnityEngine;
using TMPro;

/// <summary>
/// Вспомогательный скрипт для настройки системы отображения награды
/// Добавьте этот скрипт к объекту с DestructibleObject для автоматической настройки
/// </summary>
public class RewardDisplayHelper : MonoBehaviour
{
    [Header("Настройки отображения")]
    [SerializeField] private bool autoSetup = true;
    [SerializeField] private float displayDistance = 5f;
    [SerializeField] private Color textColor = Color.yellow;
    [SerializeField] private float fontSize = 2f;
    [SerializeField] private Vector3 textOffset = new Vector3(0, 2, 0);
    
    [Header("Настройки масштабирования")]
    [SerializeField] private float minScale = 0.01f;  // Минимальный размер (близко к объекту)
    [SerializeField] private float maxScale = 0.05f;  // Максимальный размер (далеко от объекта)
    [SerializeField] private float scaleSmoothTime = 0.1f; // Время плавного изменения размера
    
    [Header("Компоненты")]
    [SerializeField] private DestructibleObject destructibleObject;
    [SerializeField] private GameObject rewardDisplayObject;
    [SerializeField] private TextMeshPro rewardText;
    
    void Start()
    {
        if (autoSetup)
        {
            SetupRewardDisplay();
        }
    }
    
    /// <summary>
    /// Настраивает отображение награды
    /// </summary>
    public void SetupRewardDisplay()
    {
        // Находим DestructibleObject если не назначен
        if (destructibleObject == null)
        {
            destructibleObject = GetComponent<DestructibleObject>();
        }
        
        if (destructibleObject == null)
        {
            Debug.LogWarning($"RewardDisplayHelper на {gameObject.name} не может найти DestructibleObject!");
            return;
        }
        
        // Создаем отображение награды
        CreateRewardDisplay();
        
        // Настраиваем DestructibleObject
        SetupDestructibleObject();
        
        Debug.Log($"Настроено отображение награды для {gameObject.name}");
    }
    
    /// <summary>
    /// Создает отображение награды
    /// </summary>
    void CreateRewardDisplay()
    {
        // Создаем GameObject для отображения награды
        rewardDisplayObject = new GameObject("RewardDisplay");
        rewardDisplayObject.transform.SetParent(transform);
        rewardDisplayObject.transform.localPosition = textOffset;
        
        // Добавляем TextMeshPro
        rewardText = rewardDisplayObject.AddComponent<TextMeshPro>();
        rewardText.text = "0";
        rewardText.fontSize = fontSize;
        rewardText.color = textColor;
        rewardText.alignment = TextAlignmentOptions.Center;
        rewardText.sortingOrder = 10;
        
        // Настраиваем шрифт
        SetupFont();
        
        // Скрываем по умолчанию
        rewardDisplayObject.SetActive(false);
    }
    
    /// <summary>
    /// Настраивает шрифт для TextMeshPro
    /// </summary>
    void SetupFont()
    {
        // Пытаемся загрузить шрифт из Resources
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        
        if (font == null)
        {
            // Используем встроенный шрифт
            font = Resources.GetBuiltinResource<TMP_FontAsset>("Legacy Runtime/TextMeshPro/Fonts & Materials/LiberationSans SDF");
        }
        
        if (font != null)
        {
            rewardText.font = font;
        }
        else
        {
            Debug.LogWarning($"Не удалось найти шрифт для TextMeshPro на {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Настраивает DestructibleObject
    /// </summary>
    void SetupDestructibleObject()
    {
        // Используем рефлексию для установки приватных полей
        var destructibleType = typeof(DestructibleObject);
        
        // Устанавливаем showRewardDisplay
        var showRewardDisplayField = destructibleType.GetField("showRewardDisplay", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (showRewardDisplayField != null)
        {
            showRewardDisplayField.SetValue(destructibleObject, true);
        }
        
        // Устанавливаем displayDistance
        var displayDistanceField = destructibleType.GetField("displayDistance", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (displayDistanceField != null)
        {
            displayDistanceField.SetValue(destructibleObject, displayDistance);
        }
        
        // Устанавливаем rewardText
        var rewardTextField = destructibleType.GetField("rewardText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rewardTextField != null)
        {
            rewardTextField.SetValue(destructibleObject, rewardText);
        }
        
        // Устанавливаем rewardDisplayObject
        var rewardDisplayObjectField = destructibleType.GetField("rewardDisplayObject", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rewardDisplayObjectField != null)
        {
            rewardDisplayObjectField.SetValue(destructibleObject, rewardDisplayObject);
        }
        
        // Устанавливаем настройки масштабирования
        var minScaleField = destructibleType.GetField("minScale", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (minScaleField != null)
        {
            minScaleField.SetValue(destructibleObject, minScale);
        }
        
        var maxScaleField = destructibleType.GetField("maxScale", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (maxScaleField != null)
        {
            maxScaleField.SetValue(destructibleObject, maxScale);
        }
        
        var scaleSmoothTimeField = destructibleType.GetField("scaleSmoothTime", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (scaleSmoothTimeField != null)
        {
            scaleSmoothTimeField.SetValue(destructibleObject, scaleSmoothTime);
        }
    }
    
    /// <summary>
    /// Обновляет текст награды
    /// </summary>
    public void UpdateRewardText()
    {
        if (rewardText != null && destructibleObject != null)
        {
            // Получаем данные о награде
            var objectDataField = typeof(DestructibleObject).GetField("objectData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (objectDataField != null)
            {
                var objectData = objectDataField.GetValue(destructibleObject) as DestructibleObjectData;
                if (objectData != null)
                {
                    rewardText.text = $"{objectData.CoinAmount}";
                }
            }
        }
    }
    
    void OnValidate()
    {
        // Обновляем настройки в реальном времени в редакторе
        if (Application.isPlaying && rewardText != null)
        {
            rewardText.fontSize = fontSize;
            rewardText.color = textColor;
            rewardDisplayObject.transform.localPosition = textOffset;
        }
    }
}
