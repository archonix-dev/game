using UnityEngine;
using Mirror;

public class MouseLook : NetworkBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private bool invertY = false;
    
    [Header("Camera Settings")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private float minXRotation = -90f;
    [SerializeField] private float maxXRotation = 90f;
    
    private float xRotation = 0f;
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Настраиваем курсор только для владельца
        if (isOwned)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    void Start()
    {
        // Если не в сети, настраиваем курсор
        if (netIdentity == null || netIdentity.netId == 0)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    void Update()
    {
        // Обрабатываем ввод только для владельца
        if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
        
        HandleMouseLook();
        HandleCursorToggle();
    }
    
    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        
        if (invertY)
        {
            xRotation += mouseY;
        }
        else
        {
            xRotation -= mouseY;
        }
        
        xRotation = Mathf.Clamp(xRotation, minXRotation, maxXRotation);
        
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
    
    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

