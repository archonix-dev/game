using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Главный скрипт для управления модами
/// </summary>
public class ModConfiguration : MonoBehaviour
{
    [Header("Настройки модов")]
    [Tooltip("Префаб неактивного мода")]
    public GameObject inactiveModPrefab;
    
    [Tooltip("Префаб активного мода")]
    public GameObject activeModPrefab;
    
    [Tooltip("Родительский Transform для неактивных модов (Vertical/Horizontal Layout Group)")]
    public Transform inactiveModsParent;
    
    [Tooltip("Родительский Transform для активных модов (Vertical/Horizontal Layout Group)")]
    public Transform activeModsParent;
    
    [Tooltip("Компонент ShowAndHideAfterDelay для перезагрузки сцены")]
    public ShowAndHideAfterDelay sceneReloadController;
    
    [Header("Кнопки управления")]
    [Tooltip("Кнопка 'Применить'")]
    public Button applyButton;
    
    [Tooltip("Кнопка 'Сбросить'")]
    public Button resetButton;
    
    [Header("Версия игры")]
    [Tooltip("Текущая версия игры (например: 0.12a)")]
    public string currentGameVersion = "0.12a";
    
    [Header("Настройки путей")]
    [Tooltip("Имя компании для пути (Archonix)")]
    public string companyName = "Archonix";
    
    [Tooltip("Имя игры для пути (LastRite 0.12a)")]
    public string gameName = "LastRite 0.12a";
    
    private List<ModData> allMods = new List<ModData>();
    private List<ModData> activeMods = new List<ModData>();
    private List<GameObject> inactiveModInstances = new List<GameObject>();
    private List<GameObject> activeModInstances = new List<GameObject>();
    
    private string modsDirectoryPath;
    private const string MODS_FOLDER = "mods";
    private const string CONFIG_CLASS_NAME = "MainModClass";
    private const string CONFIG_METHOD_NAME = "ConfigurationMethod";
    private const string LOGO_FILE = "logo_mod.png";
    
    private static ModConfiguration instance;
    public static ModConfiguration Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ModConfiguration>();
            }
            return instance;
        }
    }
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Построение пути к папке модов
        BuildModsDirectoryPath();
    }
    
    void Start()
    {
        // Сканируем и загружаем все моды
        ScanAndLoadMods();
        
        // Загружаем активные моды из PlayerPrefs (после сканирования, чтобы можно было найти моды)
        LoadActiveModsFromPlayerPrefs();
        
        // Настраиваем кнопки
        SetupButtons();
        
        // Обновляем отображение
        RefreshModDisplay();
    }
    
    /// <summary>
    /// Построение пути к папке модов
    /// </summary>
    private void BuildModsDirectoryPath()
    {
        // Строим путь: %AppData%\LocalLow\CompanyName\GameName\mods
        string localLowPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Low");
        string customPath = Path.Combine(localLowPath, companyName, gameName, MODS_FOLDER);
        
        // Нормализуем путь (преобразуем в абсолютный и убираем лишние разделители)
        modsDirectoryPath = Path.GetFullPath(customPath);
        
        // Создаем папку, если её нет
        if (!Directory.Exists(modsDirectoryPath))
        {
            try
            {
                Directory.CreateDirectory(modsDirectoryPath);
                Debug.Log($"Создана папка модов: {modsDirectoryPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Не удалось создать папку модов: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Сканирование и загрузка всех модов
    /// </summary>
    private void ScanAndLoadMods()
    {
        allMods.Clear();
        
        if (!Directory.Exists(modsDirectoryPath))
        {
            Debug.LogWarning($"Папка модов не найдена: {modsDirectoryPath}");
            return;
        }
        
        // Ищем все папки модов
        string[] modDirectories = Directory.GetDirectories(modsDirectoryPath);
        
        foreach (string modDirectory in modDirectories)
        {
            ModData modData = LoadModFromFolder(modDirectory);
            if (modData != null)
            {
                // Проверяем совместимость версии
                modData.compatibility = CheckVersionCompatibility(modData.gameVersion);
                allMods.Add(modData);
            }
        }
    }
    
    /// <summary>
    /// Загрузка данных мода из папки
    /// </summary>
    private ModData LoadModFromFolder(string modFolderPath)
    {
        try
        {
            // Ищем C# файл с классом MainModClass
            string[] csFiles = Directory.GetFiles(modFolderPath, "*.cs", SearchOption.AllDirectories);
            string configCsFile = null;
            
            foreach (string csFile in csFiles)
            {
                string content = File.ReadAllText(csFile);
                if (content.Contains(CONFIG_CLASS_NAME) && content.Contains(CONFIG_METHOD_NAME))
                {
                    configCsFile = csFile;
                    break;
                }
            }
            
            if (configCsFile == null)
            {
                Debug.LogWarning($"Не найден файл с классом {CONFIG_CLASS_NAME} в папке: {modFolderPath}");
                return null;
            }
            
            // Читаем и парсим C# файл
            string csContent = File.ReadAllText(configCsFile);
            ModConfigData config = ParseModConfigFromCSharp(csContent);
            
            if (config == null || string.IsNullOrEmpty(config.modName))
            {
                Debug.LogWarning($"Неверный формат конфигурации в файле: {configCsFile}");
                return null;
            }
            
            // Загружаем логотип
            Sprite logo = null;
            string logoPath = Path.Combine(modFolderPath, LOGO_FILE);
            if (File.Exists(logoPath))
            {
                logo = LoadSpriteFromFile(logoPath);
            }
            
            // Создаем ModData (используем путь к папке вместо zip файла)
            ModData modData = new ModData(modFolderPath, config.modName, config.modVersion, config.gameVersion, logo);
            
            return modData;
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при загрузке мода из папки {modFolderPath}: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Парсинг конфигурации из C# файла
    /// </summary>
    private ModConfigData ParseModConfigFromCSharp(string csContent)
    {
        if (string.IsNullOrEmpty(csContent))
        {
            return null;
        }
        
        ModConfigData config = new ModConfigData();
        
        try
        {
            // Ищем метод ConfigurationMethod с учетом вложенных фигурных скобок
            // Используем более сложный паттерн для поиска тела метода
            int methodStartIndex = csContent.IndexOf(CONFIG_METHOD_NAME, StringComparison.OrdinalIgnoreCase);
            if (methodStartIndex == -1)
            {
                Debug.LogWarning("Не найден метод ConfigurationMethod в C# файле");
                return null;
            }
            
            // Находим начало тела метода (открывающая фигурная скобка)
            int braceStart = csContent.IndexOf('{', methodStartIndex);
            if (braceStart == -1)
            {
                Debug.LogWarning("Не найдено начало тела метода ConfigurationMethod");
                return null;
            }
            
            // Находим соответствующую закрывающую скобку, учитывая вложенность
            int braceCount = 0;
            int braceEnd = -1;
            for (int i = braceStart; i < csContent.Length; i++)
            {
                if (csContent[i] == '{')
                {
                    braceCount++;
                }
                else if (csContent[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }
            }
            
            if (braceEnd == -1)
            {
                Debug.LogWarning("Не найдено окончание тела метода ConfigurationMethod");
                return null;
            }
            
            // Извлекаем тело метода
            string methodBody = csContent.Substring(braceStart + 1, braceEnd - braceStart - 1);
            
            // Парсим присваивания в методе
            // Ищем строки вида: modName = "Mod Test"; или modName = "Mod Test";
            string modNamePattern = @"modName\s*=\s*""([^""]+)""\s*;";
            Match modNameMatch = Regex.Match(methodBody, modNamePattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (modNameMatch.Success)
            {
                config.modName = modNameMatch.Groups[1].Value.Trim();
            }
            
            string modVersionPattern = @"modVersion\s*=\s*""([^""]+)""\s*;";
            Match modVersionMatch = Regex.Match(methodBody, modVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (modVersionMatch.Success)
            {
                config.modVersion = modVersionMatch.Groups[1].Value.Trim();
            }
            
            string gameVersionPattern = @"gameVersion\s*=\s*""([^""]+)""\s*;";
            Match gameVersionMatch = Regex.Match(methodBody, gameVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (gameVersionMatch.Success)
            {
                config.gameVersion = gameVersionMatch.Groups[1].Value.Trim();
            }
            
            // Проверяем, что все поля заполнены
            if (string.IsNullOrEmpty(config.modName))
            {
                Debug.LogWarning("Не найдено поле modName в методе ConfigurationMethod");
                return null;
            }
            
            // Если modVersion или gameVersion не найдены, устанавливаем пустые строки
            if (string.IsNullOrEmpty(config.modVersion))
            {
                config.modVersion = "";
            }
            if (string.IsNullOrEmpty(config.gameVersion))
            {
                config.gameVersion = "";
            }
            
            return config;
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка парсинга C# конфигурации: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Загрузка спрайта из файла
    /// </summary>
    private Sprite LoadSpriteFromFile(string imagePath)
    {
        try
        {
            byte[] imageData = File.ReadAllBytes(imagePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (texture.LoadImage(imageData))
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при загрузке изображения {imagePath}: {e.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Проверка совместимости версии
    /// </summary>
    private VersionCompatibility CheckVersionCompatibility(string modGameVersion)
    {
        if (string.IsNullOrEmpty(modGameVersion) || string.IsNullOrEmpty(currentGameVersion))
        {
            return VersionCompatibility.Unknown;
        }
        
        // Парсим версии (формат: "0.12a", "0.31a", "1.11a")
        float modVersion = ParseVersion(modGameVersion);
        float currentVersion = ParseVersion(currentGameVersion);
        
        if (modVersion == -1 || currentVersion == -1)
        {
            return VersionCompatibility.Unknown;
        }
        
        float difference = Mathf.Abs(currentVersion - modVersion);
        
        // Кардинальное отличие - если отличается мажорная версия (целая часть)
        int modMajor = Mathf.FloorToInt(modVersion);
        int currentMajor = Mathf.FloorToInt(currentVersion);
        
        if (modMajor != currentMajor)
        {
            return VersionCompatibility.Incompatible;
        }
        
        // Незначительное отличие - если отличается минорная версия более чем на 0.2
        if (difference > 0.2f)
        {
            return VersionCompatibility.Warning;
        }
        
        return VersionCompatibility.Compatible;
    }
    
    /// <summary>
    /// Парсинг версии из строки (например "0.12a" -> 0.12)
    /// </summary>
    private float ParseVersion(string versionString)
    {
        try
        {
            // Убираем все буквы и оставляем только числа и точку
            string cleanVersion = System.Text.RegularExpressions.Regex.Replace(versionString, @"[^0-9.]", "");
            if (float.TryParse(cleanVersion, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка парсинга версии {versionString}: {e.Message}");
        }
        
        return -1f;
    }
    
    /// <summary>
    /// Загрузка активных модов из PlayerPrefs
    /// </summary>
    private void LoadActiveModsFromPlayerPrefs()
    {
        activeMods.Clear();
        
        string activeModsJson = PlayerPrefs.GetString("ActiveMods", "");
        if (!string.IsNullOrEmpty(activeModsJson))
        {
            try
            {
                ActiveModsData data = JsonUtility.FromJson<ActiveModsData>(activeModsJson);
                if (data != null && data.modPaths != null)
                {
                    foreach (string modPath in data.modPaths)
                    {
                        // Нормализуем пути для сравнения
                        string normalizedSavedPath = NormalizePath(modPath);
                        
                        // Находим мод по пути в списке всех модов
                        ModData foundMod = allMods.Find(m => NormalizePath(m.modFilePath) == normalizedSavedPath);
                        if (foundMod != null && !activeMods.Contains(foundMod))
                        {
                            activeMods.Add(foundMod);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка загрузки активных модов из PlayerPrefs: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Сохранение активных модов в PlayerPrefs
    /// </summary>
    private void SaveActiveModsToPlayerPrefs()
    {
        ActiveModsData data = new ActiveModsData();
        data.modPaths = activeMods.Select(m => m.modFilePath).ToArray();
        
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("ActiveMods", json);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Настройка кнопок
    /// </summary>
    private void SetupButtons()
    {
        if (applyButton != null)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(OnApplyButtonClicked);
        }
        
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnResetButtonClicked);
        }
    }
    
    /// <summary>
    /// Обновление отображения модов
    /// </summary>
    private void RefreshModDisplay()
    {
        // Проверяем, что префабы и родительские объекты назначены
        if (inactiveModPrefab == null || activeModPrefab == null)
        {
            Debug.LogError("Префабы модов не назначены в ModConfiguration!");
            return;
        }
        
        if (inactiveModsParent == null || activeModsParent == null)
        {
            Debug.LogError("Родительские объекты для модов не назначены в ModConfiguration!");
            return;
        }
        
        // Очищаем существующие экземпляры
        ClearModInstances();
        
        // Создаем экземпляры неактивных модов
        foreach (ModData mod in allMods)
        {
            if (!activeMods.Contains(mod))
            {
                CreateInactiveModInstance(mod);
            }
        }
        
        // Создаем экземпляры активных модов
        foreach (ModData mod in activeMods)
        {
            CreateActiveModInstance(mod);
        }
    }
    
    /// <summary>
    /// Очистка экземпляров модов
    /// </summary>
    private void ClearModInstances()
    {
        foreach (GameObject obj in inactiveModInstances)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        inactiveModInstances.Clear();
        
        foreach (GameObject obj in activeModInstances)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        activeModInstances.Clear();
    }
    
    /// <summary>
    /// Создание экземпляра неактивного мода
    /// </summary>
    private void CreateInactiveModInstance(ModData mod)
    {
        if (inactiveModPrefab == null || inactiveModsParent == null)
        {
            return;
        }
        
        GameObject instance = Instantiate(inactiveModPrefab, inactiveModsParent);
        ModItemInactive modItem = instance.GetComponent<ModItemInactive>();
        
        if (modItem != null)
        {
            modItem.Initialize(mod, this);
        }
        
        inactiveModInstances.Add(instance);
    }
    
    /// <summary>
    /// Создание экземпляра активного мода
    /// </summary>
    private void CreateActiveModInstance(ModData mod)
    {
        if (activeModPrefab == null || activeModsParent == null)
        {
            return;
        }
        
        GameObject instance = Instantiate(activeModPrefab, activeModsParent);
        ModItemActive modItem = instance.GetComponent<ModItemActive>();
        
        if (modItem != null)
        {
            modItem.Initialize(mod, this);
        }
        
        activeModInstances.Add(instance);
    }
    
    /// <summary>
    /// Активация мода (перемещение из неактивных в активные)
    /// </summary>
    public void ActivateMod(ModData mod)
    {
        if (mod == null || activeMods.Contains(mod))
        {
            return;
        }
        
        // Проверяем совместимость
        if (mod.compatibility == VersionCompatibility.Incompatible)
        {
            Debug.LogWarning($"Мод {mod.modName} несовместим с текущей версией игры!");
            return;
        }
        
        activeMods.Add(mod);
        RefreshModDisplay();
    }
    
    /// <summary>
    /// Деактивация мода (перемещение из активных в неактивные)
    /// </summary>
    public void DeactivateMod(ModData mod)
    {
        if (mod == null || !activeMods.Contains(mod))
        {
            return;
        }
        
        activeMods.Remove(mod);
        RefreshModDisplay();
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки "Применить"
    /// </summary>
    private void OnApplyButtonClicked()
    {
        // Сохраняем активные моды
        SaveActiveModsToPlayerPrefs();
        
        // Показываем объект перезагрузки
        if (sceneReloadController != null)
        {
            // Сбрасываем состояние ShowAndHideAfterDelay для показа загрузки
            ShowAndHideAfterDelay.ResetShowState();
            
            if (sceneReloadController.targetObject != null)
            {
                sceneReloadController.targetObject.SetActive(true);
            }
        }
        
        // Перезагружаем сцену
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки "Сбросить"
    /// </summary>
    private void OnResetButtonClicked()
    {
        // Очищаем активные моды
        activeMods.Clear();
        SaveActiveModsToPlayerPrefs();
        
        // Обновляем отображение
        RefreshModDisplay();
    }
    
    /// <summary>
    /// Нормализация пути для сравнения (приведение к единому формату)
    /// </summary>
    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        
        try
        {
            // Преобразуем в абсолютный путь и нормализуем разделители
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        }
        catch
        {
            return path.ToLowerInvariant();
        }
    }
    
    /// <summary>
    /// Получить путь к папке модов (для отладки)
    /// </summary>
    public string GetModsDirectoryPath()
    {
        return modsDirectoryPath;
    }
}

/// <summary>
/// Класс для хранения данных конфигурации мода из C# файла
/// </summary>
[System.Serializable]
public class ModConfigData
{
    public string modName;
    public string modVersion;
    public string gameVersion;
}

/// <summary>
/// Класс для хранения активных модов в PlayerPrefs
/// </summary>
[System.Serializable]
public class ActiveModsData
{
    public string[] modPaths;
}

