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
    
    private Quaternion initialRotation;
    private Camera cam;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        // Сохраняем начальный поворот камеры
        initialRotation = transform.localRotation;
    }
    
    void Update()
    {
        if (cam == null) return;
        
        // Получаем позицию мыши в экранных координатах (0-1)
        Vector3 mousePosition = Input.mousePosition;
        Vector3 viewportPoint = cam.ScreenToViewportPoint(mousePosition);
        
        // Преобразуем в диапазон от -1 до 1 (центр экрана = 0,0)
        float normalizedX = (viewportPoint.x - 0.5f) * 2f;
        float normalizedY = (viewportPoint.y - 0.5f) * 2f;
        
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
    /// Сброс поворота камеры к начальному
    /// </summary>
    public void ResetRotation()
    {
        initialRotation = transform.localRotation;
    }
}

