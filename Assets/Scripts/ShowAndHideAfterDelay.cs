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
    
    [Tooltip("Текст 'Загрузка модов...'")]
    public GameObject loadingModsText;
    
    [Tooltip("Ссылка на ModConfiguration для загрузки ресурсов модов")]
    public ModConfiguration modConfiguration;
    
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
        SetTextVisibility(loadingModsText, false);
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
        
        // Проверяем, есть ли активные моды для загрузки
        bool hasActiveMods = false;
        if (modConfiguration != null)
        {
            // Получаем количество активных модов через публичный метод (нужно добавить)
            // Пока проверяем через загрузку модов - если есть что загружать, то hasActiveMods = true
            hasActiveMods = modConfiguration.HasActiveMods();
        }
        
        // Если есть активные моды, загружаем их первыми (приоритет)
        if (hasActiveMods)
        {
            // Этап 1: Загрузка модов и их ресурсов (приоритет)
            yield return StartCoroutine(LoadMods());
            
            // Этап 2: Загрузка игры (PlayerPrefs)
            yield return StartCoroutine(LoadGameSettings());
            
            // Этап 3: Загрузка текстур
            yield return StartCoroutine(LoadTextures());
            
            // Этап 4: Финализация
            yield return StartCoroutine(FinalizeLoading());
        }
        else
        {
            // Если модов нет, загружаем в стандартном порядке
            // Этап 1: Загрузка игры (PlayerPrefs)
            yield return StartCoroutine(LoadGameSettings());
            
            // Этап 2: Загрузка текстур
            yield return StartCoroutine(LoadTextures());
            
            // Этап 3: Финализация
            yield return StartCoroutine(FinalizeLoading());
        }
        
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
        SetTextVisibility(loadingModsText, false);
        SetTextVisibility(loadingTexturesText, false);
        SetTextVisibility(finalizationText, false);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        // Определяем диапазон прогресса в зависимости от того, загружались ли моды первыми
        bool modsLoadedFirst = (modConfiguration != null && modConfiguration.HasActiveMods());
        float progressStart = modsLoadedFirst ? 0.25f : 0f;
        float progressRange = modsLoadedFirst ? 0.25f : 0.25f;
        
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
            
            // Обновляем общий прогресс
            totalProgress = progressStart + (stageProgress * progressRange);
            UpdateProgress(totalProgress);
            
            yield return null;
        }
        
        // Убеждаемся, что прогресс правильный
        stageProgress = 1f;
        totalProgress = progressStart + progressRange;
        UpdateProgress(totalProgress);
        
        // Скрываем текст загрузки игры
        SetTextVisibility(loadingGameText, false);
    }
    
    private IEnumerator LoadTextures()
    {
        // Показываем текст загрузки текстур
        SetTextVisibility(loadingTexturesText, true);
        SetTextVisibility(loadingGameText, false);
        SetTextVisibility(loadingModsText, false);
        SetTextVisibility(finalizationText, false);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        // Определяем диапазон прогресса в зависимости от того, загружались ли моды первыми
        bool modsLoadedFirst = (modConfiguration != null && modConfiguration.HasActiveMods());
        float progressStart = modsLoadedFirst ? 0.5f : 0.25f;
        float progressRange = 0.25f;
        
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
            
            // Обновляем общий прогресс
            totalProgress = progressStart + (stageProgress * progressRange);
            UpdateProgress(totalProgress);
            
            yield return null;
        }
        
        // Убеждаемся, что прогресс правильный
        stageProgress = 1f;
        totalProgress = progressStart + progressRange;
        UpdateProgress(totalProgress);
        
        // Скрываем текст загрузки текстур
        SetTextVisibility(loadingTexturesText, false);
    }
    
    private IEnumerator LoadMods()
    {
        // Показываем текст загрузки модов
        SetTextVisibility(loadingModsText, true);
        SetTextVisibility(loadingGameText, false);
        SetTextVisibility(loadingTexturesText, false);
        SetTextVisibility(finalizationText, false);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        // Загружаем ресурсы модов через ModConfiguration
        if (modConfiguration != null)
        {
            yield return StartCoroutine(modConfiguration.LoadModResources((progress) =>
            {
                stageProgress = progress;
                // Обновляем общий прогресс (этап 1 = 0-25% если моды загружаются первыми)
                totalProgress = stageProgress * 0.25f;
                UpdateProgress(totalProgress);
            }));
        }
        else
        {
            // Если ModConfiguration не назначен, просто ждем минимальное время
            while (elapsedTime < minLoadTimePerStage)
            {
                elapsedTime += Time.deltaTime;
                stageProgress = Mathf.Clamp01(elapsedTime / minLoadTimePerStage);
                totalProgress = stageProgress * 0.25f;
                UpdateProgress(totalProgress);
                yield return null;
            }
            stageProgress = 1f;
        }
        
        // Убеждаемся, что прогресс = 25%
        totalProgress = 0.25f;
        UpdateProgress(totalProgress);
        
        // Скрываем текст загрузки модов
        SetTextVisibility(loadingModsText, false);
    }
    
    private IEnumerator FinalizeLoading()
    {
        // Показываем текст финализации
        SetTextVisibility(finalizationText, true);
        SetTextVisibility(loadingGameText, false);
        SetTextVisibility(loadingModsText, false);
        SetTextVisibility(loadingTexturesText, false);
        
        float stageProgress = 0f;
        float elapsedTime = 0f;
        
        // Определяем диапазон прогресса в зависимости от того, загружались ли моды первыми
        bool modsLoadedFirst = (modConfiguration != null && modConfiguration.HasActiveMods());
        float progressStart = modsLoadedFirst ? 0.75f : 0.5f;
        float progressRange = 0.25f;
        
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
            
            // Обновляем общий прогресс
            totalProgress = progressStart + (stageProgress * progressRange);
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

