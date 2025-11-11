using UnityEngine;
//using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : /*NetworkBehaviour*/ MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float proneSpeed = 1f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    
    [Header("Stance Settings")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float proneHeight = 0.0000001f;
    [SerializeField] private float heightChangeSpeed = 10f;
    
    [Header("Character Controller Radius")]
    [SerializeField] private float standingCrouchRadius = 0.3f;
    [SerializeField] private float proneRadius = 0.1f;
    [SerializeField] private float radiusChangeSpeed = 10f;
    
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask = -1; // -1 означает все слои
    
    [Header("Stamina Settings")]
    [SerializeField] private float runStaminaCost = 5f;
    [SerializeField] private PlayerHealthStamina playerHealthStamina;
    
    [Header("Grab System References")]
    [SerializeField] private ObjectGrabSystem objectGrabSystem;
    [SerializeField] private PickupableGrabSystem pickupableGrabSystem;
    
    [Header("Body Parts")]
    [Tooltip("Объект головы")]
    [SerializeField] private GameObject headObject;
    [Tooltip("Объект тела")]
    [SerializeField] private GameObject bodyObject;
    [Tooltip("Объекты, которые показываются когда игрок лежит")]
    [SerializeField] private GameObject[] proneObjects = new GameObject[2];
    [Tooltip("Объект, позиция которого меняется в зависимости от стойки")]
    [SerializeField] private Transform stancePositionObject;
    
    [Header("Leg/Tentacle Settings")]
	[Tooltip("4 Transform, от которых начинается каждая 'нога' (LineRenderer)")]
	[SerializeField] private Transform[] legAnchors = new Transform[4];
	[Tooltip("4 Transform, в которых заканчивается каждая 'нога'")]
	[SerializeField] private Transform[] legFootTargets = new Transform[4];
    [Tooltip("Количество точек на одной ноге")]
    [SerializeField] private int pointsPerLeg = 8;
	[Tooltip("Толщина линии для ног")]
	[SerializeField] private float legLineWidth = 0.03f;
    [Tooltip("Горизонтальная длина шага вперед/назад")]
    [SerializeField] private float legStepForward = 0.35f;
    [Tooltip("Боковое покачивание ноги")]
    [SerializeField] private float legSway = 0.2f;
    [Tooltip("Вертикальный подъем шага")]
    [SerializeField] private float stepAmplitude = 0.15f;
    [Tooltip("Частота шага")]
    [SerializeField] private float waveFrequency = 3f;
	[Tooltip("Ускорение анимации ног при беге")]
	[SerializeField] private float runAnimSpeedMultiplier = 1.8f;
    
    [Header("Model Settings")]
    [Tooltip("Визуальная модель игрока (Transform модели)")]
    [SerializeField] private Transform playerModel;
    
    [Header("Model Y Offsets")]
    [SerializeField] private float standingModelY = -1.0802f;
    [SerializeField] private float crouchingModelY = -0.386f;
    [SerializeField] private float proneModelY = -0.59f;
    [SerializeField] private float modelOffsetSpeed = 10f;
    
    [Header("Camera Settings")]
    [Tooltip("Камера игрока (если не указана, будет найдена автоматически)")]
    [SerializeField] private Transform playerCamera;
    
    [Header("Camera Positions")]
    [SerializeField] private Vector3 standingCameraPosition = new Vector3(0.061999999f, 0.781000018f, 0.504999995f);
    [SerializeField] private Vector3 crouchingCameraPosition = new Vector3(0.061999999f, 0.781000018f, 0.504999995f);
    [SerializeField] private Vector3 proneCameraPosition = new Vector3(0.0489999987f, -0.266000003f, 1.36500001f);
    [SerializeField] private float cameraPositionSpeed = 10f;
    
    [Header("Camera Near Clipping Plane")]
    [SerializeField] private float standingCameraNear = 0.25f;
    [SerializeField] private float crouchingCameraNear = 0.09f;
    [SerializeField] private float proneCameraNear = 0.01f;
    [SerializeField] private float cameraNearChangeSpeed = 10f;
    
    private CharacterController controller;
    private Camera cameraComponent;
    private Vector3 velocity;
    private bool isGrounded;
    private float legAnimTime = 0f;
	private LineRenderer[] legRenderers;
	private Material legMaterial;
    
    private enum PlayerStance
    {
        Standing,
        Crouching,
        Prone
    }
    
    private PlayerStance currentStance = PlayerStance.Standing;
    private float targetHeight;
    private bool ctrlPressedLastFrame = false;
    private bool zPressedLastFrame = false;
    
    /// <summary>
    /// Проверяет, может ли игрок открыть меню (стоя или сидя)
    /// </summary>
    public bool CanOpenMenu()
    {
        return currentStance != PlayerStance.Prone;
    }
    
    /// <summary>
    /// Возвращает текущую стойку игрока
    /// </summary>
    public bool IsProne()
    {
        return currentStance == PlayerStance.Prone;
    }
    
    /*public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;
        
        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.parent = transform;
            groundCheckObj.transform.localPosition = new Vector3(0, -controller.height / 2, 0);
            groundCheck = groundCheckObj.transform;
        }

        if (!IsOwner)
        {
        }
    }*/
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;
        controller.radius = standingCrouchRadius;
        targetHeight = standingHeight;
        
        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.parent = transform;
            groundCheckObj.transform.localPosition = new Vector3(0, -controller.height / 2, 0);
            groundCheck = groundCheckObj.transform;
        }
        
        if (playerHealthStamina == null)
        {
            playerHealthStamina = GetComponent<PlayerHealthStamina>();
        }
        
        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                playerCamera = cam.transform;
                cameraComponent = cam;
            }
        }
        else
        {
            cameraComponent = playerCamera.GetComponent<Camera>();
        }
        
        // Находим системы захвата если не назначены
        if (objectGrabSystem == null)
        {
            objectGrabSystem = GetComponent<ObjectGrabSystem>();
        }
        
        if (pickupableGrabSystem == null)
        {
            pickupableGrabSystem = GetComponent<PickupableGrabSystem>();
        }
        
        if (playerModel != null)
        {
            Vector3 modelPosition = playerModel.localPosition;
            modelPosition.y = standingModelY;
            playerModel.localPosition = modelPosition;
        }
        
        if (playerCamera != null)
        {
            playerCamera.localPosition = standingCameraPosition;
        }
        
        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = standingCameraNear;
        }
        
		// Подготовить дефолтный материал для LineRenderer (чтобы не было фиолетового цвета при отсутствии материала)
		Shader legShader = Shader.Find("Sprites/Default");
		if (legShader == null)
		{
			legShader = Shader.Find("Unlit/Color");
		}
		if (legShader != null)
		{
			legMaterial = new Material(legShader);
			legMaterial.color = Color.black;
		}

		// Создать/настроить LineRenderer для каждой опоры ноги
		if (legAnchors != null && legAnchors.Length > 0)
		{
			legRenderers = new LineRenderer[legAnchors.Length];
			for (int i = 0; i < legAnchors.Length; i++)
			{
				var anchor = legAnchors[i];
				if (anchor == null) continue;

				LineRenderer lr = anchor.GetComponent<LineRenderer>();
				if (lr == null) lr = anchor.gameObject.AddComponent<LineRenderer>();

				lr.useWorldSpace = true;
				lr.positionCount = Mathf.Max(2, pointsPerLeg);
				lr.startColor = Color.black;
				lr.endColor = Color.black;
				if (legMaterial != null)
				{
					lr.material = legMaterial;
				}
				// Толщина линии
				lr.widthMultiplier = Mathf.Max(0.001f, legLineWidth);
				lr.startWidth = lr.widthMultiplier;
				lr.endWidth = lr.widthMultiplier;
				legRenderers[i] = lr;
			}
		}
    }
    
    void Update()
    {
        // Обрабатываем ввод только для владельца (закомментировано для одиночной игры)
        //if (!IsOwner) return;
        
        HandleStanceInput();
        HandleStanceChange();
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        
		// Обновляем тентакли-ноги
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        float inputMagnitude = new Vector2(horizontal, vertical).magnitude;
		bool isRunningAnim = Input.GetKey(KeyCode.LeftShift) && currentStance == PlayerStance.Standing && inputMagnitude > 0.1f;
		UpdateLegs(inputMagnitude, isRunningAnim);
        
        controller.Move(velocity * Time.deltaTime);
    }
    
    void HandleGroundCheck()
    {
        // Проверяем землю с учетом слоев, но по умолчанию все слои включены
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        // Дополнительная проверка: если игрок застрял под землей, поднимаем его
        if (isGrounded && groundCheck.position.y < transform.position.y - controller.height / 2)
        {
            // Поднимаем игрока так, чтобы его нижняя часть была на уровне земли
            float groundLevel = groundCheck.position.y + groundDistance;
            float playerBottom = transform.position.y - controller.height / 2;
            float adjustment = groundLevel - playerBottom;
            
            if (adjustment > 0.01f)
            {
                transform.position += Vector3.up * adjustment;
            }
        }
    }
    
    
    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        float currentSpeed = GetCurrentSpeed();
        Vector3 moveVelocity = move * currentSpeed;
        controller.Move(moveVelocity * Time.deltaTime);
        
        HandleRunStamina();
    }
    
    void HandleStanceInput()
    {
        bool ctrlPressed = Input.GetKey(KeyCode.LeftControl);
        bool zPressed = Input.GetKey(KeyCode.Z);
        
        if (ctrlPressed && !ctrlPressedLastFrame)
        {
            if (currentStance == PlayerStance.Crouching)
            {
                currentStance = PlayerStance.Standing;
            }
            else if (currentStance == PlayerStance.Standing)
            {
                currentStance = PlayerStance.Crouching;
            }
        }
        
        if (zPressed && !zPressedLastFrame)
        {
            if (currentStance == PlayerStance.Prone)
            {
                currentStance = PlayerStance.Standing;
            }
            else
            {
                currentStance = PlayerStance.Prone;
            }
        }
        
        ctrlPressedLastFrame = ctrlPressed;
        zPressedLastFrame = zPressed;
    }
    
    void HandleStanceChange()
    {
        float targetModelY = 0f;
        Vector3 targetCameraPosition = Vector3.zero;
        float targetCameraNear = 0.25f;
        float targetRadius = standingCrouchRadius;
        
        switch (currentStance)
        {
            case PlayerStance.Standing:
                targetHeight = standingHeight;
                targetModelY = standingModelY;
                targetCameraPosition = standingCameraPosition;
                targetCameraNear = standingCameraNear;
                targetRadius = standingCrouchRadius;
                break;
            case PlayerStance.Crouching:
                targetHeight = crouchHeight;
                targetModelY = crouchingModelY;
                targetCameraPosition = crouchingCameraPosition;
                targetCameraNear = crouchingCameraNear;
                targetRadius = standingCrouchRadius;
                break;
            case PlayerStance.Prone:
                targetHeight = proneHeight;
                targetModelY = proneModelY;
                targetCameraPosition = proneCameraPosition;
                targetCameraNear = proneCameraNear;
                targetRadius = proneRadius;
                break;
        }
        
        controller.height = Mathf.Lerp(controller.height, targetHeight, heightChangeSpeed * Time.deltaTime);
        controller.radius = Mathf.Lerp(controller.radius, targetRadius, radiusChangeSpeed * Time.deltaTime);
        
        if (groundCheck != null)
        {
            groundCheck.localPosition = new Vector3(0, -controller.height / 2, 0);
        }
        
        if (playerModel != null)
        {
            Vector3 modelPosition = playerModel.localPosition;
            modelPosition.y = Mathf.Lerp(modelPosition.y, targetModelY, modelOffsetSpeed * Time.deltaTime);
            playerModel.localPosition = modelPosition;
        }
        
        if (playerCamera != null)
        {
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetCameraPosition, cameraPositionSpeed * Time.deltaTime);
        }
        
        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = Mathf.Lerp(cameraComponent.nearClipPlane, targetCameraNear, cameraNearChangeSpeed * Time.deltaTime);
        }
        
        // Изменение позиции объекта в зависимости от стойки (моментально)
        if (stancePositionObject != null)
        {
            Vector3 targetPosition;
            if (currentStance == PlayerStance.Crouching)
            {
                // Сидя: 0.005 -0.384 0.178
                targetPosition = new Vector3(0.005f, -0.384f, 0.178f);
            }
            else
            {
                // Стоя (или лежа): 0.005 -0.092 0.178
                targetPosition = new Vector3(0.005f, -0.092f, 0.178f);
            }
            stancePositionObject.localPosition = targetPosition;
        }
        
        // Управление видимостью головы/тела/ног в зависимости от стойки
        UpdateVisibilityByStance();
    }
    
    float GetCurrentSpeed()
    {
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        bool hasMovement = Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;
        
        // Проверяем, держит ли игрок предмет в любой из систем захвата
        bool isHoldingObject = (objectGrabSystem != null && objectGrabSystem.IsHoldingObject()) ||
                              (pickupableGrabSystem != null && pickupableGrabSystem.IsHoldingObject());
        
        // В зависимости от стойки возвращаем разную скорость
        switch (currentStance)
        {
            case PlayerStance.Prone:
                return proneSpeed;
            
            case PlayerStance.Crouching:
                return crouchSpeed;
            
            case PlayerStance.Standing:
                if (isHoldingObject)
                {
                    return walkSpeed;
                }
                
                if (isRunning && hasMovement && playerHealthStamina != null && playerHealthStamina.HasEnoughStamina(runStaminaCost * Time.deltaTime))
                {
                    return runSpeed;
                }
                
                return walkSpeed;
            
            default:
                return walkSpeed;
        }
    }
    
    void HandleRunStamina()
    {
        if (playerHealthStamina == null) return;
        
        // Энергия тратится только когда игрок стоит и бежит
        // В присяди или лежа ускорение не работает, поэтому энергия не должна тратиться
        if (currentStance != PlayerStance.Standing) return;
        
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        bool hasMovement = Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;
        
        if (isRunning && hasMovement)
        {
            playerHealthStamina.UseStamina(runStaminaCost * Time.deltaTime);
        }
    }
    
    void HandleJump()
    {
        // Можно прыгать только когда стоишь
        if (Input.GetButtonDown("Jump") && isGrounded && currentStance == PlayerStance.Standing)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
    
    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
    }
    
    void UpdateVisibilityByStance()
    {
        bool showHead = true;
        bool showBody = true;
        bool showLegs = true;
        bool isProne = currentStance == PlayerStance.Prone;
        
        switch (currentStance)
        {
            case PlayerStance.Prone:
                showHead = false;
                showBody = true;
                showLegs = false;
                break;
            case PlayerStance.Crouching:
                showHead = true;
                showBody = false;
                showLegs = false;
                break;
            case PlayerStance.Standing:
                showHead = true;
                showBody = true;
                showLegs = true;
                break;
        }
        
        if (headObject != null) headObject.SetActive(showHead);
        if (bodyObject != null) bodyObject.SetActive(showBody);
        SetLegsEnabled(showLegs);
        
        // Показываем/скрываем объекты для лежания
        if (proneObjects != null)
        {
            foreach (GameObject obj in proneObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(isProne);
                }
            }
        }
    }
    
    void SetLegsEnabled(bool enabled)
    {
		if (legRenderers == null) return;
		for (int i = 0; i < legRenderers.Length; i++)
        {
			if (legRenderers[i] == null) continue;
			legRenderers[i].enabled = enabled;
        }
    }
    
	void UpdateLegs(float inputMagnitude, bool isRunning)
    {
        // Ноги видны только когда мы стоим (или когда включены)
		if (legAnchors == null || legAnchors.Length == 0 || legFootTargets == null || legFootTargets.Length == 0) return;
        
        // Небольшая анимация даже без движения
        float movementFactor = Mathf.Clamp01(inputMagnitude);
		float freq = Mathf.Lerp(1.0f, waveFrequency, movementFactor);
		if (isRunning)
		{
			freq *= Mathf.Max(1f, runAnimSpeedMultiplier);
		}
        legAnimTime += Time.deltaTime * freq;
        
		int legCount = Mathf.Min(legAnchors.Length, legFootTargets.Length);
		for (int i = 0; i < legCount; i++)
        {
			Transform anchorTransform = legAnchors[i];
			Transform footTargetTransform = legFootTargets[i];
			if (anchorTransform == null) continue;
			if (footTargetTransform == null) continue;

			LineRenderer lr = (legRenderers != null && i < legRenderers.Length) ? legRenderers[i] : null;
			if (lr == null || !lr.enabled) continue;
            
            int count = Mathf.Max(2, pointsPerLeg);
            lr.positionCount = count;
            
			// Точка начала ноги — позиция указанного якоря
			Vector3 anchor = anchorTransform.position;
			// Базовая цель ступни — указанный таргет
			Vector3 baseFoot = footTargetTransform.position;
            
            // Фаза шага для конкретной ноги
            float phase = legAnimTime + i * Mathf.PI * 0.5f;
            
            // Направление движения
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            
            // Целевая точка ступни с шагом и покачиванием
			Vector3 stepOffset = forward * (Mathf.Sin(phase) * legStepForward * (0.5f + 0.5f * movementFactor));
			Vector3 swayOffset = right * (Mathf.Cos(phase) * legSway * (0.5f + 0.5f * movementFactor));
			float lift = Mathf.Abs(Mathf.Sin(phase)) * stepAmplitude * (0.3f + 0.7f * movementFactor);
            
			Vector3 foot = baseFoot + stepOffset + swayOffset;
			foot.y = baseFoot.y + lift;
            
            // Квадратичная Безье: anchor -> control -> foot, control чуть выше середины
            Vector3 control = Vector3.Lerp(anchor, foot, 0.5f);
            control.y += stepAmplitude * (0.5f + 0.5f * movementFactor);
            
            // Семплируем кривую Безье
            for (int p = 0; p < count; p++)
            {
                float t = p / (float)(count - 1);
                Vector3 a = Vector3.Lerp(anchor, control, t);
                Vector3 b = Vector3.Lerp(control, foot, t);
                Vector3 point = Vector3.Lerp(a, b, t);
                lr.SetPosition(p, point);
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}

