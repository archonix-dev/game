using UnityEngine;

/// <summary>
/// Компонент для объектов которые могут быть разрушены после определенного количества ударов
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DestructibleObject : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private DestructibleObjectData objectData;
    
    private int currentHits = 0;
    private Rigidbody rb;
    private AudioSource audioSource;
    private MeshFilter meshFilter;
    private Renderer objectRenderer;
    
    // Для предотвращения множественных ударов в один кадр
    private float lastHitTime = 0f;
    private float hitCooldown = 0.1f;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        meshFilter = GetComponent<MeshFilter>();
        objectRenderer = GetComponent<Renderer>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D звук
            audioSource.playOnAwake = false;
        }
    }
    
    void Start()
    {
        if (objectData == null)
        {
            Debug.LogWarning($"DestructibleObject на {gameObject.name} не имеет назначенного DestructibleObjectData!");
        }
    }
    
    /// <summary>
    /// Вызывается когда объект получает удар
    /// </summary>
    public void TakeHit(float impactForce, Vector3 impactPoint, Vector3 impactDirection)
    {
        if (objectData == null) return;
        
        // Проверка cooldown
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;
        
        // Проверяем минимальную силу удара
        if (impactForce < objectData.MinimumImpactForce)
        {
            Debug.Log($"Удар слишком слабый: {impactForce} < {objectData.MinimumImpactForce}");
            return;
        }
        
        currentHits++;
        Debug.Log($"{gameObject.name} получил удар! {currentHits}/{objectData.HitsToDestroy} (сила: {impactForce})");
        
        // Визуальные эффекты
        PlayHitEffect(impactPoint);
        
        // Звук удара
        if (objectData.HitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(objectData.HitSound);
        }
        
        // Проверка на разрушение
        if (currentHits >= objectData.HitsToDestroy)
        {
            DestroyObject(impactPoint, impactDirection, impactForce);
        }
    }
    
    /// <summary>
    /// Разрушает объект и выдает награды
    /// </summary>
    private void DestroyObject(Vector3 destructionPoint, Vector3 direction, float force)
    {
        Debug.Log($"{gameObject.name} разрушен!");
        
        // ВАЖНО: Сразу отключаем визуальное представление и коллайдеры
        // чтобы объект исчез визуально даже если уничтожение займет время
        DisableObjectVisually();
        
        // Воспроизводим звук разрушения
        if (objectData.DestroySound != null)
        {
            // Создаем временный объект для звука
            GameObject soundObject = new GameObject("DestroySound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = objectData.DestroySound;
            tempAudio.spatialBlend = 1f;
            tempAudio.Play();
            Destroy(soundObject, objectData.DestroySound.length);
        }
        
        // Визуальный эффект разрушения
        if (objectData.DestroyEffectPrefab != null)
        {
            GameObject effect = Instantiate(objectData.DestroyEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }
        
        // Выдаем монеты
        int coinsToGive = objectData.GetRandomCoinAmount();
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.AddCoins(coinsToGive);
        }
        
        // Реалистичное разрушение
        if (objectData.UseRealisticDestruction && meshFilter != null && meshFilter.mesh != null)
        {
            if (SimpleDestructionManager.Instance != null)
            {
                DestroyWithSimpleDestruction(destructionPoint, force);
            }
            else
            {
                Debug.LogWarning($"SimpleDestructionManager не найден! Используем базовое разрушение для {gameObject.name}");
                CreateSimpleFragments(destructionPoint, direction);
                Destroy(gameObject, 3f);
            }
        }
        else
        {
            // Простое разрушение - создаем осколки как дочерние объекты
            CreateSimpleFragments(destructionPoint, direction);
            
            // Удаляем весь родительский объект вместе со всеми дочерними осколками через 3 секунды
            Destroy(gameObject, 3f);
        }
    }
    
    /// <summary>
    /// Отключает визуальное представление объекта (рендерер, коллайдеры)
    /// </summary>
    private void DisableObjectVisually()
    {
        // Отключаем рендерер
        if (objectRenderer != null)
        {
            objectRenderer.enabled = false;
        }
        
        // Отключаем все коллайдеры
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }
    
    /// <summary>
    /// Использует SimpleDestructionManager для разрушения (РЕКОМЕНДУЕТСЯ!)
    /// </summary>
    private void DestroyWithSimpleDestruction(Vector3 destructionPoint, float impactForce)
    {
        // Вычисляем количество осколков из настроек ScriptableObject
        int fragmentCount = objectData.CalculateShatterAmount(impactForce);
        
        // Используем SimpleDestructionManager
        SimpleDestructionManager.Instance.DestroyObject(
            gameObject,
            fragmentCount,
            objectData.FragmentExplosionForce,
            destructionPoint,
            transform,
            3f
        );
        
        Debug.Log($"[РАЗРУШЕНИЕ] SimpleDestruction применен к {gameObject.name} после {currentHits} ударов. " +
                 $"Количество осколков: {fragmentCount}, сила разлета: {objectData.FragmentExplosionForce}. " +
                 $"Осколки будут дочерними и удалятся через 3 секунды.");
        
        // Уничтожаем оригинальный объект (с дочерними осколками) через 3 секунды
        Destroy(gameObject, 3f);
    }
    
    /// <summary>
    /// Создает простые осколки если MeshSlicer не используется
    /// ОСКОЛКИ СОЗДАЮТСЯ КАК ДОЧЕРНИЕ ОБЪЕКТЫ, затем отсоединяются для физики
    /// </summary>
    private void CreateSimpleFragments(Vector3 position, Vector3 direction)
    {
        int fragmentCount = Random.Range(5, 10);
        Debug.Log($"[РАЗРУШЕНИЕ] Создаем {fragmentCount} осколков как дочерние объекты {gameObject.name}");
        
        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = $"{gameObject.name}_Fragment_{i}";
            
            // Сначала делаем дочерним для организации иерархии
            fragment.transform.SetParent(transform);
            
            fragment.transform.position = position + Random.insideUnitSphere * 0.5f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);
            fragment.transform.rotation = Random.rotation;
            
            // Копируем материал
            if (objectRenderer != null)
            {
                Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
                fragmentRenderer.material = objectRenderer.material;
            }
            
            // Добавляем физику
            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.mass = rb.mass / fragmentCount;
            fragmentRb.useGravity = true;
            
            // ВАЖНО: Отсоединяем от родителя для корректной работы физики
            // Но сохраняем мировые координаты
            fragment.transform.SetParent(transform, true);
            
            // Добавляем силу от точки удара
            Vector3 explosionDir = (fragment.transform.position - position).normalized;
            explosionDir += direction.normalized;
            fragmentRb.AddForce(explosionDir * objectData.FragmentExplosionForce, ForceMode.Impulse);
            fragmentRb.AddTorque(Random.insideUnitSphere * objectData.FragmentExplosionForce, ForceMode.Impulse);
        }
        
        Debug.Log($"[РАЗРУШЕНИЕ] Создано {fragmentCount} осколков. Весь объект {gameObject.name} (включая осколки) будет удален через 3 секунды");
    }
    
    /// <summary>
    /// Воспроизводит эффект удара
    /// </summary>
    private void PlayHitEffect(Vector3 position)
    {
        if (objectData.HitEffectPrefab != null)
        {
            GameObject effect = Instantiate(objectData.HitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    /// <summary>
    /// Получить текущее количество ударов
    /// </summary>
    public int GetCurrentHits() => currentHits;
    
    /// <summary>
    /// Получить оставшееся количество ударов до разрушения
    /// </summary>
    public int GetRemainingHits()
    {
        if (objectData == null) return 0;
        return Mathf.Max(0, objectData.HitsToDestroy - currentHits);
    }
    
    /// <summary>
    /// Сбросить счетчик ударов
    /// </summary>
    public void ResetHits()
    {
        currentHits = 0;
    }
    
    void OnDrawGizmosSelected()
    {
        if (objectData != null)
        {
            // Отображаем информацию об объекте
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, 
                $"Прочность: {currentHits}/{objectData.HitsToDestroy}\nМонеты: {objectData.MinCoins}-{objectData.MaxCoins}");
            #endif
        }
    }
}

