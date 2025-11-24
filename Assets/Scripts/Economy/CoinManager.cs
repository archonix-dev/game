using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Менеджер для управления системой монет каждого игрока отдельно
/// Синхронизируется через сеть с помощью Mirror
/// </summary>
public class CoinManager : NetworkBehaviour
{
    [Header("UI Настройки")]
    [SerializeField] private Text coinsText; // Для обычного UI Text
    
    [Header("Настройки")]
    [SerializeField] private int startingCoins = 0;
    
    [Header("Визуальная обратная связь")]
    [SerializeField] private bool animateOnChange = true;
    [SerializeField] private float animationDuration = 0.5f;
    
    // Сетевая переменная для синхронизации количества монет
    [SyncVar(hook = nameof(OnCoinsChanged))]
    private int currentCoins = 0;
    
    // Для анимации (только на клиенте)
    private int displayedCoins = 0;
    private float animationTimer = 0f;
    private int targetCoins = 0;
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Инициализируем монеты на сервере
        if (isServer)
        {
            currentCoins = startingCoins;
            Debug.Log($"[CoinManager] Инициализировано {currentCoins} монет для игрока {netId}");
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Инициализируем UI на клиенте
        displayedCoins = currentCoins;
        targetCoins = currentCoins;
        UpdateUI();
    }
    
    void Start()
    {
        // Если не в сети, инициализируем локально (для тестирования)
        if (netIdentity == null || netIdentity.netId == 0)
        {
            currentCoins = startingCoins;
            displayedCoins = currentCoins;
            targetCoins = currentCoins;
            UpdateUI();
        }
    }
    
    void Update()
    {
        // Анимация изменения монет (только для локального игрока)
        if (isOwned && animateOnChange && displayedCoins != targetCoins)
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
    /// Hook для синхронизации монет (вызывается при изменении SyncVar)
    /// </summary>
    void OnCoinsChanged(int oldValue, int newValue)
    {
        // Обновляем UI только для локального игрока
        if (isOwned)
        {
            targetCoins = newValue;
            
            if (!animateOnChange)
            {
                displayedCoins = newValue;
                UpdateUI();
            }
            else
            {
                animationTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// Добавить монеты (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        
        currentCoins += amount;
        Debug.Log($"[CoinManager] Добавлено {amount} монет. Всего: {currentCoins} (игрок {netId})");
    }
    
    /// <summary>
    /// Потратить монеты (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void SpendCoins(int amount)
    {
        if (amount <= 0) return;
        
        if (currentCoins >= amount)
        {
            currentCoins -= amount;
            Debug.Log($"[CoinManager] Потрачено {amount} монет. Осталось: {currentCoins} (игрок {netId})");
        }
    }
    
    /// <summary>
    /// Установить количество монет (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void SetCoins(int amount)
    {
        currentCoins = Mathf.Max(0, amount);
        Debug.Log($"[CoinManager] Установлено {currentCoins} монет (игрок {netId})");
    }
    
    /// <summary>
    /// Пытается списать монеты напрямую на сервере (без команды).
    /// Возвращает true при успехе.
    /// </summary>
    [Server]
    public bool TrySpendCoinsServer(int amount)
    {
        if (amount <= 0)
            return true;
        
        if (currentCoins >= amount)
        {
            currentCoins -= amount;
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
    /// Обновить UI текст (только для локального игрока)
    /// </summary>
    private void UpdateUI()
    {
        // Обновляем UI только для локального игрока
        if (!isOwned && netIdentity != null && netIdentity.netId != 0)
        {
            return;
        }
        
        string coinText = CurrencyFormatter.FormatBits(displayedCoins);
        
        // Обновляем обычный Text
        if (coinsText != null)
        {
            coinsText.text = coinText;
        }
    }
    
    /// <summary>
    /// Сбросить монеты до стартового значения (вызывается на клиенте, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    public void ResetCoins()
    {
        currentCoins = startingCoins;
        Debug.Log($"[CoinManager] Монеты сброшены до {startingCoins} (игрок {netId})");
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
        if (NetworkClient.localPlayer != null)
        {
            return NetworkClient.localPlayer.GetComponent<CoinManager>();
        }
        return null;
    }
}

