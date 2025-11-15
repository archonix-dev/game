using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PlayerHealthStamina : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float baseHealth = 40f;
    
    [Header("Stamina Settings")]
    [SerializeField] private float baseStamina = 20f;
    
    // Сетевые переменные для синхронизации
    private NetworkVariable<float> maxHealth = new NetworkVariable<float>(40f, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(40f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> maxStamina = new NetworkVariable<float>(20f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> currentStamina = new NetworkVariable<float>(20f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
    
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Подписываемся на изменения сетевых переменных
        currentHealth.OnValueChanged += OnHealthChanged;
        maxHealth.OnValueChanged += OnMaxHealthChanged;
        currentStamina.OnValueChanged += OnStaminaChanged;
        maxStamina.OnValueChanged += OnMaxStaminaChanged;
        
        if (IsServer)
        {
            InitializeHealthStamina();
        }
        
        CreateUI();
        UpdateUI();
    }
    
    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        currentHealth.OnValueChanged -= OnHealthChanged;
        maxHealth.OnValueChanged -= OnMaxHealthChanged;
        currentStamina.OnValueChanged -= OnStaminaChanged;
        maxStamina.OnValueChanged -= OnMaxStaminaChanged;
        
        base.OnNetworkDespawn();
    }
    
    void Start()
    {
        // Если не в сети, инициализируем локально
        if (!IsSpawned)
        {
            InitializeHealthStamina();
            CreateUI();
            UpdateUI();
        }
    }
    
    void Update()
    {
        // Регенерация стамины только на сервере
        if (IsServer)
        {
            RegenerateStamina();
        }
    }
    
    void InitializeHealthStamina()
    {
        if (IsServer)
        {
            currentHealth.Value = baseHealth;
            maxHealth.Value = baseHealth;
            currentStamina.Value = baseStamina;
            maxStamina.Value = baseStamina;
        }
    }
    
    void OnHealthChanged(float oldValue, float newValue)
    {
        UpdateHealthUI();
        UpdateTextUI();
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
            int healthPrefabCount = Mathf.CeilToInt(maxHealth.Value / 10f);
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
            int staminaPrefabCount = Mathf.CeilToInt(maxStamina.Value / 10f);
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
        
        int activeHealthPrefabs = Mathf.CeilToInt(currentHealth.Value / 10f);
        
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
                    float alpha = CalculateAlphaForLastPrefab(currentHealth.Value, maxHealth.Value);
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
        
        int activeStaminaPrefabs = Mathf.CeilToInt(currentStamina.Value / 10f);
        
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
                    float alpha = CalculateAlphaForLastPrefab(currentStamina.Value, maxStamina.Value);
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
            healthText.text = $"{Mathf.RoundToInt(currentHealth.Value)}/{Mathf.RoundToInt(maxHealth.Value)}";
        }
        
        if (staminaText != null)
        {
            staminaText.text = $"{Mathf.RoundToInt(currentStamina.Value)}/{Mathf.RoundToInt(maxStamina.Value)}";
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
    
    void RegenerateStamina()
    {
        if (!IsServer) return;
        
        if (Time.time - lastStaminaUseTime >= staminaRegenDelay && currentStamina.Value < maxStamina.Value)
        {
            currentStamina.Value = Mathf.Min(maxStamina.Value, currentStamina.Value + staminaRegenRate * Time.deltaTime);
        }
    }
    
    public void UseStamina(float amount)
    {
        if (!IsServer) return;
        
        currentStamina.Value = Mathf.Max(0f, currentStamina.Value - amount);
        lastStaminaUseTime = Time.time;
    }
    
    public void UseHealth(float amount)
    {
        if (!IsServer) return;
        
        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
    }
    
    public void Heal(float amount)
    {
        if (!IsServer) return;
        
        currentHealth.Value = Mathf.Min(maxHealth.Value, currentHealth.Value + amount);
    }
    
    public bool HasEnoughStamina(float amount)
    {
        return currentStamina.Value >= amount;
    }
    
    public void IncreaseMaxHealth(float amount)
    {
        if (!IsServer) return;
        
        maxHealth.Value = Mathf.Min(300f, maxHealth.Value + amount);
        currentHealth.Value = Mathf.Min(maxHealth.Value, currentHealth.Value + amount);
    }
    
    public void IncreaseMaxStamina(float amount)
    {
        if (!IsServer) return;
        
        maxStamina.Value = Mathf.Min(300f, maxStamina.Value + amount);
        currentStamina.Value = Mathf.Min(maxStamina.Value, currentStamina.Value + amount);
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
    
    
    public float GetCurrentHealth() => currentHealth.Value;
    public float GetMaxHealth() => maxHealth.Value;
    public float GetCurrentStamina() => currentStamina.Value;
    public float GetMaxStamina() => maxStamina.Value;
}
