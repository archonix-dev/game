using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Упрощенная система разрушения объектов
/// Легко настраиваемая и расширяемая альтернатива MeshSlicer
/// </summary>
public class SimpleDestructionManager : MonoBehaviour
{
    private static SimpleDestructionManager instance;
    public static SimpleDestructionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SimpleDestructionManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SimpleDestructionManager");
                    instance = go.AddComponent<SimpleDestructionManager>();
                }
            }
            return instance;
        }
    }

    [Header("Настройки разрушения")]
    [Tooltip("Размер осколков относительно оригинального объекта")]
    [Range(0.05f, 3f)]
    [SerializeField] private float fragmentSizeMultiplier = 0.25f;
    
    [Tooltip("Одинаковый размер всех осколков")]
    [SerializeField] private bool uniformFragmentSize = true;
    
    [Tooltip("Использовать простые примитивы вместо копирования меша")]
    [SerializeField] private bool usePrimitiveFragments = true;
    
    [Tooltip("Тип примитива для осколков")]
    [SerializeField] private PrimitiveType fragmentPrimitiveType = PrimitiveType.Cube;
    
    [Tooltip("Радиус разлета осколков от центра")]
    [Range(0.1f, 2f)]
    [SerializeField] private float fragmentSpreadRadius = 0.5f;
    
    [Header("Физика")]
    [Tooltip("Множитель массы осколков (относительно оригинала)")]
    [Range(0.01f, 1f)]
    [SerializeField] private float fragmentMassMultiplier = 0.1f;
    
    [Tooltip("Добавить случайное вращение осколкам")]
    [SerializeField] private bool addRandomRotation = true;
    
    [Tooltip("Сила случайного вращения")]
    [Range(0f, 20f)]
    [SerializeField] private float rotationForce = 10f;
    
    [Header("События")]
    [Tooltip("Вызывается когда объект разрушен")]
    public UnityEvent<GameObject, int> OnObjectDestroyed;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Разрушить объект на осколки
    /// </summary>
    /// <param name="objectToDestroy">Объект для разрушения</param>
    /// <param name="fragmentCount">Количество осколков</param>
    /// <param name="explosionForce">Сила разлета</param>
    /// <param name="explosionCenter">Центр взрыва</param>
    /// <param name="parentTransform">Родительский transform для осколков</param>
    /// <param name="destroyAfter">Время до удаления осколков</param>
    public void DestroyObject(GameObject objectToDestroy, int fragmentCount, float explosionForce, 
        Vector3 explosionCenter, Transform parentTransform = null, float destroyAfter = 3f)
    {
        if (objectToDestroy == null) return;

        // Получаем компоненты оригинального объекта
        MeshFilter meshFilter = objectToDestroy.GetComponent<MeshFilter>();
        Renderer renderer = objectToDestroy.GetComponent<Renderer>();
        Rigidbody originalRb = objectToDestroy.GetComponent<Rigidbody>();

        if (meshFilter == null || renderer == null)
        {
            Debug.LogWarning($"Объект {objectToDestroy.name} не имеет MeshFilter или Renderer!");
            return;
        }

        Vector3 objectPosition = objectToDestroy.transform.position;
        Quaternion objectRotation = objectToDestroy.transform.rotation;
        Vector3 objectScale = objectToDestroy.transform.localScale;
        Bounds objectBounds = renderer.bounds;
        Material objectMaterial = renderer.material;
        float objectMass = originalRb != null ? originalRb.mass : 1f;

        // Вычисляем единый размер осколка
        float calculatedFragmentSize = CalculateFragmentSize(objectScale, fragmentCount);

        // Создаем осколки
        List<GameObject> fragments = new List<GameObject>();
        
        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = CreateFragment(
                objectToDestroy,
                meshFilter.sharedMesh,
                objectMaterial,
                objectPosition,
                objectRotation,
                objectScale,
                objectBounds,
                objectMass,
                i,
                calculatedFragmentSize
            );

            if (fragment != null)
            {
                fragments.Add(fragment);
                
                // Устанавливаем родителя если указан
                if (parentTransform != null)
                {
                    fragment.transform.SetParent(parentTransform);
                }
                
                // Применяем силу разлета
                ApplyExplosionForce(fragment, explosionForce, explosionCenter, objectPosition);
                
                // Удаляем через время
                Destroy(fragment, destroyAfter);
            }
        }

        Debug.Log($"[SimpleDestruction] Создано {fragments.Count} осколков для {objectToDestroy.name}. " +
                  $"Сила разлета: {explosionForce}, удаление через {destroyAfter}с");
        
        OnObjectDestroyed?.Invoke(objectToDestroy, fragments.Count);
    }

    /// <summary>
    /// Вычисляет размер осколка на основе размера объекта и количества осколков
    /// </summary>
    private float CalculateFragmentSize(Vector3 originalScale, int fragmentCount)
    {
        if (uniformFragmentSize)
        {
            // Одинаковый размер для всех осколков
            return fragmentSizeMultiplier;
        }
        else
        {
            // Размер зависит от количества осколков (чем больше осколков, тем меньше каждый)
            float volumeFactor = Mathf.Pow(fragmentCount, -1f/3f); // кубический корень из 1/N
            return fragmentSizeMultiplier * volumeFactor;
        }
    }

    /// <summary>
    /// Создает отдельный осколок
    /// </summary>
    private GameObject CreateFragment(GameObject original, Mesh originalMesh, Material material,
        Vector3 position, Quaternion rotation, Vector3 scale, Bounds bounds, float originalMass, int index, float fragmentSize)
    {
        GameObject fragment = new GameObject($"{original.name}_Fragment_{index}");
        
        // Случайная позиция в пределах объекта
        Vector3 randomOffset = new Vector3(
            Random.Range(-bounds.extents.x, bounds.extents.x),
            Random.Range(-bounds.extents.y, bounds.extents.y),
            Random.Range(-bounds.extents.z, bounds.extents.z)
        ) * fragmentSpreadRadius;
        
        fragment.transform.position = position + randomOffset;
        fragment.transform.rotation = rotation * Quaternion.Euler(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );
        
        // Используем вычисленный размер осколка (одинаковый для всех)
        fragment.transform.localScale = scale * fragmentSize;
        
        // Добавляем визуальное представление
        if (usePrimitiveFragments)
        {
            // Создаем примитив (куб, сфера и т.д.)
            GameObject primitive = GameObject.CreatePrimitive(fragmentPrimitiveType);
            primitive.transform.SetParent(fragment.transform);
            primitive.transform.localPosition = Vector3.zero;
            primitive.transform.localRotation = Quaternion.identity;
            primitive.transform.localScale = Vector3.one;
            
            // Удаляем коллайдер примитива (добавим свой)
            Destroy(primitive.GetComponent<Collider>());
            
            // Устанавливаем материал
            primitive.GetComponent<Renderer>().material = material;
        }
        else
        {
            // Используем копию оригинального меша
            MeshFilter mf = fragment.AddComponent<MeshFilter>();
            MeshRenderer mr = fragment.AddComponent<MeshRenderer>();
            mf.sharedMesh = originalMesh;
            mr.material = material;
        }
        
        // Добавляем коллайдер (упрощенный для производительности)
        Collider collider;
        if (usePrimitiveFragments)
        {
            // Используем коллайдер соответствующий примитиву
            switch (fragmentPrimitiveType)
            {
                case PrimitiveType.Sphere:
                    collider = fragment.AddComponent<SphereCollider>();
                    break;
                case PrimitiveType.Capsule:
                    collider = fragment.AddComponent<CapsuleCollider>();
                    break;
                default:
                    collider = fragment.AddComponent<BoxCollider>();
                    break;
            }
        }
        else
        {
            collider = fragment.AddComponent<BoxCollider>();
        }
        
        // Добавляем физику
        Rigidbody rb = fragment.AddComponent<Rigidbody>();
        rb.mass = originalMass * fragmentMassMultiplier;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Добавляем компонент для автоматического затухания
        SimpleFragment fragmentComponent = fragment.AddComponent<SimpleFragment>();
        fragmentComponent.Initialize(rb);
        
        return fragment;
    }

    /// <summary>
    /// Применяет силу взрыва к осколку
    /// </summary>
    private void ApplyExplosionForce(GameObject fragment, float force, Vector3 explosionCenter, Vector3 objectCenter)
    {
        Rigidbody rb = fragment.GetComponent<Rigidbody>();
        if (rb == null) return;
        
        // Направление от центра взрыва к осколку
        Vector3 directionFromExplosion = (fragment.transform.position - explosionCenter).normalized;
        
        // Направление от центра объекта (для дополнительного разлета)
        Vector3 directionFromCenter = (fragment.transform.position - objectCenter).normalized;
        
        // Комбинированное направление
        Vector3 finalDirection = (directionFromExplosion + directionFromCenter).normalized;
        
        // Добавляем немного вертикальной составляющей
        finalDirection += Vector3.up * 0.3f;
        finalDirection.Normalize();
        
        // Применяем силу
        rb.AddForce(finalDirection * force, ForceMode.Impulse);
        
        // Добавляем вращение
        if (addRandomRotation)
        {
            Vector3 randomTorque = Random.insideUnitSphere * rotationForce;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Создать простые примитивные осколки (альтернативный метод)
    /// </summary>
    public void DestroyObjectSimple(GameObject objectToDestroy, int fragmentCount, float explosionForce,
        Vector3 explosionCenter, Transform parentTransform = null, float destroyAfter = 3f)
    {
        if (objectToDestroy == null) return;

        Renderer renderer = objectToDestroy.GetComponent<Renderer>();
        Rigidbody originalRb = objectToDestroy.GetComponent<Rigidbody>();

        if (renderer == null)
        {
            Debug.LogWarning($"Объект {objectToDestroy.name} не имеет Renderer!");
            return;
        }

        Vector3 objectPosition = objectToDestroy.transform.position;
        Vector3 objectScale = objectToDestroy.transform.localScale;
        Bounds objectBounds = renderer.bounds;
        Material objectMaterial = renderer.material;
        float objectMass = originalRb != null ? originalRb.mass : 1f;

        // Вычисляем единый размер осколка
        float calculatedFragmentSize = CalculateFragmentSize(objectScale, fragmentCount);
        float fragmentSize = calculatedFragmentSize * Mathf.Min(objectBounds.size.x, objectBounds.size.y, objectBounds.size.z);

        // Создаем простые примитивные осколки
        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(fragmentPrimitiveType);
            fragment.name = $"{objectToDestroy.name}_{fragmentPrimitiveType}Fragment_{i}";

            // Случайная позиция
            Vector3 randomOffset = new Vector3(
                Random.Range(-objectBounds.extents.x, objectBounds.extents.x),
                Random.Range(-objectBounds.extents.y, objectBounds.extents.y),
                Random.Range(-objectBounds.extents.z, objectBounds.extents.z)
            ) * fragmentSpreadRadius;

            fragment.transform.position = objectPosition + randomOffset;
            fragment.transform.rotation = Random.rotation;

            // Одинаковый размер для всех осколков
            fragment.transform.localScale = Vector3.one * fragmentSize;

            // Материал
            fragment.GetComponent<Renderer>().material = objectMaterial;

            // Физика
            Rigidbody rb = fragment.AddComponent<Rigidbody>();
            rb.mass = objectMass * fragmentMassMultiplier;
            rb.useGravity = true;

            // Родитель
            if (parentTransform != null)
            {
                fragment.transform.SetParent(parentTransform);
            }

            // Сила разлета
            ApplyExplosionForce(fragment, explosionForce, explosionCenter, objectPosition);

            // Компонент осколка
            SimpleFragment fragmentComponent = fragment.AddComponent<SimpleFragment>();
            fragmentComponent.Initialize(rb);

            // Удаление
            Destroy(fragment, destroyAfter);
        }

        Debug.Log($"[SimpleDestruction] Создано {fragmentCount} {fragmentPrimitiveType} осколков для {objectToDestroy.name}");
        
        OnObjectDestroyed?.Invoke(objectToDestroy, fragmentCount);
    }

    // Геттеры для настроек (могут быть полезны)
    public float FragmentSizeMultiplier => fragmentSizeMultiplier;
    public float FragmentSpreadRadius => fragmentSpreadRadius;
    public bool UniformFragmentSize => uniformFragmentSize;
    public bool UsePrimitiveFragments => usePrimitiveFragments;
    public PrimitiveType FragmentPrimitiveType => fragmentPrimitiveType;

    /// <summary>
    /// Установить настройки программно
    /// </summary>
    public void SetFragmentSettings(float sizeMultiplier, float spreadRadius, bool uniform = true)
    {
        fragmentSizeMultiplier = Mathf.Clamp(sizeMultiplier, 0.05f, 0.8f);
        fragmentSpreadRadius = Mathf.Clamp(spreadRadius, 0.1f, 2f);
        uniformFragmentSize = uniform;
    }

    /// <summary>
    /// Установить настройки физики программно
    /// </summary>
    public void SetPhysicsSettings(float massMultiplier, float rotationForce)
    {
        fragmentMassMultiplier = Mathf.Clamp(massMultiplier, 0.01f, 1f);
        this.rotationForce = Mathf.Clamp(rotationForce, 0f, 20f);
    }
}

