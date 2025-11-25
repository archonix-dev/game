using UnityEngine;
using TMPro;
using Mirror;
using UnityEngine.UI;

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
    
    [Tooltip("AudioSource для звука смерти (если не назначен, будет создан автоматически)")]
    [SerializeField] private AudioSource deathAudioSource;
    
    [Tooltip("AudioClip для звука смерти")]
    [SerializeField] private AudioClip deathAudioClip;
    
    [Tooltip("Громкость звука смерти")]
    [SerializeField] private float deathAudioVolume = 0.8f;
    
    [Header("Death Explosion Settings")]
    [Tooltip("Радиус взрыва при смерти игрока (для тряски камеры)")]
    [SerializeField] private float deathExplosionRadius = 10f;
    
    [Tooltip("Максимальная интенсивность тряски камеры при смерти игрока")]
    [SerializeField] private float maxDeathExplosionShakeIntensity = 0.25f;
    
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

    [Header("Player Identity")]
    [SyncVar(hook = nameof(OnPlayerDisplayNameChanged))]
    private string syncedPlayerDisplayName = string.Empty;
    private string cachedPlayerDisplayName = string.Empty;
    
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
    
    [Header("Crosshair Targeting Settings")]
    [Tooltip("Image, показывающийся при наведении на любые объекты кроме игроков")]
    [SerializeField] private Image defaultCrosshairImage;
    [Tooltip("Image, показывающийся при наведении на игрока")]
    [SerializeField] private Image playerCrosshairImage;
    [Tooltip("Дистанция проверки наведения для смены Image")]
    [SerializeField] private float crosshairRayDistance = 20f;
    [Tooltip("Слои для Raycast наведения (по умолчанию все слои)")]
    [SerializeField] private LayerMask crosshairLayerMask = ~0;
    
    [Header("Damage Feedback Settings")]
    [SerializeField] private string hitAnimation = "hit";
    [SerializeField] private float hitAnimationDuration = 0.1f;
    [SerializeField] private AudioSource damageAudioSource;
    [SerializeField] private AudioClip damageAudioClip;
    [SerializeField] private float damageAudioVolume = 0.6f;
    
    [Header("Stealth Settings")]
    [Tooltip("Тег триггеров, внутри которых игрок считается скрытым от мобов")]
    [SerializeField] private string hiddenZoneTag = "Hidden";
    
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
    private Vector3 lastLegPosition;
    private float smoothedLegSpeed = 0f;
    private bool legPositionInitialized = false;
    private int hiddenZoneContacts = 0;
    public bool IsHiddenFromMobs { get; private set; }
    
    public enum PlayerStance
    {
        Standing,
        Crouching,
        Prone
    }
    
    // Синхронизированная стойка игрока
    [SyncVar(hook = nameof(OnStanceChanged))]
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
    // Синхронизированное состояние захвата предметов
    [SyncVar(hook = nameof(OnHoldingObjectChanged))]
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
    private bool lastCrosshairAimedAtPlayer = false;
    
    // Переменные для смерти
    // Синхронизированное состояние смерти
    [SyncVar(hook = nameof(OnDeathStateChanged))]
    private bool isDead = false;
    private bool wasDead = false;
    [SyncVar]
    private bool isDeathAnimationPlaying = false;
    private float deathAnimationStartTime = -1f;
    private bool deathParticleShown = false;
    // Опциональная позиция для спавна трупа (если null, используется позиция игрока)
    private Vector3? customCorpseSpawnPosition = null;
    
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
    
    void Awake()
    {
        // Проверяем и отключаем NetworkRigidbodyReliable если нет Rigidbody
        // (Player использует CharacterController, а не Rigidbody)
        // Делаем это в Awake, чтобы компонент был отключен до FixedUpdate
        var networkRigidbody = GetComponent<Mirror.NetworkRigidbodyReliable>();
        if (networkRigidbody != null)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                // Если нет Rigidbody, отключаем NetworkRigidbodyReliable
                networkRigidbody.enabled = false;
                Debug.LogWarning($"[PlayerController] NetworkRigidbodyReliable отключен на {gameObject.name} в Awake, так как нет компонента Rigidbody. Player использует CharacterController.");
            }
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RefreshPlayerDisplayName();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Сбрасываем все флаги смерти при спавне
        ResetDeathState();
        
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
            
            // ВАЖНО: Отключаем CharacterController для других игроков
            // NetworkTransform будет управлять позицией напрямую через transform.position
            // CharacterController блокирует прямое изменение transform.position
            if (controller != null)
            {
                controller.enabled = false;
                Debug.Log($"[PlayerController] CharacterController отключен для удаленного игрока {gameObject.name}");
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
            
            // Для локального игрока CharacterController должен быть включен
            if (controller != null)
            {
                controller.enabled = true;
            }
        }
        
        cachedPlayerDisplayName = string.IsNullOrEmpty(syncedPlayerDisplayName) ? cachedPlayerDisplayName : syncedPlayerDisplayName;

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
        
        // Инициализируем AudioSource для звука смерти если не назначен
        if (deathAudioSource == null)
        {
            deathAudioSource = GetComponent<AudioSource>();
            if (deathAudioSource == null)
            {
                deathAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Настраиваем AudioSource для звука смерти
        if (deathAudioSource != null)
        {
            deathAudioSource.clip = deathAudioClip;
            deathAudioSource.volume = deathAudioVolume;
            deathAudioSource.spatialBlend = 1f; // 3D звук
            deathAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            deathAudioSource.maxDistance = 100f;
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

        InitializeCrosshairUI();

        lastLegPosition = transform.position;
        smoothedLegSpeed = 0f;
        legPositionInitialized = true;
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

        // Обновляем стойку/видимость для всех игроков (включая удалённых)
        HandleStanceChange();

        // Обновляем анимацию ног для всех экземпляров
        UpdateLegAnimationState();

        // Обновляем общие анимации (для локальных и удалённых игроков)
        HandleSharedAnimations();

        // Обрабатываем ввод только для владельца
        if (!isOwned) return;
        
        // Отслеживаем ввод для анимаций
        HandleInputTracking();
        
        HandleStanceInput();
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        
        // Обновляем состояние захвата предметов
        UpdateGrabState();
        
        // Обрабатываем анимации владельца (IdleLong и др.)
        HandleOwnerAnimations();

        HandleCrosshairTargeting();
        
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
        // Обрабатываем ввод только для владельца
        if (!isOwned) return;
        
        bool ctrlPressed = Input.GetKey(KeyCode.LeftControl);
        bool zPressed = Input.GetKey(KeyCode.Z);
        
        if (ctrlPressed && !ctrlPressedLastFrame)
        {
            // Сохраняем предыдущую стойку перед изменением
            previousStance = currentStance;
            
            PlayerStance newStance;
            if (currentStance == PlayerStance.Crouching)
            {
                newStance = PlayerStance.Standing;
            }
            else if (currentStance == PlayerStance.Standing)
            {
                newStance = PlayerStance.Crouching;
            }
            else
            {
                newStance = currentStance;
            }
            
            // Синхронизируем изменение стойки через сервер
            if (isServer)
            {
                currentStance = newStance;
            }
            else
            {
                SetStanceCommand(newStance);
            }
        }
        
        if (zPressed && !zPressedLastFrame)
        {
            // Сохраняем предыдущую стойку перед изменением
            previousStance = currentStance;
            
            PlayerStance newStance;
            if (currentStance == PlayerStance.Prone)
            {
                newStance = PlayerStance.Standing;
            }
            else
            {
                newStance = PlayerStance.Prone;
            }
            
            // Синхронизируем изменение стойки через сервер
            if (isServer)
            {
                currentStance = newStance;
            }
            else
            {
                SetStanceCommand(newStance);
            }
        }
        
        ctrlPressedLastFrame = ctrlPressed;
        zPressedLastFrame = zPressed;
    }
    
    /// <summary>
    /// Command для изменения стойки (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetStanceCommand(PlayerStance newStance)
    {
        currentStance = newStance;
    }
    
    /// <summary>
    /// Hook для изменения стойки (вызывается при изменении SyncVar)
    /// </summary>
    void OnStanceChanged(PlayerStance oldStance, PlayerStance newStance)
    {
        // Обновляем предыдущую стойку для анимаций
        if (oldStance != newStance)
        {
            previousStance = oldStance;
        }
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
        
        // Проверяем, открыто ли меню (курсор разблокирован)
        bool isMenuOpen = Cursor.lockState != CursorLockMode.Locked;
        
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
        
        // Если меню открыто, всегда показываем headObject для владельца
        if (isMenuOpen && isOwned)
        {
            showHead = true;
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

    void InitializeCrosshairUI()
    {
        lastCrosshairAimedAtPlayer = true; // заставляем UpdateCrosshairImages обновить состояние
        UpdateCrosshairImages(false);
    }
    
    void UpdateLegAnimationState()
    {
        if (legAnchors == null || legAnchors.Length == 0 || legFootTargets == null || legFootTargets.Length == 0)
            return;

        UpdateLegMovementMetrics();

        float normalizedSpeed = Mathf.Clamp01(smoothedLegSpeed / Mathf.Max(0.001f, runSpeed));
        bool isRunningAnim = false;

        if (isOwned)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            float inputMagnitude = Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);
            normalizedSpeed = Mathf.Max(normalizedSpeed, inputMagnitude);
            isRunningAnim = Input.GetKey(KeyCode.LeftShift) && currentStance == PlayerStance.Standing && inputMagnitude > 0.1f;
        }
        else
        {
            isRunningAnim = normalizedSpeed > 0.6f && currentStance == PlayerStance.Standing;
        }

        UpdateLegs(normalizedSpeed, isRunningAnim);
    }

    void UpdateLegMovementMetrics()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f)
            return;

        if (!legPositionInitialized)
        {
            lastLegPosition = transform.position;
            smoothedLegSpeed = 0f;
            legPositionInitialized = true;
            return;
        }

        float instantSpeed = (transform.position - lastLegPosition).magnitude / delta;
        lastLegPosition = transform.position;
        smoothedLegSpeed = Mathf.Lerp(smoothedLegSpeed, instantSpeed, delta * 10f);
    }

	void UpdateLegs(float movementFactor, bool isRunning)
    {
        // Ноги видны только когда мы стоим (или когда включены)
		if (legAnchors == null || legAnchors.Length == 0 || legFootTargets == null || legFootTargets.Length == 0) return;
        
        // Небольшая анимация даже без движения
        movementFactor = Mathf.Clamp01(movementFactor);
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
    /// Обрабатывает анимации, которые должны проигрываться у всех игроков
    /// </summary>
    private void HandleSharedAnimations()
    {
        if (animator == null)
            return;
        
        animator.SetBool("idle", true);
        HandleStanceTransitionAnimations();
        HandleGrabAnimations();
    }
    
    /// <summary>
    /// Обрабатывает анимации, зависящие от локального ввода (например, idlelong)
    /// </summary>
    private void HandleOwnerAnimations()
    {
        if (animator == null)
            return;
        
        HandleIdleLongAnimation();
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
        
        bool newHoldingState = holdingFromObjectGrab || holdingFromPickupable;
        
        // Если состояние изменилось или если мы думаем что держим, но на самом деле нет - обновляем
        if (newHoldingState != isHoldingObject || (isHoldingObject && !newHoldingState))
        {
            // Синхронизируем состояние захвата через сервер
            if (isServer)
            {
                isHoldingObject = newHoldingState;
            }
            else if (isOwned)
            {
                SetHoldingObjectCommand(newHoldingState);
            }
        }
    }
    
    /// <summary>
    /// Command для изменения состояния захвата (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetHoldingObjectCommand(bool holding)
    {
        isHoldingObject = holding;
    }
    
    /// <summary>
    /// Hook для изменения состояния захвата (вызывается при изменении SyncVar)
    /// </summary>
    void OnHoldingObjectChanged(bool oldValue, bool newValue)
    {
        // Обновляем предыдущее состояние для анимаций
        wasHoldingObject = oldValue;
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
        else
        {
            // Если не держим предмет, но анимация удержания все еще активна - отключаем её
            if (isGrabHoldAnimationActive && !string.IsNullOrEmpty(grabHoldAnimation))
            {
                animator.SetBool(grabHoldAnimation, false);
                isGrabHoldAnimationActive = false;
            }
            
            // Сбрасываем скорость анимации когда не держим предмет
            if (!isHoldingObject)
            {
                animator.speed = 1f;
            }
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
        
        string playerName = GetPlayerDisplayName();
        
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

    void HandleCrosshairTargeting()
    {
        if (!isOwned)
        {
            return;
        }

        Transform camTransform = playerCamera != null ? playerCamera : (cameraComponent != null ? cameraComponent.transform : null);
        if (camTransform == null)
        {
            UpdateCrosshairImages(false);
            return;
        }

        float rayDistance = crosshairRayDistance > 0f ? crosshairRayDistance : Mathf.Max(lookAtDistance, 1f);
        int mask = crosshairLayerMask.value == 0 ? Physics.DefaultRaycastLayers : crosshairLayerMask.value;
        bool aimingAtPlayer = false;

        if (Physics.Raycast(camTransform.position, camTransform.forward, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            PlayerController otherPlayer = hit.collider.GetComponentInParent<PlayerController>();
            if (otherPlayer != null && otherPlayer != this)
            {
                aimingAtPlayer = true;
            }
        }

        UpdateCrosshairImages(aimingAtPlayer);
    }

    void UpdateCrosshairImages(bool aimingAtPlayer)
    {
        if (lastCrosshairAimedAtPlayer == aimingAtPlayer)
        {
            return;
        }

        lastCrosshairAimedAtPlayer = aimingAtPlayer;

        if (defaultCrosshairImage != null)
        {
            defaultCrosshairImage.enabled = !aimingAtPlayer;
        }

        if (playerCrosshairImage != null)
        {
            playerCrosshairImage.enabled = aimingAtPlayer;
        }
    }

    
    /// <summary>
    /// Обновляет текст 3D никнейма из NetworkPlayer
    /// </summary>
    void UpdatePlayerName3DText()
    {
        if (playerName3DText == null)
            return;
        
        string playerName = GetPlayerDisplayName();
        playerName3DText.text = playerName;
    }

    #region Player Identity
    void OnPlayerDisplayNameChanged(string _, string newName)
    {
        cachedPlayerDisplayName = string.IsNullOrWhiteSpace(newName) ? GetFallbackPlayerName() : newName;
        UpdateNameTagText();
        UpdatePlayerName3DText();
    }

    public string GetPlayerDisplayName()
    {
        if (!string.IsNullOrEmpty(cachedPlayerDisplayName))
            return cachedPlayerDisplayName;

        if (!string.IsNullOrEmpty(syncedPlayerDisplayName))
        {
            cachedPlayerDisplayName = syncedPlayerDisplayName;
            return cachedPlayerDisplayName;
        }

        if (isServer)
        {
            RefreshPlayerDisplayName();
            if (!string.IsNullOrEmpty(cachedPlayerDisplayName))
                return cachedPlayerDisplayName;
        }

        return GetFallbackPlayerName();
    }

    string GetFallbackPlayerName()
    {
        return $"Player {netId}";
    }

    [Server]
    void RefreshPlayerDisplayName()
    {
        string resolvedName = ResolvePlayerDisplayName();
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = GetFallbackPlayerName();
        }

        cachedPlayerDisplayName = resolvedName;

        if (syncedPlayerDisplayName != resolvedName)
        {
            syncedPlayerDisplayName = resolvedName;
        }
    }

    [Server]
    string ResolvePlayerDisplayName()
    {
        string fallbackName = GetFallbackPlayerName();
        int connectionId = -1;

        if (connectionToClient != null)
        {
            connectionId = connectionToClient.connectionId;
        }
        else if (netIdentity != null && netIdentity.connectionToClient != null)
        {
            connectionId = netIdentity.connectionToClient.connectionId;
        }

        if (connectionId >= 0 &&
            PlayerCustomizationStorage.TryGetByConnectionId(connectionId, out var data) &&
            data != null)
        {
            if (!string.IsNullOrWhiteSpace(data.playerName))
            {
                return data.playerName;
            }

            if (data.steamId != 0 &&
                PlayerCustomizationStorage.TryGetBySteamId(data.steamId, out var steamData) &&
                steamData != null &&
                !string.IsNullOrWhiteSpace(steamData.playerName))
            {
                return steamData.playerName;
            }
        }

        return fallbackName;
    }
    #endregion

    [Server]
    public void NotifyDamageTaken(float damageAmount)
    {
        if (damageAmount <= 0f)
            return;

        PlayDamageFeedbackLocal();
        RpcPlayDamageFeedback();
    }

    [ClientRpc]
    void RpcPlayDamageFeedback()
    {
        if (isServer)
            return;

        PlayDamageFeedbackLocal();
    }

    void PlayDamageFeedbackLocal()
    {
        PlayHitAnimation();
        PlayDamageSound();
    }

    void PlayHitAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(hitAnimation))
            return;

        animator.ResetTrigger(hitAnimation);
        animator.SetTrigger(hitAnimation);
    }

    void PlayDamageSound()
    {
        if (damageAudioSource == null || damageAudioClip == null)
            return;

        damageAudioSource.clip = damageAudioClip;
        damageAudioSource.volume = damageAudioVolume;
        damageAudioSource.Stop();
        damageAudioSource.Play();
    }
    
    /// <summary>
    /// Проверяет, смотрит ли какой-либо другой игрок на этого игрока
    /// В локальной игре всегда возвращает false
    /// </summary>
    void CheckIfBeingLookedAt()
    {
        // В локальной игре нет других игроков
        wasBeingLookedAt = isBeingLookedAt;
        isBeingLookedAt = false;
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
        bool newDeadState = currentHealth <= 0f;
        
        // Синхронизируем состояние смерти через сервер
        if (newDeadState != isDead)
        {
            if (isServer)
            {
                isDead = newDeadState;
            }
            else if (isOwned)
            {
                SetDeathStateCommand(newDeadState);
            }
        }
        
        // Если игрок только что умер
        if (!wasDead && isDead)
        {
            StartDeathSequence();
        }
        
        wasDead = isDead;
    }
    
    /// <summary>
    /// Command для изменения состояния смерти (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    protected void SetDeathStateCommand(bool dead)
    {
        isDead = dead;
    }
    
    /// <summary>
    /// Hook для изменения состояния смерти (вызывается при изменении SyncVar)
    /// </summary>
    void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        wasDead = oldValue;
        
        // Если игрок только что умер, запускаем последовательность смерти
        if (!oldValue && newValue)
        {
            StartDeathSequence();
        }
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
        
        // Проигрываем звук смерти
        PlayDeathSound();
        
        // Останавливаем движение
        velocity = Vector3.zero;
    }
    
    /// <summary>
    /// Проигрывает звук смерти
    /// </summary>
    void PlayDeathSound()
    {
        if (deathAudioSource == null || deathAudioClip == null)
            return;
        
        deathAudioSource.clip = deathAudioClip;
        deathAudioSource.volume = deathAudioVolume;
        deathAudioSource.Stop();
        deathAudioSource.Play();
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
    /// Сбрасывает состояние смерти при спавне игрока
    /// </summary>
    void ResetDeathState()
    {
        isDead = false;
        wasDead = false;
        isDeathAnimationPlaying = false;
        deathAnimationStartTime = -1f;
        deathParticleShown = false;
        
        // Деактивируем и останавливаем Particle System смерти
        if (deathParticleSystem != null)
        {
            deathParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            deathParticleSystem.gameObject.SetActive(false);
        }
        
        // Останавливаем звук смерти
        if (deathAudioSource != null && deathAudioSource.isPlaying)
        {
            deathAudioSource.Stop();
        }
        
        Debug.Log("[PlayerController] Состояние смерти сброшено при спавне");
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
        
        // Вызываем тряску камеры для всех игроков при смерти
        BodyCamEffect.TriggerExplosionShakeForAllPlayers(transform.position, maxDeathExplosionShakeIntensity, deathExplosionRadius);
        
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
        // Используем кастомную позицию, если она задана, иначе позицию игрока
        Vector3 spawnPosition = customCorpseSpawnPosition.HasValue ? customCorpseSpawnPosition.Value : transform.position;
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
        
        string playerName = GetPlayerDisplayName();
        
        // Устанавливаем никнейм в труп
        corpseItem.SetPlayerName(playerName);
        Debug.Log($"[PlayerController] Труп игрока {playerName} заспавнен на позиции {spawnPosition} с размером {corpseScale}");
        
        // Сбрасываем кастомную позицию после использования
        customCorpseSpawnPosition = null;
    }
    
    /// <summary>
    /// Устанавливает кастомную позицию для спавна трупа (вызывается перед смертью)
    /// </summary>
    [Server]
    public void SetCustomCorpseSpawnPosition(Vector3 position)
    {
        customCorpseSpawnPosition = position;
        Debug.Log($"[PlayerController] Установлена кастомная позиция для спавна трупа: {position}");
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
    
    [TargetRpc]
    public void TargetShowTerminalLoadingScreen(NetworkConnection target)
    {
        var loadingController = LobbyMainLoadingController.Instance;
        if (loadingController != null)
        {
            loadingController.StartClientLoadingSequence();
        }
    }

    #region Hidden Zones
    private void OnTriggerEnter(Collider other)
    {
        HandleHiddenZoneTrigger(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        HandleHiddenZoneTrigger(other, false);
    }

    private void HandleHiddenZoneTrigger(Collider other, bool entered)
    {
        if (!isActiveAndEnabled || other == null || string.IsNullOrEmpty(hiddenZoneTag))
            return;

        if (!other.CompareTag(hiddenZoneTag))
            return;

        if (entered)
        {
            hiddenZoneContacts++;
        }
        else
        {
            hiddenZoneContacts = Mathf.Max(0, hiddenZoneContacts - 1);
        }

        bool shouldBeHidden = hiddenZoneContacts > 0;
        if (shouldBeHidden == IsHiddenFromMobs)
            return;

        IsHiddenFromMobs = shouldBeHidden;
    }
    #endregion
    
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}

