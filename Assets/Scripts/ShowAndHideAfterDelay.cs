using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class ShowAndHideAfterDelay : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Объект, который нужно показать и скрыть")]
    public GameObject targetObject;
    
    [Header("UI Элементы")]
    [Tooltip("Slider для отображения прогресса загрузки")]
    public Slider progressSlider;
    
    [Tooltip("Текст 'Загрузка игры...'")]
    public GameObject loadingGameText;
    
    [Tooltip("Текст 'Загрузка текстур...'")]
    public GameObject loadingTexturesText;
    
    [Tooltip("Текст 'Финализация...'")]
    public GameObject finalizationText;
    
    [Header("Анимация")]
    [Tooltip("Animator компонент для управления анимацией")]
    public Animator animator;
    
    [Tooltip("Время паузы анимации (в секундах)")]
    public float animationPauseTime = 3f;
    
    [Header("Настройки загрузки")]
    [Tooltip("Минимальное время загрузки каждого этапа (в секундах)")]
    public float minLoadTimePerStage = 0.5f;
    
    [Tooltip("Время до скрытия объекта после возобновления анимации (в секундах)")]
    public float hideDelayAfterAnimationResume = 3f;
    
    [Tooltip("Пути к текстурам для загрузки (опционально)")]
    public string[] texturePaths;
    
    private static bool hasShownOnce = false;
    private float animationPauseTimer = 0f;
    private bool isAnimationPaused = false;
    private float animationSpeedBeforePause = 1f;
    
    // Поля для асинхронной загрузки
    private float totalProgress = 0f;
    private float totalElapsedTime = 0f;
    private bool animationPauseChecked = false;
    private bool isLoadingComplete = false;
    private bool animationResumed = false;
    
    void Start()
    {
        if (targetObject == null)
        {
            targetObject = gameObject;
        }
        
        if (!hasShownOnce)
        {
            hasShownOnce = true;
            targetObject.SetActive(true);
            
            // Инициализация UI
            InitializeUI();
            
            // Запуск асинхронной загрузки
            StartCoroutine(AsyncLoadSequence());
        }
        else
        {
            targetObject.SetActive(false);
        }
    }
    
    void Update()
    {
        // Управление паузой анимации во время загрузки (до 3 секунд)
        if (isAnimationPaused && animator != null && !isLoadingComplete)
        {
            animationPauseTimer += Time.deltaTime;
            
            // Если прошло 3 секунды, анимация остается приостановленной до завершения загрузки
            // (возобновление произойдет в AsyncLoadSequence после завершения загрузки)
        }
    }
    
    private void InitializeUI()
    {
        // Инициализация slider
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
        }
        
        // Скрываем все тексты изначально
        SetTextVisibility(loadingGameText, false);
        SetTextVisibility(loadingTexturesText, false);
        SetTextVisibility(finalizationText, false);
    }
    
    private void SetTextVisibility(GameObject textObject, bool visible)
    {
        if (textObject != null)
        {
            textObject.SetActive(visible);
        }
    }
    
    private void UpdateProgress(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }
    }
    
    private IEnumerator AsyncLoadSequence()
    {
        // Инициализация переменных загрузки
        totalProgress = 0f;
        totalElapsedTime = 0f;
        animationPauseChecked = false;
        isLoadingComplete = false;
        animationResumed = false;
        
        // Этап 1: Загрузка игры (PlayerPrefs)
        yield return StartCoroutine(LoadGameSettings());
        
        // Этап 2: Загрузка текстур
        yield return StartCoroutine(LoadTextures());
        
        // Этап 3: Финализация
        yield return StartCoroutine(FinalizeLoading());
        
        // Загрузка завершена
        isLoadingComplete = true;
        
        // Возобновляем анимацию, если она была приостановлена
        if (isAnimationPaused && animator != null)
        {
            animator.speed = animationSpeedBeforePause;
            isAnimationPaused = false;
            animationPauseTimer = 0f;
            animationResumed = true;
        }
        else if (animator != null)
        {
            // Анимация не была приостановлена (загрузка завершилась до 3 секунд)
            animationResumed = true;
        }
        
        // Запускаем корутину для скрытия объекта через 3 секунды после возобновления анимации
        StartCoroutine(HideObjectAfterDelay());
    }
    
    private IEnumerator HideObjectAfterDelay()
    {
        // Ждем указанное время после возобновления анимации
        yield return new WaitForSeconds(hideDelayAfterAnimationResume);
        
        // Скрываем объект
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }
    
    private IEnumerator LoadGameSettings()
    {
        // Показываем текст загрузки игры
        SetTextVisibility(loadingGameText, true);
        SetTextVisibility(loadingTexturesText, false);
        SetTextVisibility(finalizationText, false);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        while (stageProgress < 0.99f || elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            totalElapsedTime += Time.deltaTime;
            
            // Проверка на паузу анимации на 3 секунде
            if (!animationPauseChecked && totalElapsedTime >= animationPauseTime)
            {
                CheckAnimationPause();
                animationPauseChecked = true;
            }
            
            // Загружаем PlayerPrefs настройки
            if (stageProgress < 0.3f)
            {
                // Загрузка базовых настроек
                PlayerPrefs.GetInt("PlayerCoins", 0);
                PlayerPrefs.GetFloat("MasterVolume", 1f);
                PlayerPrefs.GetFloat("MusicVolume", 1f);
                PlayerPrefs.GetString("Language", "ru");
                stageProgress = 0.3f;
            }
            else if (stageProgress < 0.6f)
            {
                // Загрузка дополнительных настроек
                PlayerPrefs.GetInt("QualityLevel", 2);
                PlayerPrefs.GetFloat("RenderScale", 1f);
                stageProgress = 0.6f;
            }
            else if (stageProgress < 0.99f)
            {
                // Завершение этапа
                stageProgress = Mathf.Lerp(stageProgress, 1f, Time.deltaTime * 2f);
            }
            else
            {
                // Принудительно завершаем этап
                stageProgress = 1f;
            }
            
            // Обновляем общий прогресс (этап 1 = 0-33%)
            totalProgress = stageProgress * 0.33f;
            UpdateProgress(totalProgress);
            
            yield return null;
        }
        
        // Убеждаемся, что прогресс = 33%
        stageProgress = 1f;
        totalProgress = 0.33f;
        UpdateProgress(totalProgress);
        
        // Скрываем текст загрузки игры
        SetTextVisibility(loadingGameText, false);
    }
    
    private IEnumerator LoadTextures()
    {
        // Показываем текст загрузки текстур
        SetTextVisibility(loadingTexturesText, true);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        // Минимальная задержка для этапа загрузки текстур
        while (stageProgress < 0.99f || elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            totalElapsedTime += Time.deltaTime;
            
            // Проверка на паузу анимации на 3 секунде
            if (!animationPauseChecked && totalElapsedTime >= animationPauseTime)
            {
                CheckAnimationPause();
                animationPauseChecked = true;
            }
            
            // Плавное увеличение прогресса
            if (stageProgress < 0.99f)
            {
                stageProgress = Mathf.Lerp(stageProgress, 1f, Time.deltaTime * 2f);
            }
            else
            {
                stageProgress = 1f;
            }
            
            // Обновляем общий прогресс (этап 2 = 33-66%)
            totalProgress = 0.33f + (stageProgress * 0.33f);
            UpdateProgress(totalProgress);
            
            yield return null;
        }
        
        // Убеждаемся, что прогресс этапа = 66%
        stageProgress = 1f;
        totalProgress = 0.66f;
        UpdateProgress(totalProgress);
        
        // Скрываем текст загрузки текстур
        SetTextVisibility(loadingTexturesText, false);
    }
    
    private IEnumerator FinalizeLoading()
    {
        // Показываем текст финализации
        SetTextVisibility(finalizationText, true);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        while (stageProgress < 0.99f || elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            totalElapsedTime += Time.deltaTime;
            
            // Проверка на паузу анимации на 3 секунде
            if (!animationPauseChecked && totalElapsedTime >= animationPauseTime)
            {
                CheckAnimationPause();
                animationPauseChecked = true;
            }
            
            // Прогрузка скриптов и финализация
            if (stageProgress < 0.5f)
            {
                // Инициализация менеджеров
                if (CoinManager.Instance == null)
                {
                    // Ждем инициализации CoinManager
                }
                stageProgress = 0.5f;
            }
            else if (stageProgress < 0.99f)
            {
                // Завершение загрузки
                stageProgress = Mathf.Lerp(stageProgress, 1f, Time.deltaTime * 2f);
            }
            else
            {
                stageProgress = 1f;
            }
            
            // Обновляем общий прогресс (этап 3 = 66-100%)
            totalProgress = 0.66f + (stageProgress * 0.34f);
            UpdateProgress(totalProgress);
            
            yield return null;
        }
        
        // Убеждаемся, что прогресс = 100%
        stageProgress = 1f;
        totalProgress = 1f;
        UpdateProgress(totalProgress);
        
        // Скрываем текст финализации
        SetTextVisibility(finalizationText, false);
    }
    
    private void CheckAnimationPause()
    {
        if (animator != null && !isAnimationPaused)
        {
            // Сохраняем текущую скорость анимации
            animationSpeedBeforePause = animator.speed;
            
            // Приостанавливаем анимацию
            animator.speed = 0f;
            isAnimationPaused = true;
            animationPauseTimer = 0f;
        }
    }
    
    public static void ResetShowState()
    {
        hasShownOnce = false;
    }
}

