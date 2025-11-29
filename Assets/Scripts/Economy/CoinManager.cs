using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Менеджер для управления системой монет каждого игрока отдельно
/// Сохраняется в PlayerPrefs и работает локально без зависимости от сервера
/// </summary>
public class CoinManager : MonoBehaviour
{
    private const string PLAYER_PREFS_COINS_KEY = "PlayerCoins";
    
    [Header("UI Настройки")]
    [SerializeField] private Text coinsText; // Для обычного UI Text
    
    [Header("Настройки")]
    [SerializeField] private int startingCoins = 300;
    
    [Header("Визуальная обратная связь")]
    [SerializeField] private bool animateOnChange = true;
    [SerializeField] private float animationDuration = 0.5f;
    
    // Локальное количество монет
    private int currentCoins = 0;
    
    // Для анимации
    private int displayedCoins = 0;
    private float animationTimer = 0f;
    private int targetCoins = 0;
    
    void Start()
    {
        // Загружаем монеты из PlayerPrefs при старте
        //LoadCoins();
        currentCoins = startingCoins;
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
    /// Загружает монеты из PlayerPrefs
    /// </summary>
    private void LoadCoins()
    {
        if (PlayerPrefs.HasKey(PLAYER_PREFS_COINS_KEY))
        {
            currentCoins = PlayerPrefs.GetInt(PLAYER_PREFS_COINS_KEY);
        }
        else
        {
            currentCoins = startingCoins;
            SaveCoins(); // Сохраняем стартовое значение
        }
        Debug.Log($"[CoinManager] Загружено {currentCoins} монет из PlayerPrefs");
    }
    
    /// <summary>
    /// Сохраняет монеты в PlayerPrefs
    /// </summary>
    private void SaveCoins()
    {
        PlayerPrefs.SetInt(PLAYER_PREFS_COINS_KEY, currentCoins);
        PlayerPrefs.Save();
        Debug.Log($"[CoinManager] Сохранено {currentCoins} монет в PlayerPrefs");
    }
    
    /// <summary>
    /// Обновляет количество монет и сохраняет
    /// </summary>
    private void SetCoinsInternal(int amount, bool save = true)
    {
        int oldValue = currentCoins;
        currentCoins = Mathf.Max(0, amount);
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
        
        if (save)
        {
            SaveCoins();
        }
        
        Debug.Log($"[CoinManager] Монеты изменены: {oldValue} -> {currentCoins}");
    }
    
    /// <summary>
    /// Добавить монеты (локально)
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        
        SetCoinsInternal(currentCoins + amount);
    }
    
    /// <summary>
    /// Потратить монеты (локально)
    /// </summary>
    public void SpendCoins(int amount)
    {
        if (amount <= 0) return;
        
        if (currentCoins >= amount)
        {
            SetCoinsInternal(currentCoins - amount);
        }
    }
    
    /// <summary>
    /// Установить количество монет (локально)
    /// </summary>
    public void SetCoins(int amount)
    {
        SetCoinsInternal(amount);
    }
    
    /// <summary>
    /// Пытается списать монеты. Возвращает true при успехе.
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
            return true;
        
        if (currentCoins >= amount)
        {
            SetCoinsInternal(currentCoins - amount);
            return true;
        }
        
        return false;
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
        string coinText = CurrencyFormatter.FormatBits(displayedCoins);
        
        // Обновляем обычный Text
        if (coinsText != null)
        {
            coinsText.text = coinText;
        }
    }
    
    /// <summary>
    /// Сбросить монеты до стартового значения
    /// </summary>
    public void ResetCoins()
    {
        SetCoinsInternal(startingCoins);
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
    
    /// <summary>
    /// Получить CoinManager от локального игрока (для обратной совместимости)
    /// </summary>
    public static CoinManager GetLocalPlayerCoinManager()
    {
        // Ищем CoinManager на любом объекте в сцене
        CoinManager manager = FindObjectOfType<CoinManager>();
        return manager;
    }
}

