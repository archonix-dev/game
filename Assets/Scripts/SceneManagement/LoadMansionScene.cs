using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Простой скрипт для загрузки сцены Mansion при нажатии на кнопку
/// </summary>
public class LoadMansionScene : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Кнопка, при нажатии на которую произойдет переход на сцену Mansion")]
    public Button button;
    
    [Tooltip("Имя сцены для загрузки")]
    public string sceneName = "Mansion";
    
    void Start()
    {
        // Если кнопка не назначена, пытаемся найти её на этом же объекте
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        
        // Подписываемся на событие нажатия кнопки
        if (button != null)
        {
            button.onClick.AddListener(LoadScene);
        }
        else
        {
            Debug.LogWarning("Кнопка не найдена! Укажите кнопку в инспекторе или добавьте компонент Button на этот объект.");
        }
    }
    
    /// <summary>
    /// Загружает сцену Mansion
    /// </summary>
    private void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("Имя сцены не указано!");
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

