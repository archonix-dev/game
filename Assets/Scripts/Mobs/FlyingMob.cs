using UnityEngine;
using System.Collections;
using Mirror;
using TMPro;
using VLB; // Добавляем namespace для VolumetricLightBeam

[RequireComponent(typeof(NetworkIdentity))]
public class FlyingMob : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float wanderSpeed = 3f;
    [SerializeField] private float chaseSpeed = 6f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float flyingHeight = 3f;
    [SerializeField] private float hoverAmplitude = 0.2f;
    [SerializeField] private float hoverFrequency = 2f;
    
    [Header("Scanning Settings")]
    [SerializeField] private float scanInterval = 15f;
    [SerializeField] private float scanDuration = 5.3f;
    [SerializeField] private LayerMask playerLayer = 1 << 0;
    
    [Header("Spot Detection Settings")]
    [SerializeField] private float spotDetectionRadius = 4f;
    [SerializeField] private float spotHeight = 6f;
    
    [Header("Attack Settings")]
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private int damageToPlayer = 5;
    [SerializeField] private float chaseDistance = 2f;
    
    [Header("Volumetric Light Settings")]
    [SerializeField] private VolumetricLightBeam volumetricLight;
    [SerializeField] private Color normalColor = new Color(0f, 0.71f, 1f); // 00B6FF
    [SerializeField] private Color scanningColor = new Color(1f, 0f, 0.05f); // FF000E
    [SerializeField] private float colorTransitionSpeed = 2f;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource idleAudioSource;
    [SerializeField] private AudioSource scanningAudioSource;
    [SerializeField] private AudioSource explosionAudioSource;
    [SerializeField] private AudioClip idleSound;
    [SerializeField] private AudioClip scanningSound;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float idleVolume = 0.3f;
    [SerializeField] private float scanningVolume = 0.5f;
    [SerializeField] private float explosionVolume = 0.8f;
    
    [Header("References")]
    [SerializeField] private GameObject explosionEffectObject;
    [SerializeField] private ParticleSystem explosionParticleSystem;
    [SerializeField] private Material mobMaterialOverride;
    [SerializeField] private TMP_Text alertText3D;
    [SerializeField] private AudioSource alarmAudioSource;
    
    [Header("Material Settings")]
    [SerializeField] private string baseColorProperty = "_BaseColor";
    [SerializeField] private string emissionColorProperty = "_EmissionColor";
    [SerializeField] private float emissionOnIntensity = 10f;
    
    [Header("Alert Settings")]
    [SerializeField] private string attackAlertMessage = "> system attack!!!";
    [SerializeField] private float typewriterCharInterval = 0.05f;
    [SerializeField] private float alertExtraDelay = 1f;
    [SerializeField] private float diveSpeedMultiplier = 4f;
    [SerializeField] private AudioClip alarmClip;
    [SerializeField] private float alarmVolume = 0.8f;
    [SerializeField] private bool mirrorFacingWhenAlerted = true;
    
    private Transform player;
    private Vector3 targetPosition;
    private Vector3 startPosition;
    [SyncVar] private MobState currentState = MobState.Wandering;
    [SyncVar(hook = nameof(OnIsScanningChanged))] private bool isScanning = false;
    [SyncVar(hook = nameof(OnTargetEmissionChanged))] private float syncedTargetEmissionIntensity = 0f;
    private float lastScanTime = 0f;
    private Rigidbody rb;
    private float hoverOffset;
    
    // Сканирование переменные
    private float scanStartTime;
    private bool isScanningTransition = false;
    private Color currentColor;
    private Color targetColor;
    [SyncVar(hook = nameof(OnTargetColorChanged))] private Color syncedTargetColor;
    private float currentEmissionIntensity = 0f;
    private float targetEmissionIntensity = 0f;
    private bool alertVisualOverride = false;
    private Material mobMaterial;
    private Renderer mobRenderer;
    private Coroutine alertTextCoroutine;
    private float scanRotationDirection = 1f;
    private float currentScanRotation = 0f;
    private const float maxScanRotation = 90f;
    [SerializeField] private float explosionEffectLifetime = 3f;
    
    private enum MobState
    {
        Wandering,
        Scanning,
        Chasing,
        Exploding
    }
    
    void Start()
    {
        if (isServer)
        {
            player = FindClosestPlayer();
            SetTargetVisual(normalColor, 0f);
        }
        
        mobRenderer = GetComponentInChildren<Renderer>();
        InitializeMobMaterial();
        UpdateMobMaterialColor();
        
        if (explosionEffectObject != null)
        {
            explosionEffectObject.SetActive(false);
            if (explosionParticleSystem == null)
            {
                explosionParticleSystem = explosionEffectObject.GetComponentInChildren<ParticleSystem>(true);
            }
        }
        
        // Сохраняем стартовую позицию
        startPosition = transform.position;
        
        // Настраиваем Rigidbody для летающего моба
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;
        
        // Отключаем NavMeshAgent если он есть
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
        
        // Находим VolumetricLightBeam если не назначен
        if (volumetricLight == null)
        {
            volumetricLight = GetComponentInChildren<VolumetricLightBeam>();
        }
        
        // Инициализируем AudioSource если не назначены
        InitializeAudioSources();
        
        // Инициализируем цвета
        currentColor = normalColor;
        targetColor = normalColor;
        targetEmissionIntensity = 0f;
        currentEmissionIntensity = 0f;
        UpdateVolumetricLightColor();
        UpdateMobMaterialColor();
        
        if (alertText3D != null)
        {
            alertText3D.text = string.Empty;
            alertText3D.gameObject.SetActive(false);
        }
        
        // Случайное смещение для hover эффекта
        hoverOffset = Random.Range(0f, 2f * Mathf.PI);
        
        // Устанавливаем начальную высоту
        Vector3 newPosition = transform.position;
        newPosition.y = startPosition.y + flyingHeight;
        transform.position = newPosition;
        
        // Устанавливаем начальное состояние
        if (isServer)
        {
            SetWanderPoint();
            StartCoroutine(StateMachine());
        }
        
        // Запускаем фоновый звук
        PlayIdleSound();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Color initialColor = syncedTargetColor == default ? normalColor : syncedTargetColor;
        targetColor = initialColor;
        currentColor = initialColor;
        targetEmissionIntensity = syncedTargetEmissionIntensity;
        currentEmissionIntensity = targetEmissionIntensity;
        UpdateVolumetricLightColor();
        UpdateMobMaterialColor();
    }

    void FacePlayer()
    {
        if (!isServer || player == null)
            return;
        
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        
        if (direction.sqrMagnitude < 0.0001f)
            return;
        
        Vector3 lookDirection = direction;
        if (mirrorFacingWhenAlerted)
        {
            lookDirection = -lookDirection;
        }
        
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    
    void InitializeAudioSources()
    {
        // Создаем или находим AudioSource для idle звука
        if (idleAudioSource == null)
        {
            idleAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Создаем или находим AudioSource для scanning звука
        if (scanningAudioSource == null)
        {
            scanningAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Настраиваем AudioSource для idle звука
        idleAudioSource.clip = idleSound;
        idleAudioSource.volume = idleVolume;
        idleAudioSource.loop = true;
        idleAudioSource.spatialBlend = 1f; // 3D звук
        idleAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        idleAudioSource.minDistance = 1f;
        idleAudioSource.maxDistance = 100f;
        idleAudioSource.dopplerLevel = 0f;
        
        // Настраиваем AudioSource для scanning звука
        scanningAudioSource.clip = scanningSound;
        scanningAudioSource.volume = scanningVolume;
        scanningAudioSource.loop = true;
        scanningAudioSource.spatialBlend = 1f; // 3D звук
        scanningAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        scanningAudioSource.minDistance = 1f;
        scanningAudioSource.maxDistance = 100f;
        scanningAudioSource.dopplerLevel = 0f;
        
        if (alarmAudioSource == null)
        {
            alarmAudioSource = gameObject.AddComponent<AudioSource>();
        }
        alarmAudioSource.clip = alarmClip;
        alarmAudioSource.volume = alarmVolume;
        alarmAudioSource.loop = true;
        alarmAudioSource.spatialBlend = 1f;
        alarmAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        alarmAudioSource.minDistance = 1f;
        alarmAudioSource.maxDistance = 120f;
        alarmAudioSource.dopplerLevel = 0f;

        if (explosionAudioSource == null)
        {
            GameObject explosionAudioObject = new GameObject("ExplosionAudioSource");
            explosionAudioObject.transform.SetParent(transform);
            explosionAudioObject.transform.localPosition = Vector3.zero;
            explosionAudioSource = explosionAudioObject.AddComponent<AudioSource>();
        }
        explosionAudioSource.clip = explosionSound;
        explosionAudioSource.volume = explosionVolume;
        explosionAudioSource.loop = false;
        explosionAudioSource.playOnAwake = false;
        explosionAudioSource.spatialBlend = 1f;
        explosionAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        explosionAudioSource.minDistance = 1f;
        explosionAudioSource.maxDistance = 120f;
        explosionAudioSource.dopplerLevel = 0f;
    }

    void InitializeMobMaterial()
    {
        if (mobMaterialOverride != null)
        {
            mobMaterial = Instantiate(mobMaterialOverride);
            if (mobRenderer == null)
            {
                mobRenderer = GetComponentInChildren<Renderer>();
            }

            if (mobRenderer != null)
            {
                mobRenderer.material = mobMaterial;
            }
        }
        else if (mobRenderer != null)
        {
            mobMaterial = mobRenderer.material;
        }

        if (mobMaterial == null)
        {
            Debug.LogWarning($"{nameof(FlyingMob)} on {gameObject.name} could not find a material to control.");
        }
    }
    
    void Update()
    {
        if (isServer)
        {
            // Обновляем ссылку на игрока если он null, неактивен или не имеет валидного NetworkIdentity
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                player = FindClosestPlayer();
            }
            else
            {
                // Проверяем, что игрок все еще в сети
                NetworkIdentity playerIdentity = player.GetComponent<NetworkIdentity>();
                if (playerIdentity == null || !NetworkServer.spawned.ContainsKey(playerIdentity.netId))
                {
                    player = FindClosestPlayer();
                }
            }
            
            HandleHoverMovement();
            HandleScanningRotation();
            
            if (currentState == MobState.Wandering)
            {
                HandleMovement();
            }
        }
        
        HandleColorTransitions();
    }
    
    void HandleHoverMovement()
    {
        if (!isServer) return;
        
        // Эффект парения (небольшие колебания вверх-вниз)
        float hoverY = Mathf.Sin((Time.time + hoverOffset) * hoverFrequency) * hoverAmplitude;
        Vector3 hoverPosition = transform.position;
        hoverPosition.y += hoverY * Time.deltaTime;
        transform.position = hoverPosition;
    }
    
    void HandleColorTransitions()
    {
        // Плавный переход цвета и эмиссии
        currentColor = Color.Lerp(currentColor, targetColor, colorTransitionSpeed * Time.deltaTime);
        currentEmissionIntensity = Mathf.Lerp(currentEmissionIntensity, targetEmissionIntensity, colorTransitionSpeed * Time.deltaTime);
        
        UpdateVolumetricLightColor();
        UpdateMobMaterialColor();
    }
    
    void HandleScanningRotation()
    {
        if (!isServer) return;
        
        if (currentState == MobState.Scanning && isScanning)
        {
            // Плавное вращение сканирования
            float targetRotation = scanRotationDirection * maxScanRotation;
            currentScanRotation = Mathf.Lerp(currentScanRotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // При достижении цели меняем направление
            if (Mathf.Abs(currentScanRotation - targetRotation) < 1f)
            {
                scanRotationDirection *= -1f;
            }
            
            // Применяем вращение
            transform.rotation = Quaternion.Euler(0f, currentScanRotation, 0f);
        }
    }
    
    void UpdateVolumetricLightColor()
    {
        if (volumetricLight != null)
        {
            volumetricLight.color = currentColor;
            
            // Принудительно обновляем материал после изменения цвета
            volumetricLight.UpdateAfterManualPropertyChange();
        }
    }
    
    void UpdateMobMaterialColor()
    {
        if (mobMaterial == null) return;
        
        if (mobMaterial.HasProperty(baseColorProperty))
        {
            mobMaterial.SetColor(baseColorProperty, currentColor);
        }
        
        if (mobMaterial.HasProperty(emissionColorProperty))
        {
            Color emissionColor = currentColor * Mathf.LinearToGammaSpace(currentEmissionIntensity);
            mobMaterial.SetColor(emissionColorProperty, emissionColor);
        }
        
        if (currentEmissionIntensity > 0.05f)
        {
            mobMaterial.EnableKeyword("_EMISSION");
        }
        else
        {
            mobMaterial.DisableKeyword("_EMISSION");
        }
    }
    
    void PlayIdleSound()
    {
        // Звуки проигрываются только на клиентах для правильной пространственной обработки
        if (!isClient) return;
        
        if (idleAudioSource != null && idleSound != null)
        {
            idleAudioSource.Play();
        }
    }
    
    void StopIdleSound()
    {
        if (!isClient) return;
        
        if (idleAudioSource != null)
        {
            idleAudioSource.Stop();
        }
    }
    
    void PlayScanningSound()
    {
        // Звуки проигрываются только на клиентах для правильной пространственной обработки
        if (!isClient) return;
        
        if (scanningAudioSource != null && scanningSound != null)
        {
            scanningAudioSource.Play();
        }
    }
    
    void StopScanningSound()
    {
        if (!isClient) return;
        
        if (scanningAudioSource != null)
        {
            scanningAudioSource.Stop();
        }
    }
    
    void PlayAlarmSound(bool play)
    {
        // Звуки проигрываются только на клиентах для правильной пространственной обработки
        if (!isClient) return;
        
        if (alarmAudioSource == null || alarmClip == null)
            return;
        
        if (play)
        {
            if (!alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Play();
            }
        }
        else
        {
            if (alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Stop();
            }
        }
    }
    
    [Server]
    void TriggerAttackAlertVisuals()
    {
        HandleAttackAlertVisuals();
        RpcHandleAttackAlertVisuals();
    }
    
    void HandleAttackAlertVisuals()
    {
        StartTypewriterEffect();
        PlayAlarmSound(true);
    }
    
    [ClientRpc]
    void RpcHandleAttackAlertVisuals()
    {
        if (isServer)
            return;
        
        HandleAttackAlertVisuals();
    }
    
    [Server]
    void BroadcastStopAttackAlertVisuals(bool clearText = true)
    {
        StopAttackAlertVisualsLocal(clearText);
        RpcStopAttackAlertVisuals(clearText);
    }
    
    [ClientRpc]
    void RpcStopAttackAlertVisuals(bool clearText)
    {
        if (isServer)
            return;
        
        StopAttackAlertVisualsLocal(clearText);
    }
    
    void StopAttackAlertVisualsLocal(bool clearText = true)
    {
        if (alertTextCoroutine != null)
        {
            StopCoroutine(alertTextCoroutine);
            alertTextCoroutine = null;
        }
        
        if (alertText3D != null && clearText)
        {
            alertText3D.text = string.Empty;
            alertText3D.gameObject.SetActive(false);
        }
        
        PlayAlarmSound(false);
    }
    
    void StartTypewriterEffect()
    {
        if (alertText3D == null || string.IsNullOrEmpty(attackAlertMessage))
            return;
        
        if (alertTextCoroutine != null)
        {
            StopCoroutine(alertTextCoroutine);
        }
        
        alertTextCoroutine = StartCoroutine(TypewriterRoutine());
    }
    
    IEnumerator TypewriterRoutine()
    {
        alertText3D.gameObject.SetActive(true);
        alertText3D.text = string.Empty;
        
        float delay = Mathf.Max(typewriterCharInterval, 0.01f);
        
        foreach (char c in attackAlertMessage)
        {
            alertText3D.text += c;
            yield return new WaitForSeconds(delay);
        }
    }
    
    float GetAlertPresentationDuration()
    {
        int messageLength = string.IsNullOrEmpty(attackAlertMessage) ? 0 : attackAlertMessage.Length;
        float typeDuration = messageLength * Mathf.Max(typewriterCharInterval, 0.01f);
        return typeDuration + Mathf.Max(alertExtraDelay, 0f);
    }
    
    void StartScanningTransition()
    {
        isScanningTransition = true;
        scanStartTime = Time.time;
        currentScanRotation = 0f;
        scanRotationDirection = 1f;
        
        // Переключаем звуки
        StopIdleSound();
        PlayScanningSound();
    }
    
    void EndScanningTransition()
    {
        isScanningTransition = false;
        
        // Переключаем звуки обратно
        StopScanningSound();
        PlayIdleSound();
    }
    
    [Server]
    void SetScanningState(bool value)
    {
        if (isScanning == value)
            return;
        
        isScanning = value;
        
        if (!alertVisualOverride)
        {
            SetTargetVisual(value ? scanningColor : normalColor, value ? emissionOnIntensity : 0f);
        }
        
        if (value)
        {
            StartScanningTransition();
        }
        else
        {
            EndScanningTransition();
        }
    }
    
    void OnIsScanningChanged(bool oldValue, bool newValue)
    {
        if (isServer)
            return;
        
        if (newValue)
        {
            StartScanningTransition();
        }
        else
        {
            EndScanningTransition();
        }
    }
    
    [Server]
    void SetTargetVisual(Color color, float emissionIntensity)
    {
        bool sameColor = syncedTargetColor == color;
        bool sameEmission = Mathf.Approximately(syncedTargetEmissionIntensity, emissionIntensity);
        
        if (sameColor && sameEmission)
            return;
        
        syncedTargetColor = color;
        syncedTargetEmissionIntensity = emissionIntensity;
        ApplyTargetVisual(color, emissionIntensity);
        RpcApplyTargetVisual(color, emissionIntensity);
    }
    
    void OnTargetColorChanged(Color oldValue, Color newValue)
    {
        ApplyTargetVisual(newValue, syncedTargetEmissionIntensity);
    }
    
    void OnTargetEmissionChanged(float oldValue, float newValue)
    {
        ApplyTargetVisual(syncedTargetColor, newValue);
    }
    
    void ApplyTargetVisual(Color color, float emissionIntensity)
    {
        targetColor = color;
        targetEmissionIntensity = emissionIntensity;
    }
    
    [ClientRpc]
    void RpcApplyTargetVisual(Color color, float emissionIntensity)
    {
        if (isServer)
            return;
        
        ApplyTargetVisual(color, emissionIntensity);
    }
    
    [Server]
    void SetAlertVisuals(bool enabled)
    {
        alertVisualOverride = enabled;
        
        if (enabled)
        {
            SetTargetVisual(scanningColor, emissionOnIntensity);
        }
        else if (!isScanning)
        {
            SetTargetVisual(normalColor, 0f);
        }
        else
        {
            SetTargetVisual(scanningColor, emissionOnIntensity);
        }
    }
    
    void HandleMovement()
    {
        if (!isServer) return;
        
        if (currentState == MobState.Scanning || currentState == MobState.Exploding)
            return;
            
        // Рассчитываем направление и расстояние до цели
        Vector3 direction = (targetPosition - transform.position);
        float distanceToTarget = direction.magnitude;
        
        if (distanceToTarget > 0.5f)
        {
            // Нормализуем направление
            direction.Normalize();
            
            // Выбираем скорость в зависимости от состояния
            float currentSpeed = currentState == MobState.Chasing ? chaseSpeed : wanderSpeed;
            
            // Двигаемся к цели
            transform.position += direction * currentSpeed * Time.deltaTime;
            
            // Плавный поворот к цели (только по горизонтали)
            if (direction != Vector3.zero && currentState != MobState.Scanning)
            {
                Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z);
                if (horizontalDirection.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }
        else if (currentState == MobState.Wandering)
        {
            // Достигли точки блуждания - выбираем новую
            SetWanderPoint();
        }
    }
    
    [Server]
    IEnumerator StateMachine()
    {
        while (true)
        {
            switch (currentState)
            {
                case MobState.Wandering:
                    yield return StartCoroutine(WanderState());
                    break;
                    
                case MobState.Scanning:
                    yield return StartCoroutine(ScanningState());
                    break;
                    
                case MobState.Chasing:
                    yield return StartCoroutine(ChasingState());
                    break;
                    
                case MobState.Exploding:
                    yield return StartCoroutine(ExplodingState());
                    break;
            }
            yield return null;
        }
    }
    
    [Server]
    IEnumerator WanderState()
    {
        SetScanningState(false);
        
        while (currentState == MobState.Wandering)
        {
            // Проверяем время для сканирования
            if (Time.time - lastScanTime >= scanInterval)
            {
                currentState = MobState.Scanning;
                break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }
    
    [Server]
    IEnumerator ScanningState()
    {
        // Останавливаем движение
        targetPosition = transform.position;
        SetScanningState(true);
        lastScanTime = Time.time;
        
        float scanStartTime = Time.time;
        bool playerDetected = false;
        
        // Сканируем в течение указанного времени
        while (Time.time - scanStartTime < scanDuration && !playerDetected)
        {
            Transform detectedPlayer = FindPlayerInSpot();
            if (detectedPlayer != null)
            {
                player = detectedPlayer;
                playerDetected = true;
                currentState = MobState.Chasing;
                break;
            }
            
            yield return null;
        }
        
        // Если игрок не найден, возвращаемся к блужданию
        if (!playerDetected)
        {
            SetScanningState(false);
            currentState = MobState.Wandering;
            SetWanderPoint();
        }
    }
    
    [Server]
    IEnumerator ChasingState()
    {
        SetScanningState(false);
        SetAlertVisuals(true);
        TriggerAttackAlertVisuals();
        
        float presentationDuration = GetAlertPresentationDuration();
        float elapsed = 0f;
        
        while (elapsed < presentationDuration)
        {
            // Проверяем валидность игрока
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                player = FindClosestPlayer();
                if (player == null)
                {
                    BroadcastStopAttackAlertVisuals();
                    SetAlertVisuals(false);
                    currentState = MobState.Wandering;
                    SetWanderPoint();
                    yield break;
                }
            }
            else
            {
                // Проверяем, что игрок все еще в сети
                NetworkIdentity playerIdentity = player.GetComponent<NetworkIdentity>();
                if (playerIdentity == null || !NetworkServer.spawned.ContainsKey(playerIdentity.netId))
                {
                    player = FindClosestPlayer();
                    if (player == null)
                    {
                        BroadcastStopAttackAlertVisuals();
                        SetAlertVisuals(false);
                        currentState = MobState.Wandering;
                        SetWanderPoint();
                        yield break;
                    }
                }
            }

            FacePlayer();
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Оставляем текст на экране, но прекращаем "печать" и тревогу
        BroadcastStopAttackAlertVisuals(false);
        
        float diveSpeed = chaseSpeed * Mathf.Max(diveSpeedMultiplier, 1f);
        
        while (currentState == MobState.Chasing)
        {
            // Проверяем валидность игрока
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                player = FindClosestPlayer();
                if (player == null)
                {
                    BroadcastStopAttackAlertVisuals();
                    SetAlertVisuals(false);
                    currentState = MobState.Wandering;
                    SetWanderPoint();
                    break;
                }
            }
            else
            {
                // Проверяем, что игрок все еще в сети
                NetworkIdentity playerIdentity = player.GetComponent<NetworkIdentity>();
                if (playerIdentity == null || !NetworkServer.spawned.ContainsKey(playerIdentity.netId))
                {
                    player = FindClosestPlayer();
                    if (player == null)
                    {
                        BroadcastStopAttackAlertVisuals();
                        SetAlertVisuals(false);
                        currentState = MobState.Wandering;
                        SetWanderPoint();
                        break;
                    }
                }
            }
            
            Vector3 direction = (player.position - transform.position);
            float distanceToPlayer = direction.magnitude;
            if (distanceToPlayer <= Mathf.Epsilon)
            {
                yield return null;
                continue;
            }
            
            direction.Normalize();
            targetPosition = player.position;
            targetPosition.y = transform.position.y;
            
            transform.position += direction * diveSpeed * Time.deltaTime;
            
            Vector3 horizontalDirection = new Vector3(direction.x, 0f, direction.z);
            if (horizontalDirection.sqrMagnitude > 0.01f)
            {
                Vector3 lookDir = horizontalDirection;
                if (mirrorFacingWhenAlerted)
                {
                    lookDir = -lookDir;
                }
                
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
            
            if (distanceToPlayer <= chaseDistance)
            {
                currentState = MobState.Exploding;
                break;
            }
            
            yield return null;
        }
        
        BroadcastStopAttackAlertVisuals();
    }
    
    [Server]
    IEnumerator ExplodingState()
    {
        // Останавливаем движение
        targetPosition = transform.position;
        SetScanningState(false);
        SetAlertVisuals(false);
        BroadcastStopAttackAlertVisuals();
        
        HandleExplosionEffects();
        RpcHandleExplosionEffects();
        
        // Наносим урон игроку и откидываем его
        if (player != null && Vector3.Distance(transform.position, player.position) <= explosionRadius)
        {
            // Наносим урон
            PlayerHealthStamina playerHealth = player.GetComponent<PlayerHealthStamina>();
            if (playerHealth != null)
            {
                playerHealth.UseHealth(damageToPlayer);
            }
            
            // Откидываем игрока
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 explosionDirection = (player.position - transform.position).normalized;
                explosionDirection.y = 0.3f;
                playerRb.AddForce(explosionDirection * explosionForce, ForceMode.Impulse);
            }
        }
        
        // Уничтожаем моба
        if (NetworkServer.active)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        yield return null;
    }
    
    void HandleExplosionEffects()
    {
        if (volumetricLight != null)
        {
            volumetricLight.enabled = false;
        }
        
        StopIdleSound();
        StopScanningSound();
        PlayAlarmSound(false);
        PlayExplosionSound();
        
        if (explosionEffectObject != null)
        {
            Transform effectTransform = explosionEffectObject.transform;
            effectTransform.SetParent(null, true);
            explosionEffectObject.transform.position = transform.position;
            explosionEffectObject.transform.rotation = transform.rotation;
            explosionEffectObject.SetActive(true);
            
            if (explosionParticleSystem == null)
            {
                explosionParticleSystem = explosionEffectObject.GetComponentInChildren<ParticleSystem>();
            }
            
            if (explosionParticleSystem != null)
            {
                explosionParticleSystem.Play(true);
                float totalDuration = explosionParticleSystem.main.duration + explosionParticleSystem.main.startLifetime.constantMax;
                Destroy(explosionEffectObject, Mathf.Max(totalDuration, explosionEffectLifetime));
            }
            else
            {
                Destroy(explosionEffectObject, explosionEffectLifetime);
            }
        }
    }
    
    [ClientRpc]
    void RpcHandleExplosionEffects()
    {
        if (isServer)
            return;
        
        HandleExplosionEffects();
    }

    void PlayExplosionSound()
    {
        // Звук проигрывается только на клиентах для правильной пространственной обработки
        if (!isClient) return;
        
        if (explosionAudioSource == null || explosionSound == null)
            return;

        explosionAudioSource.Stop();
        explosionAudioSource.transform.SetParent(null, true);
        explosionAudioSource.transform.position = transform.position;
        explosionAudioSource.Play();
        Destroy(explosionAudioSource.gameObject, explosionSound.length + 0.5f);
    }
    
    void SetWanderPoint()
    {
        if (!isServer) return;
        
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        targetPosition = startPosition + new Vector3(randomCircle.x, flyingHeight, randomCircle.y);
    }
    
    [Server]
    Transform FindPlayerInSpot()
    {
        float height = Mathf.Max(spotHeight, 0.01f);
        float baseRadius = Mathf.Max(spotDetectionRadius, 0.01f);
        float maxDistance = Mathf.Sqrt(height * height + baseRadius * baseRadius);
        Vector3 apex = transform.position;
        
        // Используем NetworkServer.spawned для поиска всех игроков в сети
        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            
            PlayerController controller = identity.GetComponent<PlayerController>();
            if (controller == null) continue;
            
            // Проверяем, что игрок активен
            if (!controller.gameObject.activeInHierarchy)
                continue;
            
            Transform playerTransform = controller.transform;
            Vector3 playerPosition = playerTransform.position;
            Vector3 toPlayer = playerPosition - apex;
            float distanceToPlayer = toPlayer.magnitude;
            
            // Проверяем, что игрок в пределах максимального расстояния
            if (distanceToPlayer > maxDistance)
                continue;
            
            // Проверяем, что игрок находится под мобом (y < apex.y)
            if (toPlayer.y > 0f)
                continue;
            
            // Вычисляем вертикальную глубину
            float verticalDepth = Mathf.Min(height, -toPlayer.y);
            if (verticalDepth <= 0f)
                continue;
            
            // Вычисляем допустимый радиус на этой глубине (конус)
            float normalizedDepth = verticalDepth / height;
            float allowedRadius = baseRadius * normalizedDepth;
            
            // Проверяем горизонтальное расстояние
            Vector2 horizontal = new Vector2(toPlayer.x, toPlayer.z);
            if (horizontal.sqrMagnitude <= allowedRadius * allowedRadius)
            {
                return playerTransform;
            }
        }
        
        return null;
    }
    
    [Server]
    Transform FindClosestPlayer()
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        
        // Ищем всех игроков через NetworkServer.spawned
        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            
            // Проверяем, что объект активен
            if (!identity.gameObject.activeInHierarchy)
                continue;
            
            PlayerController controller = identity.GetComponent<PlayerController>();
            if (controller == null) continue;
            
            // Проверяем, что игрок не мертв (если есть компонент здоровья)
            PlayerHealthStamina health = identity.GetComponent<PlayerHealthStamina>();
            if (health != null && health.GetCurrentHealth() <= 0f)
                continue;
            
            Transform candidate = controller.transform;
            float sqrDistance = (candidate.position - transform.position).sqrMagnitude;
            
            if (sqrDistance < closestDistance)
            {
                closestDistance = sqrDistance;
                closest = candidate;
            }
        }
        
        // Fallback: ищем по тегу (для одиночной игры или если NetworkServer не работает)
        if (closest == null)
        {
            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject taggedPlayer in taggedPlayers)
            {
                if (taggedPlayer == null || !taggedPlayer.activeInHierarchy)
                    continue;
                
                PlayerController controller = taggedPlayer.GetComponent<PlayerController>();
                if (controller == null) continue;
                
                float sqrDistance = (taggedPlayer.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < closestDistance)
                {
                    closestDistance = sqrDistance;
                    closest = taggedPlayer.transform;
                }
            }
        }
        
        return closest;
    }
    
    void OnDrawGizmosSelected()
    {
        Vector3 currentPosition = transform.position;
        Vector3 wanderCenter = Application.isPlaying ? startPosition : currentPosition;
        
        // Визуализация радиуса взрыва
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(currentPosition, explosionRadius);
        
        // Визуализация радиуса блуждания
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(wanderCenter, wanderRadius);
        
        // Визуализация текущей цели (только в игре)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPosition, 0.3f);
            Gizmos.DrawLine(currentPosition, targetPosition);
        }
        
        // Визуализация высоты полета
        Gizmos.color = Color.white;
        DrawFlyingHeightGizmo(wanderCenter);
        
        // Визуализация зоны сканирования (конус)
        if (spotDetectionRadius > 0f && spotHeight > 0f)
        {
            Gizmos.color = Color.cyan;
            DrawConeGizmo(currentPosition, spotHeight, spotDetectionRadius);
        }
    }
    
    void DrawFlyingHeightGizmo(Vector3 basePosition)
    {
        Vector3 hoverPosition = basePosition + Vector3.up * flyingHeight;
        Gizmos.DrawLine(basePosition, hoverPosition);
        Gizmos.DrawSphere(basePosition, 0.1f);
        Gizmos.DrawSphere(hoverPosition, 0.1f);
    }
    
    void DrawConeGizmo(Vector3 apex, float height, float baseRadius)
    {
        if (height <= 0f || baseRadius <= 0f)
            return;
        
        Vector3 baseCenter = apex + Vector3.down * height;
        const int segments = 24;
        Vector3 firstPoint = baseCenter + Vector3.forward * baseRadius;
        Vector3 prevPoint = firstPoint;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector3 nextPoint = baseCenter + new Vector3(sin * baseRadius, 0f, cos * baseRadius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
        
        Gizmos.DrawLine(prevPoint, firstPoint);
        
        Vector3[] directions =
        {
            Vector3.forward,
            -Vector3.forward,
            Vector3.right,
            -Vector3.right
        };
        
        foreach (Vector3 dir in directions)
        {
            Gizmos.DrawLine(apex, baseCenter + dir * baseRadius);
        }
    }
}