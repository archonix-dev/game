using UnityEngine;
using Mirror;

public class ObjectGrabSystem : NetworkBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float grabDistance = 3f;
    [SerializeField] private float holdDistance = 2f;
    [SerializeField] private float objectDistance = 0.5f; // Дополнительное расстояние объекта от holdPoint
    [SerializeField] private LayerMask grabbableLayer;
    [SerializeField] private Transform holdPoint;
    
    [Header("Physics Settings")]
    [SerializeField] private float grabForce = 500f;
    [SerializeField] private float dampingForce = 50f;
    [SerializeField] private float maxGrabVelocity = 10f;
    [SerializeField] private float rotationSpeed = 5f;
    
    [Header("Weight System")]
    [SerializeField] private float maxComfortableWeight = 10f;
    [SerializeField] private float weightSlipFactor = 0.1f;
    [SerializeField] private float movementSlipMultiplier = 2f;
    [SerializeField] private float dropWeightThreshold = 50f;
    
    [Header("Strength System")]
    [SerializeField] private float baseStrength = 1f; // Базовая сила хвата
    // Синхронизированная сила хвата
    [SyncVar]
    [SerializeField] private float currentStrength = 1f; // Текущая сила хвата (с бонусами)
    [SerializeField] private float strengthMultiplier = 0.25f; // Множитель для уменьшения силы в 4 раза
    
    [Header("Mouse Sensitivity Adjustment")]
    [SerializeField] private float weightSensitivityReduction = 0.5f;
    [SerializeField] private MouseLook mouseLook;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float highlightIntensity = 1.5f;
    
    [Header("Grab Line Settings")]
    [SerializeField] private Transform lineStartPoint; // Начальная точка линии (если не указана, используется holdPoint)
    [SerializeField] private int lineSegments = 20; // Количество сегментов для волн
    [SerializeField] private float waveAmplitude = 0.1f; // Амплитуда волн
    [SerializeField] private float waveFrequency = 2f; // Частота волн
    
    [Header("Throw System")]
    [SerializeField] private float maxThrowForce = 40f;
    [SerializeField] private float throwChargeSpeed = 2f;
    [SerializeField] private float throwUIStartShakeTime = 3f;
    [SerializeField] private float throwUIShakeIntensity = 10f;
    [SerializeField] private GameObject throwUIObject; // UI объект с картинкой и текстом
    [SerializeField] private UnityEngine.UI.Image throwForceImage; // Картинка силы броска
    [SerializeField] private UnityEngine.UI.Text throwForceText; // Текст силы броска
    [SerializeField] private float staminaCostPerThrowForce = 2f; // Стоимость стамины за единицу силы броска
    [SerializeField] private PlayerHealthStamina playerHealthStamina;
    
    // Синхронизированный захваченный объект (NetworkIdentity)
    [SyncVar(hook = nameof(OnGrabbedObjectChanged))]
    private uint grabbedObjectNetId = 0;
    private DestructibleObject currentGrabbedObject;
    private DestructibleObject currentLookingAt;
    private Camera playerCamera;
    private Rigidbody grabbedRigidbody;
    private float currentWeight;
    // Синхронизированное скольжение
    [SyncVar]
    private float slipAccumulation;
    private float originalMouseSensitivity;
    private Vector3 lastPlayerPosition;
    
    // Для сохранения точки захвата
    private Vector3 grabLocalOffset; // Локальное смещение от центра объекта до точки захвата
    private RaycastHit currentHit; // Сохраняем информацию о рейкасте
    
    // Для визуального выделения
    private Material highlightedMaterial;
    private Color originalEmissionColor;
    private bool wasEmissionEnabled;
    
    // Для визуализации удержания
    private LineRenderer grabLineRenderer;
    
    // Для системы броска
    // Синхронизированная сила броска
    [SyncVar]
    private float currentThrowForce = 0f;
    // Синхронизированное состояние зарядки броска
    [SyncVar]
    private bool isChargingThrow = false;
    private float throwChargeTime = 0f;
    private Vector3 originalThrowUIPosition;
    private Color originalThrowTextColor;
    
    void Start()
    {
        playerCamera = GetComponent<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        
        // Создаем точку удержания если не назначена
        if (holdPoint == null)
        {
            GameObject holdPointObj = new GameObject("HoldPoint");
            holdPointObj.transform.parent = transform;
            holdPointObj.transform.localPosition = new Vector3(0, -0.3f, holdDistance);
            holdPoint = holdPointObj.transform;
        }
        
        lastPlayerPosition = transform.root.position;
        
        // Находим компонент MouseLook если не назначен
        if (mouseLook == null)
        {
            mouseLook = GetComponent<MouseLook>();
        }
        
        // Находим компонент PlayerHealthStamina если не назначен
        if (playerHealthStamina == null)
        {
            playerHealthStamina = GetComponent<PlayerHealthStamina>();
        }
        
        // Инициализируем UI для броска
        InitializeThrowUI();
        
        // Инициализируем силу хвата
        currentStrength = baseStrength;
        
        // Создаем LineRenderer для визуализации удержания
        CreateGrabLineRenderer();
    }
    
    void Update()
    {
        // Обрабатываем ввод только для владельца
        if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
        
        // Проверяем, на что смотрит игрок
        CheckForGrabbableObject();
        
        // Проверяем, что захваченный объект еще существует (не был уничтожен)
        if (currentGrabbedObject != null && (currentGrabbedObject.gameObject == null || currentGrabbedObject == null))
        {
            // Объект был уничтожен - принудительно освобождаем его
            ForceReleaseObject();
        }
        
        // Обработка захвата/отпускания
        if (Input.GetMouseButtonDown(0))
        {
            if (currentGrabbedObject == null && currentLookingAt != null)
            {
                TryGrabObject(currentLookingAt);
            }
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            if (currentGrabbedObject != null)
            {
                ReleaseObject();
            }
        }
        
        // Вращение предмета колесиком мыши (улучшенное)
        if (currentGrabbedObject != null && Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            // Вращаем по разным осям в зависимости от зажатых клавиш
            if (Input.GetKey(KeyCode.LeftControl))
            {
                holdPoint.Rotate(Vector3.up, scroll * 500f * Time.deltaTime, Space.World);
            }
            else if (Input.GetKey(KeyCode.LeftAlt))
            {
                holdPoint.Rotate(Vector3.right, scroll * 500f * Time.deltaTime, Space.Self);
            }
            else
            {
                holdPoint.Rotate(Vector3.forward, scroll * 500f * Time.deltaTime, Space.Self);
            }
        }
        
        // Система броска
        HandleThrowSystem();
        
        // Проверка на скольжение и падение
        if (currentGrabbedObject != null)
        {
            HandleWeightAndSlipping();
        }
    }
    
    void FixedUpdate()
    {
        // Проверяем, что объект еще существует (не был уничтожен)
        if (currentGrabbedObject != null && grabbedRigidbody != null)
        {
            // Дополнительная проверка: объект может быть уничтожен, но ссылка еще не null
            if (currentGrabbedObject.gameObject == null || grabbedRigidbody.gameObject == null)
            {
                // Объект был уничтожен - освобождаем его
                ForceReleaseObject();
                return;
            }
            
            MoveGrabbedObject();
            UpdateGrabLineRenderer();
        }
        else
        {
            // Когда нет захваченного объекта, убеждаемся, что линия скрыта
            HideGrabLineRenderer();

            if (grabbedObjectNetId != 0)
            {
                // Объект был уничтожен, но netId еще не сброшен - освобождаем
                ForceReleaseObject();
            }
        }
    }
    
    void CheckForGrabbableObject()
    {
        if (playerCamera == null) return;
        
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
        // Убираем подсветку с предыдущего объекта и уведомляем о том, что больше не смотрим
        if (currentLookingAt != null && currentLookingAt != currentGrabbedObject)
        {
            RemoveHighlight(currentLookingAt);
            currentLookingAt.SetPlayerLookingAt(false); // Уведомляем что больше не смотрим
            currentLookingAt = null;
        }
        
        if (Physics.Raycast(ray, out RaycastHit hit, grabDistance, grabbableLayer))
        {
            DestructibleObject grabbable = hit.collider.GetComponent<DestructibleObject>();
            if (grabbable != null && grabbable != currentGrabbedObject)
            {
                currentLookingAt = grabbable;
                currentHit = hit; // Сохраняем информацию о рейкасте
                HighlightObject(grabbable);
                grabbable.SetPlayerLookingAt(true); // Уведомляем что смотрим на объект
            }
        }
    }
    
    void TryGrabObject(DestructibleObject grabbable)
    {
        // Обрабатываем захват только для владельца
        if (!isOwned) return;
        
        // Проверяем, можно ли взять предмет с учетом силы хвата
        float effectiveWeightThreshold = dropWeightThreshold * currentStrength;
        if (grabbable.objectWeight > effectiveWeightThreshold)
        {
            return;
        }
        
        NetworkIdentity objectNetId = grabbable.GetComponent<NetworkIdentity>();
        if (objectNetId == null || objectNetId.netId == 0)
        {
            Debug.LogWarning("[ObjectGrabSystem] Объект не имеет NetworkIdentity!");
            return;
        }
        
        // Синхронизируем захват через сервер
        if (isServer)
        {
            grabbedObjectNetId = objectNetId.netId;
            currentGrabbedObject = grabbable;
        }
        else
        {
            GrabObjectCommand(objectNetId.netId);
        }
        
        grabbedRigidbody = grabbable.GetComponent<Rigidbody>();
        
        if (grabbedRigidbody != null)
        {
            // Вычисляем локальное смещение от центра объекта до точки захвата
            // Это позволит держать объект в той точке, где мы его схватили
            Vector3 grabPoint = currentHit.point;
            Vector3 objectCenter = grabbedRigidbody.position;
            
            // Преобразуем мировое смещение в локальное пространство объекта
            grabLocalOffset = grabbedRigidbody.transform.InverseTransformPoint(grabPoint);
            
            // Настраиваем физику
            grabbedRigidbody.useGravity = true; // Оставляем гравитацию для реалистичности
            grabbedRigidbody.linearDamping = 2f;
            grabbedRigidbody.angularDamping = 5f;
            
            currentWeight = grabbable.objectWeight;
            slipAccumulation = 0f;
            
            // Снижаем чувствительность мыши в зависимости от веса
            if (mouseLook != null)
            {
                float weightFactor = Mathf.Clamp01(currentWeight / maxComfortableWeight);
                // Здесь можно добавить изменение чувствительности, но для этого 
                // нужно сделать mouseSensitivity публичным в MouseLook
            }
            
            grabbable.OnGrabbed();
            RemoveHighlight(grabbable);
            
            // Показываем UI для броска
            ShowThrowUI();
        }
    }
    
    /// <summary>
    /// Command для захвата объекта (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void GrabObjectCommand(uint objectNetId)
    {
        grabbedObjectNetId = objectNetId;
    }
    
    /// <summary>
    /// Hook для изменения захваченного объекта (вызывается при изменении SyncVar)
    /// </summary>
    void OnGrabbedObjectChanged(uint oldNetId, uint newNetId)
    {
        // Находим объект по NetworkIdentity
        if (newNetId == 0)
        {
            currentGrabbedObject = null;
            grabbedRigidbody = null;
        }
        else
        {
            NetworkIdentity foundNetId = null;
            foreach (NetworkIdentity netId in FindObjectsOfType<NetworkIdentity>())
            {
                if (netId.netId == newNetId)
                {
                    foundNetId = netId;
                    break;
                }
            }
            
            if (foundNetId != null)
            {
                currentGrabbedObject = foundNetId.GetComponent<DestructibleObject>();
                if (currentGrabbedObject != null)
                {
                    grabbedRigidbody = currentGrabbedObject.GetComponent<Rigidbody>();
                }
            }
        }
    }
    
    public void ReleaseObject()
    {
        // Обрабатываем отпускание только для владельца
        if (!isOwned) return;
        
        if (currentGrabbedObject != null)
        {
            // Если мы заряжали бросок - бросаем предмет
            if (isChargingThrow && currentThrowForce > 0)
            {
                ThrowObject();
            }
            
            currentGrabbedObject.OnReleased();
            
            if (grabbedRigidbody != null)
            {
                // Восстанавливаем нормальную физику
                grabbedRigidbody.linearDamping = 0f;
                grabbedRigidbody.angularDamping = 0.05f;
                grabbedRigidbody.useGravity = true;
            }
            
            // Синхронизируем отпускание через сервер
            if (isServer)
            {
                grabbedObjectNetId = 0;
                currentThrowForce = 0f;
                isChargingThrow = false;
            }
            else
            {
                ReleaseObjectCommand();
            }
            
            // Скрываем UI броска
            HideThrowUI();
            
            // Скрываем линию удержания
            HideGrabLineRenderer();
            
            // Сбрасываем все переменные броска
            currentGrabbedObject = null;
            grabbedRigidbody = null;
            currentWeight = 0f;
            slipAccumulation = 0f;
            throwChargeTime = 0f;
            
            // Сбрасываем состояние наведения
            if (currentLookingAt != null)
            {
                currentLookingAt.SetPlayerLookingAt(false);
                currentLookingAt = null;
            }
        }
    }
    
    /// <summary>
    /// Command для отпускания объекта (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void ReleaseObjectCommand()
    {
        grabbedObjectNetId = 0;
        currentThrowForce = 0f;
        isChargingThrow = false;
    }
    
    /// <summary>
    /// Принудительно освобождает объект (используется когда объект был уничтожен)
    /// </summary>
    void ForceReleaseObject()
    {
        // Сбрасываем все переменные
        currentGrabbedObject = null;
        grabbedRigidbody = null;
        currentWeight = 0f;
        slipAccumulation = 0f;
        throwChargeTime = 0f;
        currentThrowForce = 0f;
        isChargingThrow = false;
        
        // Синхронизируем через сервер
        if (isServer)
        {
            grabbedObjectNetId = 0;
        }
        else if (isOwned)
        {
            ReleaseObjectCommand();
        }
        
        // Скрываем UI и линию
        HideThrowUI();
        HideGrabLineRenderer();
        
        // Сбрасываем состояние наведения
        if (currentLookingAt != null)
        {
            currentLookingAt.SetPlayerLookingAt(false);
            currentLookingAt = null;
        }
    }
    
    void MoveGrabbedObject()
    {
        if (grabbedRigidbody == null || currentGrabbedObject == null) 
        {
            // Объект был уничтожен - освобождаем его
            if (grabbedObjectNetId != 0)
            {
                ForceReleaseObject();
            }
            return;
        }
        
        // Дополнительная проверка: объект может быть уничтожен
        if (currentGrabbedObject.gameObject == null || grabbedRigidbody.gameObject == null)
        {
            ForceReleaseObject();
            return;
        }
        
        // Целевая позиция - точка удержания + дополнительное расстояние вперед
        Vector3 targetPosition = holdPoint.position + holdPoint.forward * objectDistance;
        
        // Вычисляем мировую позицию точки захвата на объекте
        Vector3 grabPointWorld = grabbedRigidbody.transform.TransformPoint(grabLocalOffset);
        
        // Применяем скольжение в зависимости от веса
        float weightFactor = Mathf.Clamp01(currentWeight / maxComfortableWeight);
        float slipOffset = slipAccumulation * weightFactor;
        
        // Добавляем случайное дрожание для тяжелых объектов
        if (currentWeight > maxComfortableWeight)
        {
            Vector3 shake = new Vector3(
                Mathf.PerlinNoise(Time.time * 2f, 0f) - 0.5f,
                Mathf.PerlinNoise(Time.time * 2f, 1f) - 0.5f,
                Mathf.PerlinNoise(Time.time * 2f, 2f) - 0.5f
            ) * weightFactor * 0.1f;
            targetPosition += shake;
        }
        
        // Вычисляем силу притяжения к точке удержания
        Vector3 directionToTarget = targetPosition - grabPointWorld;
        float distanceToTarget = directionToTarget.magnitude;
        
        // Адаптивная сила в зависимости от расстояния, веса и силы хвата
        float strengthFactor = 1f / currentStrength; // Чем больше сила, тем меньше штраф
        float adaptiveForce = grabForce * currentStrength / (1f + weightFactor * strengthFactor);
        Vector3 force = directionToTarget.normalized * adaptiveForce * distanceToTarget;
        
        // Добавляем компенсацию гравитации (чтобы рука "держала" объект)
        // Не полная компенсация - для тяжелых объектов оставляем часть гравитации
        float gravityCompensation = Mathf.Clamp01(1f - weightFactor * 0.5f);
        force += Vector3.up * (Physics.gravity.magnitude * grabbedRigidbody.mass * gravityCompensation);
        
        // Применяем демпфирование к линейной скорости
        Vector3 dampingVelocity = -grabbedRigidbody.linearVelocity * dampingForce;
        
        // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Применяем силу В ТОЧКЕ ЗАХВАТА, а не в центре масс
        // Это создаст реалистичный момент вращения
        grabbedRigidbody.AddForceAtPosition(force + dampingVelocity, grabPointWorld);
        
        // Ограничиваем скорость
        if (grabbedRigidbody.linearVelocity.magnitude > maxGrabVelocity)
        {
            grabbedRigidbody.linearVelocity = grabbedRigidbody.linearVelocity.normalized * maxGrabVelocity;
        }
        
        // Применяем демпфирование к угловой скорости для стабилизации
        // Но НЕ принудительно выравниваем вращение
        grabbedRigidbody.angularVelocity *= (1f - Time.fixedDeltaTime * rotationSpeed * 0.5f);
        
        // Опционально: слабая стабилизация вращения только при удержании колесика мыши
        // Это позволит игроку стабилизировать объект при необходимости
        if (Input.GetKey(KeyCode.LeftShift))
        {
            Quaternion targetRotation = holdPoint.rotation;
            Quaternion deltaRotation = targetRotation * Quaternion.Inverse(grabbedRigidbody.rotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            
            if (angle > 180f) angle -= 360f;
            
            Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * rotationSpeed * 10f / (1f + weightFactor));
            grabbedRigidbody.AddTorque(torque, ForceMode.VelocityChange);
        }
    }
    
    void HandleWeightAndSlipping()
    {
        // Обрабатываем скольжение только для владельца
        if (!isOwned) return;
        
        // Проверяем, что объект еще существует
        if (currentGrabbedObject == null || (currentGrabbedObject.gameObject == null))
        {
            ForceReleaseObject();
            return;
        }
        
        // Вычисляем скорость движения игрока
        Vector3 currentPlayerPosition = transform.root.position;
        float playerMovementSpeed = (currentPlayerPosition - lastPlayerPosition).magnitude / Time.deltaTime;
        lastPlayerPosition = currentPlayerPosition;
        
        // Вычисляем фактор скольжения с учетом силы хвата
        float weightFactor = Mathf.Clamp01(currentWeight / (maxComfortableWeight * currentStrength));
        float movementFactor = playerMovementSpeed * movementSlipMultiplier;
        
        // Накапливаем скольжение (уменьшаем скольжение при большей силе)
        float strengthSlipReduction = 1f / currentStrength;
        float newSlipAccumulation = slipAccumulation + (weightFactor * weightSlipFactor * strengthSlipReduction + movementFactor * weightSlipFactor) * Time.deltaTime;
        
        // Синхронизируем скольжение через сервер
        if (isServer)
        {
            slipAccumulation = newSlipAccumulation;
        }
        else
        {
            SetSlipAccumulationCommand(newSlipAccumulation);
        }
        
        // Если предмет слишком тяжелый или игрок двигается слишком быстро - роняем
        float effectiveDropThreshold = dropWeightThreshold * currentStrength * 0.8f;
        if (slipAccumulation > 1f || currentWeight > effectiveDropThreshold && playerMovementSpeed > 5f)
        {
            ReleaseObject();
            return;
        }
        
        // Постепенно уменьшаем скольжение если игрок стоит на месте с легким предметом
        if (playerMovementSpeed < 0.1f && currentWeight < maxComfortableWeight)
        {
            float reducedSlip = Mathf.Max(0f, slipAccumulation - Time.deltaTime * 0.5f);
            if (isServer)
            {
                slipAccumulation = reducedSlip;
            }
            else
            {
                SetSlipAccumulationCommand(reducedSlip);
            }
        }
        
        // Проверяем расстояние до целевой точки (holdPoint + objectDistance)
        if (grabbedRigidbody != null)
        {
            Vector3 targetPoint = holdPoint.position + holdPoint.forward * objectDistance;
            // Используем точку захвата вместо центра объекта
            Vector3 grabPointWorld = grabbedRigidbody.transform.TransformPoint(grabLocalOffset);
            float distanceFromTarget = Vector3.Distance(grabPointWorld, targetPoint);
            
            // Если предмет слишком далеко - роняем
            if (distanceFromTarget > (holdDistance + objectDistance) * 1.5f)
            {
                ReleaseObject();
            }
        }
    }
    
    /// <summary>
    /// Command для установки скольжения (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetSlipAccumulationCommand(float slip)
    {
        slipAccumulation = slip;
    }
    
    void HighlightObject(DestructibleObject grabbable)
    {
        Renderer renderer = grabbable.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            highlightedMaterial = renderer.material;
            
            // Сохраняем оригинальные настройки emission
            wasEmissionEnabled = highlightedMaterial.IsKeywordEnabled("_EMISSION");
            if (wasEmissionEnabled)
            {
                originalEmissionColor = highlightedMaterial.GetColor("_EmissionColor");
            }
            
            // Включаем emission для подсветки
            highlightedMaterial.EnableKeyword("_EMISSION");
            highlightedMaterial.SetColor("_EmissionColor", highlightColor * highlightIntensity);
        }
    }
    
    void RemoveHighlight(DestructibleObject grabbable)
    {
        if (highlightedMaterial != null)
        {
            if (wasEmissionEnabled)
            {
                highlightedMaterial.SetColor("_EmissionColor", originalEmissionColor);
            }
            else
            {
                highlightedMaterial.DisableKeyword("_EMISSION");
                highlightedMaterial.SetColor("_EmissionColor", Color.black);
            }
            
            highlightedMaterial = null;
        }
    }
    
    void OnDrawGizmos()
    {
        if (Application.isPlaying && currentGrabbedObject != null)
        {
            // Вычисляем позицию точки захвата
            Vector3 grabPointWorld = grabbedRigidbody.transform.TransformPoint(grabLocalOffset);
            
            // Рисуем линию от камеры до точки захвата
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, grabPointWorld);
            
            // Рисуем точку удержания (holdPoint)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(holdPoint.position, 0.08f);
            
            // Рисуем целевую точку объекта (holdPoint + objectDistance)
            Vector3 targetPoint = holdPoint.position + holdPoint.forward * objectDistance;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPoint, 0.12f);
            Gizmos.DrawLine(holdPoint.position, targetPoint);
            
            // Показываем уровень скольжения на точке захвата
            Gizmos.color = Color.Lerp(Color.green, Color.red, slipAccumulation);
            Gizmos.DrawWireSphere(grabPointWorld, 0.2f);
            
            // Рисуем маленькую сферу на точке захвата
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(grabPointWorld, 0.05f);
            
            // Линия от центра объекта до точки захвата
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(grabbedRigidbody.position, grabPointWorld);
        }
        
        // Показываем дальность захвата
        if (playerCamera == null)
        {
            playerCamera = GetComponent<Camera>();
            if (playerCamera == null) return;
        }
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * grabDistance);
    }
    
    /// <summary>
    /// Инициализирует UI для системы броска
    /// </summary>
    void InitializeThrowUI()
    {
        if (throwUIObject != null)
        {
            originalThrowUIPosition = throwUIObject.transform.localPosition;
            throwUIObject.SetActive(false);
        }
        
        if (throwForceText != null)
        {
            originalThrowTextColor = throwForceText.color;
        }
    }
    
    /// <summary>
    /// Показывает UI для броска
    /// </summary>
    void ShowThrowUI()
    {
        if (throwUIObject != null)
        {
            throwUIObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Скрывает UI для броска
    /// </summary>
    void HideThrowUI()
    {
        if (throwUIObject != null)
        {
            throwUIObject.SetActive(false);
        }
        
        // Сбрасываем UI в исходное состояние
        if (throwForceText != null)
        {
            throwForceText.color = originalThrowTextColor;
        }
        
        if (throwUIObject != null)
        {
            throwUIObject.transform.localPosition = originalThrowUIPosition;
        }
    }
    
    /// <summary>
    /// Обрабатывает систему броска
    /// </summary>
    void HandleThrowSystem()
    {
        if (currentGrabbedObject == null) return;
        
        // Начинаем или отменяем зарядку броска при нажатии G
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (isChargingThrow)
            {
                // Отменяем зарядку если уже заряжаем
                if (isServer)
                {
                    isChargingThrow = false;
                    currentThrowForce = 0f;
                }
                else
                {
                    SetChargingThrowCommand(false);
                    SetThrowForceCommand(0f);
                }
                throwChargeTime = 0f;
                UpdateThrowUI();
            }
            else
            {
                // Проверяем достаточно ли стамины для начала зарядки
                float minStaminaCost = staminaCostPerThrowForce * 1f;
                if (playerHealthStamina != null && playerHealthStamina.HasEnoughStamina(minStaminaCost))
                {
                    // Начинаем зарядку
                    if (isServer)
                    {
                        isChargingThrow = true;
                        currentThrowForce = 0f;
                    }
                    else
                    {
                        SetChargingThrowCommand(true);
                        SetThrowForceCommand(0f);
                    }
                    throwChargeTime = 0f;
                }
            }
        }
        
        // Заряжаем бросок при удержании G
        if (Input.GetKey(KeyCode.G) && isChargingThrow)
        {
            float newThrowForce = Mathf.Clamp(throwChargeTime * throwChargeSpeed, 0f, maxThrowForce);
            float staminaCost = (newThrowForce - currentThrowForce) * staminaCostPerThrowForce;
            
            if (playerHealthStamina != null && playerHealthStamina.HasEnoughStamina(staminaCost))
            {
                throwChargeTime += Time.deltaTime;
                
                // Синхронизируем силу броска через сервер
                if (isServer)
                {
                    currentThrowForce = newThrowForce;
                }
                else
                {
                    SetThrowForceCommand(newThrowForce);
                }
                
                playerHealthStamina.UseStamina(staminaCost);
                
                // Обновляем UI
                UpdateThrowUI();
            }
            else
            {
                // Недостаточно стамины - останавливаем зарядку
                if (isServer)
                {
                    isChargingThrow = false;
                    currentThrowForce = 0f;
                }
                else
                {
                    SetChargingThrowCommand(false);
                    SetThrowForceCommand(0f);
                }
                throwChargeTime = 0f;
                UpdateThrowUI();
            }
        }
        
        // НЕ сбрасываем зарядку при отпускании G - зарядка сохраняется до отпускания ЛКМ
        // Зарядка сбрасывается только при отпускании ЛКМ в методе ReleaseObject()
    }
    
    /// <summary>
    /// Command для установки силы броска (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetThrowForceCommand(float force)
    {
        currentThrowForce = force;
    }
    
    /// <summary>
    /// Command для установки состояния зарядки броска (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetChargingThrowCommand(bool charging)
    {
        isChargingThrow = charging;
    }
    
    /// <summary>
    /// Обновляет UI броска
    /// </summary>
    void UpdateThrowUI()
    {
        if (throwUIObject == null) return;
        
        // Обновляем текст силы броска
        if (throwForceText != null)
        {
            if (isChargingThrow)
            {
                
                // Меняем цвет на красный при долгом удержании
                if (throwChargeTime > throwUIStartShakeTime)
                {
                    throwForceText.color = Color.red;
                }
                else
                {
                    float redIntensity = throwChargeTime / throwUIStartShakeTime;
                    throwForceText.color = Color.Lerp(originalThrowTextColor, Color.red, redIntensity);
                }
            }
            else
            {
                throwForceText.color = originalThrowTextColor;
            }
        }
        
        // Обновляем картинку силы броска
        if (throwForceImage != null)
        {
            float fillAmount = currentThrowForce / maxThrowForce;
            throwForceImage.fillAmount = fillAmount;
        }
        
        // Добавляем тряску при долгом удержании
        if (throwChargeTime > throwUIStartShakeTime)
        {
            Vector3 shakeOffset = new Vector3(
                Random.Range(-throwUIShakeIntensity, throwUIShakeIntensity),
                Random.Range(-throwUIShakeIntensity, throwUIShakeIntensity),
                0f
            ) * 0.1f;
            throwUIObject.transform.localPosition = originalThrowUIPosition + shakeOffset;
        }
        else
        {
            throwUIObject.transform.localPosition = originalThrowUIPosition;
        }
    }
    
    /// <summary>
    /// Бросает объект с накопленной силой
    /// </summary>
    void ThrowObject()
    {
        if (grabbedRigidbody == null || currentThrowForce <= 0) return;
        
        // Вычисляем направление броска (вперед от камеры)
        Vector3 throwDirection = playerCamera.transform.forward;
        
        // Применяем силу броска
        Vector3 throwForce = throwDirection * currentThrowForce;
        grabbedRigidbody.AddForce(throwForce, ForceMode.Impulse);
        
        // Добавляем небольшой случайный момент вращения для реалистичности
        Vector3 randomTorque = new Vector3(
            Random.Range(-5f, 5f),
            Random.Range(-5f, 5f),
            Random.Range(-5f, 5f)
        );
        grabbedRigidbody.AddTorque(randomTorque, ForceMode.Impulse);
        
        // Тратим финальную стамину за бросок
        if (playerHealthStamina != null)
        {
            float finalStaminaCost = currentThrowForce * staminaCostPerThrowForce * 0.1f;
            playerHealthStamina.UseStamina(finalStaminaCost);
        }
    }
    
    // Публичные методы для получения информации о состоянии
    public bool IsHoldingObject() 
    {
        // Проверяем, что объект действительно существует
        if (currentGrabbedObject != null)
        {
            // Проверяем, что GameObject еще существует
            if (currentGrabbedObject.gameObject == null)
            {
                // Объект был уничтожен - сбрасываем состояние
                ForceReleaseObject();
                return false;
            }
            
            // Объект существует и захвачен
            return true;
        }
        
        // Если currentGrabbedObject null, но netId не 0 - объект мог быть уничтожен
        if (grabbedObjectNetId != 0)
        {
            ForceReleaseObject();
            return false;
        }
        
        return false;
    }
    public float GetCurrentWeight() => currentWeight;
    public float GetSlipAmount() => slipAccumulation;
    public DestructibleObject GetCurrentObject() => currentGrabbedObject;
    
    // Методы для системы броска
    public bool IsChargingThrow() => isChargingThrow;
    public float GetCurrentThrowForce() => currentThrowForce;
    public float GetThrowChargeProgress() => currentThrowForce / maxThrowForce;
    
    // Методы для системы силы
    public void AddStrengthBonus(float bonus)
    {
        float newStrength = currentStrength + bonus * strengthMultiplier;
        if (isServer)
        {
            currentStrength = newStrength;
        }
        else if (isOwned)
        {
            SetStrengthCommand(newStrength);
        }
    }
    
    public void RemoveStrengthBonus(float bonus)
    {
        float newStrength = Mathf.Max(baseStrength, currentStrength - bonus * strengthMultiplier);
        if (isServer)
        {
            currentStrength = newStrength;
        }
        else if (isOwned)
        {
            SetStrengthCommand(newStrength);
        }
    }
    
    public void ResetStrength()
    {
        if (isServer)
        {
            currentStrength = baseStrength;
        }
        else if (isOwned)
        {
            SetStrengthCommand(baseStrength);
        }
    }
    
    /// <summary>
    /// Command для установки силы (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetStrengthCommand(float strength)
    {
        currentStrength = strength;
    }
    
    public float GetCurrentStrength() => currentStrength;
    public float GetBaseStrength() => baseStrength;
    
    /// <summary>
    /// Создает LineRenderer для визуализации удержания предмета
    /// </summary>
    void CreateGrabLineRenderer()
    {
        GameObject lineObj = new GameObject("GrabLineRenderer");
        lineObj.transform.parent = transform;
        
        grabLineRenderer = lineObj.AddComponent<LineRenderer>();
        grabLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        grabLineRenderer.startColor = Color.cyan;
        grabLineRenderer.endColor = Color.cyan;
        grabLineRenderer.startWidth = 0.1f;
        grabLineRenderer.endWidth = 0.06f;
        grabLineRenderer.positionCount = lineSegments + 1; // +1 для плавности
        grabLineRenderer.useWorldSpace = true;
        grabLineRenderer.enabled = false;
    }
    
    /// <summary>
    /// Обновляет позиции LineRenderer для визуализации удержания
    /// </summary>
    void UpdateGrabLineRenderer()
    {
        if (grabLineRenderer == null || currentGrabbedObject == null) return;
        
        // Показываем линию только когда держим предмет
        grabLineRenderer.enabled = true;
        
        // Определяем начальную точку линии
        Vector3 startPos = (lineStartPoint != null) ? lineStartPoint.position : holdPoint.position;
        
        // Позиция конца линии - точка захвата на объекте
        Vector3 grabPointWorld = grabbedRigidbody.transform.TransformPoint(grabLocalOffset);
        
        // Создаем анимированную линию с волнами
        CreateAnimatedLine(startPos, grabPointWorld);
        
        // Меняем цвет в зависимости от скольжения
        Color lineColor = Color.Lerp(Color.cyan, Color.red, slipAccumulation);
        grabLineRenderer.startColor = lineColor;
        grabLineRenderer.endColor = lineColor;
    }
    
    /// <summary>
    /// Создает анимированную линию с волнами
    /// </summary>
    void CreateAnimatedLine(Vector3 startPos, Vector3 endPos)
    {
        Vector3[] linePoints = new Vector3[lineSegments + 1];
        
        // Вычисляем направление и расстояние
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;
        Vector3 normalizedDirection = direction.normalized;
        
        // Вычисляем перпендикуляр для волн
        Vector3 perpendicular = Vector3.Cross(normalizedDirection, Vector3.up).normalized;
        if (perpendicular.magnitude < 0.1f)
        {
            perpendicular = Vector3.Cross(normalizedDirection, Vector3.right).normalized;
        }
        
        // Интенсивность волн зависит от скольжения
        float waveIntensity = slipAccumulation;
        float currentAmplitude = waveAmplitude * waveIntensity;
        float currentFrequency = waveFrequency * (1f + waveIntensity);
        
        for (int i = 0; i <= lineSegments; i++)
        {
            float t = (float)i / lineSegments;
            Vector3 basePosition = Vector3.Lerp(startPos, endPos, t);
            
            // Добавляем волны только если есть скольжение
            if (waveIntensity > 0.01f)
            {
                float waveOffset = Mathf.Sin(t * currentFrequency * Mathf.PI + Time.time * 3f) * currentAmplitude;
                basePosition += perpendicular * waveOffset;
            }
            
            linePoints[i] = basePosition;
        }
        
        // Устанавливаем все точки линии
        grabLineRenderer.SetPositions(linePoints);
    }
    
    /// <summary>
    /// Скрывает LineRenderer когда предмет не захвачен
    /// </summary>
    void HideGrabLineRenderer()
    {
        if (grabLineRenderer != null)
        {
            grabLineRenderer.enabled = false;
        }
    }
}

