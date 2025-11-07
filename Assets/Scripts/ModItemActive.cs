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
    
    [Header("Warning Objects")]
    [Tooltip("GameObject предупреждения о несовместимости (warning_use_mod)")]
    public GameObject warningUseMod;
    
    [Tooltip("GameObject блокировки мода (dont_use_mod)")]
    public GameObject dontUseMod;
    
    private ModData modData;
    private ModConfiguration modConfiguration;
    
    /// <summary>
    /// Инициализация активного мода
    /// </summary>
    public void Initialize(ModData mod, ModConfiguration config)
    {
        modData = mod;
        modConfiguration = config;
        
        UpdateUI();
        SetupButton();
        UpdateCompatibilityWarnings();
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
        
        // Устанавливаем версию в формате: "Version_mod Listrite version Version_mod_game"
        if (textVersion != null)
        {
            textVersion.text = $"{modData.modVersion} Listrite version {modData.gameVersion}";
        }
    }
    
    /// <summary>
    /// Настройка кнопки деактивации
    /// </summary>
    private void SetupButton()
    {
        if (deactivateButton != null)
        {
            deactivateButton.onClick.RemoveAllListeners();
            deactivateButton.onClick.AddListener(OnDeactivateButtonClicked);
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
            modConfiguration.DeactivateMod(modData);
        }
    }
    
    void OnDestroy()
    {
        if (deactivateButton != null)
        {
            deactivateButton.onClick.RemoveAllListeners();
        }
    }
}

