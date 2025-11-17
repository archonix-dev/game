using UnityEngine;

public class CameraMouseFollow : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Скорость поворота камеры за мышью")]
    public float rotationSpeed = 2f;
    
    [Tooltip("Максимальный угол поворота по горизонтали (в градусах)")]
    public float maxHorizontalAngle = 15f;
    
    [Tooltip("Максимальный угол поворота по вертикали (в градусах)")]
    public float maxVerticalAngle = 10f;
    
    [Tooltip("Использовать плавное следование")]
    public bool smoothFollow = true;
    
    [Tooltip("Множитель чувствительности мыши (0-500 из KeybindScript будет нормализован к 0-2)")]
    [SerializeField] private float sensitivityMultiplier = 1f;
    
    private Quaternion initialRotation;
    private Camera cam;
    private KeybindScript keybindScript;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        // Сохраняем начальный поворот камеры
        initialRotation = transform.localRotation;
        
        // Находим KeybindScript для получения чувствительности мыши
        keybindScript = KeybindScript.Instance;
        
        // Загружаем чувствительность из KeybindScript
        LoadMouseSensitivity();
    }
    
    /// <summary>
    /// Загружает чувствительность мыши из KeybindScript
    /// </summary>
    private void LoadMouseSensitivity()
    {
        float mouseSensitivity = 100f; // Значение по умолчанию
        
        if (keybindScript != null)
        {
            mouseSensitivity = keybindScript.GetMouseSensitivity();
        }
        
        // Нормализуем чувствительность (0-500) к множителю (0-2)
        // 100 (по умолчанию) = 1.0, 500 (максимум) = 2.0, 0 (минимум) = 0.0
        sensitivityMultiplier = Mathf.Clamp(mouseSensitivity / 100f, 0f, 2f);
    }
    
    void Update()
    {
        if (cam == null) return;
        
        // Обновляем чувствительность мыши из KeybindScript (на случай изменения в настройках)
        UpdateMouseSensitivity();
        
        // Получаем позицию мыши в экранных координатах (0-1)
        Vector3 mousePosition = Input.mousePosition;
        Vector3 viewportPoint = cam.ScreenToViewportPoint(mousePosition);
        
        // Преобразуем в диапазон от -1 до 1 (центр экрана = 0,0)
        float normalizedX = (viewportPoint.x - 0.5f) * 2f;
        float normalizedY = (viewportPoint.y - 0.5f) * 2f;
        
        // Применяем чувствительность мыши к нормализованным значениям
        normalizedX *= sensitivityMultiplier;
        normalizedY *= sensitivityMultiplier;
        
        // Вычисляем целевые углы поворота (инвертируем Y для естественного движения)
        float targetYaw = normalizedX * maxHorizontalAngle;
        float targetPitch = -normalizedY * maxVerticalAngle;
        
        // Создаем целевой поворот относительно начального
        Quaternion targetRotation = initialRotation * Quaternion.Euler(targetPitch, targetYaw, 0f);
        
        // Плавно поворачиваем камеру
        if (smoothFollow)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            transform.localRotation = targetRotation;
        }
    }
    
    /// <summary>
    /// Обновляет чувствительность мыши из KeybindScript
    /// </summary>
    private void UpdateMouseSensitivity()
    {
        if (keybindScript != null)
        {
            float mouseSensitivity = keybindScript.GetMouseSensitivity();
            // Нормализуем чувствительность (0-500) к множителю (0-2)
            sensitivityMultiplier = Mathf.Clamp(mouseSensitivity / 100f, 0f, 2f);
        }
        else
        {
            // Если KeybindScript не найден, пытаемся найти его снова
            keybindScript = KeybindScript.Instance;
            if (keybindScript != null)
            {
                float mouseSensitivity = keybindScript.GetMouseSensitivity();
                sensitivityMultiplier = Mathf.Clamp(mouseSensitivity / 100f, 0f, 2f);
            }
            else
            {
                // Если KeybindScript не найден, используем значение по умолчанию
                sensitivityMultiplier = 1f; // Значение по умолчанию (100 / 100 = 1.0)
            }
        }
    }
    
    /// <summary>
    /// Сброс поворота камеры к начальному
    /// </summary>
    public void ResetRotation()
    {
        initialRotation = transform.localRotation;
    }
}

