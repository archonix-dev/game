using UnityEngine;

/// <summary>
/// Скрипт для UI элемента, который смещается в зависимости от позиции курсора
/// </summary>
public class UICursorRotation : MonoBehaviour
{
    [Header("Позиции смещения")]
    [Tooltip("Позиция когда курсор под объектом")]
    [SerializeField] private Vector3 positionBelow = new Vector3(-26.99991f, -20.3f, 0f);
    
    [Tooltip("Позиция когда курсор над объектом")]
    [SerializeField] private Vector3 positionAbove = new Vector3(-26.99991f, 18.6f, 0f);
    
    [Tooltip("Позиция когда курсор слева от объекта")]
    [SerializeField] private Vector3 positionLeft = new Vector3(-39.7f, 3.100023f, 0f);
    
    [Tooltip("Позиция когда курсор справа от объекта")]
    [SerializeField] private Vector3 positionRight = new Vector3(-13.5f, 3.100023f, 0f);
    
    [Tooltip("Базовая позиция (центр, когда курсор в центре объекта)")]
    [SerializeField] private Vector3 basePosition = new Vector3(-26.99991f, 3.100023f, 0f);
    
    [Header("Настройки смещения")]
    [Tooltip("Скорость смещения (0 = мгновенно, больше = плавнее)")]
    [SerializeField] private float moveSpeed = 10f;
    
    [Tooltip("Чувствительность определения направления курсора (0.5 = средняя зона)")]
    [SerializeField] private float sensitivity = 0.5f;
    
    [Header("Настройки Canvas")]
    [Tooltip("Canvas, к которому принадлежит этот UI элемент (если не указан, будет найден автоматически)")]
    [SerializeField] private Canvas canvas;
    
    [Tooltip("Камера для World Space Canvas (если не указана, будет использована Main Camera)")]
    [SerializeField] private Camera worldCamera;
    
    private RectTransform rectTransform;
    private Vector3 targetPosition;
    private Vector3 currentPosition;
    private Vector3 initialPosition;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (rectTransform == null)
        {
            Debug.LogError("UICursorRotation: RectTransform не найден на объекте!");
            enabled = false;
            return;
        }
        
        // Сохраняем начальную позицию
        initialPosition = rectTransform.localPosition;
        currentPosition = initialPosition;
        
        // Используем базовую позицию, если она установлена, иначе используем текущую позицию
        if (basePosition != Vector3.zero || initialPosition != Vector3.zero)
        {
            // Если базовая позиция не была установлена, используем текущую позицию
            if (basePosition == Vector3.zero)
            {
                basePosition = initialPosition;
            }
            currentPosition = basePosition;
        }
        
        targetPosition = currentPosition;
        rectTransform.localPosition = currentPosition;
        
        // Находим Canvas, если не указан
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        
        // Находим камеру для World Space Canvas
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            if (worldCamera == null)
            {
                worldCamera = canvas.worldCamera;
                if (worldCamera == null)
                {
                    worldCamera = Camera.main;
                }
            }
        }
    }
    
    void Update()
    {
        if (rectTransform == null) return;
        
        // Вычисляем целевую позицию на основе позиции курсора
        CalculateTargetPosition();
        
        // Плавно смещаем к целевой позиции
        if (moveSpeed > 0f)
        {
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, moveSpeed * Time.deltaTime);
        }
        else
        {
            currentPosition = targetPosition;
        }
        
        // Применяем позицию
        rectTransform.localPosition = currentPosition;
    }
    
    /// <summary>
    /// Вычисляет целевую позицию на основе позиции курсора
    /// </summary>
    private void CalculateTargetPosition()
    {
        Vector2 cursorScreenPos = Input.mousePosition;
        Camera cam = null;
        
        // Определяем камеру в зависимости от типа Canvas
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                cam = canvas.worldCamera;
            }
            else if (canvas.renderMode == RenderMode.WorldSpace)
            {
                cam = worldCamera ?? canvas.worldCamera ?? Camera.main;
            }
        }
        
        // Получаем экранную позицию UI объекта
        Vector2 uiScreenPos;
        Vector2 bottomLeft;
        Vector2 topRight;
        
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Для ScreenSpaceOverlay позиция уже в экранных координатах
            uiScreenPos = rectTransform.position;
            
            // Получаем углы RectTransform в локальных координатах и конвертируем в экранные
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            bottomLeft = corners[0];
            topRight = corners[2];
        }
        else
        {
            // Для других режимов используем камеру
            cam = cam ?? Camera.main;
            uiScreenPos = RectTransformUtility.WorldToScreenPoint(cam, rectTransform.position);
            
            // Получаем размеры RectTransform в экранных координатах
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        }
        
        // Вычисляем смещение курсора относительно UI объекта
        Vector2 offset = cursorScreenPos - uiScreenPos;
        
        float width = Mathf.Abs(topRight.x - bottomLeft.x);
        float height = Mathf.Abs(topRight.y - bottomLeft.y);
        
        // Вычисляем нормализованное смещение относительно размера объекта
        // sensitivity определяет порог для определения направления
        float normalizedX = width > 0.01f ? (offset.x / width) : 0f;
        float normalizedY = height > 0.01f ? (offset.y / height) : 0f;
        
        // Определяем доминирующее направление
        // Используем абсолютные значения для определения приоритета направления
        float absX = Mathf.Abs(normalizedX);
        float absY = Mathf.Abs(normalizedY);
        
        bool isLeft = normalizedX < -sensitivity;
        bool isRight = normalizedX > sensitivity;
        bool isBelow = normalizedY < -sensitivity;
        bool isAbove = normalizedY > sensitivity;
        
        // Определяем целевую позицию на основе направления
        // Приоритет отдается более выраженному направлению
        if (absY > absX)
        {
            // Вертикальное направление более выражено
            if (isAbove)
            {
                targetPosition = positionAbove;
            }
            else if (isBelow)
            {
                targetPosition = positionBelow;
            }
            else
            {
                targetPosition = basePosition;
            }
        }
        else if (absX > absY)
        {
            // Горизонтальное направление более выражено
            if (isLeft)
            {
                targetPosition = positionLeft;
            }
            else if (isRight)
            {
                targetPosition = positionRight;
            }
            else
            {
                targetPosition = basePosition;
            }
        }
        else
        {
            // Курсор в центре или одинаковое смещение по обеим осям
            targetPosition = basePosition;
        }
    }
    
    /// <summary>
    /// Сброс позиции к начальному состоянию
    /// </summary>
    public void ResetPosition()
    {
        if (rectTransform != null)
        {
            currentPosition = basePosition;
            targetPosition = basePosition;
            rectTransform.localPosition = basePosition;
        }
    }
    
    /// <summary>
    /// Установка базовой позиции программно
    /// </summary>
    public void SetBasePosition(Vector3 newBasePosition)
    {
        basePosition = newBasePosition;
    }
}
