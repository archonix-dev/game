using System;
using System.Collections;
using System.Collections.Generic;
using Diagnostics = System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    
    [Tooltip("Кнопка 'Вверх' для изменения приоритета мода")]
    public Button moveUpButton;
    
    [Tooltip("Кнопка 'Вниз' для изменения приоритета мода")]
    public Button moveDownButton;
    
    [Header("Настройки музыки и звуков")]
    [Tooltip("Оригинальный AudioClip для музыки меню (используется для получения пути к файлу в проекте)")]
    public AudioClip originalMenuMusicClip;
    
    [Tooltip("Оригинальный AudioClip для звука кнопки 'Применить' (используется для получения пути к файлу в проекте)")]
    public AudioClip originalApplyButtonClip;
    
    [Tooltip("Оригинальный AudioClip для звука кнопки 'Назад' (используется для получения пути к файлу в проекте)")]
    public AudioClip originalBackButtonClip;
    
    [Tooltip("Путь к папке со звуками в проекте относительно Assets (например: UI/sounds)")]
    public string projectSoundsFolderPath = "UI/sounds";
    
    [Header("Версия игры")]
    [Tooltip("Текущая версия игры (например: 0.12a)")]
    public string currentGameVersion = "0.12a";
    
    [Header("Настройки путей")]
    [Tooltip("Имя компании для пути (Archonix)")]
    public string companyName = "Archonix";
    
    [Tooltip("Имя игры для пути (localhost 0.12a)")]
    public string gameName = "localhost 0.12a";
    
    private List<ModData> allMods = new List<ModData>();
    private List<ModData> activeMods = new List<ModData>();
    private List<GameObject> inactiveModInstances = new List<GameObject>();
    private List<GameObject> activeModInstances = new List<GameObject>();
    
    private ModData selectedMod = null; // Выбранный мод для изменения приоритета
    
    // Хранилище для временной замены AudioClip
    private Dictionary<AudioSource, AudioClip> originalClipsBackup = new Dictionary<AudioSource, AudioClip>();
    private AudioClip currentMenuMusicClip = null;
    private AudioClip currentApplyButtonClip = null;
    private AudioClip currentBackButtonClip = null;
    
    // Загруженные AudioClip из модов (для билда)
    private AudioClip loadedMenuMusicClip = null;
    private AudioClip loadedApplyButtonClip = null;
    private AudioClip loadedBackButtonClip = null;
    
    // Пути к файлам в проекте
    private string projectMenuMusicPath;
    private string projectApplyButtonPath;
    private string projectBackButtonPath;
    
    // Пути к backup оригинальных файлов
    private string backupMenuMusicPath;
    private string backupApplyButtonPath;
    private string backupBackButtonPath;
    
    private string modsDirectoryPath;
    private string projectAssetsPath;
    private const string MODS_FOLDER = "mods";
    private const string CONFIG_CLASS_NAME = "MainModClass";
    private const string CONFIG_METHOD_NAME = "ConfigurationMethod";
    private const string LOGO_FILE = "logo_mod.png";
    private const string SOUNDS_FOLDER = "sounds";
    private const string MENU_FOLDER = "menu";
    private const string MENU_MUSIC_FILE = "menu.mp3";
    private const string MISC_FOLDER = "misc";
    private const string APPLY_BUTTON_FILE = "applybutton.mp3";
    private const string BACK_BUTTON_FILE = "backbutton.mp3";
    private const string REQUIRED_MOD_NAME = "localhost"; // Обязательный мод, который всегда должен быть активен
    
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
        // Устанавливаем instance (без DontDestroyOnLoad, объект будет уничтожаться при перезагрузке сцены)
        instance = this;
        
        // Построение пути к папке модов
        BuildModsDirectoryPath();
        
        // Построение путей к файлам в проекте
        BuildProjectSoundPaths();
        
        // Сканируем и загружаем все моды
        ScanAndLoadMods();
        
        // Инициализируем активные моды (без сохранения)
        InitializeActiveMods();
    }
    
    void Start()
    {
        // Настраиваем кнопки
        SetupButtons();
        
        // Обновляем отображение
        RefreshModDisplay();
        
        // НЕ применяем музыку сразу в Start()
        // Музыка будет загружена и применена в ShowAndHideAfterDelay при загрузке активных модов
        
        // Подписываемся на событие загрузки сцены для применения звуков при переходах между сценами
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDestroy()
    {
        // Отписываемся от события
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Восстанавливаем оригинальные звуки при уничтожении объекта
        RestoreAllOriginalAudioClips();
        
        // Очищаем загруженные AudioClip (для билда)
        #if !UNITY_EDITOR
        if (loadedMenuMusicClip != null)
        {
            Destroy(loadedMenuMusicClip);
            loadedMenuMusicClip = null;
        }
        if (loadedApplyButtonClip != null)
        {
            Destroy(loadedApplyButtonClip);
            loadedApplyButtonClip = null;
        }
        if (loadedBackButtonClip != null)
        {
            Destroy(loadedBackButtonClip);
            loadedBackButtonClip = null;
        }
        #endif
    }
    
    /// <summary>
    /// Обработчик загрузки сцены - применяет звуки из модов в новой сцене
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Применяем все загруженные звуки из модов при загрузке каждой новой сцены
        ApplyAllModSoundsToScene();
    }
    
    /// <summary>
    /// Применение всех звуков из модов к текущей сцене (для билда)
    /// </summary>
    private void ApplyAllModSoundsToScene()
    {
        #if !UNITY_EDITOR
        // В билде применяем все загруженные звуки из модов
        // Используем небольшую задержку, чтобы все AudioSource успели инициализироваться
        StartCoroutine(ApplyAllModSoundsCoroutine());
        #else
        // В редакторе просто запускаем проигрывание музыки
        StartMenuMusicPlayback();
        #endif
    }
    
    /// <summary>
    /// Корутина для применения всех звуков из модов с небольшой задержкой
    /// </summary>
    private IEnumerator ApplyAllModSoundsCoroutine()
    {
        // Небольшая задержка, чтобы все AudioSource успели инициализироваться в новой сцене
        yield return new WaitForSeconds(0.1f);
        
        // Проверяем, что загруженные AudioClip существуют (моды уже загружены)
        // Если они null, значит моды еще не загружены или не активны
        bool hasLoadedClips = (loadedMenuMusicClip != null) || (loadedApplyButtonClip != null) || (loadedBackButtonClip != null);
        
        if (hasLoadedClips)
        {
            // Применяем музыку меню
            StartMenuMusicPlayback();
            
            // Применяем звуки кнопок
            ApplyButtonSoundsInBuild();
            
            Debug.Log("[ModConfiguration] Применены все звуки из модов к новой сцене");
        }
        else
        {
            // Если моды еще не загружены, просто запускаем оригинальную музыку
            StartMenuMusicPlayback();
        }
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
            }
            catch (Exception e)
            {
                Debug.LogError($"Не удалось создать папку модов: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Построение путей к файлам звуков в проекте
    /// </summary>
    private void BuildProjectSoundPaths()
    {
        // Получаем путь к папке Assets проекта
        projectAssetsPath = Application.dataPath;
        
        // Получаем пути к файлам на основе AudioClip
        projectMenuMusicPath = GetAudioClipFilePath(originalMenuMusicClip, MENU_MUSIC_FILE);
        projectApplyButtonPath = GetAudioClipFilePath(originalApplyButtonClip, APPLY_BUTTON_FILE);
        projectBackButtonPath = GetAudioClipFilePath(originalBackButtonClip, BACK_BUTTON_FILE);
        
        // Создаем папку для backup, если её нет
        string backupFolder = Path.Combine(projectAssetsPath, projectSoundsFolderPath, "backup");
        if (!Directory.Exists(backupFolder))
        {
            try
            {
                Directory.CreateDirectory(backupFolder);
            }
            catch (Exception e)
            {
                Debug.LogError($"Не удалось создать папку для backup: {e.Message}");
            }
        }
        
        // Пути к backup файлам
        backupMenuMusicPath = Path.Combine(backupFolder, MENU_MUSIC_FILE);
        backupApplyButtonPath = Path.Combine(backupFolder, APPLY_BUTTON_FILE);
        backupBackButtonPath = Path.Combine(backupFolder, BACK_BUTTON_FILE);
    }
    
    /// <summary>
    /// Получение пути к файлу AudioClip в проекте
    /// </summary>
    private string GetAudioClipFilePath(AudioClip clip, string defaultFileName)
    {
        if (clip == null)
        {
            // Если AudioClip не назначен, используем путь по умолчанию
            return Path.Combine(Application.dataPath, projectSoundsFolderPath, defaultFileName);
        }
        
        #if UNITY_EDITOR
        // В редакторе используем AssetDatabase для получения пути
        string assetPath = AssetDatabase.GetAssetPath(clip);
        if (!string.IsNullOrEmpty(assetPath))
        {
            // Преобразуем путь относительно Assets в абсолютный путь
            string relativePath = assetPath.Replace("Assets/", "").Replace("Assets\\", "");
            return Path.Combine(Application.dataPath, relativePath);
        }
        #endif
        
        // Если не удалось получить путь через AssetDatabase, используем путь по умолчанию
        return Path.Combine(Application.dataPath, projectSoundsFolderPath, defaultFileName);
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
                
                // Обязательный мод "localhost" всегда совместим со всеми версиями
                if (modData.modName == REQUIRED_MOD_NAME)
                {
                    modData.compatibility = VersionCompatibility.Compatible;
                }
                
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
            
            // НЕ загружаем музыку здесь - она будет загружена в ShowAndHideAfterDelay
            // при загрузке активных модов после нажатия кнопки "Применить"
            // Проверяем только наличие файла для информации (опционально)
            // string musicPath = Path.Combine(modFolderPath, SOUNDS_FOLDER, MENU_FOLDER, MENU_MUSIC_FILE);
            // bool hasMusic = File.Exists(musicPath);
            
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
    /// Корутина для загрузки музыки меню из мода
    /// </summary>
    private IEnumerator LoadMenuMusicCoroutine(ModData modData, string musicPath)
    {
        string fileUrl = "file://" + musicPath;
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                modData.menuMusicClip = DownloadHandlerAudioClip.GetContent(www);
                
                // Если мод активен, применяем музыку сразу после загрузки
                // Это происходит после перезагрузки сцены
                if (activeMods.Contains(modData))
                {
                    ApplyMenuMusicFromMods();
                }
            }
            else
            {
                Debug.LogWarning($"Не удалось загрузить музыку из {musicPath}: {www.error}");
            }
        }
    }
    
    /// <summary>
    /// Загрузка всех ресурсов из активных модов с отслеживанием прогресса
    /// Копирует файлы из модов в папку проекта, заменяя оригинальные файлы
    /// </summary>
    public IEnumerator LoadModResources(System.Action<float> progressCallback = null)
    {
        if (activeMods == null || activeMods.Count == 0)
        {
            // Если нет активных модов, восстанавливаем оригинальные файлы из backup
            // Восстанавливаем файлы из backup
            RestoreOriginalSoundFiles();
            
            #if !UNITY_EDITOR
            // В билде загружаем оригинальные AudioClip из восстановленных файлов
            if (!string.IsNullOrEmpty(projectMenuMusicPath) && File.Exists(projectMenuMusicPath))
            {
                yield return StartCoroutine(LoadAudioClipFromFile(projectMenuMusicPath, (clip) => {
                    loadedMenuMusicClip = clip;
                }));
            }
            if (!string.IsNullOrEmpty(projectApplyButtonPath) && File.Exists(projectApplyButtonPath))
            {
                yield return StartCoroutine(LoadAudioClipFromFile(projectApplyButtonPath, (clip) => {
                    loadedApplyButtonClip = clip;
                }));
            }
            if (!string.IsNullOrEmpty(projectBackButtonPath) && File.Exists(projectBackButtonPath))
            {
                yield return StartCoroutine(LoadAudioClipFromFile(projectBackButtonPath, (clip) => {
                    loadedBackButtonClip = clip;
                }));
            }
            // Применяем восстановленные звуки кнопок в билде
            ApplyButtonSoundsInBuild();
            #else
            // В редакторе обновляем AssetDatabase
            RefreshAssetDatabase();
            ReloadAudioClipsAfterRestore();
            yield return new WaitForSeconds(0.3f);
            #endif
            
            // Запускаем проигрывание музыки меню с оригинальными файлами
            StartMenuMusicPlayback();
            
            progressCallback?.Invoke(1f);
            yield break;
        }
        
        // Сначала сохраняем оригинальные файлы в backup (если еще не сохранены)
        SaveOriginalSoundFilesToBackup();
        
        // Ищем первый мод с ресурсами (первый в списке = самый высокий приоритет)
        ModData modWithResources = null;
        string musicSourcePath = null;
        string applyButtonSourcePath = null;
        string backButtonSourcePath = null;
        
        foreach (ModData mod in activeMods)
        {
            string musicPath = Path.Combine(mod.modFilePath, SOUNDS_FOLDER, MENU_FOLDER, MENU_MUSIC_FILE);
            string applyPath = Path.Combine(mod.modFilePath, SOUNDS_FOLDER, MISC_FOLDER, APPLY_BUTTON_FILE);
            string backPath = Path.Combine(mod.modFilePath, SOUNDS_FOLDER, MISC_FOLDER, BACK_BUTTON_FILE);
            
            if (File.Exists(musicPath) || File.Exists(applyPath) || File.Exists(backPath))
            {
                modWithResources = mod;
                if (File.Exists(musicPath)) musicSourcePath = musicPath;
                if (File.Exists(applyPath)) applyButtonSourcePath = applyPath;
                if (File.Exists(backPath)) backButtonSourcePath = backPath;
                break;
            }
        }
        
        int totalFiles = 0;
        int copiedFiles = 0;
        
        if (musicSourcePath != null) totalFiles++;
        if (applyButtonSourcePath != null) totalFiles++;
        if (backButtonSourcePath != null) totalFiles++;
        
        if (totalFiles == 0)
        {
            // Если нет файлов для копирования из модов, восстанавливаем оригинальные файлы из backup
            RestoreOriginalSoundFiles();
            
            #if !UNITY_EDITOR
            // В билде загружаем оригинальные AudioClip из восстановленных файлов
            if (!string.IsNullOrEmpty(projectMenuMusicPath) && File.Exists(projectMenuMusicPath))
            {
                yield return StartCoroutine(LoadAudioClipFromFile(projectMenuMusicPath, (clip) => {
                    loadedMenuMusicClip = clip;
                }));
            }
            if (!string.IsNullOrEmpty(projectApplyButtonPath) && File.Exists(projectApplyButtonPath))
            {
                yield return StartCoroutine(LoadAudioClipFromFile(projectApplyButtonPath, (clip) => {
                    loadedApplyButtonClip = clip;
                }));
            }
            if (!string.IsNullOrEmpty(projectBackButtonPath) && File.Exists(projectBackButtonPath))
            {
                yield return StartCoroutine(LoadAudioClipFromFile(projectBackButtonPath, (clip) => {
                    loadedBackButtonClip = clip;
                }));
            }
            // Применяем восстановленные звуки кнопок в билде
            ApplyButtonSoundsInBuild();
            #else
            // В редакторе обновляем AssetDatabase
            RefreshAssetDatabase();
            ReloadAudioClipsAfterRestore();
            yield return new WaitForSeconds(0.3f);
            #endif
            
            // Запускаем проигрывание музыки меню с оригинальными файлами
            StartMenuMusicPlayback();
            
            progressCallback?.Invoke(1f);
            yield break;
        }
        
        // Копируем файлы из мода в папку проекта
        yield return null; // Даем кадр для обновления UI
        
        // Копируем музыку меню
        if (musicSourcePath != null && !string.IsNullOrEmpty(projectMenuMusicPath))
        {
            if (CopyFileFromModToProject(musicSourcePath, projectMenuMusicPath))
            {
                copiedFiles++;
                progressCallback?.Invoke((float)copiedFiles / totalFiles);
            }
            yield return null;
        }
        
        // Копируем звук кнопки "Применить"
        if (applyButtonSourcePath != null && !string.IsNullOrEmpty(projectApplyButtonPath))
        {
            if (CopyFileFromModToProject(applyButtonSourcePath, projectApplyButtonPath))
            {
                copiedFiles++;
                progressCallback?.Invoke((float)copiedFiles / totalFiles);
            }
            yield return null;
        }
        
        // Копируем звук кнопки "Назад"
        if (backButtonSourcePath != null && !string.IsNullOrEmpty(projectBackButtonPath))
        {
            if (CopyFileFromModToProject(backButtonSourcePath, projectBackButtonPath))
            {
                copiedFiles++;
                progressCallback?.Invoke((float)copiedFiles / totalFiles);
            }
            yield return null;
        }
        
        // Обновляем AssetDatabase, чтобы Unity подхватил изменения (только в редакторе)
        RefreshAssetDatabase();
        
        // В билде загружаем AudioClip напрямую из файлов
        #if !UNITY_EDITOR
        // Загружаем AudioClip из скопированных файлов
        if (musicSourcePath != null && !string.IsNullOrEmpty(projectMenuMusicPath))
        {
            yield return StartCoroutine(LoadAudioClipFromFile(projectMenuMusicPath, (clip) => {
                loadedMenuMusicClip = clip;
            }));
        }
        
        if (applyButtonSourcePath != null && !string.IsNullOrEmpty(projectApplyButtonPath))
        {
            yield return StartCoroutine(LoadAudioClipFromFile(projectApplyButtonPath, (clip) => {
                loadedApplyButtonClip = clip;
            }));
        }
        
        if (backButtonSourcePath != null && !string.IsNullOrEmpty(projectBackButtonPath))
        {
            yield return StartCoroutine(LoadAudioClipFromFile(projectBackButtonPath, (clip) => {
                loadedBackButtonClip = clip;
            }));
        }
        
        // Применяем загруженные звуки кнопок в билде
        ApplyButtonSoundsInBuild();
        #else
        // В редакторе перезагружаем AudioClip через AssetDatabase
        ReloadAudioClipsAfterCopy();
        yield return new WaitForSeconds(0.1f);
        #endif
        
        // Запускаем проигрывание музыки меню
        StartMenuMusicPlayback();
        
        // Убеждаемся, что прогресс = 100%
        progressCallback?.Invoke(1f);
    }
    
    /// <summary>
    /// Применение звуков кнопок в билде (замена AudioClip в AudioSource)
    /// Применяется ко всем сценам
    /// </summary>
    private void ApplyButtonSoundsInBuild()
    {
        #if !UNITY_EDITOR
        if (originalApplyButtonClip == null && originalBackButtonClip == null)
        {
            return;
        }
        
        // Находим все AudioSource в текущей сцене
        AudioSource[] allAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();
        
        int replacedCount = 0;
        
        foreach (AudioSource audioSource in allAudioSources)
        {
            if (audioSource == null || audioSource.gameObject == null)
            {
                continue;
            }
            
            // Заменяем звук кнопки "Применить"
            if (originalApplyButtonClip != null && audioSource.clip == originalApplyButtonClip)
            {
                if (loadedApplyButtonClip != null)
                {
                    audioSource.clip = loadedApplyButtonClip;
                    replacedCount++;
                }
            }
            
            // Заменяем звук кнопки "Назад"
            if (originalBackButtonClip != null && audioSource.clip == originalBackButtonClip)
            {
                if (loadedBackButtonClip != null)
                {
                    audioSource.clip = loadedBackButtonClip;
                    replacedCount++;
                }
            }
        }
        
        #endif
    }
    
    /// <summary>
    /// Загрузка AudioClip из файла (для билда)
    /// </summary>
    private IEnumerator LoadAudioClipFromFile(string filePath, System.Action<AudioClip> onComplete)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogWarning($"[ModConfiguration] Файл не найден для загрузки AudioClip: {filePath}");
            onComplete?.Invoke(null);
            yield break;
        }
        
        string fileUrl = "file://" + filePath;
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    // Защищаем AudioClip от уничтожения при очистке памяти
                    clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    onComplete?.Invoke(clip);
                }
                else
                {
                    Debug.LogWarning($"[ModConfiguration] Не удалось создать AudioClip из файла: {filePath}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[ModConfiguration] Ошибка загрузки AudioClip из {filePath}: {www.error}");
                onComplete?.Invoke(null);
            }
        }
    }
    
    /// <summary>
    /// Перезагрузка AudioClip после копирования файлов из мода
    /// </summary>
    private void ReloadAudioClipsAfterCopy()
    {
        #if UNITY_EDITOR
        try
        {
            // Перезагружаем AudioClip для музыки меню
            if (originalMenuMusicClip != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalMenuMusicClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // Импортируем заново, чтобы Unity подхватил изменения
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            
            // Перезагружаем AudioClip для звука кнопки "Применить"
            if (originalApplyButtonClip != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalApplyButtonClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            
            // Перезагружаем AudioClip для звука кнопки "Назад"
            if (originalBackButtonClip != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalBackButtonClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при перезагрузке AudioClip после копирования: {e.Message}");
        }
        #endif
    }
    
    /// <summary>
    /// Копирование файла из мода в папку проекта
    /// </summary>
    private bool CopyFileFromModToProject(string sourcePath, string destinationPath)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"Исходный файл не найден: {sourcePath}");
                return false;
            }
            
            // Создаем директорию назначения, если её нет
            string destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            
            // Копируем файл, перезаписывая существующий
            File.Copy(sourcePath, destinationPath, true);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при копировании файла из {sourcePath} в {destinationPath}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Сохранение оригинальных файлов в backup
    /// </summary>
    private void SaveOriginalSoundFilesToBackup()
    {
        try
        {
            // Создаем папку backup, если её нет
            string backupFolder = Path.GetDirectoryName(backupMenuMusicPath);
            if (!string.IsNullOrEmpty(backupFolder) && !Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
            }
            
            // Сохраняем оригинальную музыку меню в backup (если еще не сохранена)
            if (File.Exists(projectMenuMusicPath))
            {
                if (!File.Exists(backupMenuMusicPath))
                {
                    File.Copy(projectMenuMusicPath, backupMenuMusicPath, true);
                }
            }
            else
            {
                Debug.LogWarning($"[ModConfiguration] Файл проекта музыки меню не найден: {projectMenuMusicPath}");
            }
            
            // Сохраняем оригинальный звук кнопки "Применить" в backup (если еще не сохранен)
            if (File.Exists(projectApplyButtonPath))
            {
                if (!File.Exists(backupApplyButtonPath))
                {
                    File.Copy(projectApplyButtonPath, backupApplyButtonPath, true);
                }
            }
            else
            {
                Debug.LogWarning($"[ModConfiguration] Файл проекта звука 'Применить' не найден: {projectApplyButtonPath}");
            }
            
            // Сохраняем оригинальный звук кнопки "Назад" в backup (если еще не сохранен)
            if (File.Exists(projectBackButtonPath))
            {
                if (!File.Exists(backupBackButtonPath))
                {
                    File.Copy(projectBackButtonPath, backupBackButtonPath, true);
                }
            }
            else
            {
                Debug.LogWarning($"[ModConfiguration] Файл проекта звука 'Назад' не найден: {projectBackButtonPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при сохранении оригинальных файлов в backup: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// Восстановление оригинальных файлов из backup
    /// </summary>
    private void RestoreOriginalSoundFiles()
    {
        try
        {
            bool restored = false;
            
            // Восстанавливаем оригинальную музыку меню из backup
            if (File.Exists(backupMenuMusicPath))
            {
                // Создаем директорию назначения, если её нет
                string destDir = Path.GetDirectoryName(projectMenuMusicPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                
                File.Copy(backupMenuMusicPath, projectMenuMusicPath, true);
                restored = true;
            }
            else
            {
                Debug.LogWarning($"[ModConfiguration] Backup файл музыки меню не найден: {backupMenuMusicPath}");
            }
            
            // Восстанавливаем оригинальный звук кнопки "Применить" из backup
            if (File.Exists(backupApplyButtonPath))
            {
                // Создаем директорию назначения, если её нет
                string destDir = Path.GetDirectoryName(projectApplyButtonPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                
                File.Copy(backupApplyButtonPath, projectApplyButtonPath, true);
                restored = true;
            }
            else
            {
                Debug.LogWarning($"[ModConfiguration] Backup файл звука 'Применить' не найден: {backupApplyButtonPath}");
            }
            
            // Восстанавливаем оригинальный звук кнопки "Назад" из backup
            if (File.Exists(backupBackButtonPath))
            {
                // Создаем директорию назначения, если её нет
                string destDir = Path.GetDirectoryName(projectBackButtonPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                
                File.Copy(backupBackButtonPath, projectBackButtonPath, true);
                restored = true;
            }
            else
            {
                Debug.LogWarning($"[ModConfiguration] Backup файл звука 'Назад' не найден: {backupBackButtonPath}");
            }
            
            if (restored)
            {
                // Обновляем AssetDatabase, чтобы Unity подхватил изменения
                RefreshAssetDatabase();
                
                // Перезагружаем AudioClip после восстановления файлов
                ReloadAudioClipsAfterRestore();
            }
            else
            {
                Debug.LogWarning("[ModConfiguration] Не удалось восстановить ни один файл из backup. Возможно, backup файлы не были созданы.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при восстановлении оригинальных файлов из backup: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// Перезагрузка AudioClip после восстановления файлов
    /// </summary>
    private void ReloadAudioClipsAfterRestore()
    {
        #if UNITY_EDITOR
        try
        {
            // Получаем пути к файлам через абсолютные пути
            string menuMusicAssetPath = GetAssetPathFromAbsolutePath(projectMenuMusicPath);
            string applyButtonAssetPath = GetAssetPathFromAbsolutePath(projectApplyButtonPath);
            string backButtonAssetPath = GetAssetPathFromAbsolutePath(projectBackButtonPath);
            
            // Перезагружаем AudioClip для музыки меню
            if (!string.IsNullOrEmpty(menuMusicAssetPath) && File.Exists(projectMenuMusicPath))
            {
                AssetDatabase.ImportAsset(menuMusicAssetPath, ImportAssetOptions.ForceUpdate);
            }
            else if (originalMenuMusicClip != null)
            {
                // Если не удалось получить путь из абсолютного, используем путь из AudioClip
                string assetPath = AssetDatabase.GetAssetPath(originalMenuMusicClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            
            // Перезагружаем AudioClip для звука кнопки "Применить"
            if (!string.IsNullOrEmpty(applyButtonAssetPath) && File.Exists(projectApplyButtonPath))
            {
                AssetDatabase.ImportAsset(applyButtonAssetPath, ImportAssetOptions.ForceUpdate);
            }
            else if (originalApplyButtonClip != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalApplyButtonClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            
            // Перезагружаем AudioClip для звука кнопки "Назад"
            if (!string.IsNullOrEmpty(backButtonAssetPath) && File.Exists(projectBackButtonPath))
            {
                AssetDatabase.ImportAsset(backButtonAssetPath, ImportAssetOptions.ForceUpdate);
            }
            else if (originalBackButtonClip != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalBackButtonClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при перезагрузке AudioClip: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        #endif
    }
    
    /// <summary>
    /// Преобразование абсолютного пути в путь относительно Assets
    /// </summary>
    private string GetAssetPathFromAbsolutePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
        {
            return null;
        }
        
        string dataPath = Application.dataPath;
        if (absolutePath.StartsWith(dataPath))
        {
            // Убираем путь к Assets и добавляем "Assets/"
            string relativePath = absolutePath.Substring(dataPath.Length);
            relativePath = relativePath.Replace('\\', '/');
            if (relativePath.StartsWith("/"))
            {
                relativePath = relativePath.Substring(1);
            }
            return "Assets/" + relativePath;
        }
        
        return null;
    }
    
    /// <summary>
    /// Обновление AssetDatabase для применения изменений файлов
    /// </summary>
    private void RefreshAssetDatabase()
    {
        #if UNITY_EDITOR
        // Обновляем AssetDatabase, чтобы Unity подхватил изменения файлов
        AssetDatabase.Refresh();
        #endif
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
    /// Инициализация активных модов (без сохранения)
    /// </summary>
    private void InitializeActiveMods()
    {
        activeMods.Clear();
        
        // Автоматически добавляем обязательный мод "localhost", если он доступен
        EnsureRequiredModIsActive();
    }
    
    /// <summary>
    /// Убеждается, что обязательный мод "localhost" всегда активен (если доступен)
    /// </summary>
    private void EnsureRequiredModIsActive()
    {
        // Ищем мод "localhost" в списке всех модов
        ModData localhostMod = allMods.Find(m => m.modName == REQUIRED_MOD_NAME);
        
        if (localhostMod != null)
        {
            // Устанавливаем совместимость как Compatible для обязательного мода "localhost"
            // Он всегда поддерживается на всех версиях
            localhostMod.compatibility = VersionCompatibility.Compatible;
            
            // Если мод найден и он еще не активен, добавляем его
            if (!activeMods.Contains(localhostMod))
            {
                activeMods.Add(localhostMod);
            }
            // Если мод активен, но не в начале списка - перемещаем его в начало (самый высокий приоритет)
            else if (activeMods.Count > 0 && activeMods[0] != localhostMod)
            {
                // Не перемещаем автоматически - пользователь может изменить приоритет через кнопки
                // Но гарантируем, что он всегда активен
            }
        }
        else
        {
            Debug.LogWarning($"[ModConfiguration] Обязательный мод '{REQUIRED_MOD_NAME}' не найден в списке модов");
        }
    }
    
    /// <summary>
    /// Сохранение активных модов (отключено - моды не сохраняются между сессиями)
    /// </summary>
    private void SaveActiveMods()
    {
        // Моды не сохраняются между сессиями
        EnsureRequiredModIsActive();
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
        
        if (moveUpButton != null)
        {
            moveUpButton.onClick.RemoveAllListeners();
            moveUpButton.onClick.AddListener(OnMoveUpButtonClicked);
            moveUpButton.interactable = false; // По умолчанию неактивна
        }
        
        if (moveDownButton != null)
        {
            moveDownButton.onClick.RemoveAllListeners();
            moveDownButton.onClick.AddListener(OnMoveDownButtonClicked);
            moveDownButton.interactable = false; // По умолчанию неактивна
        }
        
        UpdatePriorityButtonsState();
    }
    
    /// <summary>
    /// Обновление состояния кнопок приоритета
    /// </summary>
    private void UpdatePriorityButtonsState()
    {
        if (moveUpButton != null)
        {
            moveUpButton.interactable = (selectedMod != null && activeMods.IndexOf(selectedMod) > 0);
        }
        
        if (moveDownButton != null)
        {
            moveDownButton.interactable = (selectedMod != null && activeMods.IndexOf(selectedMod) < activeMods.Count - 1);
        }
    }
    
    /// <summary>
    /// Обновление отображения модов
    /// </summary>
    private void RefreshModDisplay()
    {
        // Убеждаемся, что обязательный мод "localhost" всегда активен перед отображением
        EnsureRequiredModIsActive();
        
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
            // Устанавливаем состояние выбора
            modItem.SetSelected(selectedMod == mod);
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
        
        // Только обновляем UI, но НЕ применяем изменения
        // Изменения будут применены только после нажатия кнопки "Применить"
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
        
        // Запрещаем деактивацию обязательного мода "localhost"
        if (mod.modName == REQUIRED_MOD_NAME)
        {
            Debug.LogWarning($"[ModConfiguration] Нельзя деактивировать обязательный мод '{REQUIRED_MOD_NAME}'");
            return;
        }
        
        activeMods.Remove(mod);
        
        // Только обновляем UI, но НЕ применяем изменения
        // Изменения будут применены только после нажатия кнопки "Применить"
        RefreshModDisplay();
        
        // Если удаленный мод был выбран, снимаем выбор
        if (selectedMod == mod)
        {
            selectedMod = null;
            UpdatePriorityButtonsState();
        }
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки "Применить"
    /// </summary>
    private void OnApplyButtonClicked()
    {
        // Сохраняем активные моды (отключено - моды не сохраняются)
        SaveActiveMods();
        
        // Показываем объект перезагрузки
        if (sceneReloadController != null)
        {
            // Сбрасываем состояние ShowAndHideAfterDelay для показа загрузки
            ShowAndHideAfterDelay.ResetShowState();
            
            if (sceneReloadController.objectToHide != null)
            {
                sceneReloadController.objectToHide.SetActive(true);
            }
        }
        
        // Перезагружаем сцену для применения всех изменений модов
        // После перезагрузки сцены моды будут применены
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки "Сбросить" - открывает проводник в папке модов
    /// </summary>
    private void OnResetButtonClicked()
    {
        // Открываем проводник в папке модов
        if (!string.IsNullOrEmpty(modsDirectoryPath))
        {
            try
            {
                // Создаем папку, если её нет
                if (!Directory.Exists(modsDirectoryPath))
                {
                    Directory.CreateDirectory(modsDirectoryPath);
                }
                
                // Открываем проводник Windows в указанной папке
                Diagnostics.Process.Start("explorer.exe", modsDirectoryPath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Не удалось открыть папку модов: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Путь к папке модов не установлен");
        }
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
    
    /// <summary>
    /// Проверка наличия активных модов
    /// </summary>
    public bool HasActiveMods()
    {
        return activeMods != null && activeMods.Count > 0;
    }
    
    /// <summary>
    /// Проверка, является ли мод обязательным (не может быть деактивирован)
    /// </summary>
    public bool IsRequiredMod(ModData mod)
    {
        if (mod == null)
        {
            return false;
        }
        
        return mod.modName == REQUIRED_MOD_NAME;
    }
    
    /// <summary>
    /// Проверка, является ли мод обязательным по имени
    /// </summary>
    public bool IsRequiredModName(string modName)
    {
        return modName == REQUIRED_MOD_NAME;
    }
    
    /// <summary>
    /// Применение музыки меню из активных модов с учетом приоритета
    /// Файлы уже скопированы в проект, Unity автоматически обновит AudioClip
    /// </summary>
    private void ApplyMenuMusicFromMods()
    {
        // Файлы уже скопированы в проект в LoadModResources()
        // Unity автоматически обновит AudioClip после RefreshAssetDatabase()
    }
    
    /// <summary>
    /// Восстановление оригинальной музыки меню
    /// </summary>
    private void RestoreOriginalMenuMusic()
    {
        if (currentMenuMusicClip == null || originalMenuMusicClip == null)
        {
            return;
        }
        
        // Восстанавливаем оригинальный AudioClip во всех AudioSource
        ReplaceAudioClipInAllAudioSources(currentMenuMusicClip, originalMenuMusicClip);
        currentMenuMusicClip = null;
    }
    
    /// <summary>
    /// Применение звуков кнопок из активных модов с учетом приоритета
    /// Файлы уже скопированы в проект, Unity автоматически обновит AudioClip
    /// </summary>
    private void ApplyButtonSoundsFromMods()
    {
        // Файлы уже скопированы в проект в LoadModResources()
        // Unity автоматически обновит AudioClip после RefreshAssetDatabase()
    }
    
    /// <summary>
    /// Восстановление оригинального звука кнопки "Применить"
    /// </summary>
    private void RestoreOriginalApplyButtonSound()
    {
        if (currentApplyButtonClip == null || originalApplyButtonClip == null)
        {
            return;
        }
        
        ReplaceAudioClipInAllAudioSources(currentApplyButtonClip, originalApplyButtonClip);
        currentApplyButtonClip = null;
    }
    
    /// <summary>
    /// Восстановление оригинального звука кнопки "Назад"
    /// </summary>
    private void RestoreOriginalBackButtonSound()
    {
        if (currentBackButtonClip == null || originalBackButtonClip == null)
        {
            return;
        }
        
        ReplaceAudioClipInAllAudioSources(currentBackButtonClip, originalBackButtonClip);
        currentBackButtonClip = null;
    }
    
    /// <summary>
    /// Замена AudioClip во всех AudioSource, которые используют оригинальный клип
    /// Ищет во всех сценах и загруженных объектах
    /// </summary>
    private void ReplaceAudioClipInAllAudioSources(AudioClip originalClip, AudioClip newClip)
    {
        if (originalClip == null || newClip == null)
        {
            return;
        }
        
        // Находим все AudioSource в текущей сцене и во всех загруженных объектах
        AudioSource[] allAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();
        
        int replacedCount = 0;
        foreach (AudioSource audioSource in allAudioSources)
        {
            // Пропускаем префабы и объекты, которые не находятся в сцене
            // Проверяем, что объект активен в иерархии или является частью сцены
            if (audioSource == null || audioSource.gameObject == null)
            {
                continue;
            }
            
            // Пропускаем префабы в проекте (они не инстанцированы)
            #if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(audioSource))
            {
                continue;
            }
            #endif
            
            // Если AudioSource использует оригинальный клип, заменяем его
            if (audioSource.clip == originalClip)
            {
                // Сохраняем оригинальный клип для восстановления (если еще не сохранен)
                if (!originalClipsBackup.ContainsKey(audioSource))
                {
                    originalClipsBackup[audioSource] = originalClip;
                }
                
                // Заменяем клип
                audioSource.clip = newClip;
                replacedCount++;
            }
        }
    }
    
    /// <summary>
    /// Запуск проигрывания музыки меню во всех AudioSource, которые используют музыку меню
    /// Применяется ко всем сценам, не только к меню
    /// </summary>
    private void StartMenuMusicPlayback()
    {
        if (originalMenuMusicClip == null)
        {
            return;
        }
        
        // Находим все AudioSource в текущей сцене
        AudioSource[] allAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>();
        
        int replacedCount = 0;
        int startedCount = 0;
        AudioClip clipToUse = null;
        
        #if !UNITY_EDITOR
        // В билде используем загруженный AudioClip из мода
        clipToUse = loadedMenuMusicClip;
        #else
        // В редакторе используем оригинальный клип (он обновлен через AssetDatabase)
        clipToUse = originalMenuMusicClip;
        #endif
        
        foreach (AudioSource audioSource in allAudioSources)
        {
            if (audioSource == null || audioSource.gameObject == null)
            {
                continue;
            }
            
            #if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(audioSource))
            {
                continue;
            }
            #endif
            
            // Если AudioSource использует оригинальный клип музыки меню
            if (audioSource.clip == originalMenuMusicClip)
            {
                #if !UNITY_EDITOR
                // В билде заменяем на загруженный AudioClip из мода
                if (clipToUse != null)
                {
                    audioSource.clip = clipToUse;
                    replacedCount++;
                }
                #else
                // В редакторе перезагружаем через AssetDatabase
                string assetPath = AssetDatabase.GetAssetPath(originalMenuMusicClip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AudioClip reloadedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (reloadedClip != null)
                    {
                        audioSource.clip = reloadedClip;
                        replacedCount++;
                    }
                }
                #endif
                
                // Запускаем проигрывание, если оно еще не начато и AudioSource должен автоматически проигрываться
                if (!audioSource.isPlaying && audioSource.clip != null && audioSource.playOnAwake)
                {
                    audioSource.Play();
                    startedCount++;
                }
            }
        }
    }
    
    /// <summary>
    /// Восстановление всех оригинальных AudioClip
    /// </summary>
    private void RestoreAllOriginalAudioClips()
    {
        int restoredCount = 0;
        
        // Восстанавливаем все сохраненные оригинальные клипы
        foreach (var kvp in originalClipsBackup)
        {
            AudioSource audioSource = kvp.Key;
            AudioClip originalClip = kvp.Value;
            
            if (audioSource != null && originalClip != null)
            {
                audioSource.clip = originalClip;
                restoredCount++;
            }
        }
        
        // Очищаем backup
        originalClipsBackup.Clear();
        currentMenuMusicClip = null;
        currentApplyButtonClip = null;
        currentBackButtonClip = null;
    }
    
    /// <summary>
    /// Выбор мода для изменения приоритета
    /// </summary>
    public void SelectMod(ModData mod)
    {
        selectedMod = mod;
        UpdatePriorityButtonsState();
        
        // Обновляем визуальное отображение выбранного мода
        RefreshModDisplay();
    }
    
    /// <summary>
    /// Отмена выбора мода
    /// </summary>
    public void DeselectMod()
    {
        selectedMod = null;
        UpdatePriorityButtonsState();
        RefreshModDisplay();
    }
    
    /// <summary>
    /// Получить выбранный мод
    /// </summary>
    public ModData GetSelectedMod()
    {
        return selectedMod;
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки "Вверх"
    /// </summary>
    private void OnMoveUpButtonClicked()
    {
        if (selectedMod == null)
        {
            return;
        }
        
        int currentIndex = activeMods.IndexOf(selectedMod);
        if (currentIndex > 0)
        {
            // Перемещаем мод вверх
            activeMods.RemoveAt(currentIndex);
            activeMods.Insert(currentIndex - 1, selectedMod);
            
            // Только обновляем UI, но НЕ применяем изменения
            // Изменения будут применены только после нажатия кнопки "Применить"
            RefreshModDisplay();
            UpdatePriorityButtonsState();
        }
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки "Вниз"
    /// </summary>
    private void OnMoveDownButtonClicked()
    {
        if (selectedMod == null)
        {
            return;
        }
        
        int currentIndex = activeMods.IndexOf(selectedMod);
        if (currentIndex < activeMods.Count - 1)
        {
            // Перемещаем мод вниз
            activeMods.RemoveAt(currentIndex);
            activeMods.Insert(currentIndex + 1, selectedMod);
            
            // Только обновляем UI, но НЕ применяем изменения
            // Изменения будут применены только после нажатия кнопки "Применить"
            RefreshModDisplay();
            UpdatePriorityButtonsState();
        }
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
/// Класс для хранения данных активных модов (не используется для сохранения)
/// </summary>
[System.Serializable]
public class ActiveModsData
{
    public string[] modPaths;
}


