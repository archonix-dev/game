using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton менеджер для управления системой монет
/// </summary>
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }
    
    [Header("UI Настройки")]
    [SerializeField] private Text coinsText; // Для обычного UI Text
    
    [Header("Настройки")]
    [SerializeField] private int startingCoins = 0;
    [SerializeField] private bool saveCoins = true;
    [SerializeField] private string saveKey = "PlayerCoins";
    
    [Header("Визуальная обратная связь")]
    [SerializeField] private bool animateOnChange = true;
    [SerializeField] private float animationDuration = 0.5f;
    
    private int currentCoins = 0;
    private AudioSource audioSource;
    
    // Для анимации
    private int displayedCoins = 0;
    private float animationTimer = 0f;
    private int targetCoins = 0;
    
    void Awake()
    {
        // Singleton паттерн
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    void Start()
    {
        // Загружаем сохраненные монеты или устанавливаем стартовое значение
        if (saveCoins && PlayerPrefs.HasKey(saveKey))
        {
            currentCoins = PlayerPrefs.GetInt(saveKey, startingCoins);
        }
        else
        {
            currentCoins = startingCoins;
        }
        
        displayedCoins = currentCoins;
        targetCoins = currentCoins;
        UpdateUI();
    }
    
    void Update()
    {
        // Анимация изменения монет
        if (animateOnChange && displayedCoins != targetCoins)
        {
            animationTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(animationTimer / animationDuration);
            
            displayedCoins = Mathf.RoundToInt(Mathf.Lerp(displayedCoins, targetCoins, progress));
            
            UpdateUI();
            
            if (progress >= 1f)
            {
                displayedCoins = targetCoins;
                animationTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// Добавить монеты
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        
        currentCoins += amount;
        targetCoins = currentCoins;
        
        
        if (!animateOnChange)
        {
            displayedCoins = currentCoins;
            UpdateUI();
        }
        else
        {
            animationTimer = 0f;
        }
        
        // Сохраняем
        SaveCoins();
    }
    
    /// <summary>
    /// Потратить монеты
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return false;
        
        if (currentCoins >= amount)
        {
            currentCoins -= amount;
            targetCoins = currentCoins;
            
            if (!animateOnChange)
            {
                displayedCoins = currentCoins;
                UpdateUI();
            }
            else
            {
                animationTimer = 0f;
            }
            
            SaveCoins();
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Установить количество монет
    /// </summary>
    public void SetCoins(int amount)
    {
        currentCoins = Mathf.Max(0, amount);
        targetCoins = currentCoins;
        displayedCoins = currentCoins;
        UpdateUI();
        SaveCoins();
    }
    
    /// <summary>
    /// Получить текущее количество монет
    /// </summary>
    public int GetCoins()
    {
        return currentCoins;
    }
    
    /// <summary>
    /// Проверить хватает ли монет
    /// </summary>
    public bool HasEnoughCoins(int amount)
    {
        return currentCoins >= amount;
    }
    
    /// <summary>
    /// Обновить UI текст
    /// </summary>
    private void UpdateUI()
    {
        string coinText = displayedCoins.ToString();
        
        // Обновляем обычный Text
        if (coinsText != null)
        {
            coinsText.text = coinText;
        }
    }
    
    /// <summary>
    /// Сохранить монеты в PlayerPrefs
    /// </summary>
    private void SaveCoins()
    {
        if (saveCoins)
        {
            PlayerPrefs.SetInt(saveKey, currentCoins);
            PlayerPrefs.Save();
        }
    }
    
    /// <summary>
    /// Сбросить монеты до стартового значения
    /// </summary>
    public void ResetCoins()
    {
        SetCoins(startingCoins);
    }
    
    /// <summary>
    /// Установить UI компоненты динамически
    /// </summary>
    public void SetUIText(Text text)
    {
        coinsText = text;
        UpdateUI();
    }
    
    /// <summary>
    /// Установить UI компоненты динамически (TextMeshPro)
    /// </summary>
    public void SetUITextTMP(TextMeshProUGUI text)
    {
        UpdateUI();
    }
    
    void OnApplicationQuit()
    {
        SaveCoins();
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            SaveCoins();
        }
    }
}

