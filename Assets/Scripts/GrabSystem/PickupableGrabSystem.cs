using UnityEngine;

/// <summary>
/// Система захвата для PickupableItem объектов (без разрушения)
/// </summary>
public class PickupableGrabSystem : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float grabDistance = 3f;
    [SerializeField] private float holdDistance = 2f;
    [SerializeField] private float objectDistance = 0.5f;
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
    
    [Header("Visual Feedback")]
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float highlightIntensity = 1.5f;
    
    private PickupableItem currentGrabbedObject;
    private PickupableItem currentLookingAt;
    private Camera playerCamera;
    private Rigidbody grabbedRigidbody;
    private float currentWeight;
    private float slipAccumulation;
    private Vector3 lastPlayerPosition;
    
    // Для сохранения точки захвата
    private Vector3 grabLocalOffset;
    private RaycastHit currentHit;
    
    // Для визуального выделения
    private Material highlightedMaterial;
    private Color originalEmissionColor;
    private bool wasEmissionEnabled;
    
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
        
        // Вращение предмета колесиком мыши
        if (currentGrabbedObject != null && Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
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
        
        // Убираем подсветку с предыдущего объекта
        if (currentLookingAt != null && currentLookingAt != currentGrabbedObject)
        {
            RemoveHighlight(currentLookingAt);
            currentLookingAt = null;
        }
        
        if (Physics.Raycast(ray, out RaycastHit hit, grabDistance, grabbableLayer))
        {
            PickupableItem grabbable = hit.collider.GetComponent<PickupableItem>();
            if (grabbable != null && grabbable != currentGrabbedObject)
            {
                currentLookingAt = grabbable;
                currentHit = hit;
                HighlightObject(grabbable);
            }
        }
    }
    
    void TryGrabObject(PickupableItem grabbable)
    {
        // Проверяем, можно ли взять предмет
        if (grabbable.GetInventoryItem().weight > dropWeightThreshold)
        {
            return;
        }
        
        currentGrabbedObject = grabbable;
        grabbedRigidbody = grabbable.GetComponent<Rigidbody>();
        
        if (grabbedRigidbody != null)
        {
            // Вычисляем локальное смещение от центра объекта до точки захвата
            Vector3 grabPoint = currentHit.point;
            Vector3 objectCenter = grabbedRigidbody.position;
            
            grabLocalOffset = grabbedRigidbody.transform.InverseTransformPoint(grabPoint);
            
            // Настраиваем физику
            grabbedRigidbody.useGravity = true;
            grabbedRigidbody.linearDamping = 2f;
            grabbedRigidbody.angularDamping = 5f;
            
            currentWeight = grabbable.GetInventoryItem().weight;
            slipAccumulation = 0f;
            
            RemoveHighlight(grabbable);
        }
    }
    
    public void ReleaseObject()
    {
        if (currentGrabbedObject != null)
        {
            if (grabbedRigidbody != null)
            {
                // Восстанавливаем нормальную физику
                grabbedRigidbody.linearDamping = 0f;
                grabbedRigidbody.angularDamping = 0.05f;
                grabbedRigidbody.useGravity = true;
            }
            
            // Сбрасываем все переменные
            currentGrabbedObject = null;
            grabbedRigidbody = null;
            currentWeight = 0f;
            slipAccumulation = 0f;
            
            // Сбрасываем состояние наведения
            if (currentLookingAt != null)
            {
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
        
        // Добавляем компенсацию гравитации
        float gravityCompensation = Mathf.Clamp01(1f - weightFactor * 0.5f);
        force += Vector3.up * (Physics.gravity.magnitude * grabbedRigidbody.mass * gravityCompensation);
        
        // Применяем демпфирование к линейной скорости
        Vector3 dampingVelocity = -grabbedRigidbody.linearVelocity * dampingForce;
        
        // Применяем силу В ТОЧКЕ ЗАХВАТА
        grabbedRigidbody.AddForceAtPosition(force + dampingVelocity, grabPointWorld);
        
        // Ограничиваем скорость
        if (grabbedRigidbody.linearVelocity.magnitude > maxGrabVelocity)
        {
            grabbedRigidbody.linearVelocity = grabbedRigidbody.linearVelocity.normalized * maxGrabVelocity;
        }
        
        // Применяем демпфирование к угловой скорости
        grabbedRigidbody.angularVelocity *= (1f - Time.fixedDeltaTime * rotationSpeed * 0.5f);
        
        // Стабилизация вращения при удержании Shift
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
            ReleaseObject();
            return;
        }
        
        // Постепенно уменьшаем скольжение если игрок стоит на месте с легким предметом
        if (playerMovementSpeed < 0.1f && currentWeight < maxComfortableWeight)
        {
            slipAccumulation = Mathf.Max(0f, slipAccumulation - Time.deltaTime * 0.5f);
        }
        
        // Проверяем расстояние до целевой точки
        if (grabbedRigidbody != null)
        {
            Vector3 targetPoint = holdPoint.position + holdPoint.forward * objectDistance;
            Vector3 grabPointWorld = grabbedRigidbody.transform.TransformPoint(grabLocalOffset);
            float distanceFromTarget = Vector3.Distance(grabPointWorld, targetPoint);
            
            // Если предмет слишком далеко - роняем
            if (distanceFromTarget > (holdDistance + objectDistance) * 1.5f)
            {
                ReleaseObject();
            }
        }
    }
    
    void HighlightObject(PickupableItem grabbable)
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
    
    void RemoveHighlight(PickupableItem grabbable)
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
    
    // Публичные методы для получения информации о состоянии
    public bool IsHoldingObject() => currentGrabbedObject != null;
    public float GetCurrentWeight() => currentWeight;
    public float GetSlipAmount() => slipAccumulation;
    public PickupableItem GetCurrentObject() => currentGrabbedObject;
}
