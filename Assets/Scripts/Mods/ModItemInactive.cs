using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Скрипт для неактивного префаба мода
/// </summary>
public class ModItemInactive : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Image компонент для отображения логотипа мода")]
    public Image modLogoImage;
    
    [Tooltip("TextMeshProUGUI компонент для отображения названия мода")]
    public Text textNameMod;
    
    [Tooltip("TextMeshProUGUI компонент для отображения версии мода")]
    public Text textVersion;
    
    [Tooltip("Кнопка для активации мода")]
    public Button activateButton;
    
    [Header("Outlines")]
    [Tooltip("Outline предупреждения о частичной совместимости")]
    public Outline warningOutline;
    
    [Tooltip("Outline блокировки мода")]
    public Outline blockOutline;
    
    private ModData modData;
    private ModConfiguration modConfiguration;
    
    /// <summary>
    /// Инициализация неактивного мода
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
    /// Настройка кнопки активации
    /// </summary>
    private void SetupButton()
    {
        if (activateButton != null)
        {
            activateButton.onClick.RemoveAllListeners();
            
            // Если мод несовместим, блокируем кнопку
            if (modData.compatibility == VersionCompatibility.Incompatible)
            {
                activateButton.interactable = false;
            }
            else
            {
                // Разрешаем активацию для Compatible, Warning и Unknown
                activateButton.interactable = true;
                activateButton.onClick.AddListener(OnActivateButtonClicked);
            }
        }
    }
    
    /// <summary>
    /// Обновление предупреждений о совместимости
    /// </summary>
    private void UpdateCompatibilityWarnings()
    {
        SetOutlineState(warningOutline, false);
        SetOutlineState(blockOutline, false);

        switch (modData.compatibility)
        {
            case VersionCompatibility.Warning:
                SetOutlineState(warningOutline, true);
                break;
                
            case VersionCompatibility.Incompatible:
                SetOutlineState(blockOutline, true);
                break;
                
            case VersionCompatibility.Compatible:
            case VersionCompatibility.Unknown:
            default:
                // Ничего не показываем
                break;
        }
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки активации
    /// </summary>
    private void OnActivateButtonClicked()
    {
        if (modData != null && modConfiguration != null)
        {
            modConfiguration.ActivateMod(modData);
        }
    }

    private void SetOutlineState(Outline outline, bool state)
    {
        if (outline != null)
        {
            outline.enabled = state;
        }
    }
    
    void OnDestroy()
    {
        if (activateButton != null)
        {
            activateButton.onClick.RemoveAllListeners();
        }
    }
}

