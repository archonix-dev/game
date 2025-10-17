using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Компонент для отображения UI монет. Автоматически подключается к CoinManager
/// </summary>
public class CoinUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Используйте Text для стандартного UI или TextMeshProUGUI для TextMeshPro")]
    [SerializeField] private Text coinsText;
    [SerializeField] private TextMeshProUGUI coinsTMPText;
    
    [Header("Display Settings")]
    [SerializeField] private string prefix = "Монеты: ";
    [SerializeField] private string suffix = "";
    [SerializeField] private bool useThousandsSeparator = false;
    
    [Header("Animation Settings")]
    [SerializeField] private bool animateOnIncrease = true;
    [SerializeField] private float scaleMultiplier = 1.2f;
    [SerializeField] private float scaleAnimDuration = 0.3f;
    
    [Header("Color Settings")]
    [SerializeField] private bool changeColorOnIncrease = true;
    [SerializeField] private Color increaseColor = Color.green;
    [SerializeField] private float colorChangeDuration = 0.5f;
    
    private int lastCoinAmount = 0;
    private Vector3 originalScale;
    private float scaleTimer = 0f;
    private bool isScaling = false;
    
    private Color originalColor;
    private float colorTimer = 0f;
    private bool isColorChanging = false;
    
    void Start()
    {
        // Сохраняем оригинальный scale
        originalScale = transform.localScale;
        
        // Получаем оригинальный цвет
        if (coinsText != null)
        {
            originalColor = coinsText.color;
        }
        else if (coinsTMPText != null)
        {
            originalColor = coinsTMPText.color;
        }
        
        // Регистрируемся в CoinManager
        if (CoinManager.Instance != null)
        {
            if (coinsText != null)
            {
                CoinManager.Instance.SetUIText(coinsText);
            }
            if (coinsTMPText != null)
            {
                CoinManager.Instance.SetUITextTMP(coinsTMPText);
            }
            
            lastCoinAmount = CoinManager.Instance.GetCoins();
        }
        else
        {
            Debug.LogWarning("CoinUI: CoinManager не найден в сцене!");
        }
        
        UpdateDisplay();
    }
    
    void Update()
    {
        // Проверяем изменения количества монет
        if (CoinManager.Instance != null)
        {
            int currentCoins = CoinManager.Instance.GetCoins();
            if (currentCoins != lastCoinAmount)
            {
                if (currentCoins > lastCoinAmount)
                {
                    OnCoinsIncreased();
                }
                lastCoinAmount = currentCoins;
            }
        }
        
        // Анимация scale
        if (isScaling)
        {
            scaleTimer += Time.deltaTime;
            float progress = scaleTimer / scaleAnimDuration;
            
            if (progress < 0.5f)
            {
                // Увеличиваем
                float scale = Mathf.Lerp(1f, scaleMultiplier, progress * 2f);
                transform.localScale = originalScale * scale;
            }
            else
            {
                // Уменьшаем обратно
                float scale = Mathf.Lerp(scaleMultiplier, 1f, (progress - 0.5f) * 2f);
                transform.localScale = originalScale * scale;
            }
            
            if (progress >= 1f)
            {
                transform.localScale = originalScale;
                isScaling = false;
                scaleTimer = 0f;
            }
        }
        
        // Анимация цвета
        if (isColorChanging)
        {
            colorTimer += Time.deltaTime;
            float progress = colorTimer / colorChangeDuration;
            
            Color currentColor = Color.Lerp(increaseColor, originalColor, progress);
            
            if (coinsText != null)
            {
                coinsText.color = currentColor;
            }
            if (coinsTMPText != null)
            {
                coinsTMPText.color = currentColor;
            }
            
            if (progress >= 1f)
            {
                if (coinsText != null) coinsText.color = originalColor;
                if (coinsTMPText != null) coinsTMPText.color = originalColor;
                
                isColorChanging = false;
                colorTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// Обновить отображение монет
    /// </summary>
    private void UpdateDisplay()
    {
        if (CoinManager.Instance == null) return;
        
        int coins = CoinManager.Instance.GetCoins();
        string displayText = FormatCoinText(coins);
        
        if (coinsText != null)
        {
            coinsText.text = displayText;
        }
        if (coinsTMPText != null)
        {
            coinsTMPText.text = displayText;
        }
    }
    
    /// <summary>
    /// Форматировать текст с монетами
    /// </summary>
    private string FormatCoinText(int amount)
    {
        string amountText;
        
        if (useThousandsSeparator)
        {
            amountText = amount.ToString("N0");
        }
        else
        {
            amountText = amount.ToString();
        }
        
        return prefix + amountText + suffix;
    }
    
    /// <summary>
    /// Вызывается когда монеты увеличиваются
    /// </summary>
    private void OnCoinsIncreased()
    {
        if (animateOnIncrease)
        {
            isScaling = true;
            scaleTimer = 0f;
        }
        
        if (changeColorOnIncrease)
        {
            isColorChanging = true;
            colorTimer = 0f;
        }
    }
    
    /// <summary>
    /// Установить префикс текста
    /// </summary>
    public void SetPrefix(string newPrefix)
    {
        prefix = newPrefix;
        UpdateDisplay();
    }
    
    /// <summary>
    /// Установить суффикс текста
    /// </summary>
    public void SetSuffix(string newSuffix)
    {
        suffix = newSuffix;
        UpdateDisplay();
    }
}

