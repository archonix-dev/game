using UnityEngine;
using Mirror;

/// <summary>
/// Добавляет эффект bodycam к камере: покачивания при ходьбе, наклоны при поворотах
/// </summary>
[RequireComponent(typeof(Camera))]
public class BodyCamEffect : NetworkBehaviour
{
    [Header("Head Bob Settings")]
    [Tooltip("Амплитуда вертикального покачивания при ходьбе")]
    [SerializeField] private float headBobAmplitude = 0.1f;
    
    [Tooltip("Частота покачивания при ходьбе")]
    [SerializeField] private float headBobFrequency = 3f;
    
    [Tooltip("Скорость затухания покачивания при остановке")]
    [SerializeField] private float headBobDamping = 5f;
    
    [Header("Tilt Settings")]
    [Tooltip("Максимальный угол наклона при поворотах (в градусах)")]
    [SerializeField] private float maxTiltAngle = 3f;
    
    [Tooltip("Скорость наклона при поворотах")]
    [SerializeField] private float tiltSpeed = 8f;
    
    [Tooltip("Скорость возврата наклона в исходное положение")]
    [SerializeField] private float tiltReturnSpeed = 4f;
    
    [Header("Random Shake Settings")]
    [Tooltip("Интенсивность случайных покачиваний (0 = отключено)")]
    [SerializeField] private float randomShakeIntensity = 0.01f;
    
    [Tooltip("Частота случайных покачиваний")]
    [SerializeField] private float randomShakeFrequency = 0.5f;
    
    [Header("Speed Multipliers")]
    [Tooltip("Множитель покачивания при беге")]
    [SerializeField] private float runMultiplier = 1.5f;
    
    [Tooltip("Множитель покачивания при приседании")]
    [SerializeField] private float crouchMultiplier = 0.5f;
    
    [Tooltip("Множитель покачивания в положении лежа")]
    [SerializeField] private float proneMultiplier = 0.2f;
    
    [Header("References")]
    [Tooltip("PlayerController для получения информации о движении и стойке")]
    [SerializeField] private PlayerController playerController;
    
    [Tooltip("CharacterController для получения скорости движения")]
    [SerializeField] private CharacterController characterController;
    
    private Vector3 originalLocalPosition;
    private float headBobTimer = 0f;
    private float currentHeadBobOffset = 0f;
    private float currentTilt = 0f;
    private Vector3 lastPosition;
    private float movementSpeed = 0f;
    private float smoothedMovementSpeed = 0f;
    private Vector3 randomShakeOffset = Vector3.zero;
    private float randomShakeTimer = 0f;
    
    void Awake()
    {
        // Сохраняем исходную локальную позицию камеры
        originalLocalPosition = transform.localPosition;
        
        // Находим компоненты если не назначены
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }
        
        if (characterController == null)
        {
            characterController = GetComponentInParent<CharacterController>();
        }
        
        // Инициализируем позицию корневого объекта для вычисления скорости
        if (transform.root != null && transform.root != transform)
        {
            lastPosition = transform.root.position;
        }
        else
        {
            lastPosition = transform.position;
        }
    }
    
    void Start()
    {
        // Инициализируем позицию корневого объекта
        if (transform.root != null && transform.root != transform)
        {
            lastPosition = transform.root.position;
        }
        else
        {
            lastPosition = transform.position;
        }
    }
    
    void Update()
    {
        // Обрабатываем эффект только для владельца камеры
        if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
        
        // Вычисляем скорость движения
        CalculateMovementSpeed();
        
        // Применяем эффекты
        ApplyHeadBob();
        ApplyTilt();
        ApplyRandomShake();
    }
    
    void LateUpdate()
    {
        // Обрабатываем эффект только для владельца камеры
        if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
        
        // Обновляем позицию камеры в LateUpdate, чтобы применить эффекты после всех других обновлений
        UpdateCameraPosition();
    }
    
    /// <summary>
    /// Вычисляет скорость движения игрока
    /// </summary>
    void CalculateMovementSpeed()
    {
        if (characterController != null)
        {
            // Используем velocity из CharacterController для более точного определения скорости
            Vector3 velocity = characterController.velocity;
            // Игнорируем вертикальную скорость (прыжки/падение)
            velocity.y = 0f;
            movementSpeed = velocity.magnitude;
            
            // Сглаживаем скорость для плавности
            smoothedMovementSpeed = Mathf.Lerp(smoothedMovementSpeed, movementSpeed, Time.deltaTime * 10f);
        }
        else if (transform.root != null && transform.root != transform)
        {
            // Если нет CharacterController, используем позицию корневого объекта (игрока)
            Vector3 currentPosition = transform.root.position;
            float deltaTime = Time.deltaTime;
            
            if (deltaTime > 0f)
            {
                movementSpeed = Vector3.Distance(currentPosition, lastPosition) / deltaTime;
                lastPosition = currentPosition;
                
                // Сглаживаем скорость для плавности
                smoothedMovementSpeed = Mathf.Lerp(smoothedMovementSpeed, movementSpeed, Time.deltaTime * 10f);
            }
        }
        else
        {
            // Если нет CharacterController и корневого объекта, используем ввод
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            float inputMagnitude = new Vector2(horizontal, vertical).magnitude;
            
            // Используем скорость ходьбы по умолчанию (4 м/с)
            float targetSpeed = inputMagnitude * 4f;
            smoothedMovementSpeed = Mathf.Lerp(smoothedMovementSpeed, targetSpeed, Time.deltaTime * 10f);
        }
    }
    
    /// <summary>
    /// Применяет эффект head bob (вертикальное покачивание при ходьбе)
    /// </summary>
    void ApplyHeadBob()
    {
        // Получаем множитель в зависимости от стойки
        float stanceMultiplier = 1f;
        if (playerController != null)
        {
            if (playerController.IsProne())
            {
                stanceMultiplier = proneMultiplier;
            }
            else if (playerController.IsCrouching())
            {
                stanceMultiplier = crouchMultiplier;
            }
            else if (Input.GetKey(KeyCode.LeftShift) && smoothedMovementSpeed > 0.1f)
            {
                stanceMultiplier = runMultiplier;
            }
        }
        
        // Вычисляем интенсивность покачивания на основе скорости
        // Используем скорость бега (6 м/с) как максимальную для нормализации
        float speedFactor = Mathf.Clamp01(smoothedMovementSpeed / 6f);
        
        // Минимальный порог для начала покачивания (даже при медленной ходьбе)
        float minSpeedThreshold = 0.5f; // м/с
        
        if (smoothedMovementSpeed > minSpeedThreshold)
        {
            // Увеличиваем таймер покачивания
            headBobTimer += Time.deltaTime * headBobFrequency * speedFactor * stanceMultiplier;
            
            // Вычисляем вертикальное смещение (синусоида)
            currentHeadBobOffset = Mathf.Sin(headBobTimer) * headBobAmplitude * speedFactor * stanceMultiplier;
        }
        else
        {
            // Плавно затухаем покачивание при остановке
            currentHeadBobOffset = Mathf.Lerp(currentHeadBobOffset, 0f, Time.deltaTime * headBobDamping);
            headBobTimer = 0f;
        }
    }
    
    /// <summary>
    /// Применяет наклон камеры при поворотах
    /// </summary>
    void ApplyTilt()
    {
        // Получаем скорость поворота мыши
        float mouseX = Input.GetAxis("Mouse X");
        
        // Вычисляем целевой наклон на основе скорости поворота
        float targetTilt = -mouseX * maxTiltAngle;
        
        // Плавно интерполируем к целевому наклону
        if (Mathf.Abs(mouseX) > 0.01f)
        {
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        }
        else
        {
            // Возвращаем наклон в исходное положение при отсутствии поворота
            currentTilt = Mathf.Lerp(currentTilt, 0f, Time.deltaTime * tiltReturnSpeed);
        }
    }
    
    /// <summary>
    /// Применяет случайные покачивания для эффекта bodycam
    /// </summary>
    void ApplyRandomShake()
    {
        if (randomShakeIntensity <= 0f)
        {
            randomShakeOffset = Vector3.zero;
            return;
        }
        
        randomShakeTimer += Time.deltaTime * randomShakeFrequency;
        
        // Генерируем случайные смещения с использованием Perlin noise для плавности
        float x = Mathf.PerlinNoise(randomShakeTimer, 0f) * 2f - 1f;
        float y = Mathf.PerlinNoise(0f, randomShakeTimer) * 2f - 1f;
        float z = Mathf.PerlinNoise(randomShakeTimer, randomShakeTimer) * 2f - 1f;
        
        // Применяем интенсивность и уменьшаем при отсутствии движения
        float movementFactor = Mathf.Clamp01(smoothedMovementSpeed / 4f);
        randomShakeOffset = new Vector3(x, y, z) * randomShakeIntensity * movementFactor;
    }
    
    /// <summary>
    /// Обновляет позицию и поворот камеры с учетом всех эффектов
    /// </summary>
    void UpdateCameraPosition()
    {
        // Убеждаемся, что исходная позиция установлена
        if (originalLocalPosition == Vector3.zero)
        {
            originalLocalPosition = transform.localPosition;
        }
        
        // Применяем head bob к позиции
        Vector3 newPosition = originalLocalPosition;
        newPosition.y += currentHeadBobOffset;
        
        // Добавляем случайные покачивания
        newPosition += randomShakeOffset;
        
        transform.localPosition = newPosition;
        
        // Применяем наклон к повороту (только по оси Z для наклона)
        // Сохраняем текущие углы X и Y, изменяем только Z
        Quaternion currentRotation = transform.localRotation;
        Vector3 eulerAngles = currentRotation.eulerAngles;
        eulerAngles.z = currentTilt;
        transform.localRotation = Quaternion.Euler(eulerAngles);
    }
    
    /// <summary>
    /// Сбрасывает все эффекты к исходному состоянию
    /// </summary>
    public void ResetEffects()
    {
        headBobTimer = 0f;
        currentHeadBobOffset = 0f;
        currentTilt = 0f;
        randomShakeOffset = Vector3.zero;
        randomShakeTimer = 0f;
        transform.localPosition = originalLocalPosition;
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, 0f);
    }
    
    void OnEnable()
    {
        // Сохраняем исходную позицию при включении
        if (originalLocalPosition == Vector3.zero)
        {
            originalLocalPosition = transform.localPosition;
        }
    }
    
    void OnValidate()
    {
        // В редакторе обновляем исходную позицию при изменении компонента
        if (Application.isPlaying && originalLocalPosition != Vector3.zero)
        {
            // Не перезаписываем, если уже установлена
        }
    }
}

