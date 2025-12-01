using UnityEngine;
using System.Collections;

/// <summary>
/// Контроллер для отображения титров игры
/// </summary>
public class CreditsController : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("Canvas с титрами (должен быть World Space или Screen Space)")]
    public Canvas creditsCanvas;
    
    [Tooltip("GameObject с титрами, который нужно активировать/деактивировать")]
    public GameObject creditsPanel;
    
    [Header("Animation")]
    [Tooltip("Animator компонент для проигрывания анимации титров")]
    public Animator creditsAnimator;
    
    [Tooltip("Имя триггера анимации для запуска титров")]
    public string animationTriggerName = "PlayCredits";
    
    [Header("Control Settings")]
    [Tooltip("Разрешить закрытие по ESC")]
    public bool allowCloseByESC = true;
    
    [Tooltip("Длительность анимации в секундах")]
    private const float animationDuration = 60f;
    
    private bool isShowing = false;
    private Coroutine creditsCoroutine;
    
    void Start()
    {
        // Скрываем титры при старте
        HideCredits();
    }
    
    void Update()
    {
        // Обработка ESC для закрытия титров
        if (allowCloseByESC && isShowing && Input.GetKeyDown(KeyCode.Escape))
        {
            StopCredits();
        }
    }
    
    /// <summary>
    /// Запускает показ титров
    /// </summary>
    public void ShowCredits()
    {
        if (isShowing)
        {
            return; // Уже показываются
        }
        
        isShowing = true;
        
        // Активируем панель титров
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
        
        if (creditsCanvas != null)
        {
            creditsCanvas.gameObject.SetActive(true);
        }
        
        // Настраиваем Canvas для World Space, если нужно
        if (creditsCanvas != null)
        {
            if (creditsCanvas.renderMode == RenderMode.WorldSpace && creditsCanvas.worldCamera == null)
            {
                // Получаем камеру автоматически
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    creditsCanvas.worldCamera = mainCamera;
                }
            }
        }
        
        // Запускаем анимацию
        if (creditsAnimator != null && !string.IsNullOrEmpty(animationTriggerName))
        {
            creditsAnimator.SetTrigger(animationTriggerName);
        }
        
        // Запускаем корутину для отслеживания длительности анимации
        if (creditsCoroutine != null)
        {
            StopCoroutine(creditsCoroutine);
        }
        creditsCoroutine = StartCoroutine(ShowCreditsSequence());
    }
    
    /// <summary>
    /// Останавливает показ титров
    /// </summary>
    public void StopCredits()
    {
        if (!isShowing)
        {
            return;
        }
        
        isShowing = false;
        
        if (creditsCoroutine != null)
        {
            StopCoroutine(creditsCoroutine);
            creditsCoroutine = null;
        }
        
        HideCredits();
    }
    
    /// <summary>
    /// Скрывает титры
    /// </summary>
    private void HideCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
        
        if (creditsCanvas != null)
        {
            creditsCanvas.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Последовательность показа титров (ожидание завершения анимации)
    /// </summary>
    private IEnumerator ShowCreditsSequence()
    {
        // Ждем завершения анимации (60 секунд)
        yield return new WaitForSeconds(animationDuration);
        
        // После завершения анимации закрываем титры
        StopCredits();
    }
    
    /// <summary>
    /// Проверяет, показываются ли титры в данный момент
    /// </summary>
    public bool IsShowing()
    {
        return isShowing;
    }
}
