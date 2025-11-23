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
    
    [Header("Stance Transition Settings")]
    [Tooltip("Длительность перехода между стойками (в секундах)")]
    [SerializeField] private float stanceTransitionDuration = 0.5f;
    
    [Tooltip("Интенсивность покачивания при переходе в присед")]
    [SerializeField] private float crouchTransitionShake = 0.05f;
    
    [Tooltip("Интенсивность покачивания при переходе в лежа")]
    [SerializeField] private float proneTransitionShake = 0.1f;
    
    [Tooltip("Интенсивность покачивания при подъеме из приседа/лежа")]
    [SerializeField] private float standUpTransitionShake = 0.08f;
    
    [Tooltip("Скорость затухания покачивания при переходе")]
    [SerializeField] private float transitionShakeDamping = 3f;
    
    [Header("Stance-Based Tilt Settings")]
    [Tooltip("Множитель наклона при приседании (0.5 = в два раза меньше)")]
    [SerializeField] private float crouchTiltMultiplier = 0.5f;
    
    [Tooltip("Множитель наклона в положении лежа (0 = отключено)")]
    [SerializeField] private float proneTiltMultiplier = 0.1f;
    
    [Header("Explosion Shake Settings")]
    [Tooltip("Максимальная интенсивность тряски при взрыве (на минимальном расстоянии)")]
    [SerializeField] private float maxExplosionShakeIntensity = 0.3f;
    
    [Tooltip("Максимальное расстояние, на котором взрыв вызывает тряску камеры")]
    [SerializeField] private float maxExplosionShakeDistance = 20f;
    
    [Tooltip("Длительность тряски при взрыве (в секундах)")]
    [SerializeField] private float explosionShakeDuration = 0.5f;
    
    [Tooltip("Скорость затухания тряски при взрыве")]
    [SerializeField] private float explosionShakeDamping = 5f;
    
    [Tooltip("Частота тряски при взрыве")]
    [SerializeField] private float explosionShakeFrequency = 20f;
    
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
    
    // Переменные для отслеживания стойки и переходов
    private bool wasCrouching = false;
    private bool wasProne = false;
    private bool wasStanding = true;
    private float stanceTransitionTimer = 0f;
    private Vector3 transitionShakeOffset = Vector3.zero;
    private float smoothedStanceMultiplier = 1f;
    private float targetStanceMultiplier = 1f;
    
    // Переменные для тряски при взрывах
    private float explosionShakeTimer = 0f;
    private float explosionShakeIntensity = 0f;
    private Vector3 explosionShakeOffset = Vector3.zero;
    
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
        
        // Инициализируем состояние стойки
        if (playerController != null)
        {
            wasCrouching = playerController.IsCrouching();
            wasProne = playerController.IsProne();
            wasStanding = playerController.IsStanding();
            
            // Устанавливаем начальный множитель стойки
            if (wasProne)
            {
                targetStanceMultiplier = proneMultiplier;
            }
            else if (wasCrouching)
            {
                targetStanceMultiplier = crouchMultiplier;
            }
            else
            {
                targetStanceMultiplier = 1f;
            }
            smoothedStanceMultiplier = targetStanceMultiplier;
        }
    }
    
    void Update()
    {
        // Обрабатываем эффект только для владельца камеры
        if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
        
        // Вычисляем скорость движения
        CalculateMovementSpeed();
        
        // Обрабатываем переходы между стойками
        HandleStanceTransitions();
        
        // Применяем эффекты
        ApplyHeadBob();
        ApplyTilt();
        ApplyRandomShake();
        ApplyExplosionShake();
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
    /// Обрабатывает переходы между стойками и применяет покачивание при переходе
    /// </summary>
    void HandleStanceTransitions()
    {
        if (playerController == null) return;
        
        bool isCrouching = playerController.IsCrouching();
        bool isProne = playerController.IsProne();
        bool isStanding = playerController.IsStanding();
        
        // Определяем целевую стойку для множителя
        targetStanceMultiplier = 1f;
        if (isProne)
        {
            targetStanceMultiplier = proneMultiplier;
        }
        else if (isCrouching)
        {
            targetStanceMultiplier = crouchMultiplier;
        }
        else if (Input.GetKey(KeyCode.LeftShift) && smoothedMovementSpeed > 0.1f)
        {
            targetStanceMultiplier = runMultiplier;
        }
        
        // Плавно интерполируем множитель стойки
        smoothedStanceMultiplier = Mathf.Lerp(smoothedStanceMultiplier, targetStanceMultiplier, Time.deltaTime * 5f);
        
        // Обнаруживаем изменение стойки
        float transitionShakeIntensity = 0f;
        bool isTransitioningToStanding = false;
        
        // Переход в присед
        if (isCrouching && !wasCrouching)
        {
            transitionShakeIntensity = crouchTransitionShake;
            stanceTransitionTimer = 0f;
        }
        // Переход в лежа
        else if (isProne && !wasProne)
        {
            transitionShakeIntensity = proneTransitionShake;
            stanceTransitionTimer = 0f;
        }
        // Подъем из приседа/лежа
        else if (isStanding && (wasCrouching || wasProne))
        {
            transitionShakeIntensity = standUpTransitionShake;
            stanceTransitionTimer = 0f;
            isTransitioningToStanding = true;
        }
        
        // Обновляем состояние
        wasCrouching = isCrouching;
        wasProne = isProne;
        wasStanding = isStanding;
        
        // Применяем покачивание при переходе
        if (stanceTransitionTimer < stanceTransitionDuration)
        {
            stanceTransitionTimer += Time.deltaTime;
            
            // Вычисляем силу покачивания (убывает со временем)
            float transitionProgress = stanceTransitionTimer / stanceTransitionDuration;
            float shakeStrength = transitionShakeIntensity * (1f - transitionProgress);
            
            // Применяем покачивание вниз при переходе в присед/лежа, вверх при подъеме
            float verticalDirection = isTransitioningToStanding ? 1f : -1f;
            float horizontalShake = Mathf.Sin(stanceTransitionTimer * 15f) * shakeStrength * 0.5f;
            
            transitionShakeOffset = new Vector3(
                horizontalShake,
                Mathf.Sin(stanceTransitionTimer * 12f) * shakeStrength * verticalDirection,
                Mathf.Cos(stanceTransitionTimer * 10f) * shakeStrength * 0.3f
            );
        }
        else
        {
            // Плавно затухаем покачивание после перехода
            transitionShakeOffset = Vector3.Lerp(transitionShakeOffset, Vector3.zero, Time.deltaTime * transitionShakeDamping);
        }
    }
    
    /// <summary>
    /// Применяет эффект head bob (вертикальное покачивание при ходьбе)
    /// </summary>
    void ApplyHeadBob()
    {
        // Используем плавно интерполированный множитель стойки
        float stanceMultiplier = smoothedStanceMultiplier;
        
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
    /// Применяет наклон камеры при поворотах с учетом стойки
    /// </summary>
    void ApplyTilt()
    {
        // Получаем скорость поворота мыши
        float mouseX = Input.GetAxis("Mouse X");
        
        // Определяем множитель наклона в зависимости от стойки
        float tiltMultiplier = 1f;
        if (playerController != null)
        {
            if (playerController.IsProne())
            {
                tiltMultiplier = proneTiltMultiplier;
            }
            else if (playerController.IsCrouching())
            {
                tiltMultiplier = crouchTiltMultiplier;
            }
        }
        
        // Вычисляем целевой наклон на основе скорости поворота с учетом множителя
        float targetTilt = -mouseX * maxTiltAngle * tiltMultiplier;
        
        // Плавно интерполируем к целевому наклону
        if (Mathf.Abs(mouseX) > 0.01f)
        {
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        }
        else
        {
            // Возвращаем наклон в исходное положение при отсутствии поворота
            // Используем более быстрое возвращение при приседе/лежа для реалистичности
            float returnSpeed = tiltMultiplier < 0.5f ? tiltReturnSpeed * 1.5f : tiltReturnSpeed;
            currentTilt = Mathf.Lerp(currentTilt, 0f, Time.deltaTime * returnSpeed);
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
    /// Применяет тряску камеры при взрывах
    /// </summary>
    void ApplyExplosionShake()
    {
        if (explosionShakeIntensity <= 0f || explosionShakeTimer <= 0f)
        {
            // Плавно затухаем остаточную тряску
            explosionShakeOffset = Vector3.Lerp(explosionShakeOffset, Vector3.zero, Time.deltaTime * explosionShakeDamping);
            return;
        }
        
        // Уменьшаем таймер
        explosionShakeTimer -= Time.deltaTime;
        
        // Вычисляем силу тряски (убывает со временем и расстоянием)
        float timeFactor = explosionShakeTimer / explosionShakeDuration;
        float currentIntensity = explosionShakeIntensity * timeFactor;
        
        // Генерируем тряску с использованием синусоидальных функций для реалистичности
        float shakeTime = (explosionShakeDuration - explosionShakeTimer) * explosionShakeFrequency;
        float x = Mathf.Sin(shakeTime * 1.3f) * currentIntensity;
        float y = Mathf.Sin(shakeTime * 1.7f + Mathf.PI * 0.5f) * currentIntensity;
        float z = Mathf.Cos(shakeTime * 1.1f) * currentIntensity * 0.5f;
        
        explosionShakeOffset = new Vector3(x, y, z);
        
        // Сбрасываем интенсивность когда таймер закончился
        if (explosionShakeTimer <= 0f)
        {
            explosionShakeIntensity = 0f;
        }
    }
    
    /// <summary>
    /// Вызывает тряску камеры при взрыве (вызывается извне)
    /// </summary>
    /// <param name="intensity">Интенсивность тряски (0-1)</param>
    public void TriggerExplosionShake(float intensity)
    {
        if (intensity <= 0f)
            return;
        
        // Устанавливаем новую тряску, перезаписывая старую если она сильнее
        if (intensity > explosionShakeIntensity || explosionShakeTimer <= 0f)
        {
            explosionShakeIntensity = intensity;
            explosionShakeTimer = explosionShakeDuration;
        }
    }
    
    /// <summary>
    /// Статический метод для вызова тряски камеры на всех игроках при взрыве
    /// Работает на сервере и клиентах, вызывая тряску для всех найденных игроков
    /// </summary>
    /// <param name="explosionPosition">Позиция взрыва</param>
    /// <param name="maxIntensity">Максимальная интенсивность тряски (на минимальном расстоянии)</param>
    /// <param name="maxDistance">Максимальное расстояние, на котором взрыв вызывает тряску</param>
    public static void TriggerExplosionShakeForAllPlayers(Vector3 explosionPosition, float maxIntensity = 0.3f, float maxDistance = 20f)
    {
        // Ищем всех игроков в сети
        // На сервере используем NetworkServer.spawned, на клиентах используем FindObjectsOfType
        System.Collections.Generic.IEnumerable<PlayerController> players;
        
        if (NetworkServer.active)
        {
            // На сервере используем NetworkServer.spawned для поиска всех игроков
            System.Collections.Generic.List<PlayerController> serverPlayers = new System.Collections.Generic.List<PlayerController>();
            foreach (var kvp in NetworkServer.spawned)
            {
                NetworkIdentity identity = kvp.Value;
                if (identity == null)
                    continue;
                
                PlayerController playerController = identity.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    serverPlayers.Add(playerController);
                }
            }
            players = serverPlayers;
        }
        else
        {
            // На клиенте используем FindObjectsOfType для поиска всех игроков
            players = Object.FindObjectsOfType<PlayerController>();
        }
        
        // Применяем тряску для каждого игрока
        foreach (PlayerController playerController in players)
        {
            if (playerController == null)
                continue;
            
            // Проверяем, что игрок активен и не мертв
            if (!playerController.gameObject.activeInHierarchy)
                continue;
            
            PlayerHealthStamina health = playerController.GetComponent<PlayerHealthStamina>();
            if (health != null && health.GetCurrentHealth() <= 0f)
                continue;
            
            // Вычисляем расстояние до взрыва
            Vector3 playerPosition = playerController.transform.position;
            float distance = Vector3.Distance(playerPosition, explosionPosition);
            
            // Если игрок вне радиуса взрыва, пропускаем
            if (distance > maxDistance)
                continue;
            
            // Вычисляем интенсивность на основе расстояния (обратная зависимость)
            float distanceFactor = 1f - (distance / maxDistance);
            float intensity = maxIntensity * distanceFactor;
            
            // Находим BodyCamEffect на камере игрока
            Camera playerCamera = playerController.GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                BodyCamEffect bodyCamEffect = playerCamera.GetComponent<BodyCamEffect>();
                if (bodyCamEffect != null)
                {
                    bodyCamEffect.TriggerExplosionShake(intensity);
                }
            }
        }
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
        
        // Добавляем покачивание при переходе между стойками
        newPosition += transitionShakeOffset;
        
        // Добавляем случайные покачивания
        newPosition += randomShakeOffset;
        
        // Добавляем тряску при взрывах
        newPosition += explosionShakeOffset;
        
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
        transitionShakeOffset = Vector3.zero;
        stanceTransitionTimer = 0f;
        smoothedStanceMultiplier = 1f;
        targetStanceMultiplier = 1f;
        wasCrouching = false;
        wasProne = false;
        wasStanding = true;
        explosionShakeTimer = 0f;
        explosionShakeIntensity = 0f;
        explosionShakeOffset = Vector3.zero;
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

