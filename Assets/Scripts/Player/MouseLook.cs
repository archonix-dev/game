using UnityEngine;
using Mirror;

public class MouseLook : NetworkBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 100f; // Значение по умолчанию (будет перезаписано из KeybindScript)
    [SerializeField] private bool invertY = false;
    
    [Header("Camera Settings")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private float minXRotation = -90f;
    [SerializeField] private float maxXRotation = 90f;
    
    private float xRotation = 0f;
    private KeybindScript keybindScript;
    
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
        
        // Находим KeybindScript для получения чувствительности мыши
        keybindScript = KeybindScript.Instance;
        
        // Загружаем чувствительность из KeybindScript
        LoadMouseSensitivity();
    }
    
    /// <summary>
    /// Загружает чувствительность мыши из KeybindScript
    /// </summary>
    private void LoadMouseSensitivity()
    {
        if (keybindScript != null)
        {
            mouseSensitivity = keybindScript.GetMouseSensitivity();
        }
    }
    
    void Update()
    {
        // Обрабатываем ввод только для владельца
        if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
        
        // Обновляем чувствительность мыши из KeybindScript (на случай изменения в настройках)
        UpdateMouseSensitivity();
        
        HandleMouseLook();
        HandleCursorToggle();
    }
    
    /// <summary>
    /// Обновляет чувствительность мыши из KeybindScript
    /// </summary>
    private void UpdateMouseSensitivity()
    {
        if (keybindScript != null)
        {
            mouseSensitivity = keybindScript.GetMouseSensitivity();
        }
        else
        {
            // Если KeybindScript не найден, пытаемся найти его снова
            keybindScript = KeybindScript.Instance;
            if (keybindScript != null)
            {
                mouseSensitivity = keybindScript.GetMouseSensitivity();
            }
        }
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

