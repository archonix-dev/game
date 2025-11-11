using UnityEngine;
using System.Collections;
using TMPro;

public class ShowAndHideAfterDelay : MonoBehaviour
{
    [Header("TextMeshPro 3D Components")]
    [Tooltip("Первая строка TextMeshPro 3D (для 'sudo starting game')")]
    public TextMeshPro firstLineText;
    
    [Tooltip("Вторая строка TextMeshPro 3D (для 'launching...' и статусов загрузки)")]
    public TextMeshPro secondLineText;
    
    [Tooltip("Третья строка TextMeshPro 3D (для 'archonix dev...')")]
    public TextMeshPro thirdLineText;
    
    [Header("GameObjects")]
    [Tooltip("GameObject для анимации scale во время загрузки")]
    public GameObject loadingScaleObject;
    
    [Tooltip("Объект, который нужно скрыть после завершения")]
    public GameObject objectToHide;
    
    [Tooltip("Объект, который появляется после 'launching...'")]
    public GameObject objectAfterLaunching;
    
    [Tooltip("Объект, который скрывается после загрузки")]
    public GameObject objectToHideAfterLoading;
    
    [Header("Settings")]
    [Tooltip("Ссылка на ModConfiguration для загрузки ресурсов модов")]
    public ModConfiguration modConfiguration;
    
    [Tooltip("Скорость печати текста (символов в секунду)")]
    public float typingSpeed = 20f;
    
    [Tooltip("Задержка перед началом (в секундах)")]
    public float startDelay = 1f;
    
    [Tooltip("Время анимации scale (в секундах)")]
    public float scaleAnimationDuration = 2f;
    
    [Tooltip("Минимальное время загрузки каждого этапа (в секундах)")]
    public float minLoadTimePerStage = 0.5f;
    
    private static bool hasShownOnce = false;
    private Vector3 initialScale = new Vector3(0f, 1f, 1f);
    private Vector3 targetScale = new Vector3(-1f, 1f, 1f);
    
    void Start()
    {
        // Убеждаемся, что объект активен
        if (objectToHide != null && !objectToHide.activeSelf)
        {
            objectToHide.SetActive(true);
        }
        
        // Проверяем, был ли вызван ResetShowState() перед перезагрузкой сцены
        // Если hasShownOnce = false, значит нужно показать загрузку
        if (!hasShownOnce)
        {
            hasShownOnce = true;
            
            // Инициализация
            Initialize();
            
            // Запуск последовательности
            StartCoroutine(LoadingSequence());
        }
        else
        {
            // Если уже показывали, сразу скрываем объект
            if (objectToHide != null)
            {
                objectToHide.SetActive(false);
            }
        }
    }
    
    private void Initialize()
    {
        // Убеждаемся, что объект активен
        if (objectToHide != null)
        {
            objectToHide.SetActive(true);
        }
        
        // Очищаем все тексты
        if (firstLineText != null)
        {
            firstLineText.text = "";
            firstLineText.gameObject.SetActive(true);
        }
        
        if (secondLineText != null)
        {
            secondLineText.text = "";
            secondLineText.gameObject.SetActive(true);
        }
        
        if (thirdLineText != null)
        {
            thirdLineText.text = "";
            thirdLineText.gameObject.SetActive(false);
        }
        
        // Устанавливаем начальный scale для объекта загрузки
        if (loadingScaleObject != null)
        {
            loadingScaleObject.transform.localScale = initialScale;
        }
        
        // Скрываем объект, который появится после launching
        if (objectAfterLaunching != null)
        {
            objectAfterLaunching.SetActive(false);
        }
        
        // Убеждаемся, что объект для скрытия после загрузки активен изначально
        if (objectToHideAfterLoading != null)
        {
            objectToHideAfterLoading.SetActive(true);
        }
    }
    
    private IEnumerator LoadingSequence()
    {
        // Ждем задержку перед началом
        yield return new WaitForSeconds(startDelay);
        
        // Этап 1: Печатаем "sudo starting game" в первой строке
        if (firstLineText != null)
        {
            yield return StartCoroutine(TypeText(firstLineText, "$ sudo starting game"));
        }
        
        // Этап 2: Печатаем "launching..." во второй строке
        if (secondLineText != null)
        {
            yield return StartCoroutine(TypeText(secondLineText, "launching..."));
            
            // Показываем объект после launching
            if (objectAfterLaunching != null)
            {
                objectAfterLaunching.SetActive(true);
            }
            
            // Очищаем вторую строку
            secondLineText.text = "";
            
            // Запускаем анимацию scale
            if (loadingScaleObject != null)
            {
                StartCoroutine(AnimateScale());
            }
            
            // Загружаем игру
            yield return StartCoroutine(LoadGame());
            
            // Скрываем объект и первую строку после завершения загрузки
            if (objectAfterLaunching != null)
            {
                objectAfterLaunching.SetActive(false);
            }
            
            if (firstLineText != null)
            {
                firstLineText.gameObject.SetActive(false);
            }
        }
        
        // Этап 3: Показываем третью строку и печатаем "archonix dev..."
        if (thirdLineText != null)
        {
            thirdLineText.gameObject.SetActive(true);
            yield return StartCoroutine(TypeText(thirdLineText, "archonix dev..."));
        }
        
        // Этап 4: Скрываем объекты после загрузки
        if (objectToHide != null)
        {
            objectToHide.SetActive(false);
        }
        
        if (objectToHideAfterLoading != null)
        {
            objectToHideAfterLoading.SetActive(false);
        }
    }
    
    private IEnumerator TypeText(TextMeshPro textComponent, string text)
    {
        if (textComponent == null || string.IsNullOrEmpty(text))
        {
            yield break;
        }
        
        textComponent.text = "";
        float delay = 1f / typingSpeed;
        
        for (int i = 0; i < text.Length; i++)
        {
            textComponent.text += text[i];
            yield return new WaitForSeconds(delay);
        }
    }
    
    private IEnumerator AnimateScale()
    {
        if (loadingScaleObject == null)
        {
            yield break;
        }
        
        float elapsedTime = 0f;
        Vector3 startScale = initialScale;
        Vector3 endScale = targetScale;
        
        while (elapsedTime < scaleAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / scaleAnimationDuration);
            
            // Используем плавную интерполяцию
            loadingScaleObject.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            
            yield return null;
        }
        
        // Убеждаемся, что scale установлен точно
        loadingScaleObject.transform.localScale = endScale;
    }
    
    private IEnumerator LoadGame()
    {
        // Проверяем, есть ли активные моды для загрузки
        bool hasActiveMods = false;
        if (modConfiguration != null)
        {
            hasActiveMods = modConfiguration.HasActiveMods();
        }
        
        // Если есть активные моды, загружаем их первыми
        if (hasActiveMods)
        {
            // Загрузка модов
            yield return StartCoroutine(LoadMods());
            
            // Загрузка настроек игры
            yield return StartCoroutine(LoadGameSettings());
            
            // Загрузка текстур
            yield return StartCoroutine(LoadTextures());
            
            // Финализация
            yield return StartCoroutine(FinalizeLoading());
        }
        else
        {
            // Если модов нет, загружаем в стандартном порядке
            yield return StartCoroutine(LoadGameSettings());
            yield return StartCoroutine(LoadTextures());
            yield return StartCoroutine(FinalizeLoading());
        }
    }
    
    private IEnumerator LoadMods()
    {
        if (secondLineText != null)
        {
            secondLineText.text = "";
            yield return StartCoroutine(TypeText(secondLineText, "loading mods..."));
        }
        
        float elapsedTime = 0f;
        
        // Загружаем ресурсы модов через ModConfiguration
        if (modConfiguration != null)
        {
            yield return StartCoroutine(modConfiguration.LoadModResources((progress) =>
            {
                // Прогресс загрузки модов
            }));
        }
        
        // Минимальное время для этапа
        while (elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Очищаем текст
        if (secondLineText != null)
        {
            secondLineText.text = "";
        }
    }
    
    private IEnumerator LoadGameSettings()
    {
        if (secondLineText != null)
        {
            secondLineText.text = "";
            yield return StartCoroutine(TypeText(secondLineText, "loading game settings..."));
        }
        
        float elapsedTime = 0f;
        
        // Загружаем PlayerPrefs настройки
        PlayerPrefs.GetInt("PlayerCoins", 0);
        PlayerPrefs.GetFloat("MasterVolume", 1f);
        PlayerPrefs.GetFloat("MusicVolume", 1f);
        PlayerPrefs.GetString("Language", "ru");
        PlayerPrefs.GetInt("QualityLevel", 2);
        PlayerPrefs.GetFloat("RenderScale", 1f);
        
        // Минимальное время для этапа
        while (elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Очищаем текст
        if (secondLineText != null)
        {
            secondLineText.text = "";
        }
    }
    
    private IEnumerator LoadTextures()
    {
        if (secondLineText != null)
        {
            secondLineText.text = "";
            yield return StartCoroutine(TypeText(secondLineText, "loading textures..."));
        }
        
        float elapsedTime = 0f;
        
        // Минимальное время для этапа
        while (elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Очищаем текст
        if (secondLineText != null)
        {
            secondLineText.text = "";
        }
    }
    
    private IEnumerator FinalizeLoading()
    {
        if (secondLineText != null)
        {
            secondLineText.text = "";
            yield return StartCoroutine(TypeText(secondLineText, "finalizing..."));
        }
        
        float elapsedTime = 0f;
        
        // Инициализация менеджеров
        if (CoinManager.Instance == null)
        {
            // Ждем инициализации CoinManager
        }
        
        // Минимальное время для этапа
        while (elapsedTime < minLoadTimePerStage)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Очищаем текст
        if (secondLineText != null)
        {
            secondLineText.text = "";
        }
    }
    
    public static void ResetShowState()
    {
        hasShownOnce = false;
    }
    
    /// <summary>
    /// Принудительный запуск последовательности загрузки (для рестарта сцены)
    /// </summary>
    public void ForceStartSequence()
    {
        // Останавливаем все корутины на этом объекте
        StopAllCoroutines();
        
        // Сбрасываем флаг
        hasShownOnce = false;
        
        // Убеждаемся, что объект активен
        if (objectToHide != null)
        {
            objectToHide.SetActive(true);
        }
        
        // Инициализация
        Initialize();
        
        // Запуск последовательности
        StartCoroutine(LoadingSequence());
    }
}
