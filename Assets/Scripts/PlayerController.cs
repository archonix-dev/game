using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float proneSpeed = 0.8f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    
    [Header("Stance Settings")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float proneHeight = 0.6f;
    [SerializeField] private float stanceTransitionSpeed = 10f;
    
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    
    private enum PlayerStance { Standing, Crouching, Prone }
    private PlayerStance currentStance = PlayerStance.Standing;
    private float targetHeight;
    private Vector3 targetCenter;
    
    public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standingHeight;
        targetHeight = standingHeight;
        
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
    }
    
    void Update()
    {
        // Обрабатываем ввод только для владельца
        if (!IsOwner) return;
        
        HandleGroundCheck();
        HandleStance();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        
        controller.Move(velocity * Time.deltaTime);
        SmoothStanceTransition();
    }
    
    void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }
    
    void HandleStance()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (currentStance == PlayerStance.Crouching)
            {
                if (CanStandUp())
                {
                    SetStance(PlayerStance.Standing);
                }
            }
            else if (currentStance == PlayerStance.Standing)
            {
                SetStance(PlayerStance.Crouching);
            }
            else if (currentStance == PlayerStance.Prone)
            {
                SetStance(PlayerStance.Crouching);
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (currentStance == PlayerStance.Prone)
            {
                if (CanStandUp())
                {
                    SetStance(PlayerStance.Standing);
                }
            }
            else
            {
                SetStance(PlayerStance.Prone);
            }
        }
        
        if (Input.GetKey(KeyCode.LeftControl) && currentStance == PlayerStance.Standing)
        {
            SetStance(PlayerStance.Crouching);
        }
        else if (Input.GetKeyUp(KeyCode.LeftControl) && currentStance == PlayerStance.Crouching)
        {
            if (CanStandUp())
            {
                SetStance(PlayerStance.Standing);
            }
        }
    }
    
    void SetStance(PlayerStance newStance)
    {
        currentStance = newStance;
        
        switch (currentStance)
        {
            case PlayerStance.Standing:
                targetHeight = standingHeight;
                break;
            case PlayerStance.Crouching:
                targetHeight = crouchHeight;
                break;
            case PlayerStance.Prone:
                targetHeight = proneHeight;
                break;
        }
        
        targetCenter = new Vector3(0, targetHeight / 2, 0);
    }
    
    bool CanStandUp()
    {
        float checkDistance = standingHeight - controller.height;
        Vector3 start = transform.position + Vector3.up * (controller.height / 2);
        
        return !Physics.SphereCast(start, controller.radius, Vector3.up, out RaycastHit hit, checkDistance);
    }
    
    void SmoothStanceTransition()
    {
        if (Mathf.Abs(controller.height - targetHeight) > 0.01f)
        {
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * stanceTransitionSpeed);
            controller.center = Vector3.Lerp(controller.center, targetCenter, Time.deltaTime * stanceTransitionSpeed);
            
            if (groundCheck != null)
            {
                groundCheck.localPosition = new Vector3(0, -controller.height / 2, 0);
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
    }
    
    float GetCurrentSpeed()
    {
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        
        switch (currentStance)
        {
            case PlayerStance.Standing:
                return isRunning ? runSpeed : walkSpeed;
            case PlayerStance.Crouching:
                return crouchSpeed;
            case PlayerStance.Prone:
                return proneSpeed;
            default:
                return walkSpeed;
        }
    }
    
    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded && currentStance != PlayerStance.Prone)
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

