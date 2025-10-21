using UnityEngine;
//using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : /*NetworkBehaviour*/ MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    
    [Header("Stance Settings")]
    [SerializeField] private float standingHeight = 2f;
    
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
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    
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
        
        // Находим системы захвата если не назначены
        if (objectGrabSystem == null)
        {
            objectGrabSystem = GetComponent<ObjectGrabSystem>();
        }
        
        if (pickupableGrabSystem == null)
        {
            pickupableGrabSystem = GetComponent<PickupableGrabSystem>();
        }
    }
    
    void Update()
    {
        // Обрабатываем ввод только для владельца (закомментировано для одиночной игры)
        //if (!IsOwner) return;
        
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        
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
    
    float GetCurrentSpeed()
    {
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        bool hasMovement = Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;
        
        // Проверяем, держит ли игрок предмет в любой из систем захвата
        bool isHoldingObject = (objectGrabSystem != null && objectGrabSystem.IsHoldingObject()) ||
                              (pickupableGrabSystem != null && pickupableGrabSystem.IsHoldingObject());
        
        // Если держит предмет - не может бегать
        if (isHoldingObject)
        {
            return walkSpeed;
        }
        
        if (isRunning && hasMovement && playerHealthStamina != null && playerHealthStamina.HasEnoughStamina(runStaminaCost * Time.deltaTime))
        {
            return runSpeed;
        }
        
        return walkSpeed;
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
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
    
    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
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

