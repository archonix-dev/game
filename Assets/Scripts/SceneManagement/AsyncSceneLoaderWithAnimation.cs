using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Скрипт для асинхронной загрузки сцены с анимацией загрузки
/// </summary>
public class AsyncSceneLoaderWithAnimation : MonoBehaviour
{
    [Header("Настройки загрузки")]
    [Tooltip("Имя сцены для загрузки")]
    public string sceneName = "Mansion";
    
    [Header("Настройки анимации")]
    [Tooltip("GameObject с аниматором, который будет показываться во время загрузки")]
    public GameObject loadingObject;
    
    [Tooltip("Аниматор для анимации загрузки")]
    public Animator loadingAnimator;
    
    [Tooltip("Имя анимации загрузки")]
    private const string LOADING_ANIMATION_NAME = "loadingmainscene";
    
    [Header("Тайминги")]
    [Tooltip("Время (в секундах) до начала загрузки сцены")]
    public float loadStartTime = 3f;
    
    [Tooltip("Общее время (в секундах) до скрытия объекта")]
    public float hideTime = 8f;
    
    private bool isLoading = false;
    private AsyncOperation asyncLoad;
    private Coroutine loadingCoroutine;
    private bool isInitialized = false;
    
    void Awake()
    {
        // Делаем сам скрипт постоянным между сценами, чтобы корутина не прерывалась
        DontDestroyOnLoad(gameObject);
        
        // Инициализируем объект загрузки, чтобы он не уничтожался при переходе между сценами
        InitializeLoadingObject();
    }
    
    /// <summary>
    /// Инициализирует объект загрузки и делает его постоянным между сценами
    /// </summary>
    private void InitializeLoadingObject()
    {
        if (loadingObject != null && !isInitialized)
        {
            // Делаем объект загрузки постоянным между сценами
            DontDestroyOnLoad(loadingObject);
            isInitialized = true;
            Debug.Log("[AsyncSceneLoaderWithAnimation] Объект загрузки настроен как постоянный между сценами");
        }
    }
    
    /// <summary>
    /// Запускает процесс загрузки сцены с анимацией
    /// </summary>
    public void StartLoading()
    {
        if (isLoading)
        {
            Debug.LogWarning("[AsyncSceneLoaderWithAnimation] Загрузка уже идет!");
            return;
        }
        
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[AsyncSceneLoaderWithAnimation] Имя сцены не указано!");
            return;
        }
        
        if (loadingObject == null)
        {
            Debug.LogError("[AsyncSceneLoaderWithAnimation] Объект загрузки не назначен!");
            return;
        }
        
        // Инициализируем объект загрузки, если еще не инициализирован
        InitializeLoadingObject();
        
        if (loadingAnimator == null)
        {
            loadingAnimator = loadingObject.GetComponent<Animator>();
            if (loadingAnimator == null)
            {
                Debug.LogError("[AsyncSceneLoaderWithAnimation] Аниматор не найден на объекте загрузки!");
                return;
            }
        }
        
        isLoading = true;
        loadingCoroutine = StartCoroutine(LoadingCoroutine());
    }
    
    /// <summary>
    /// Корутина для управления процессом загрузки
    /// </summary>
    private IEnumerator LoadingCoroutine()
    {
        // Показываем объект загрузки
        loadingObject.SetActive(true);
        
        // Получаем аниматор, если не назначен
        if (loadingAnimator == null)
        {
            loadingAnimator = loadingObject.GetComponent<Animator>();
            if (loadingAnimator == null)
            {
                Debug.LogError("[AsyncSceneLoaderWithAnimation] Аниматор не найден на объекте загрузки!");
                yield break;
            }
        }
        
        // Запускаем анимацию
        loadingAnimator.Play(LOADING_ANIMATION_NAME);
        
        // Отслеживаем общее время с начала
        float totalElapsedTime = 0f;
        
        // Ждем до момента начала загрузки (3 секунды)
        while (totalElapsedTime < loadStartTime)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Приостанавливаем анимацию на 3 секунде
        AnimatorStateInfo stateInfo = loadingAnimator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = stateInfo.normalizedTime;
        loadingAnimator.speed = 0f; // Останавливаем анимацию
        
        // Начинаем асинхронную загрузку сцены
        asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false; // Не активируем сцену сразу
        
        // Ждем, пока сцена загрузится (продолжаем отслеживать время)
        while (asyncLoad.progress < 0.9f)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Сцена загружена, активируем её
        asyncLoad.allowSceneActivation = true;
        
        // Ждем, пока сцена полностью активируется (продолжаем отслеживать время)
        while (!asyncLoad.isDone)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // После активации сцены нужно подождать один кадр, чтобы новая сцена полностью загрузилась
        yield return null;
        
        // Проверяем, что объект загрузки все еще существует (он должен быть постоянным)
        if (loadingObject == null || loadingAnimator == null)
        {
            Debug.LogWarning("[AsyncSceneLoaderWithAnimation] Объект загрузки или аниматор уничтожены после загрузки сцены!");
            isLoading = false;
            yield break;
        }
        
        // Возобновляем анимацию с того же места (3 секунды)
        loadingAnimator.speed = 1f;
        loadingAnimator.Play(LOADING_ANIMATION_NAME, 0, normalizedTime);
        
        // Ждем до момента скрытия объекта (8 секунд от начала)
        // Продолжаем отслеживать время, пока не пройдет 8 секунд
        while (totalElapsedTime < hideTime)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Скрываем объект загрузки
        if (loadingObject != null)
        {
            loadingObject.SetActive(false);
        }
        
        isLoading = false;
    }
    
    /// <summary>
    /// Останавливает процесс загрузки
    /// </summary>
    public void StopLoading()
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        
        if (loadingAnimator != null)
        {
            loadingAnimator.speed = 1f;
        }
        
        if (loadingObject != null)
        {
            loadingObject.SetActive(false);
        }
        
        isLoading = false;
    }
    
    void OnDestroy()
    {
        StopLoading();
    }
}

