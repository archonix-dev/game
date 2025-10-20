using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject menuRoot;
    public PlayerController playerController;
    public MouseLook mouseLook;
    
    [Header("Settings UI References")]
    public GraphicsSettingsUI graphicsSettingsUI;
    public AudioSettingsUI audioSettingsUI;
    
    private bool isMenuOpen = false;
    
    void Start()
    {
        SetMenuState(false);
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetMenuState(!isMenuOpen);
        }
    }
    
    public void SetMenuState(bool open)
    {
        isMenuOpen = open;
        if (menuRoot != null)
        {
            menuRoot.SetActive(open);
        }
        
        if (playerController != null)
        {
            playerController.enabled = !open;
        }
        
        if (mouseLook != null)
        {
            mouseLook.enabled = !open;
        }
        
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Обновляем настройки при открытии меню
            RefreshSettingsUI();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    private void RefreshSettingsUI()
    {
        // Обновляем UI настроек при открытии меню
        if (graphicsSettingsUI != null)
        {
            graphicsSettingsUI.LoadCurrentSettings();
        }
        
        if (audioSettingsUI != null)
        {
            audioSettingsUI.LoadCurrentSettings();
        }
    }
    
    // Методы для кнопок меню (если нужны)
    public void SaveAllSettings()
    {
        if (graphicsSettingsUI != null)
        {
            // GraphicsSettingsUI сохраняет настройки автоматически при нажатии Apply
        }
        
        if (audioSettingsUI != null)
        {
            // AudioSettingsUI сохраняет настройки автоматически при нажатии Apply
        }
        
        Debug.Log("All settings saved!");
    }
    
    public void ResetAllSettings()
    {
        if (graphicsSettingsUI != null)
        {
            // Можно добавить метод ResetToDefaults в GraphicsSettingsUI
        }
        
        if (audioSettingsUI != null)
        {
            // Можно добавить метод ResetToDefaults в AudioSettingsUI
        }
        
        Debug.Log("All settings reset to defaults!");
    }
}
