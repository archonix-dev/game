using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Скрипт для активного префаба мода
/// </summary>
public class ModItemActive : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Image компонент для отображения логотипа мода")]
    public Image modLogoImage;
    
    [Tooltip("TextMeshProUGUI компонент для отображения названия мода")]
    public Text textNameMod;
    
    [Tooltip("TextMeshProUGUI компонент для отображения версии мода")]
    public Text textVersion;
    
    [Tooltip("Кнопка для деактивации мода")]
    public Button deactivateButton;
    
    [Tooltip("Кнопка для выбора мода (для изменения приоритета)")]
    public Button selectButton;
    
    [Header("Selection Visual")]
    [Tooltip("Визуальный индикатор выбранного мода (опционально)")]
    public GameObject selectionIndicator;
    
    [Tooltip("Цвет выбранного мода (опционально)")]
    public Color selectedColor = new Color(1f, 1f, 0.5f, 1f);
    
    [Header("Warning Objects")]
    [Tooltip("GameObject предупреждения о несовместимости (warning_use_mod)")]
    public GameObject warningUseMod;
    
    [Tooltip("GameObject блокировки мода (dont_use_mod)")]
    public GameObject dontUseMod;
    
    private ModData modData;
    private ModConfiguration modConfiguration;
    private bool isSelected = false;
    private Color originalColor = Color.white;
    
    /// <summary>
    /// Инициализация активного мода
    /// </summary>
    public void Initialize(ModData mod, ModConfiguration config)
    {
        modData = mod;
        modConfiguration = config;
        
        UpdateUI();
        SetupButtons();
        UpdateCompatibilityWarnings();
        
        // Сохраняем оригинальный цвет для восстановления
        if (modLogoImage != null)
        {
            originalColor = modLogoImage.color;
        }
    }
    
    /// <summary>
    /// Обновление UI элементов
    /// </summary>
    private void UpdateUI()
    {
        // Устанавливаем логотип
        if (modLogoImage != null && modData.modLogo != null)
        {
            modLogoImage.sprite = modData.modLogo;
        }
        else if (modLogoImage != null)
        {
            modLogoImage.sprite = null;
            modLogoImage.color = Color.clear;
        }
        
        // Устанавливаем название мода
        if (textNameMod != null)
        {
            textNameMod.text = modData.modName;
        }
        
        // Устанавливаем версию
        if (textVersion != null)
        {
            // Для обязательного мода "localhost" отображаем специальный текст
            if (modConfiguration != null && modConfiguration.IsRequiredMod(modData))
            {
                textVersion.text = "Системный мод. Нельзя удалить.";
            }
            else
            {
                // Для обычных модов отображаем версию в формате: "Version_mod Listrite version Version_mod_game"
                textVersion.text = $"{modData.modVersion} Listrite version {modData.gameVersion}";
            }
        }
        
        // Скрываем кнопку деактивации для обязательного мода "localhost"
        UpdateDeactivateButtonVisibility();
    }
    
    /// <summary>
    /// Обновление видимости кнопки деактивации
    /// </summary>
    private void UpdateDeactivateButtonVisibility()
    {
        if (deactivateButton != null && modConfiguration != null && modData != null)
        {
            // Скрываем кнопку деактивации, если мод является обязательным
            bool isRequired = modConfiguration.IsRequiredMod(modData);
            deactivateButton.gameObject.SetActive(!isRequired);
        }
    }
    
    /// <summary>
    /// Настройка кнопок
    /// </summary>
    private void SetupButtons()
    {
        if (deactivateButton != null)
        {
            deactivateButton.onClick.RemoveAllListeners();
            deactivateButton.onClick.AddListener(OnDeactivateButtonClicked);
        }
        
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectButtonClicked);
        }
    }
    
    /// <summary>
    /// Установка состояния выбора мода
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateSelectionVisual();
    }
    
    /// <summary>
    /// Обновление визуального отображения выбранного состояния
    /// </summary>
    private void UpdateSelectionVisual()
    {
        // Обновляем индикатор выбора
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(isSelected);
        }
        
        // Обновляем цвет (можно использовать для выделения выбранного мода)
        if (modLogoImage != null)
        {
            if (isSelected)
            {
                modLogoImage.color = selectedColor;
            }
            else
            {
                modLogoImage.color = originalColor;
            }
        }
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки выбора
    /// </summary>
    private void OnSelectButtonClicked()
    {
        if (modData != null && modConfiguration != null)
        {
            // Если мод уже выбран, снимаем выбор, иначе выбираем его
            if (modConfiguration.GetSelectedMod() == modData)
            {
                modConfiguration.DeselectMod();
            }
            else
            {
                modConfiguration.SelectMod(modData);
            }
        }
    }
    
    /// <summary>
    /// Обновление предупреждений о совместимости
    /// </summary>
    private void UpdateCompatibilityWarnings()
    {
        // Скрываем все предупреждения по умолчанию
        if (warningUseMod != null)
        {
            warningUseMod.SetActive(false);
        }
        
        if (dontUseMod != null)
        {
            dontUseMod.SetActive(false);
        }
        
        // Обязательный мод "localhost" всегда совместим, предупреждения не показываем
        if (modConfiguration != null && modConfiguration.IsRequiredMod(modData))
        {
            return;
        }
        
        // Показываем соответствующие предупреждения
        switch (modData.compatibility)
        {
            case VersionCompatibility.Warning:
                if (warningUseMod != null)
                {
                    warningUseMod.SetActive(true);
                }
                break;
                
            case VersionCompatibility.Incompatible:
                if (dontUseMod != null)
                {
                    dontUseMod.SetActive(true);
                }
                break;
                
            case VersionCompatibility.Compatible:
            case VersionCompatibility.Unknown:
            default:
                // Ничего не показываем
                break;
        }
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки деактивации
    /// </summary>
    private void OnDeactivateButtonClicked()
    {
        if (modData != null && modConfiguration != null)
        {
            // Дополнительная проверка: не позволяем деактивировать обязательный мод
            if (modConfiguration.IsRequiredMod(modData))
            {
                Debug.LogWarning($"Нельзя деактивировать обязательный мод '{modData.modName}'");
                return;
            }
            
            modConfiguration.DeactivateMod(modData);
        }
    }
    
    void OnDestroy()
    {
        if (deactivateButton != null)
        {
            deactivateButton.onClick.RemoveAllListeners();
        }
        
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
        }
    }
}

