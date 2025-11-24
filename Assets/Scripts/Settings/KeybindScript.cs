using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Скрипт для управления настройками клавиш и чувствительности мыши
/// </summary>
public class KeybindScript : MonoBehaviour
{
    [Header("Mouse Sensitivity")]
    [Tooltip("Слайдер для настройки чувствительности мыши")]
    public Slider mouseSensitivitySlider;
    
    [Tooltip("Текст для отображения текущей чувствительности мыши")]
    public Text mouseSensitivityLabel;
    
    [Tooltip("Минимальная чувствительность мыши")]
    [SerializeField] private float minMouseSensitivity = 0f;
    
    [Tooltip("Максимальная чувствительность мыши")]
    [SerializeField] private float maxMouseSensitivity = 500f;
    
    [Tooltip("Чувствительность мыши по умолчанию")]
    [SerializeField] private float defaultMouseSensitivity = 100f;
    
    private float currentMouseSensitivity = 100f;
    
    // Singleton для доступа из других скриптов
    private static KeybindScript instance;
    public static KeybindScript Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<KeybindScript>();
            }
            return instance;
        }
    }
    
    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        InitializeUI();
        LoadSettings();
    }
    
    private void InitializeUI()
    {
        // Настройка слайдера чувствительности мыши
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = minMouseSensitivity;
            mouseSensitivitySlider.maxValue = maxMouseSensitivity;
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        }
    }
    
    /// <summary>
    /// Загружает настройки из PlayerPrefs
    /// </summary>
    public void LoadSettings()
    {
        currentMouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", defaultMouseSensitivity);
        currentMouseSensitivity = Mathf.Clamp(currentMouseSensitivity, minMouseSensitivity, maxMouseSensitivity);
        
        UpdateUI();
    }
    
    /// <summary>
    /// Сохраняет настройки в PlayerPrefs
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MouseSensitivity", currentMouseSensitivity);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Обновляет UI элементы
    /// </summary>
    private void UpdateUI()
    {
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.value = currentMouseSensitivity;
        }
        
        UpdateMouseSensitivityLabel();
    }
    
    /// <summary>
    /// Обновляет текст с текущей чувствительностью мыши
    /// </summary>
    private void UpdateMouseSensitivityLabel()
    {
        if (mouseSensitivityLabel != null)
        {
            mouseSensitivityLabel.text = $"{currentMouseSensitivity:F0}";
        }
    }
    
    /// <summary>
    /// Обработчик изменения чувствительности мыши
    /// </summary>
    private void OnMouseSensitivityChanged(float value)
    {
        currentMouseSensitivity = Mathf.Clamp(value, minMouseSensitivity, maxMouseSensitivity);
        UpdateMouseSensitivityLabel();
        
        // Сохраняем настройки автоматически при изменении
        SaveSettings();
        
        Debug.Log($"[KeybindScript] Чувствительность мыши изменена: {currentMouseSensitivity:F0}");
    }
    
    /// <summary>
    /// Получает текущую чувствительность мыши
    /// </summary>
    public float GetMouseSensitivity()
    {
        return currentMouseSensitivity;
    }
    
    /// <summary>
    /// Устанавливает чувствительность мыши программно
    /// </summary>
    public void SetMouseSensitivity(float sensitivity)
    {
        currentMouseSensitivity = Mathf.Clamp(sensitivity, minMouseSensitivity, maxMouseSensitivity);
        UpdateUI();
        SaveSettings();
    }
    
    /// <summary>
    /// Сбрасывает настройки к значениям по умолчанию
    /// </summary>
    public void ResetToDefaults()
    {
        currentMouseSensitivity = defaultMouseSensitivity;
        UpdateUI();
        SaveSettings();
    }
}

