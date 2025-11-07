using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject menuRoot;
    public PlayerController playerController;
    public MouseLook mouseLook;
    
    [Header("Buttons")]
    [Tooltip("Кнопка для закрытия меню (продолжить)")]
    public Button continueButton;
    
    private bool isMenuOpen = false;
    
    void Start()
    {
        SetMenuState(false);
        
        // Подписываемся на событие нажатия кнопки "Продолжить"
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }
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
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    /// <summary>
    /// Вызывается при нажатии на кнопку "Продолжить"
    /// </summary>
    private void OnContinueButtonClicked()
    {
        SetMenuState(false);
    }
    
    void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueButtonClicked);
        }
    }
}
