using UnityEngine;
using TMPro;
using Mirror;

/// <summary>
/// Компонент для объектов которые могут быть захвачены и разрушены после определенного количества ударов
/// 
/// ТРЕБОВАНИЯ ДЛЯ МУЛЬТИПЛЕЕРА:
/// - GameObject должен иметь компонент NetworkIdentity
/// - GameObject должен иметь компонент NetworkTransformReliable или NetworkTransformHybrid для синхронизации позиции/ротации
///   (для физических объектов рекомендуется NetworkTransformReliable с updateMethod = FixedUpdate)
/// - GameObject должен иметь компонент NetworkDestructibleObject для синхронизации состояния разрушения
/// 
/// Примечание: LobbyNetworkManager автоматически добавляет NetworkTransformReliable при регистрации объектов,
/// но для префабов лучше указать компоненты заранее.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DestructibleObject : MonoBehaviour
{
    [Header("Настройки разрушения")]
    [SerializeField] public DestructibleObjectData objectData;
    
    [Header("Свойства захвата")]
    [Tooltip("Вес объекта в килограммах")]
    [SerializeField] public float objectWeight = 5f;
    
    [Tooltip("Насколько крепко можно держать объект (0-1)")]
    [SerializeField] private float gripStrength = 1f;
    
    [Tooltip("Центр масс объекта для реалистичной физики (в локальных координатах). Если Vector3.zero, будет использован центр меша")]
    [SerializeField] private Vector3 centerOfMass = Vector3.zero;
    
    [Tooltip("Автоматически вычислять центр масс из меша (если включено, centerOfMass будет проигнорирован)")]
    [SerializeField] private bool autoCalculateCenterOfMass = false;
    
    [Header("Хрупкость")]
    [SerializeField] private bool isFragile = false;
    [SerializeField] private float breakForceThreshold = 100f;
    
    [Header("Звуки захвата (опционально)")]
    [Tooltip("AudioSource для воспроизведения звуков (если не указан, будет создан автоматически)")]
    [SerializeField] private AudioSource grabAudioSource;
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip releaseSound;
    
    [Header("Эффекты первого поднятия")]
    [Tooltip("ParticleSystem, который появится при первом поднятии предмета (уже должен быть в префабе)")]
    [SerializeField] private ParticleSystem firstGrabParticleSystem;
    [Tooltip("AudioSource для звука первого поднятия (уже должен быть в префабе)")]
    [SerializeField] private AudioSource firstGrabAudioSource;
    
    [Header("Отображение награды")]
    [SerializeField] private bool showRewardDisplay = true;
    [SerializeField] private float displayDistance = 5f;
    [SerializeField] private TextMeshPro rewardText;
    [SerializeField] private GameObject rewardDisplayObject;
    
    [Header("Настройки масштабирования")]
    [SerializeField] private float minScale = 0.01f;  // Минимальный размер (близко к объекту)
    [SerializeField] private float maxScale = 0.05f;  // Максимальный размер (далеко от объекта)
    [SerializeField] private float scaleSmoothTime = 0.1f; // Время плавного изменения размера
    
    [Header("Настройки фрагментов")]
    [Tooltip("Цвет фрагментов при разрушении")]
    [SerializeField] private Color fragmentColor = Color.white;
    
    [Header("Визуальные изменения при уроне")]
    [Tooltip("Меши для каждого полученного удара. Кол-во = hitsToDestroy")]
    [SerializeField] private Mesh[] damagedMeshes;
    [Tooltip("Точки спавна частиц для каждого состояния повреждения (по порядку damagedMeshes)")]
    [SerializeField] private Transform[] damageStageHitPoints;
    [Tooltip("Система частиц, появляющаяся в точке удара")]
    [SerializeField] private ParticleSystem hitParticleSystemPrefab;
    [Tooltip("Цвет, в который перекрашиваем частицы при ударе")]
    [SerializeField] private Color hitParticleColor = Color.white;
    
    private int currentHits = 0;
    private Rigidbody rb;
    private AudioSource audioSource;
    [Header("Смена меша при уроне")]
    [Tooltip("Произвольный объект с MeshFilter, который будет менять меш при уроне")]
    [SerializeField] private MeshFilter targetMeshFilter;
    [Tooltip("Рендерер, для которого нужно отключать отображение при разрушении (если отличается от MeshFilter)")]
    [SerializeField] private Renderer targetRenderer;
    
    private MeshFilter meshFilter;
    private Renderer objectRenderer;
    private MeshCollider meshCollider;
    private Mesh defaultMesh;
    
    // Для предотвращения множественных ударов в один кадр
    private float lastHitTime = 0f;
    private float hitCooldown = 0.1f;
    
    // Для захвата объектов
    private bool isGrabbed = false;
    private bool wasGrabbedBefore = false; // Флаг первого поднятия
    private float originalMass;
    private float originalDrag;
    private float originalAngularDrag;
    
    // Для отслеживания столкновений
    private Vector3 lastVelocity;
    
    // Для отображения награды
    private Transform playerTransform;
    private bool isPlayerInRange = false;
    private bool isPlayerLookingAt = false;
    private float currentScale = 0.01f;
    private float scaleVelocity = 0f;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        meshFilter = targetMeshFilter != null ? targetMeshFilter : GetComponent<MeshFilter>();
        objectRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
        
        CacheDefaultMesh();
        
        // Инициализируем MeshCollider
        InitializeMeshCollider();
        
        // Настраиваем Rigidbody на основе веса
        if (rb != null)
        {
            originalMass = objectWeight;
            
            // Настраиваем центр масс
            if (autoCalculateCenterOfMass && meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Вычисляем центр масс из меша
                rb.centerOfMass = CalculateCenterOfMassFromMesh(meshFilter.sharedMesh);
            }
            else if (centerOfMass != Vector3.zero)
            {
                // Используем заданный центр масс
                rb.centerOfMass = centerOfMass;
            }
            // Если centerOfMass == Vector3.zero и autoCalculateCenterOfMass == false,
            // Unity использует центр по умолчанию (что обычно нормально)
            
            originalDrag = rb.linearDamping;
            originalAngularDrag = rb.angularDamping;
            
            // Обновляем вес с учетом текущего урона (если объект уже был поврежден)
            UpdateWeightBasedOnDamage();
        }
        
        // Настраиваем аудио компонент (только если не указан в инспекторе)
        if (grabAudioSource == null)
        {
            grabAudioSource = GetComponent<AudioSource>();
            if (grabAudioSource == null)
            {
                grabAudioSource = gameObject.AddComponent<AudioSource>();
                grabAudioSource.spatialBlend = 1f; // 3D звук
                grabAudioSource.playOnAwake = false;
                grabAudioSource.volume = 0.5f;
            }
        }
        
        // Сохраняем старую ссылку для обратной совместимости
        if (audioSource == null)
        {
            audioSource = grabAudioSource;
        }
    }
    
    void Start()
    {
        if (gameObject.tag == "Untagged")
        {
            gameObject.tag = "Grabbable";
        }
        InitializeRewardDisplay();
        firstGrabParticleSystem.gameObject.SetActive(false);
    }
    
    void Update()
    {
        // Обновляем отображение награды
        UpdateRewardDisplay();
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
        // Проверяем теги объектов - не получаем урон от объектов с тегами Item и Grabbable
        if (collision.gameObject.CompareTag("Item") || collision.gameObject.CompareTag("Grabbable"))
        {
            return; // Выходим из метода, не обрабатывая урон
        }
        
        // Вычисляем силу удара с учетом относительной скорости
        Vector3 relativeVelocity = lastVelocity;
        
        // Если у другого объекта есть Rigidbody, учитываем его скорость
        Rigidbody otherRb = collision.rigidbody;
        if (otherRb != null)
        {
            relativeVelocity -= otherRb.linearVelocity;
        }
        
        float impactForce = relativeVelocity.magnitude * rb.mass;
        
        // Получаем информацию о точке и направлении удара
        Vector3 impactPoint = collision.contacts[0].point;
        Vector3 impactDirection = collision.contacts[0].normal;
        
        // Проверяем столкновение с другим разрушаемым объектом
        DestructibleObject otherDestructible = collision.gameObject.GetComponent<DestructibleObject>();
        if (otherDestructible != null)
        {
            // Передаем информацию об ударе другому разрушаемому объекту
            otherDestructible.TakeHit(impactForce, impactPoint, impactDirection, gameObject);
        }
        
        // ВАЖНО: Этот объект тоже получает урон от столкновения с любыми объектами
        // (включая обычные объекты без DestructibleObject компонента)
        if (objectData != null && impactForce > 0)
        {
            // Проверяем минимальную силу удара для получения урона
            if (impactForce >= objectData.MinimumImpactForce)
            {
                // Дополнительная проверка: не получаем урон если объект захвачен и удар очень слабый
                if (!isGrabbed || impactForce >= objectData.MinimumImpactForce * 2f)
                {
                    TakeHit(impactForce, impactPoint, impactDirection, collision.gameObject);
                }
            }
        }
        
        // Если этот объект хрупкий и сила удара велика - ломаем его
        if (!isGrabbed && isFragile && impactForce > breakForceThreshold)
        {
            BreakObject(impactPoint);
        }
    }
    
    /// <summary>
    /// Вызывается когда объект получает удар
    /// </summary>
    public void TakeHit(float impactForce, Vector3 impactPoint, Vector3 impactDirection, GameObject sourceObject = null)
    {
        if (objectData == null) return;
        
        // Дополнительная проверка: не получаем урон от объектов с тегами Item и Grabbable
        if (sourceObject != null && (sourceObject.CompareTag("Item") || sourceObject.CompareTag("Grabbable")))
        {
            return;
        }
        
        // Проверка cooldown
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;
        
        // Проверяем минимальную силу удара
        if (impactForce < objectData.MinimumImpactForce)
        {
            return;
        }
        
        // Проверяем, есть ли NetworkDestructibleObject для синхронизации в мультиплеере
        NetworkDestructibleObject networkDestructible = GetComponent<NetworkDestructibleObject>();
        if (networkDestructible != null && (NetworkServer.active || NetworkClient.active))
        {
            // Используем сетевую синхронизацию
            networkDestructible.TakeHitNetworked(impactForce, impactPoint, impactDirection, sourceObject);
            return; // NetworkDestructibleObject обработает удар
        }
        
        // Локальная обработка (для одиночной игры или объектов без NetworkIdentity)
        int hits = IncrementHitCount();
        
        // Визуальные эффекты и звук
        PlayHitFeedback(impactPoint, impactDirection);
        
        // Проверка на разрушение
        if (hits >= objectData.HitsToDestroy)
        {
            DestroyObject(impactPoint, impactDirection, impactForce);
        }
    }
    
    /// <summary>
    /// Вызывается когда объект захватывается
    /// </summary>
    public void OnGrabbed()
    {
        isGrabbed = true;
        
        // Скрываем отображение награды при захвате
        HideRewardDisplay();
        
        // Проверяем, первый ли раз поднимаем предмет
        if (!wasGrabbedBefore)
        {
            wasGrabbedBefore = true;
            
            // Показываем ParticleSystem при первом поднятии
            if (firstGrabParticleSystem != null)
            {
                firstGrabParticleSystem.gameObject.SetActive(true);
                firstGrabParticleSystem.Play();
                
                // Скрываем через 3 секунды
                StartCoroutine(HideFirstGrabParticlesAfterDelay(3f));
            }
            
            // Воспроизводим звук первого поднятия
            if (firstGrabAudioSource != null)
            {
                firstGrabAudioSource.Play();
            }
        }
        
        // Воспроизводим звук захвата
        if (grabAudioSource != null && grabSound != null)
        {
            grabAudioSource.PlayOneShot(grabSound);
        }
    }
    
    /// <summary>
    /// Вызывается когда объект отпускается
    /// </summary>
    public void OnReleased()
    {
        isGrabbed = false;
        
        // Воспроизводим звук отпускания
        if (grabAudioSource != null && releaseSound != null)
        {
            grabAudioSource.PlayOneShot(releaseSound);
        }
        
        // Восстанавливаем оригинальные физические параметры
        if (rb != null)
        {
            rb.linearDamping = originalDrag;
            rb.angularDamping = originalAngularDrag;
        }
    }
    
    /// <summary>
    /// Скрывает ParticleSystem первого поднятия через указанное время
    /// </summary>
    private System.Collections.IEnumerator HideFirstGrabParticlesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (firstGrabParticleSystem != null)
        {
            firstGrabParticleSystem.Stop();
            firstGrabParticleSystem.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Разрушает хрупкий объект при сильном ударе
    /// </summary>
    void BreakObject(Vector3 impactPoint)
    {
        
        // Отключаем визуальное представление сразу
        DisableObjectVisually();
        
        // Воспроизводим звук разрушения
        if (audioSource != null && objectData != null && objectData.DestroySound != null)
        {
            // Создаем временный объект для звука, чтобы он доиграл после уничтожения объекта
            GameObject soundObject = new GameObject("BreakSound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = objectData.DestroySound;
            tempAudio.spatialBlend = 1f;
            tempAudio.Play();
            Destroy(soundObject, objectData.DestroySound.length);
        }
        
        // Проверяем, есть ли NetworkIdentity для синхронизации в мультиплеере
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        bool isNetworked = networkIdentity != null && networkIdentity.netId != 0;
        
        // Пытаемся использовать SimpleDestructionManager для реалистичного разрушения
        if (TryUseMeshCutter(impactPoint))
        {
            // SimpleDestructionManager успешно применен, уничтожаем объект с задержкой
            // В мультиплеере NetworkDestructibleObject уничтожит объект через NetworkServer
            if (!isNetworked)
            {
                Destroy(gameObject, 0.1f);
            }
        }
        else
        {
            // Используем простое разрушение
            CreateBreakEffect(impactPoint);
            // В мультиплеере NetworkDestructibleObject уничтожит объект через NetworkServer
            if (!isNetworked)
            {
                Destroy(gameObject);
            }
        }
    }
    
    /// <summary>
    /// Разрушает объект и выдает награды
    /// </summary>
    public void DestroyObject(Vector3 destructionPoint, Vector3 direction, float force)
    {
        
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
        
        // Выдаем монеты ближайшему игроку
        int coinsToGive = objectData.GetCoinAmount();
        if (coinsToGive > 0)
        {
            GiveCoinsToNearestPlayer(coinsToGive);
        }
        
        // Проверяем, есть ли NetworkIdentity для синхронизации в мультиплеере
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        bool isNetworked = networkIdentity != null && networkIdentity.netId != 0;
        
        // Реалистичное разрушение
        if (objectData.UseRealisticDestruction && meshFilter != null && meshFilter.mesh != null)
        {
            if (SimpleDestructionManager.Instance != null)
            {
                DestroyWithSimpleDestruction(destructionPoint, force);
            }
            else
            {
                CreateSimpleFragments(destructionPoint, direction);
                // В мультиплеере NetworkDestructibleObject уничтожит объект через NetworkServer
                if (!isNetworked)
                {
                    Destroy(gameObject, 3f);
                }
            }
        }
        else
        {
            // Простое разрушение - создаем осколки как дочерние объекты
            CreateSimpleFragments(destructionPoint, direction);
            
            // В мультиплеере NetworkDestructibleObject уничтожит объект через NetworkServer
            if (!isNetworked)
            {
                // Удаляем весь родительский объект вместе со всеми дочерними осколками через 3 секунды
                Destroy(gameObject, 3f);
            }
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
        
        // Используем SimpleDestructionManager с цветом фрагментов
        SimpleDestructionManager.Instance.DestroyObjectWithColor(
            gameObject,
            fragmentCount,
            objectData.FragmentExplosionForce,
            destructionPoint,
            transform,
            fragmentColor,
            3f
        );
        
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
        
        Material fragmentMaterial = CreateFragmentMaterial();
        
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
            if (fragmentMaterial != null)
            {
                Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
                if (fragmentRenderer != null)
                {
                    fragmentRenderer.sharedMaterial = fragmentMaterial;
                }
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
    /// Пытается использовать SimpleDestructionManager для реалистичного разрушения хрупких объектов
    /// </summary>
    bool TryUseMeshCutter(Vector3 impactPoint)
    {
        // Проверяем наличие необходимых компонентов
        if (meshFilter == null || meshFilter.mesh == null)
        {
            return false;
        }
        
        // Проверяем наличие SimpleDestructionManager
        if (SimpleDestructionManager.Instance == null)
        {
            return false;
        }
        
        try
        {
            // Вычисляем силу удара
            float impactForce = lastVelocity.magnitude * rb.mass;
            
            // Используем SimpleDestructionManager
            int fragmentCount = Mathf.RoundToInt(impactForce * 0.5f);
            fragmentCount = Mathf.Clamp(fragmentCount, 5, 15);
            
            SimpleDestructionManager.Instance.DestroyObjectWithColor(
                gameObject,
                fragmentCount,
                impactForce * 0.5f,
                transform.position,
                null,
                fragmentColor,
                3f
            );
            
            return true;
        }
        catch (System.Exception e)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Создает простые осколки (fallback если SimpleDestructionManager недоступен)
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
            
            // Устанавливаем цвет фрагмента
            Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
            if (fragmentRenderer != null)
            {
                // Создаем простой материал с цветом для URP
                Material fragmentMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fragmentMaterial.color = fragmentColor;
                fragmentRenderer.material = fragmentMaterial;
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
    
    // Методы для настройки объекта извне
    /// <summary>
    /// Установить вес объекта (базовый вес без учета урона)
    /// </summary>
    public void SetWeight(float weight)
    {
        objectWeight = weight;
        originalMass = weight;
        
        // Обновляем вес с учетом текущего урона
        UpdateWeightBasedOnDamage();
    }
    
    /// <summary>
    /// Получить силу захвата
    /// </summary>
    public float GetGripStrength()
    {
        return gripStrength;
    }
    
    /// <summary>
    /// Проверить, захвачен ли объект в данный момент
    /// </summary>
    public bool IsCurrentlyGrabbed()
    {
        return isGrabbed;
    }
    
    /// <summary>
    /// Сбросить счетчик ударов
    /// </summary>
    public void ResetHits()
    {
        currentHits = 0;
        
        RestoreDefaultMesh();
        UpdateWeightBasedOnDamage();
    }
    
    /// <summary>
    /// Устанавливает состояние наведения игрока на объект
    /// </summary>
    public void SetPlayerLookingAt(bool looking)
    {
        isPlayerLookingAt = looking;
    }
    
    /// <summary>
    /// Проверяет, смотрит ли игрок на объект
    /// </summary>
    public bool IsPlayerLookingAt()
    {
        return isPlayerLookingAt;
    }
    
    /// <summary>
    /// Принудительно обновляет текст награды
    /// </summary>
    public void RefreshRewardText()
    {
        UpdateRewardText();
    }
    
    /// <summary>
    /// Получить цвет фрагментов
    /// </summary>
    public Color GetFragmentColor()
    {
        return fragmentColor;
    }
    
    /// <summary>
    /// Инициализирует систему отображения награды
    /// </summary>
    void InitializeRewardDisplay()
    {
        FindPlayer();
        
        // Создаем отображение награды если не назначено
        if (rewardDisplayObject == null)
        {
            CreateRewardDisplay();
        }
        
        // Скрываем отображение по умолчанию
        HideRewardDisplay();
    }
    
    /// <summary>
    /// Ищет игрока в сцене
    /// </summary>
    void FindPlayer()
    {
        // Ищем игрока по тегу
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            // Ищем камеру как альтернативу
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerTransform = mainCamera.transform;
            }
        }
    }
    
    /// <summary>
    /// Выдает монеты ближайшему игроку
    /// </summary>
    void GiveCoinsToNearestPlayer(int coins)
    {
        // Ищем всех игроков в сцене
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0)
        {
            // Если нет игроков по тегу, пытаемся найти через NetworkClient
            if (NetworkClient.localPlayer != null)
            {
                CoinManager coinManager = NetworkClient.localPlayer.GetComponent<CoinManager>();
                if (coinManager != null)
                {
                    coinManager.AddCoins(coins);
                    return;
                }
            }
            return;
        }
        
        // Находим ближайшего игрока
        GameObject nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPlayer = player;
            }
        }
        
        // Выдаем монеты ближайшему игроку
        if (nearestPlayer != null)
        {
            CoinManager coinManager = nearestPlayer.GetComponent<CoinManager>();
            if (coinManager != null)
            {
                coinManager.AddCoins(coins);
            }
        }
    }
    
    /// <summary>
    /// Создает отображение награды
    /// </summary>
    void CreateRewardDisplay()
    {
        // Создаем GameObject для отображения награды
        rewardDisplayObject = new GameObject("RewardDisplay");
        rewardDisplayObject.transform.SetParent(transform);
        rewardDisplayObject.transform.localPosition = Vector3.up * 2f; // Над объектом
        rewardDisplayObject.transform.localScale = Vector3.one * minScale; // Устанавливаем начальный размер
        
        // Добавляем TextMeshPro
        rewardText = rewardDisplayObject.AddComponent<TextMeshPro>();
        rewardText.fontSize = 2f;
        rewardText.color = Color.yellow;
        rewardText.alignment = TextAlignmentOptions.Center;
        rewardText.sortingOrder = 10;
        
        // Устанавливаем текст награды
        rewardText.text = GetRewardText();
        
        // Настраиваем шрифт
        rewardText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (rewardText.font == null)
        {
            // Используем стандартный шрифт если не найден
            rewardText.font = Resources.GetBuiltinResource<TMP_FontAsset>("Legacy Runtime/TextMeshPro/Fonts & Materials/LiberationSans SDF");
        }
        
    }
    
    /// <summary>
    /// Обновляет отображение награды
    /// </summary>
    void UpdateRewardDisplay()
    {
        if (!showRewardDisplay || objectData == null || isGrabbed) return;
        
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }
        
        // Проверяем расстояние до игрока
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = distanceToPlayer <= displayDistance;
        
        // Показываем только если игрок в радиусе И смотрит на объект
        bool shouldShow = inRange && isPlayerLookingAt;
        
        if (shouldShow != isPlayerInRange)
        {
            isPlayerInRange = shouldShow;
            
            if (isPlayerInRange)
            {
                ShowRewardDisplay();
            }
            else
            {
                HideRewardDisplay();
            }
        }
        
        // Поворачиваем текст к игроку и обновляем размер
        if (isPlayerInRange && rewardDisplayObject != null)
        {
            rewardDisplayObject.transform.LookAt(playerTransform);
            // Поворачиваем на 180 градусов чтобы текст был читаемым
            rewardDisplayObject.transform.Rotate(0, 180, 0);
            
            // Обновляем размер в зависимости от расстояния
            UpdateRewardScale(distanceToPlayer);
            
            // Обновляем текст награды
            UpdateRewardText();
        }
    }
    
    /// <summary>
    /// Обновляет размер отображения награды в зависимости от расстояния до игрока
    /// </summary>
    void UpdateRewardScale(float distanceToPlayer)
    {
        if (rewardDisplayObject == null) return;
        
        // Вычисляем целевой размер на основе расстояния
        // Чем дальше игрок, тем больше размер (но в пределах displayDistance)
        float normalizedDistance = Mathf.Clamp01(distanceToPlayer / displayDistance);
        
        // Инвертируем: далеко = большой размер, близко = маленький размер
        float targetScale = Mathf.Lerp(minScale, maxScale, normalizedDistance);
        
        // Плавно изменяем размер
        currentScale = Mathf.SmoothDamp(currentScale, targetScale, ref scaleVelocity, scaleSmoothTime);
        
        // Применяем размер
        rewardDisplayObject.transform.localScale = Vector3.one * currentScale;
    }
    
    /// <summary>
    /// Обновляет текст награды
    /// </summary>
    void UpdateRewardText()
    {
        if (rewardText != null)
        {
            string newText = GetRewardText();
            if (rewardText.text != newText)
            {
                rewardText.text = newText;
            }
        }
    }
    
    /// <summary>
    /// Показывает отображение награды
    /// </summary>
    void ShowRewardDisplay()
    {
        if (rewardDisplayObject != null)
        {
            rewardDisplayObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Скрывает отображение награды
    /// </summary>
    void HideRewardDisplay()
    {
        if (rewardDisplayObject != null)
        {
            rewardDisplayObject.SetActive(false);
            // Сбрасываем размер к минимальному при скрытии
            rewardDisplayObject.transform.localScale = Vector3.one * minScale;
            currentScale = minScale;
            scaleVelocity = 0f;
        }
        isPlayerInRange = false;
    }
    
    /// <summary>
    /// Получает текст награды
    /// </summary>
    string GetRewardText()
    {
        if (objectData == null) 
        {
            return CurrencyFormatter.FormatBits(0);
        }
        
        int coins = objectData.CoinAmount;
        return CurrencyFormatter.FormatBits(coins);
    }
    
    void ApplyDamageMeshState()
    {
        if (meshFilter == null || damagedMeshes == null || damagedMeshes.Length == 0)
        {
            return;
        }
        
        int meshIndex = Mathf.Clamp(currentHits, 0, damagedMeshes.Length - 1);
        Mesh targetMesh = damagedMeshes[meshIndex];
        if (targetMesh != null)
        {
            meshFilter.sharedMesh = targetMesh;
            
            // Обновляем MeshCollider при изменении меша
            UpdateMeshCollider(targetMesh);
        }
    }
    
    /// <summary>
    /// Синхронизирует количество ударов и применяет соответствующий меш
    /// </summary>
    public void SyncHitState(int hits)
    {
        currentHits = Mathf.Max(0, hits);
        ApplyDamageMeshState();
        UpdateWeightBasedOnDamage();
    }
    
    /// <summary>
    /// Увеличивает количество ударов и обновляет визуальный этап повреждения
    /// </summary>
    public int IncrementHitCount()
    {
        currentHits++;
        ApplyDamageMeshState();
        UpdateWeightBasedOnDamage();
        return currentHits;
    }
    
    /// <summary>
    /// Проигрывает визуальные и звуковые эффекты удара
    /// </summary>
    public void PlayHitFeedback(Vector3 impactPoint, Vector3 impactDirection)
    {
        Vector3 particlePosition = impactPoint;
        Vector3 particleNormal = impactDirection;
        
        Transform stageSpawnPoint = GetCurrentDamageStageSpawnPoint();
        if (stageSpawnPoint != null)
        {
            particlePosition = stageSpawnPoint.position;
            particleNormal = stageSpawnPoint.forward;
        }
        
        PlayHitEffect(particlePosition);
        SpawnHitParticles(particlePosition, particleNormal);
        
        if (objectData != null && objectData.HitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(objectData.HitSound);
        }
    }
    
    void SpawnHitParticles(Vector3 position, Vector3 normal)
    {
        if (hitParticleSystemPrefab == null)
        {
            return;
        }
        
        Quaternion rotation = normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
        ParticleSystem particles = Instantiate(hitParticleSystemPrefab, position, rotation);
        
        var main = particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(hitParticleColor);
        
        float lifetime = main.loop ? 5f : main.duration + main.startLifetime.constantMax;
        Destroy(particles.gameObject, lifetime);
    }
    
    Transform GetCurrentDamageStageSpawnPoint()
    {
        if (damageStageHitPoints == null || damageStageHitPoints.Length == 0)
        {
            return null;
        }
        
        int meshIndex = Mathf.Clamp(Mathf.Max(currentHits - 1, 0), 0, damageStageHitPoints.Length - 1);
        return damageStageHitPoints[meshIndex];
    }
    
    void OnValidate()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>();
        }
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
        if (meshFilter == null || meshFilter != targetMeshFilter)
        {
            meshFilter = targetMeshFilter;
            if (meshFilter != null)
            {
                defaultMesh = meshFilter.sharedMesh;
            }
        }
        if (objectRenderer == null || objectRenderer != targetRenderer)
        {
            objectRenderer = targetRenderer;
        }
        
        CacheDefaultMesh();
        
        if (objectData != null && damagedMeshes != null && damagedMeshes.Length > 0 && damagedMeshes.Length != objectData.HitsToDestroy)
        {
            Debug.LogWarning($"[DestructibleObject] Для корректной работы добавьте {objectData.HitsToDestroy} мешей, сейчас {damagedMeshes.Length}", this);
        }
        
        if (damageStageHitPoints != null && damagedMeshes != null && damageStageHitPoints.Length > 0 && damageStageHitPoints.Length != damagedMeshes.Length)
        {
            Debug.LogWarning($"[DestructibleObject] Количество точек спавна частиц ({damageStageHitPoints.Length}) должно совпадать с количеством мешей ({damagedMeshes.Length})", this);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Вычисляем реальный центр масс, который используется в Rigidbody
        Vector3 actualCenterOfMass = Vector3.zero;
        bool isAutoCalculated = false;
        
        if (autoCalculateCenterOfMass && meshFilter != null && meshFilter.sharedMesh != null)
        {
            // Автоматически вычисленный центр масс
            actualCenterOfMass = CalculateCenterOfMassFromMesh(meshFilter.sharedMesh);
            isAutoCalculated = true;
        }
        else if (centerOfMass != Vector3.zero)
        {
            // Заданный вручную центр масс
            actualCenterOfMass = centerOfMass;
        }
        // Если centerOfMass == Vector3.zero и autoCalculateCenterOfMass == false,
        // центр масс будет в центре объекта (transform.position)
        
        // Отображаем центр масс
        Vector3 worldCenterOfMass = transform.TransformPoint(actualCenterOfMass);
        
        // Рисуем большую сферу для центра масс
        Gizmos.color = isAutoCalculated ? new Color(0f, 1f, 0f, 0.8f) : new Color(1f, 0f, 0f, 0.8f); // Зеленый если авто, красный если ручной
        Gizmos.DrawSphere(worldCenterOfMass, 0.15f);
        
        // Рисуем контур сферы
        Gizmos.color = isAutoCalculated ? Color.green : Color.red;
        Gizmos.DrawWireSphere(worldCenterOfMass, 0.15f);
        
        // Рисуем линию от центра объекта до центра масс
        Gizmos.color = isAutoCalculated ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawLine(transform.position, worldCenterOfMass);
        
        // Рисуем крестик в центре масс для лучшей видимости
        Gizmos.color = isAutoCalculated ? Color.green : Color.red;
        float crossSize = 0.2f;
        Gizmos.DrawLine(worldCenterOfMass + Vector3.left * crossSize, worldCenterOfMass + Vector3.right * crossSize);
        Gizmos.DrawLine(worldCenterOfMass + Vector3.up * crossSize, worldCenterOfMass + Vector3.down * crossSize);
        Gizmos.DrawLine(worldCenterOfMass + Vector3.forward * crossSize, worldCenterOfMass + Vector3.back * crossSize);
        
        // Отображаем радиус отображения награды
        if (showRewardDisplay)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, displayDistance);
        }
        
        // Отображаем информацию об объекте
        #if UNITY_EDITOR
        string info = $"Вес: {objectWeight}kg";
        
        // Показываем текущий вес с учетом урона
        if (rb != null && objectData != null)
        {
            float currentWeight = rb.mass;
            info += $"\nТекущий вес: {currentWeight:F1}kg";
        }
        
        info += $"\nЗахвачен: {isGrabbed}";
        
        // Информация о центре масс
        if (isAutoCalculated)
        {
            info += $"\nЦентр масс: Авто ({actualCenterOfMass.x:F2}, {actualCenterOfMass.y:F2}, {actualCenterOfMass.z:F2})";
        }
        else if (centerOfMass != Vector3.zero)
        {
            info += $"\nЦентр масс: Ручной ({actualCenterOfMass.x:F2}, {actualCenterOfMass.y:F2}, {actualCenterOfMass.z:F2})";
        }
        else
        {
            info += "\nЦентр масс: По умолчанию (0, 0, 0)";
        }
        
        if (objectData != null)
        {
            info += $"\nПрочность: {currentHits}/{objectData.HitsToDestroy}\nМонеты: {objectData.CoinAmount}";
        }
        if (showRewardDisplay)
        {
            info += $"\nРадиус награды: {displayDistance}m\nРазмер: {minScale}-{maxScale}";
        }
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, info);
        
        // Показываем метку у центра масс
        UnityEditor.Handles.Label(worldCenterOfMass + Vector3.up * 0.3f, "Центр масс", new GUIStyle() { normal = new GUIStyleState() { textColor = isAutoCalculated ? Color.green : Color.red }, fontSize = 12, fontStyle = FontStyle.Bold });
        #endif
    }
    
    Material CreateFragmentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        
        if (shader == null)
        {
            return null;
        }
        
        Material material = new Material(shader);
        material.color = fragmentColor;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", fragmentColor);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", fragmentColor);
        }
        return material;
    }
    
    void CacheDefaultMesh()
    {
        meshFilter = targetMeshFilter != null ? targetMeshFilter : GetComponent<MeshFilter>();
        if (meshFilter == null) return;
        
        if (damagedMeshes != null && damagedMeshes.Length > 0 && damagedMeshes[0] != null)
        {
            defaultMesh = damagedMeshes[0];
            meshFilter.sharedMesh = defaultMesh;
        }
        else if (meshFilter.sharedMesh != null)
        {
            defaultMesh = meshFilter.sharedMesh;
        }
        
        // Обновляем MeshCollider при кешировании меша
        if (defaultMesh != null && meshCollider != null)
        {
            UpdateMeshCollider(defaultMesh);
        }
    }
    
    void RestoreDefaultMesh()
    {
        if (meshFilter != null && defaultMesh != null)
        {
            meshFilter.sharedMesh = defaultMesh;
            
            // Обновляем MeshCollider при восстановлении меша
            UpdateMeshCollider(defaultMesh);
        }
    }
    
    /// <summary>
    /// Инициализирует MeshCollider, удаляя BoxCollider если он есть
    /// </summary>
    void InitializeMeshCollider()
    {
        // Удаляем BoxCollider если он есть
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            #if UNITY_EDITOR
            DestroyImmediate(boxCollider);
            #else
            Destroy(boxCollider);
            #endif
        }
        
        // Получаем или создаем MeshCollider
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        
        // Настраиваем MeshCollider
        meshCollider.convex = true;
        
        // Устанавливаем меш коллайдера на текущий меш объекта
        Mesh meshToUse = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh : defaultMesh;
        if (meshToUse != null)
        {
            meshCollider.sharedMesh = meshToUse;
        }
    }
    
    /// <summary>
    /// Обновляет MeshCollider при изменении меша
    /// </summary>
    void UpdateMeshCollider(Mesh newMesh)
    {
        if (meshCollider == null)
        {
            InitializeMeshCollider();
        }
        
        if (meshCollider != null && newMesh != null)
        {
            meshCollider.sharedMesh = newMesh;
            meshCollider.convex = true;
        }
    }
    
    /// <summary>
    /// Вычисляет центр масс из меша
    /// </summary>
    Vector3 CalculateCenterOfMassFromMesh(Mesh mesh)
    {
        if (mesh == null) return Vector3.zero;
        
        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0) return Vector3.zero;
        
        // Вычисляем среднюю точку всех вершин
        Vector3 sum = Vector3.zero;
        foreach (Vector3 vertex in vertices)
        {
            sum += vertex;
        }
        
        return sum / vertices.Length;
    }
    
    /// <summary>
    /// Обновляет вес объекта в зависимости от полученного урона
    /// </summary>
    void UpdateWeightBasedOnDamage()
    {
        if (rb == null || objectData == null) return;
        
        int hitsToDestroy = objectData.HitsToDestroy;
        if (hitsToDestroy <= 0) return;
        
        // Вычисляем процент оставшегося веса
        // При 0 ударах: (hitsToDestroy - 0) / hitsToDestroy = 1.0 (100%)
        // При 1 ударе: (hitsToDestroy - 1) / hitsToDestroy = 0.8 (80% если hitsToDestroy = 5)
        // При hitsToDestroy ударах: (hitsToDestroy - hitsToDestroy) / hitsToDestroy = 0 (0%)
        float remainingHits = Mathf.Max(0, hitsToDestroy - currentHits);
        float weightMultiplier = remainingHits / hitsToDestroy;
        
        // Применяем новый вес
        float newWeight = objectWeight * weightMultiplier;
        rb.mass = newWeight;
        
        // Обновляем центр масс если включено автоматическое вычисление
        if (autoCalculateCenterOfMass && meshFilter != null && meshFilter.sharedMesh != null)
        {
            rb.centerOfMass = CalculateCenterOfMassFromMesh(meshFilter.sharedMesh);
        }
    }
}

