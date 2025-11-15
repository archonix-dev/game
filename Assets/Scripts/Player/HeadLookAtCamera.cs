using UnityEngine;

public class HeadLookAtCamera : MonoBehaviour
{
    [Header("Настройки головы")]
    [Tooltip("Transform головы игрока (если не указан, используется текущий объект)")]
    public Transform headTransform;
    
    [Tooltip("Камера игрока (если не указана, используется Camera.main)")]
    public Camera playerCamera;
    
    [Tooltip("Animator компонент (если не указан, будет найден автоматически)")]
    public Animator animator;
    
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
        
        // Находим Animator если не назначен
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInParent<Animator>();
            }
            if (animator == null)
            {
                animator = FindObjectOfType<Animator>();
            }
        }
    }
    
    /// <summary>
    /// Используется для принудительного поворота головы после анимаций
    /// OnAnimatorIK вызывается после применения анимаций, поэтому может перезаписать их влияние
    /// </summary>
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || playerCamera == null)
        {
            return;
        }
        
        // Не двигаем голову, когда меню открыто (курсор разблокирован)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            animator.SetLookAtWeight(0f);
            return;
        }
        
        // Получаем позицию головы (пробуем получить из Humanoid Avatar, иначе используем приблизительную)
        Vector3 headPosition;
        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone != null)
        {
            headPosition = headBone.position;
        }
        else if (headTransform != null)
        {
            headPosition = headTransform.position;
        }
        else
        {
            // Если не Humanoid Avatar, используем приблизительную позицию головы
            headPosition = animator.transform.position + animator.transform.up * 1.7f;
        }
        
        // Используем направление камеры (куда она смотрит) для вычисления целевой позиции
        Vector3 cameraForward = playerCamera.transform.forward;
        float lookDistance = 10f; // Расстояние для LookAt
        Vector3 lookAtPosition = headPosition + cameraForward * lookDistance;
        
        // Применяем IK для головы
        // Параметры SetLookAtWeight: weight, bodyWeight, headWeight, eyesWeight, clampWeight
        // weight - общий вес IK (0-1)
        // bodyWeight - вес поворота тела (0-1, 0 = только голова)
        // headWeight - вес поворота головы (0-1)
        // eyesWeight - вес поворота глаз (0-1, не используется)
        // clampWeight - ограничение угла поворота (0-1, 0.5 = 90 градусов)
        animator.SetLookAtWeight(1f, 0f, 1f, 0f, 0.5f);
        animator.SetLookAtPosition(lookAtPosition);
    }
    
    /// <summary>
    /// Резервный метод для случаев, когда OnAnimatorIK не работает
    /// Используется, если Animator не найден или IK не настроен
    /// Также применяется дополнительно для гарантии работы поворота головы
    /// </summary>
    private void LateUpdate()
    {
        if (headTransform == null || playerCamera == null)
        {
            return;
        }
        
        // Не двигаем голову, когда меню открыто (курсор разблокирован)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }
        
        // Если Animator найден и работает, OnAnimatorIK должен обрабатывать поворот
        // Но применяем поворот и здесь для гарантии, что он работает
        Vector3 cameraForward = playerCamera.transform.forward;
        Transform parent = headTransform.parent;
        
        if (parent != null)
        {
            // Преобразуем направление камеры в локальное пространство родителя
            Vector3 localCameraDirection = parent.InverseTransformDirection(cameraForward);
            localCameraDirection.Normalize();
            
            // Вычисляем углы поворота
            float horizontalAngle = Mathf.Atan2(localCameraDirection.x, localCameraDirection.z) * Mathf.Rad2Deg;
            float verticalAngle = Mathf.Atan2(-localCameraDirection.y, 
                Mathf.Sqrt(localCameraDirection.x * localCameraDirection.x + localCameraDirection.z * localCameraDirection.z)) * Mathf.Rad2Deg;
            
            // Ограничиваем углы
            horizontalAngle = Mathf.Clamp(horizontalAngle, -maxHorizontalAngle, maxHorizontalAngle);
            verticalAngle = Mathf.Clamp(verticalAngle, -maxVerticalAngle, maxVerticalAngle);
            
            // Создаем целевой поворот
            Quaternion targetLocalRotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0f);
            
            // Применяем поворот
            headTransform.localRotation = Quaternion.Slerp(
                headTransform.localRotation,
                targetLocalRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            // Если нет родителя, используем глобальный поворот
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            headTransform.rotation = Quaternion.Slerp(
                headTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}

