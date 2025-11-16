using UnityEngine;
using TMPro;
using Mirror;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
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
    
    [Header("Death Settings")]
    [Tooltip("Префаб трупа игрока (должен иметь CorpseItem компонент)")]
    [SerializeField] private GameObject deathCorpsePrefab;
    
    [Tooltip("Название анимации смерти")]
    [SerializeField] private string deathAnimation = "Death";
    
    [Tooltip("Длительность анимации смерти (в секундах)")]
    [SerializeField] private float deathAnimationDuration = 2f;
    
    [Tooltip("Время появления Particle System во время анимации смерти (в секундах от начала)")]
    [SerializeField] private float deathParticleStartTime = 1.33f;
    
    [Tooltip("Particle System для эффекта смерти")]
    [SerializeField] private ParticleSystem deathParticleSystem;
    
    [Header("Grab System References")]
    [SerializeField] private ObjectGrabSystem objectGrabSystem;
    [SerializeField] private PickupableGrabSystem pickupableGrabSystem;
    
    [Header("Body Parts")]
    [Tooltip("Объект головы")]
    [SerializeField] private GameObject headObject;
    [Tooltip("Объект тела")]
    [SerializeField] private GameObject bodyObject;
    [Tooltip("Объект, позиция которого меняется в зависимости от стойки")]
    [SerializeField] private Transform stancePositionObject;
    
    [Header("3D Name Tag")]
    [Tooltip("TextMeshPro компонент для отображения никнейма и здоровья игрока")]
    [SerializeField] private TextMeshPro nameTagText;
    
    [Tooltip("TextMeshPro компонент для отображения никнейма игрока при взгляде других игроков (3D)")]
    [SerializeField] private TextMeshPro playerName3DText;
    
    [Tooltip("Название анимации показа никнейма (используется существующий animator)")]
    [SerializeField] private string playerNameShowAnimation = "Show";
    
    [Tooltip("Название анимации idle никнейма (пока смотрят, используется существующий animator)")]
    [SerializeField] private string playerNameIdleAnimation = "Idle";
    
    [Tooltip("Название анимации скрытия никнейма (используется существующий animator)")]
    [SerializeField] private string playerNameHideAnimation = "Hide";
    
    [Tooltip("Время показа анимации (в секундах)")]
    [SerializeField] private float playerNameShowDuration = 0.3f;
    
    [Tooltip("Время скрытия анимации (в секундах)")]
    [SerializeField] private float playerNameHideDuration = 0.3f;
    
    [Tooltip("Расстояние для определения взгляда на игрока")]
    [SerializeField] private float lookAtDistance = 10f;
    
    [Tooltip("Слой для определения взгляда (должен включать игроков)")]
    [SerializeField] private LayerMask playerLookAtLayer = -1;
    
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
	[SerializeField] public float runAnimSpeedMultiplier = 1.8f;
    
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
    
    [Header("Animation Settings")]
    [Tooltip("Animator компонент для проигрывания анимаций")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Время бездействия (в секундах) перед проигрыванием idlelong")]
    [SerializeField] private float idleLongDelay = 60f;
    
    [Tooltip("Интервал между проигрываниями idlelong (в секундах)")]
    [SerializeField] private float idleLongInterval = 60f;
    
    [Header("Grab Animation Settings")]
    [Tooltip("Название анимации начала захвата предмета")]
    [SerializeField] private string grabStartAnimation = "grabstart";
    
    [Tooltip("Название анимации удержания предмета (цикличная)")]
    [SerializeField] private string grabHoldAnimation = "grabhold";
    
    [Tooltip("Название анимации отпускания предмета")]
    [SerializeField] private string grabReleaseAnimation = "grabrelease";
    
    [Tooltip("Задержка перед переходом к анимации удержания (в секундах)")]
    [SerializeField] private float grabHoldDelay = 0.2f;
    
    [Tooltip("Задержка перед деактивацией анимации отпускания (в секундах)")]
    [SerializeField] private float grabReleaseDelay = 0.2f;
    
    [Tooltip("Минимальный множитель скорости анимации захвата (когда скольжения почти нет)")]
    [SerializeField] private float grabAnimationMinSpeed = 1f;
    
    [Tooltip("Максимальный множитель скорости анимации захвата (при максимальном скольжении)")]
    [SerializeField] private float grabAnimationMaxSpeed = 2f;
    
    private CharacterController controller;
    private Camera cameraComponent;
    private Vector3 velocity;
    private bool isGrounded;
    private float legAnimTime = 0f;
	private LineRenderer[] legRenderers;
	private Material legMaterial;
    private NetworkPlayer networkPlayer;
    
    private enum PlayerStance
    {
        Standing,
        Crouching,
        Prone
    }
    
    private PlayerStance currentStance = PlayerStance.Standing;
    private PlayerStance previousStance = PlayerStance.Standing;
    private float targetHeight;
    private bool ctrlPressedLastFrame = false;
    private bool zPressedLastFrame = false;
    
    // Переменные для отслеживания ввода и анимаций
    private float lastInputTime = 0f;
    private float lastIdleLongPlayTime = -1000f; // Время последнего проигрывания idlelong
    private bool isIdleLongPlaying = false;
    private Vector2 lastMousePosition = Vector2.zero;
    
    // Переменные для задержки скрытия bodyObject при приседании
    private float crouchTransitionStartTime = -1f;
    private const float crouchBodyHideDelay = 0.3f;
    
    // Переменные для задержки скрытия headObject при переходе в положение лежа
    private float proneTransitionStartTime = -1f;
    private const float proneHeadHideDelay = 0.5f;
    
    // Переменные для анимаций захвата предметов
    private bool wasHoldingObject = false;
    private bool isHoldingObject = false;
    private float grabStartTime = -1f;
    private float grabReleaseTime = -1f;
    private bool isGrabStartAnimationPlaying = false;
    private bool isGrabHoldAnimationActive = false;
    private bool isGrabReleaseAnimationPlaying = false;
    
    // Переменные для 3D никнейма при взгляде других игроков
    private bool isBeingLookedAt = false;
    private bool wasBeingLookedAt = false;
    private enum PlayerNameState
    {
        Hidden,
        Showing,
        Visible,
        Hiding
    }
    private PlayerNameState currentNameState = PlayerNameState.Hidden;
    private float nameAnimationStartTime = -1f;
    private Collider playerCollider;
    
    // Переменные для смерти
    private bool isDead = false;
    private bool wasDead = false;
    private bool isDeathAnimationPlaying = false;
    private float deathAnimationStartTime = -1f;
    private bool deathParticleShown = false;
    
    /// <summary>
    /// Проверяет, может ли игрок открыть меню (стоя или сидя)
    /// </summary>
    public bool CanOpenMenu()
    {
        return currentStance != PlayerStance.Prone;
    }
    
    /// <summary>
    /// Возвращает true, если игрок лежит
    /// </summary>
    public bool IsProne()
    {
        return currentStance == PlayerStance.Prone;
    }
    
    /// <summary>
    /// Возвращает true, если игрок стоит
    /// </summary>
    public bool IsStanding()
    {
        return currentStance == PlayerStance.Standing;
    }
    
    /// <summary>
    /// Возвращает true, если игрок сидит
    /// </summary>
    public bool IsCrouching()
    {
        return currentStance == PlayerStance.Crouching;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
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

        if (!isOwned)
        {
            // Отключаем камеру для других игроков
            if (playerCamera != null)
            {
                Camera cam = playerCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.enabled = false;
                }
            }
            // Управление отключится в Update через проверку IsOwner
        }
        else
        {
            // Включаем камеру для владельца
            if (playerCamera != null)
            {
                Camera cam = playerCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.enabled = true;
                }
            }
        }
        
        InitializeComponents();
    }
    
    void InitializeComponents()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }
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
        
        // Находим Animator если не назначен
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
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
        
        // Находим NetworkPlayer для получения никнейма
        if (networkPlayer == null)
        {
            networkPlayer = GetComponent<NetworkPlayer>();
            if (networkPlayer == null)
            {
                networkPlayer = GetComponentInParent<NetworkPlayer>();
            }
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
        
        // Инициализируем время последнего ввода
        lastInputTime = Time.time;
        lastMousePosition = Input.mousePosition;
        
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
		
		// Инициализируем коллайдер для определения взгляда других игроков
		if (playerCollider == null)
		{
			playerCollider = GetComponent<Collider>();
			if (playerCollider == null)
			{
				// Создаем CapsuleCollider если его нет
				CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
				capsuleCollider.height = standingHeight;
				capsuleCollider.radius = standingCrouchRadius;
				capsuleCollider.center = new Vector3(0, standingHeight / 2, 0);
				playerCollider = capsuleCollider;
			}
		}
		
		// Инициализируем 3D никнейм (скрыт по умолчанию)
		if (playerName3DText != null)
		{
			playerName3DText.gameObject.SetActive(false);
			UpdatePlayerName3DText();
		}
		
		// Используем существующий animator для анимаций никнейма

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
        // Проверяем смерть игрока (для всех игроков) - должно быть первым
        CheckDeath();
        
        // Если игрок мертв, не обрабатываем остальное
        if (isDead)
        {
            // Обрабатываем только последовательность смерти
            if (isDeathAnimationPlaying)
            {
                HandleDeathSequence();
            }
            return;
        }
        
        // Проверяем взгляд других игроков и обрабатываем анимации 3D никнейма (для всех игроков)
        CheckIfBeingLookedAt();
        HandlePlayerNameAnimations();
        
        // Обрабатываем ввод только для владельца
        if (!isOwned) return;
        
        // Отслеживаем ввод для анимаций
        HandleInputTracking();
        
        HandleStanceInput();
        HandleStanceChange();
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        
        // Обновляем состояние захвата предметов
        UpdateGrabState();
        
        // Обрабатываем анимации
        HandleAnimations();
        
		// Обновляем тентакли-ноги
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        float inputMagnitude = new Vector2(horizontal, vertical).magnitude;
		bool isRunningAnim = Input.GetKey(KeyCode.LeftShift) && currentStance == PlayerStance.Standing && inputMagnitude > 0.1f;
		UpdateLegs(inputMagnitude, isRunningAnim);
        
        // Обновляем текст с никнеймом и здоровьем
        UpdateNameTagText();
        
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
            // Сохраняем предыдущую стойку перед изменением
            previousStance = currentStance;
            
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
            // Сохраняем предыдущую стойку перед изменением
            previousStance = currentStance;
            
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
        // Отслеживаем переход в состояние Crouching для задержки скрытия bodyObject
        if (currentStance == PlayerStance.Crouching && previousStance != PlayerStance.Crouching)
        {
            crouchTransitionStartTime = Time.time;
        }
        else if (currentStance != PlayerStance.Crouching)
        {
            // Сбрасываем таймер, если вышли из состояния Crouching
            crouchTransitionStartTime = -1f;
        }
        
        // Отслеживаем переход в состояние Prone из Standing для задержки скрытия headObject
        if (currentStance == PlayerStance.Prone && previousStance == PlayerStance.Standing)
        {
            proneTransitionStartTime = Time.time;
        }
        else if (currentStance != PlayerStance.Prone)
        {
            // Сбрасываем таймер, если вышли из состояния Prone
            proneTransitionStartTime = -1f;
        }
        
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
                // Проверяем, прошло ли 0.5 секунды с момента перехода в Prone из Standing
                if (proneTransitionStartTime > 0 && (Time.time - proneTransitionStartTime) >= proneHeadHideDelay)
                {
                    showHead = false;
                }
                else
                {
                    // Показываем headObject во время задержки
                    showHead = true;
                }
                showBody = true;
                showLegs = false;
                break;
            case PlayerStance.Crouching:
                showHead = true;
                // Проверяем, прошло ли 0.3 секунды с момента перехода в Crouching
                if (crouchTransitionStartTime > 0 && (Time.time - crouchTransitionStartTime) >= crouchBodyHideDelay)
                {
                    showBody = false;
                }
                else
                {
                    // Показываем bodyObject во время задержки
                    showBody = true;
                }
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
        
        // Обновляем видимость 3D текста с никнеймом и здоровьем
        UpdateNameTagVisibility();
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
    
    /// <summary>
    /// Отслеживает ввод игрока (мышь и клавиатура) для определения бездействия
    /// </summary>
    private void HandleInputTracking()
    {
        bool hasInput = false;
        
        // Проверяем движение мыши
        Vector2 currentMousePosition = Input.mousePosition;
        if (Vector2.Distance(currentMousePosition, lastMousePosition) > 0.1f)
        {
            hasInput = true;
            lastMousePosition = currentMousePosition;
        }
        
        // Проверяем нажатия клавиш
        if (Input.anyKeyDown || Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
        {
            hasInput = true;
        }
        
        // Проверяем движение мыши через оси
        if (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f)
        {
            hasInput = true;
        }
        
        // Обновляем время последнего ввода
        if (hasInput)
        {
            lastInputTime = Time.time;
            isIdleLongPlaying = false;
        }
    }
    
    /// <summary>
    /// Обрабатывает все анимации игрока
    /// </summary>
    private void HandleAnimations()
    {
        if (animator == null)
            return;
        
        // Постоянно проигрываем idle анимацию
        animator.SetBool("idle", true);
        
        // Обрабатываем idlelong анимацию
        HandleIdleLongAnimation();
        
        // Обрабатываем анимации переходов между стойками
        HandleStanceTransitionAnimations();
        
        // Обрабатываем анимации захвата предметов
        HandleGrabAnimations();
    }
    
    /// <summary>
    /// Обрабатывает анимацию idlelong (проигрывается после 1 минуты бездействия)
    /// Случайным образом выбирает одну из трех анимаций: idlelong, idlelong2, idlelong3
    /// </summary>
    private void HandleIdleLongAnimation()
    {
        if (animator == null)
            return;
        
        float timeSinceLastInput = Time.time - lastInputTime;
        float timeSinceLastIdleLong = Time.time - lastIdleLongPlayTime;
        
        // Проверяем, прошла ли минута без ввода и можно ли проиграть анимацию снова
        if (timeSinceLastInput >= idleLongDelay && !isIdleLongPlaying && timeSinceLastIdleLong >= idleLongInterval)
        {
            // Случайным образом выбираем одну из трех анимаций
            int randomAnimation = Random.Range(0, 3);
            
            switch (randomAnimation)
            {
                case 0:
                    animator.SetTrigger("idlelong");
                    break;
                case 1:
                    animator.SetTrigger("idlelong2");
                    break;
                case 2:
                    animator.SetTrigger("idlelong3");
                    break;
            }
            
            lastIdleLongPlayTime = Time.time;
            isIdleLongPlaying = true;
        }
        
        // Сбрасываем флаг, если анимация закончилась
        if (isIdleLongPlaying && timeSinceLastInput < idleLongDelay)
        {
            isIdleLongPlaying = false;
        }
    }
    
    /// <summary>
    /// Обрабатывает анимации переходов между стойками
    /// </summary>
    private void HandleStanceTransitionAnimations()
    {
        if (animator == null)
            return;
        
        // Проверяем, изменилась ли стойка
        if (previousStance != currentStance)
        {
            // Standing -> Crouching: swaptosit
            if (previousStance == PlayerStance.Standing && currentStance == PlayerStance.Crouching)
            {
                animator.SetTrigger("swaptosit");
            }
            // Crouching -> Standing: escapetosit
            else if (previousStance == PlayerStance.Crouching && currentStance == PlayerStance.Standing)
            {
                animator.SetTrigger("escapetosit");
            }
            // Standing/Crouching -> Prone: swaptolie
            else if ((previousStance == PlayerStance.Standing || previousStance == PlayerStance.Crouching) && currentStance == PlayerStance.Prone)
            {
                animator.SetTrigger("swaptolie");
            }
            // Prone -> Standing: escapetolie
            else if (previousStance == PlayerStance.Prone && currentStance == PlayerStance.Standing)
            {
                animator.SetTrigger("escapetolie");
            }
            
            // Обновляем previousStance после проигрывания анимации, чтобы избежать повторных срабатываний
            previousStance = currentStance;
        }
    }
    
    /// <summary>
    /// Обновляет состояние захвата предметов
    /// </summary>
    private void UpdateGrabState()
    {
        // Сохраняем предыдущее состояние
        wasHoldingObject = isHoldingObject;
        
        // Проверяем, держит ли игрок предмет в любой из систем захвата
        bool holdingFromObjectGrab = (objectGrabSystem != null && objectGrabSystem.IsHoldingObject());
        bool holdingFromPickupable = (pickupableGrabSystem != null && pickupableGrabSystem.IsHoldingObject());
        
        isHoldingObject = holdingFromObjectGrab || holdingFromPickupable;
    }
    
    /// <summary>
    /// Получает текущее значение скольжения из активной системы захвата
    /// </summary>
    private float GetCurrentSlipAmount()
    {
        float slipAmount = 0f;
        
        // Получаем значение скольжения из ObjectGrabSystem
        if (objectGrabSystem != null && objectGrabSystem.IsHoldingObject())
        {
            slipAmount = objectGrabSystem.GetSlipAmount();
        }
        // Получаем значение скольжения из PickupableGrabSystem
        else if (pickupableGrabSystem != null && pickupableGrabSystem.IsHoldingObject())
        {
            slipAmount = pickupableGrabSystem.GetSlipAmount();
        }
        
        return slipAmount;
    }
    
    /// <summary>
    /// Обрабатывает анимации захвата предметов
    /// </summary>
    private void HandleGrabAnimations()
    {
        if (animator == null)
            return;
        
        // Проверяем начало захвата (было false, стало true)
        if (!wasHoldingObject && isHoldingObject)
        {
            // Начинаем первую анимацию (начало захвата)
            if (!string.IsNullOrEmpty(grabStartAnimation))
            {
                animator.SetTrigger(grabStartAnimation);
                isGrabStartAnimationPlaying = true;
                grabStartTime = Time.time;
            }
        }
        
        // Проверяем, прошло ли 0.2 секунды после начала захвата
        if (isGrabStartAnimationPlaying && grabStartTime > 0 && (Time.time - grabStartTime) >= grabHoldDelay)
        {
            // Переключаемся на цикличную анимацию удержания
            if (!string.IsNullOrEmpty(grabHoldAnimation))
            {
                animator.SetBool(grabHoldAnimation, true);
                isGrabHoldAnimationActive = true;
            }
            isGrabStartAnimationPlaying = false;
            grabStartTime = -1f;
        }
        
        // Если держим предмет, продолжаем цикличную анимацию
        if (isHoldingObject && isGrabHoldAnimationActive)
        {
            if (!string.IsNullOrEmpty(grabHoldAnimation))
            {
                animator.SetBool(grabHoldAnimation, true);
                
                // Ускоряем анимацию в зависимости от скольжения (красноты LineRenderer)
                float slipAmount = GetCurrentSlipAmount();
                // Нормализуем значение скольжения (0-1) и интерполируем между минимальной и максимальной скоростью
                // Чем больше скольжение, тем быстрее анимация (от grabAnimationMinSpeed до grabAnimationMaxSpeed)
                float normalizedSlip = Mathf.Clamp01(slipAmount);
                float animationSpeed = Mathf.Lerp(grabAnimationMinSpeed, grabAnimationMaxSpeed, normalizedSlip);
                animator.speed = animationSpeed;
            }
        }
        else if (!isGrabHoldAnimationActive)
        {
            // Сбрасываем скорость анимации только когда анимация удержания не активна
            animator.speed = 1f;
        }
        
        // Проверяем отпускание предмета (было true, стало false)
        if (wasHoldingObject && !isHoldingObject)
        {
            // Отключаем цикличную анимацию удержания
            if (!string.IsNullOrEmpty(grabHoldAnimation))
            {
                animator.SetBool(grabHoldAnimation, false);
                isGrabHoldAnimationActive = false;
            }
            
            // Сбрасываем скорость анимации при отпускании
            animator.speed = 1f;
            
            // Проигрываем анимацию отпускания
            if (!string.IsNullOrEmpty(grabReleaseAnimation))
            {
                animator.SetTrigger(grabReleaseAnimation);
                isGrabReleaseAnimationPlaying = true;
                grabReleaseTime = Time.time;
            }
        }
        
        // Проверяем, прошло ли 0.2 секунды после отпускания
        if (isGrabReleaseAnimationPlaying && grabReleaseTime > 0 && (Time.time - grabReleaseTime) >= grabReleaseDelay)
        {
            // Анимация отпускания должна стать неактивной (она сама завершится)
            // Просто сбрасываем флаг
            isGrabReleaseAnimationPlaying = false;
            grabReleaseTime = -1f;
        }
    }
    
    /// <summary>
    /// Обновляет текст с никнеймом и здоровьем игрока
    /// </summary>
    void UpdateNameTagText()
    {
        if (nameTagText == null)
            return;
        
        string playerName = "Player";
        
        // Получаем никнейм из NetworkPlayer
        if (networkPlayer != null)
        {
            playerName = networkPlayer.PlayerName;
        }
        
        // Получаем здоровье из PlayerHealthStamina
        string healthText = "";
        if (playerHealthStamina != null)
        {
            float currentHealth = playerHealthStamina.GetCurrentHealth();
            float maxHealth = playerHealthStamina.GetMaxHealth();
            healthText = $"Здоровье : {Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
        }
        else
        {
            healthText = "Здоровье : 0/0";
        }
        
        // Формируем итоговый текст: никнейм с новой строки, затем здоровье
        nameTagText.text = $"{playerName}\n{healthText}";
    }
    
    /// <summary>
    /// Обновляет видимость 3D текста в зависимости от стойки (показывается только когда стоит)
    /// </summary>
    void UpdateNameTagVisibility()
    {
        if (nameTagText == null)
            return;
        
        // Показываем текст только когда игрок стоит
        bool shouldShow = currentStance == PlayerStance.Standing;
        nameTagText.gameObject.SetActive(shouldShow);
    }
    
    /// <summary>
    /// Обновляет текст 3D никнейма из NetworkPlayer
    /// </summary>
    void UpdatePlayerName3DText()
    {
        if (playerName3DText == null)
            return;
        
        string playerName = "Player";
        
        // Получаем никнейм из NetworkPlayer (как в ChatSystem)
        if (networkPlayer != null)
        {
            playerName = networkPlayer.PlayerName;
        }
        
        playerName3DText.text = playerName;
    }
    
    /// <summary>
    /// Проверяет, смотрит ли какой-либо другой игрок на этого игрока
    /// </summary>
    void CheckIfBeingLookedAt()
    {
        // Не проверяем если не заспавнен
        if (netIdentity == null || netIdentity.netId == 0)
        {
            isBeingLookedAt = false;
            return;
        }
        
        // Ищем всех других игроков в сети
        bool foundLooker = false;
        
        if (NetworkClient.active || NetworkServer.active)
        {
            // Получаем всех подключенных клиентов через Mirror
            foreach (var connection in NetworkServer.connections.Values)
            {
                // Пропускаем себя
                int currentConnectionId = connectionToClient != null ? connectionToClient.connectionId : 0;
                if (connection.connectionId == currentConnectionId)
                    continue;
                
                // Ищем NetworkPlayer для этого клиента
                NetworkPlayer otherPlayer = FindNetworkPlayerByConnectionId((uint)connection.connectionId);
                if (otherPlayer == null)
                    continue;
                
                // Получаем камеру другого игрока
                Camera otherCamera = otherPlayer.GetComponentInChildren<Camera>();
                if (otherCamera == null || !otherCamera.enabled)
                    continue;
                
                // Проверяем, смотрит ли камера другого игрока на этого игрока
                Vector3 directionToPlayer = transform.position - otherCamera.transform.position;
                float distance = directionToPlayer.magnitude;
                
                // Проверяем расстояние
                if (distance > lookAtDistance)
                    continue;
                
                // Проверяем угол (направление камеры должно быть примерно в сторону игрока)
                Vector3 cameraForward = otherCamera.transform.forward;
                float dot = Vector3.Dot(cameraForward.normalized, directionToPlayer.normalized);
                
                // Если угол меньше 0.7 (примерно 45 градусов), игрок не смотрит
                if (dot < 0.7f)
                    continue;
                
                // Делаем raycast от камеры другого игрока к этому игроку
                Ray ray = new Ray(otherCamera.transform.position, directionToPlayer.normalized);
                if (playerCollider != null && playerCollider.Raycast(ray, out RaycastHit hit, lookAtDistance))
                {
                    // Проверяем, что попали именно в этого игрока
                    if (hit.collider == playerCollider)
                    {
                        foundLooker = true;
                        break;
                    }
                }
            }
        }
        
        wasBeingLookedAt = isBeingLookedAt;
        isBeingLookedAt = foundLooker;
    }
    
    /// <summary>
    /// Находит NetworkPlayer по clientId
    /// </summary>
    private NetworkPlayer FindNetworkPlayerByConnectionId(uint connectionId)
    {
        NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
        foreach (NetworkPlayer player in allPlayers)
        {
            if (player.netIdentity != null && player.netIdentity.netId != 0 && player.PlayerId == connectionId)
            {
                return player;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Обрабатывает анимации показа/скрытия 3D никнейма
    /// </summary>
    void HandlePlayerNameAnimations()
    {
        if (playerName3DText == null || animator == null)
            return;
        
        // Не показываем никнейм самому себе
        if (isOwned)
        {
            if (playerName3DText.gameObject.activeSelf)
            {
                playerName3DText.gameObject.SetActive(false);
                currentNameState = PlayerNameState.Hidden;
            }
            return;
        }
        
        // Обновляем текст никнейма
        UpdatePlayerName3DText();
        
        // Обрабатываем переходы состояний
        if (!wasBeingLookedAt && isBeingLookedAt)
        {
            // Начали смотреть на игрока - начинаем показ
            if (currentNameState == PlayerNameState.Hidden)
            {
                currentNameState = PlayerNameState.Showing;
                nameAnimationStartTime = Time.time;
                
                // Активируем объект и запускаем анимацию показа
                playerName3DText.gameObject.SetActive(true);
                if (!string.IsNullOrEmpty(playerNameShowAnimation))
                {
                    animator.SetTrigger(playerNameShowAnimation);
                }
            }
        }
        else if (wasBeingLookedAt && !isBeingLookedAt)
        {
            // Перестали смотреть на игрока - начинаем скрытие
            if (currentNameState == PlayerNameState.Visible || currentNameState == PlayerNameState.Showing)
            {
                currentNameState = PlayerNameState.Hiding;
                nameAnimationStartTime = Time.time;
                
                // Запускаем анимацию скрытия
                if (!string.IsNullOrEmpty(playerNameHideAnimation))
                {
                    animator.SetTrigger(playerNameHideAnimation);
                }
            }
        }
        
        // Обрабатываем переходы внутри состояний
        switch (currentNameState)
        {
            case PlayerNameState.Showing:
                // Проверяем, прошло ли время показа
                if (nameAnimationStartTime > 0 && (Time.time - nameAnimationStartTime) >= playerNameShowDuration)
                {
                    currentNameState = PlayerNameState.Visible;
                    nameAnimationStartTime = -1f;
                    
                    // Запускаем idle анимацию
                    if (!string.IsNullOrEmpty(playerNameIdleAnimation))
                    {
                        animator.SetBool(playerNameIdleAnimation, true);
                    }
                }
                break;
                
            case PlayerNameState.Visible:
                // Продолжаем idle анимацию пока смотрят
                if (isBeingLookedAt)
                {
                    if (!string.IsNullOrEmpty(playerNameIdleAnimation))
                    {
                        animator.SetBool(playerNameIdleAnimation, true);
                    }
                }
                break;
                
            case PlayerNameState.Hiding:
                // Проверяем, прошло ли время скрытия
                if (nameAnimationStartTime > 0 && (Time.time - nameAnimationStartTime) >= playerNameHideDuration)
                {
                    currentNameState = PlayerNameState.Hidden;
                    nameAnimationStartTime = -1f;
                    
                    // Скрываем объект
                    playerName3DText.gameObject.SetActive(false);
                    
                    // Сбрасываем анимации
                    if (!string.IsNullOrEmpty(playerNameIdleAnimation))
                    {
                        animator.SetBool(playerNameIdleAnimation, false);
                    }
                }
                break;
        }
    }
    
    /// <summary>
    /// Проверяет смерть игрока и обрабатывает её
    /// </summary>
    void CheckDeath()
    {
        if (playerHealthStamina == null)
            return;
        
        // Проверяем здоровье
        float currentHealth = playerHealthStamina.GetCurrentHealth();
        isDead = currentHealth <= 0f;
        
        // Если игрок только что умер
        if (!wasDead && isDead)
        {
            StartDeathSequence();
        }
        
        wasDead = isDead;
    }
    
    /// <summary>
    /// Начинает последовательность смерти: проигрывает анимацию
    /// </summary>
    void StartDeathSequence()
    {
        if (isDeathAnimationPlaying)
            return; // Уже начали процесс смерти
        
        isDeathAnimationPlaying = true;
        deathAnimationStartTime = Time.time;
        deathParticleShown = false;
        
        // Проигрываем анимацию смерти
        if (animator != null && !string.IsNullOrEmpty(deathAnimation))
        {
            animator.SetTrigger(deathAnimation);
            Debug.Log($"[PlayerController] Начата анимация смерти: {deathAnimation}");
        }
        
        // Останавливаем движение
        velocity = Vector3.zero;
    }
    
    /// <summary>
    /// Обрабатывает последовательность смерти: показывает эффекты и спавнит труп
    /// </summary>
    void HandleDeathSequence()
    {
        if (deathAnimationStartTime < 0)
            return;
        
        float timeSinceDeath = Time.time - deathAnimationStartTime;
        
        // Показываем Particle System через указанное время
        if (!deathParticleShown && timeSinceDeath >= deathParticleStartTime)
        {
            ShowDeathParticle();
            deathParticleShown = true;
        }
        
        // После завершения анимации спавним труп и удаляем игрока
        if (timeSinceDeath >= deathAnimationDuration)
        {
            CompleteDeath();
        }
    }
    
    /// <summary>
    /// Показывает Particle System эффекта смерти
    /// </summary>
    void ShowDeathParticle()
    {
        if (deathParticleSystem != null)
        {
            // Устанавливаем позицию Particle System на позицию игрока
            deathParticleSystem.transform.position = transform.position;
            deathParticleSystem.gameObject.SetActive(true);
            deathParticleSystem.Play();
            Debug.Log("[PlayerController] Particle System смерти активирован");
        }
    }
    
    /// <summary>
    /// Завершает процесс смерти: спавнит труп и удаляет игрока
    /// </summary>
    void CompleteDeath()
    {
        if (netIdentity == null || netIdentity.netId == 0) return;
        
        // Спавним труп на месте игрока
        SpawnCorpse();
        
        // Удаляем игрока (деспавним NetworkIdentity)
        if (isServer)
        {
            NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
            if (networkIdentity != null && networkIdentity.netId != 0)
            {
                NetworkServer.Destroy(gameObject);
            }
        }
        
        // Сбрасываем флаги
        isDeathAnimationPlaying = false;
        deathAnimationStartTime = -1f;
        deathParticleShown = false;
    }
    
    
    /// <summary>
    /// Спавнит префаб трупа на месте игрока (вызывается только на сервере)
    /// </summary>
    void SpawnCorpse()
    {
        if (deathCorpsePrefab == null)
        {
            Debug.LogWarning("[PlayerController] deathCorpsePrefab не назначен в инспекторе!");
            return;
        }
        
        // Позиция и ротация для спавна трупа
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;
        
        // Спавним префаб
        GameObject corpse = Instantiate(deathCorpsePrefab, spawnPosition, spawnRotation);
        
        // Устанавливаем нужный размер трупа
        float corpseScale = 0.0002247104f;
        corpse.transform.localScale = new Vector3(corpseScale, corpseScale, corpseScale);
        
        // Получаем или добавляем компонент CorpseItem и передаем никнейм игрока
        CorpseItem corpseItem = corpse.GetComponent<CorpseItem>();
        if (corpseItem == null)
        {
            // Добавляем компонент если его нет
            corpseItem = corpse.AddComponent<CorpseItem>();
            Debug.Log("[PlayerController] Компонент CorpseItem добавлен к трупу");
        }
        
        // Получаем никнейм игрока из NetworkPlayer
        string playerName = "Unknown Player";
        if (networkPlayer != null)
        {
            playerName = networkPlayer.PlayerName;
        }
        
        // Устанавливаем никнейм в труп
        corpseItem.SetPlayerName(playerName);
        Debug.Log($"[PlayerController] Труп игрока {playerName} заспавнен на позиции {spawnPosition} с размером {corpseScale}");
    }
    
    /// <summary>
    /// Убивает игрока (устанавливает здоровье в 0) - для тестирования
    /// Вызывается через ServerRpc для работы в мультиплеере
    /// </summary>
    [Command(requiresAuthority = false)]
    public void KillPlayerCommand()
    {
        if (playerHealthStamina == null)
            return;
        
        // Устанавливаем здоровье в 0
        playerHealthStamina.UseHealth(playerHealthStamina.GetCurrentHealth());
    }
    
    /// <summary>
    /// Публичный метод для убийства игрока (вызывает ServerRpc)
    /// </summary>
    public void KillPlayer()
    {
        if (netIdentity != null && netIdentity.netId != 0)
        {
            KillPlayerCommand();
        }
        else
        {
            // В одиночной игре вызываем напрямую
            if (playerHealthStamina != null)
            {
                playerHealthStamina.UseHealth(playerHealthStamina.GetCurrentHealth());
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

