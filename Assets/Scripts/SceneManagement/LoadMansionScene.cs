using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Скрипт для загрузки сцены Mansion при нажатии на кнопку с анимацией загрузки
/// </summary>
public class LoadMansionScene : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Кнопка, при нажатии на которую произойдет переход на сцену Mansion")]
    public Button button;
    
    [Tooltip("Имя сцены для загрузки")]
    public string sceneName = "Mansion";
    
    [Header("Загрузчик сцены")]
    [Tooltip("Компонент AsyncSceneLoaderWithAnimation для асинхронной загрузки с анимацией")]
    public AsyncSceneLoaderWithAnimation sceneLoader;
    
    void Start()
    {
        // Если кнопка не назначена, пытаемся найти её на этом же объекте
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        
        // Если загрузчик не назначен, пытаемся найти его на этом же объекте
        if (sceneLoader == null)
        {
            sceneLoader = GetComponent<AsyncSceneLoaderWithAnimation>();
        }
        
        // Если загрузчик все еще не найден, пытаемся найти его в сцене
        if (sceneLoader == null)
        {
            sceneLoader = FindObjectOfType<AsyncSceneLoaderWithAnimation>();
        }
        
        // Подписываемся на событие нажатия кнопки
        if (button != null)
        {
            button.onClick.AddListener(LoadScene);
        }
        else
        {
            Debug.LogWarning("[LoadMansionScene] Кнопка не найдена! Укажите кнопку в инспекторе или добавьте компонент Button на этот объект.");
        }
        
        // Проверяем наличие загрузчика
        if (sceneLoader == null)
        {
            Debug.LogWarning("[LoadMansionScene] AsyncSceneLoaderWithAnimation не найден! Будет использована синхронная загрузка.");
        }
    }
    
    /// <summary>
    /// Загружает сцену Mansion (асинхронно с анимацией или синхронно)
    /// </summary>
    private void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LoadMansionScene] Имя сцены не указано!");
            return;
        }
        
        // Проверяем, есть ли активное сетевое подключение
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && (networkManager.IsHost || networkManager.IsServer))
        {
            // Если мы хост/сервер, загружаем сцену для всех через NetworkManager
            if (networkManager.SceneManager != null)
            {
                Debug.Log($"[LoadMansionScene] Загрузка сцены {sceneName} через NetworkManager (для всех клиентов)");
                networkManager.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
                return;
            }
        }
        
        // Если нет сетевого подключения или мы клиент, загружаем локально
        // Если есть загрузчик с анимацией, используем его
        if (sceneLoader != null)
        {
            // Устанавливаем имя сцены в загрузчик
            sceneLoader.sceneName = sceneName;
            sceneLoader.StartLoading();
        }
        else
        {
            // Иначе используем синхронную загрузку (старый способ)
            Debug.LogWarning("[LoadMansionScene] Используется синхронная загрузка, так как AsyncSceneLoaderWithAnimation не найден.");
            SceneManager.LoadScene(sceneName);
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта
        if (button != null)
        {
            button.onClick.RemoveListener(LoadScene);
        }
    }
}

