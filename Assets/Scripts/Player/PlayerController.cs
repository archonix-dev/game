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
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    
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
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
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
        UpdateAnimations();
        
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
    
    void UpdateAnimations()
    {
        if (animator == null) return;
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool hasMovement = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && currentStance == PlayerStance.Standing;
        
        // Устанавливаем параметры анимации
        animator.SetInteger("Stance", (int)currentStance);
        animator.SetBool("IsMoving", hasMovement);
        animator.SetBool("IsRunning", isRunning);
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

