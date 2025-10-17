using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : MonoBehaviour
{
    [Header("Object Properties")]
    [Tooltip("Вес объекта в килограммах")]
    [SerializeField] public float objectWeight = 5f;
    
    [Header("Grip Settings")]
    [Tooltip("Насколько крепко можно держать объект (0-1)")]
    [SerializeField] private float gripStrength = 1f;
    
    [Tooltip("Центр масс объекта для реалистичной физики")]
    [SerializeField] private Vector3 centerOfMass = Vector3.zero;
    
    [Header("Object Type")]
    [SerializeField] private bool isFragile = false;
    [SerializeField] private float breakForceThreshold = 100f;
    
    [Header("Sound Effects (Optional)")]
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip releaseSound;
    [SerializeField] private AudioClip breakSound;
    
    private Rigidbody rb;
    private AudioSource audioSource;
    private bool isGrabbed = false;
    private float originalMass;
    private float originalDrag;
    private float originalAngularDrag;
    
    // Для отслеживания столкновений
    private Vector3 lastVelocity;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Настраиваем Rigidbody на основе веса
        if (rb != null)
        {
            rb.mass = objectWeight;
            rb.centerOfMass = centerOfMass;
            originalMass = rb.mass;
            originalDrag = rb.linearDamping;
            originalAngularDrag = rb.angularDamping;
        }
        
        // Настраиваем аудио компонент
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (grabSound != null || releaseSound != null || breakSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D звук
            audioSource.volume = 0.5f;
        }
    }
    
    void Start()
    {
        // Автоматически назначаем тег если не назначен
        if (gameObject.tag == "Untagged")
        {
            gameObject.tag = "Grabbable";
        }
        
        // Проверяем наличие коллайдера
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"GrabbableObject на {gameObject.name} не имеет Collider! Добавьте коллайдер для корректной работы.");
        }
    }
    
    void FixedUpdate()
    {
        if (rb != null)
        {
            lastVelocity = rb.linearVelocity;
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Вычисляем силу удара
        float impactForce = lastVelocity.magnitude * rb.mass;
        
        // Проверяем столкновение с разрушаемым объектом
        DestructibleObject destructible = collision.gameObject.GetComponent<DestructibleObject>();
        if (destructible != null)
        {
            // Передаем информацию об ударе разрушаемому объекту
            Vector3 impactPoint = collision.contacts[0].point;
            Vector3 impactDirection = collision.contacts[0].normal;
            
            destructible.TakeHit(impactForce, impactPoint, impactDirection);
            
            Debug.Log($"{gameObject.name} ударил {collision.gameObject.name} с силой {impactForce}");
        }
        
        // Если этот объект хрупкий и сила удара велика - ломаем его
        if (!isGrabbed && isFragile && impactForce > breakForceThreshold)
        {
            BreakObject(collision.contacts[0].point);
        }
    }
    
    public void OnGrabbed()
    {
        isGrabbed = true;
        
        // Воспроизводим звук захвата
        if (audioSource != null && grabSound != null)
        {
            audioSource.PlayOneShot(grabSound);
        }
        
        // Можно добавить визуальные эффекты
        // Например, изменить цвет или включить частицы
    }
    
    public void OnReleased()
    {
        isGrabbed = false;
        
        // Воспроизводим звук отпускания
        if (audioSource != null && releaseSound != null)
        {
            audioSource.PlayOneShot(releaseSound);
        }
        
        // Восстанавливаем оригинальные физические параметры
        if (rb != null)
        {
            rb.linearDamping = originalDrag;
            rb.angularDamping = originalAngularDrag;
        }
    }
    
    void BreakObject(Vector3 impactPoint)
    {
        Debug.Log($"{gameObject.name} разбился!");
        
        // Отключаем визуальное представление сразу
        DisableVisuals();
        
        // Воспроизводим звук разрушения
        if (audioSource != null && breakSound != null)
        {
            // Создаем временный объект для звука, чтобы он доиграл после уничтожения объекта
            GameObject soundObject = new GameObject("BreakSound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = breakSound;
            tempAudio.spatialBlend = 1f;
            tempAudio.Play();
            Destroy(soundObject, breakSound.length);
        }
        
        // Пытаемся использовать MeshCutter для реалистичного разрушения
        if (TryUseMeshCutter(impactPoint))
        {
            // MeshCutter успешно применен, уничтожаем объект с задержкой
            Destroy(gameObject, 0.1f);
        }
        else
        {
            // Используем простое разрушение
            CreateBreakEffect(impactPoint);
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Пытается использовать MeshCutter для реалистичного разрушения (только для хрупких объектов)
    /// НЕ используется для DestructibleObject - они имеют свою систему разрушения
    /// </summary>
    bool TryUseMeshCutter(Vector3 impactPoint)
    {
        // НЕ применяем MeshCutter если есть DestructibleObject компонент
        // DestructibleObject имеет свою логику разрушения после определенного количества ударов
        if (GetComponent<DestructibleObject>() != null)
        {
            return false; // Пусть DestructibleObject сам управляет разрушением
        }
        
        // Проверяем наличие необходимых компонентов
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
        {
            return false;
        }
        
        // Проверяем наличие MeshCutterManager
        if (MeshCutterManager.Instance == null)
        {
            return false;
        }
        
        try
        {
            // Вычисляем силу удара
            float impactForce = lastVelocity.magnitude * rb.mass;
            
            // Применяем MeshCutter с автоудалением осколков через 3 секунды
            MeshCutterManager.Instance.DamageMesh(gameObject, impactForce, null, 3f);
            Debug.Log($"MeshCutter успешно применен к {gameObject.name} (сила: {impactForce})");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Не удалось применить MeshCutter к {gameObject.name}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Отключает визуальное представление объекта
    /// </summary>
    void DisableVisuals()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }
        
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }
    
    /// <summary>
    /// Создает простые осколки (fallback если MeshCutter недоступен)
    /// </summary>
    void CreateBreakEffect(Vector3 position)
    {
        // Простой эффект разрушения - создаем несколько маленьких кубиков
        int fragmentCount = Random.Range(5, 10);
        
        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = position + Random.insideUnitSphere * 0.5f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);
            fragment.transform.rotation = Random.rotation;
            
            // Копируем материал оригинального объекта
            Renderer originalRenderer = GetComponent<Renderer>();
            Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
            if (originalRenderer != null && fragmentRenderer != null)
            {
                fragmentRenderer.material = originalRenderer.material;
            }
            
            // Добавляем физику
            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.mass = objectWeight / fragmentCount;
            
            // Добавляем силу от точки удара
            Vector3 explosionDir = (fragment.transform.position - position).normalized;
            fragmentRb.AddForce(explosionDir * 5f, ForceMode.Impulse);
            fragmentRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
            
            // Уничтожаем осколки через 3 секунды
            Destroy(fragment, 3f);
        }
    }
    
    // Метод для настройки объекта извне
    public void SetWeight(float weight)
    {
        objectWeight = weight;
        if (rb != null)
        {
            rb.mass = weight;
            originalMass = weight;
        }
    }
    
    public float GetGripStrength()
    {
        return gripStrength;
    }
    
    public bool IsCurrentlyGrabbed()
    {
        return isGrabbed;
    }
    
    // Визуализация центра масс в редакторе
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 worldCenterOfMass = transform.TransformPoint(centerOfMass);
        Gizmos.DrawWireSphere(worldCenterOfMass, 0.1f);
        Gizmos.DrawLine(transform.position, worldCenterOfMass);
        
        // Отображаем информацию о весе
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, 
            $"Вес: {objectWeight}kg\nЗахвачен: {isGrabbed}");
        #endif
    }
}

