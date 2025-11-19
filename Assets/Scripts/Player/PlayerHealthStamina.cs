using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class PlayerHealthStamina : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float baseHealth = 40f;
    
    [Header("Stamina Settings")]
    [SerializeField] private float baseStamina = 20f;
    
    // Сетевые переменные для синхронизации Mirror
    [SyncVar(hook = nameof(OnMaxHealthChanged))]
    private float maxHealth = 40f;
    
    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth = 40f;
    
    [SyncVar(hook = nameof(OnMaxStaminaChanged))]
    private float maxStamina = 20f;
    
    [SyncVar(hook = nameof(OnStaminaChanged))]
    private float currentStamina = 20f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float staminaRegenDelay = 2f;
    
    [Header("UI References")]
    [SerializeField] private Transform healthPrefabParent;
    [SerializeField] private Transform staminaPrefabParent;
    [SerializeField] private GameObject healthPrefab;
    [SerializeField] private GameObject staminaPrefab;
    [SerializeField] private Text healthText;
    [SerializeField] private Text staminaText;
    
    private float lastStaminaUseTime;
    private GameObject[] healthPrefabs;
    private GameObject[] staminaPrefabs;
    private Image[] healthImages;
    private Image[] staminaImages;
    private PlayerController playerController;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }
    
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Инициализируем значения на сервере
        if (isServer)
        {
            InitializeHealthStamina();
        }
        
        CreateUI();
        UpdateUI();
    }
    
    void Start()
    {
        // Если не в сети, инициализируем локально
        if (netIdentity == null || netIdentity.netId == 0)
        {
            InitializeHealthStamina();
            CreateUI();
            UpdateUI();
        }
    }
    
    void Update()
    {
        // Регенерация стамины только на сервере
        if (isServer)
        {
            RegenerateStamina();
        }
    }
    
    void InitializeHealthStamina()
    {
        if (isServer)
        {
            currentHealth = baseHealth;
            maxHealth = baseHealth;
            currentStamina = baseStamina;
            maxStamina = baseStamina;
        }
    }
    
    void OnHealthChanged(float oldValue, float newValue)
    {
        UpdateHealthUI();
        UpdateTextUI();

        if (isServer && newValue < oldValue)
        {
            float damageAmount = oldValue - newValue;
            NotifyDamageTaken(damageAmount);
        }
    }
    
    void OnMaxHealthChanged(float oldValue, float newValue)
    {
        RecreateHealthUI();
        UpdateUI();
    }
    
    void OnStaminaChanged(float oldValue, float newValue)
    {
        UpdateStaminaUI();
        UpdateTextUI();
    }
    
    void OnMaxStaminaChanged(float oldValue, float newValue)
    {
        RecreateStaminaUI();
        UpdateUI();
    }
    
    void CreateUI()
    {
        CreateHealthUI();
        CreateStaminaUI();
    }
    
    void CreateHealthUI()
    {
        if (healthPrefabParent != null && healthPrefab != null)
        {
            int healthPrefabCount = Mathf.CeilToInt(maxHealth / 10f);
            healthPrefabs = new GameObject[healthPrefabCount];
            healthImages = new Image[healthPrefabCount];
            
            for (int i = 0; i < healthPrefabCount; i++)
            {
                healthPrefabs[i] = Instantiate(healthPrefab, healthPrefabParent);
                healthImages[i] = healthPrefabs[i].GetComponent<Image>();
            }
        }
    }
    
    void CreateStaminaUI()
    {
        if (staminaPrefabParent != null && staminaPrefab != null)
        {
            int staminaPrefabCount = Mathf.CeilToInt(maxStamina / 10f);
            staminaPrefabs = new GameObject[staminaPrefabCount];
            staminaImages = new Image[staminaPrefabCount];
            
            for (int i = 0; i < staminaPrefabCount; i++)
            {
                staminaPrefabs[i] = Instantiate(staminaPrefab, staminaPrefabParent);
                staminaImages[i] = staminaPrefabs[i].GetComponent<Image>();
            }
        }
    }
    
    void UpdateUI()
    {
        UpdateHealthUI();
        UpdateStaminaUI();
        UpdateTextUI();
    }
    
    void UpdateHealthUI()
    {
        if (healthPrefabs == null || healthImages == null) return;
        
        int activeHealthPrefabs = Mathf.CeilToInt(currentHealth / 10f);
        
        for (int i = 0; i < healthPrefabs.Length; i++)
        {
            if (healthPrefabs[i] != null && healthImages[i] != null)
            {
                if (i < activeHealthPrefabs - 1)
                {
                    // Полностью видимые префабы
                    healthPrefabs[i].SetActive(true);
                    Color color = healthImages[i].color;
                    color.a = 1f;
                    healthImages[i].color = color;
                }
                else if (i == activeHealthPrefabs - 1)
                {
                    // Последний префаб с плавной прозрачностью
                    healthPrefabs[i].SetActive(true);
                    float alpha = CalculateAlphaForLastPrefab(currentHealth, maxHealth);
                    Color color = healthImages[i].color;
                    color.a = alpha;
                    healthImages[i].color = color;
                }
                else
                {
                    // Неактивные префабы
                    healthPrefabs[i].SetActive(false);
                }
            }
        }
    }
    
    void UpdateStaminaUI()
    {
        if (staminaPrefabs == null || staminaImages == null) return;
        
        int activeStaminaPrefabs = Mathf.CeilToInt(currentStamina / 10f);
        
        for (int i = 0; i < staminaPrefabs.Length; i++)
        {
            if (staminaPrefabs[i] != null && staminaImages[i] != null)
            {
                if (i < activeStaminaPrefabs - 1)
                {
                    // Полностью видимые префабы
                    staminaPrefabs[i].SetActive(true);
                    Color color = staminaImages[i].color;
                    color.a = 1f;
                    staminaImages[i].color = color;
                }
                else if (i == activeStaminaPrefabs - 1)
                {
                    // Последний префаб с плавной прозрачностью
                    staminaPrefabs[i].SetActive(true);
                    float alpha = CalculateAlphaForLastPrefab(currentStamina, maxStamina);
                    Color color = staminaImages[i].color;
                    color.a = alpha;
                    staminaImages[i].color = color;
                }
                else
                {
                    // Неактивные префабы
                    staminaPrefabs[i].SetActive(false);
                }
            }
        }
    }
    
    void UpdateTextUI()
    {
        if (healthText != null)
        {
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
        }
        
        if (staminaText != null)
        {
            staminaText.text = $"{Mathf.RoundToInt(currentStamina)}/{Mathf.RoundToInt(maxStamina)}";
        }
    }
    
    float CalculateAlphaForLastPrefab(float currentValue, float maxValue)
    {
        // Вычисляем остаток от деления на 10 (количество поинтов в последнем префабе)
        float remainder = currentValue % 10f;
        
        // Если остаток равен 0, значит префаб должен быть полностью видимым
        if (remainder == 0f)
            return 1f;
        
        // Возвращаем прозрачность от 0 до 1 в зависимости от остатка
        return remainder / 10f;
    }

    void NotifyDamageTaken(float damageAmount)
    {
        if (damageAmount <= 0f)
            return;

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.NotifyDamageTaken(damageAmount);
        }
    }
    
    void RegenerateStamina()
    {
        if (!isServer) return;
        
        if (Time.time - lastStaminaUseTime >= staminaRegenDelay && currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Использовать стамину (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void UseStamina(float amount)
    {
        if (amount <= 0) return;
        
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
    }
    
    /// <summary>
    /// Использовать здоровье (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void UseHealth(float amount)
    {
        if (amount <= 0) return;
        
        currentHealth = Mathf.Max(0f, currentHealth - amount);
    }
    
    /// <summary>
    /// Лечить игрока (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void Heal(float amount)
    {
        if (amount <= 0) return;
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
    
    public bool HasEnoughStamina(float amount)
    {
        return currentStamina >= amount;
    }
    
    /// <summary>
    /// Увеличить максимальное здоровье (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void IncreaseMaxHealth(float amount)
    {
        if (amount <= 0) return;
        
        maxHealth = Mathf.Min(300f, maxHealth + amount);
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
    
    /// <summary>
    /// Увеличить максимальную стамину (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void IncreaseMaxStamina(float amount)
    {
        if (amount <= 0) return;
        
        maxStamina = Mathf.Min(300f, maxStamina + amount);
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }
    
    /// <summary>
    /// Добавить стамину (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void AddStamina(float amount)
    {
        if (amount <= 0) return;
        
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }
    
    void RecreateHealthUI()
    {
        if (healthPrefabs != null)
        {
            foreach (GameObject prefab in healthPrefabs)
            {
                if (prefab != null)
                {
                    DestroyImmediate(prefab);
                }
            }
        }
        
        healthPrefabs = null;
        healthImages = null;
        CreateHealthUI();
    }
    
    void RecreateStaminaUI()
    {
        if (staminaPrefabs != null)
        {
            foreach (GameObject prefab in staminaPrefabs)
            {
                if (prefab != null)
                {
                    DestroyImmediate(prefab);
                }
            }
        }
        
        staminaPrefabs = null;
        staminaImages = null;
        CreateStaminaUI();
    }
    
    
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => maxStamina;
}
