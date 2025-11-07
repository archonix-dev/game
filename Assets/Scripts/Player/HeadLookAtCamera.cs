using UnityEngine;

public class HeadLookAtCamera : MonoBehaviour
{
    [Header("Настройки головы")]
    [Tooltip("Transform головы игрока (если не указан, используется текущий объект)")]
    public Transform headTransform;
    
    [Tooltip("Камера игрока (если не указана, используется Camera.main)")]
    public Camera playerCamera;
    
    [Header("Ограничения поворота")]
    [Tooltip("Максимальный угол поворота головы по вертикали (в градусах)")]
    [Range(0f, 90f)]
    public float maxVerticalAngle = 45f;
    
    [Tooltip("Максимальный угол поворота головы по горизонтали (в градусах)")]
    [Range(0f, 90f)]
    public float maxHorizontalAngle = 60f;
    
    [Header("Сглаживание")]
    [Tooltip("Скорость поворота головы (чем больше, тем быстрее)")]
    public float rotationSpeed = 5f;
    
    private void Start()
    {
        if (headTransform == null)
        {
            headTransform = transform;
        }
        
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Camera>();
            }
        }
    }
    
    private void LateUpdate()
    {
        if (headTransform == null || playerCamera == null)
        {
            return;
        }
        
        Vector3 cameraForward = playerCamera.transform.forward;
        Transform parent = headTransform.parent;
        
        if (parent == null)
        {
            headTransform.rotation = Quaternion.LookRotation(cameraForward);
            return;
        }
        
        Vector3 localCameraDirection = parent.InverseTransformDirection(cameraForward);
        
        float horizontalAngle = Mathf.Atan2(localCameraDirection.x, localCameraDirection.z) * Mathf.Rad2Deg;
        float verticalAngle = -Mathf.Asin(localCameraDirection.y) * Mathf.Rad2Deg;
        
        horizontalAngle = Mathf.Clamp(horizontalAngle, -maxHorizontalAngle, maxHorizontalAngle);
        verticalAngle = Mathf.Clamp(verticalAngle, -maxVerticalAngle, maxVerticalAngle);
        
        Quaternion targetLocalRotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0f);
        
        headTransform.localRotation = Quaternion.Slerp(
            headTransform.localRotation,
            targetLocalRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}

