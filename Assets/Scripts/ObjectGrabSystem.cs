using UnityEngine;

public class ObjectGrabSystem : MonoBehaviour
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
    
    [Header("Mouse Sensitivity Adjustment")]
    [SerializeField] private float weightSensitivityReduction = 0.5f;
    [SerializeField] private MouseLook mouseLook;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float highlightIntensity = 1.5f;
    
    [Header("Throw System")]
    [SerializeField] private float maxThrowForce = 40f;
    [SerializeField] private float throwChargeSpeed = 2f;
    [SerializeField] private float throwUIStartShakeTime = 3f;
    [SerializeField] private float throwUIShakeIntensity = 10f;
    [SerializeField] private GameObject throwUIObject; // UI объект с картинкой и текстом
    [SerializeField] private UnityEngine.UI.Image throwForceImage; // Картинка силы броска
    [SerializeField] private UnityEngine.UI.Text throwForceText; // Текст силы броска
    
    private DestructibleObject currentGrabbedObject;
    private DestructibleObject currentLookingAt;
    private Camera playerCamera;
    private Rigidbody grabbedRigidbody;
    private float currentWeight;
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
    
    // Для системы броска
    private float currentThrowForce = 0f;
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
        
        // Инициализируем UI для броска
        InitializeThrowUI();
    }
    
    void Update()
    {
        // Проверяем, на что смотрит игрок
        CheckForGrabbableObject();
        
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
        if (currentGrabbedObject != null && grabbedRigidbody != null)
        {
            MoveGrabbedObject();
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
        // Проверяем, можно ли взять предмет
        if (grabbable.objectWeight > dropWeightThreshold)
        {
            Debug.Log($"Объект слишком тяжелый! Вес: {grabbable.objectWeight}kg");
            return;
        }
        
        currentGrabbedObject = grabbable;
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
    
    void ReleaseObject()
    {
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
            
            // Скрываем UI броска
            HideThrowUI();
            
            // Сбрасываем все переменные броска
            currentGrabbedObject = null;
            grabbedRigidbody = null;
            currentWeight = 0f;
            slipAccumulation = 0f;
            currentThrowForce = 0f;
            isChargingThrow = false;
            throwChargeTime = 0f;
            
            // Сбрасываем состояние наведения
            if (currentLookingAt != null)
            {
                currentLookingAt.SetPlayerLookingAt(false);
                currentLookingAt = null;
            }
        }
    }
    
    void MoveGrabbedObject()
    {
        if (grabbedRigidbody == null) return;
        
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
        
        // Адаптивная сила в зависимости от расстояния и веса
        float adaptiveForce = grabForce / (1f + weightFactor);
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
        // Вычисляем скорость движения игрока
        Vector3 currentPlayerPosition = transform.root.position;
        float playerMovementSpeed = (currentPlayerPosition - lastPlayerPosition).magnitude / Time.deltaTime;
        lastPlayerPosition = currentPlayerPosition;
        
        // Вычисляем фактор скольжения
        float weightFactor = Mathf.Clamp01(currentWeight / maxComfortableWeight);
        float movementFactor = playerMovementSpeed * movementSlipMultiplier;
        
        // Накапливаем скольжение
        slipAccumulation += (weightFactor * weightSlipFactor + movementFactor * weightSlipFactor) * Time.deltaTime;
        
        // Если предмет слишком тяжелый или игрок двигается слишком быстро - роняем
        if (slipAccumulation > 1f || currentWeight > dropWeightThreshold * 0.8f && playerMovementSpeed > 5f)
        {
            Debug.Log("Предмет выскользнул из рук!");
            ReleaseObject();
            return;
        }
        
        // Постепенно уменьшаем скольжение если игрок стоит на месте с легким предметом
        if (playerMovementSpeed < 0.1f && currentWeight < maxComfortableWeight)
        {
            slipAccumulation = Mathf.Max(0f, slipAccumulation - Time.deltaTime * 0.5f);
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
                Debug.Log("Предмет слишком далеко отклонился!");
                ReleaseObject();
            }
        }
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
                isChargingThrow = false;
                throwChargeTime = 0f;
                currentThrowForce = 0f;
                UpdateThrowUI();
                Debug.Log("Зарядка броска отменена");
            }
            else
            {
                // Начинаем зарядку
                isChargingThrow = true;
                throwChargeTime = 0f;
                currentThrowForce = 0f;
                Debug.Log("Начата зарядка броска");
            }
        }
        
        // Заряжаем бросок при удержании G
        if (Input.GetKey(KeyCode.G) && isChargingThrow)
        {
            throwChargeTime += Time.deltaTime;
            currentThrowForce = Mathf.Clamp(throwChargeTime * throwChargeSpeed, 0f, maxThrowForce);
            
            // Обновляем UI
            UpdateThrowUI();
        }
        
        // НЕ сбрасываем зарядку при отпускании G - зарядка сохраняется до отпускания ЛКМ
        // Зарядка сбрасывается только при отпускании ЛКМ в методе ReleaseObject()
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
        
        Debug.Log($"Брошен объект {currentGrabbedObject.name} с силой {currentThrowForce}");
    }
    
    // Публичные методы для получения информации о состоянии
    public bool IsHoldingObject() => currentGrabbedObject != null;
    public float GetCurrentWeight() => currentWeight;
    public float GetSlipAmount() => slipAccumulation;
    public DestructibleObject GetCurrentObject() => currentGrabbedObject;
    
    // Методы для системы броска
    public bool IsChargingThrow() => isChargingThrow;
    public float GetCurrentThrowForce() => currentThrowForce;
    public float GetThrowChargeProgress() => currentThrowForce / maxThrowForce;
}

