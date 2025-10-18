using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вспомогательный скрипт для настройки UI системы броска
/// Добавьте этот скрипт к Canvas или UI панели для автоматической настройки
/// </summary>
public class ThrowUIHelper : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject throwUIObject;
    [SerializeField] private Image throwForceImage;
    [SerializeField] private Text throwForceText;
    
    [Header("Settings")]
    [SerializeField] private ObjectGrabSystem grabSystem;
    
    void Start()
    {
        // Автоматически находим ObjectGrabSystem если не назначен
        if (grabSystem == null)
        {
            grabSystem = FindObjectOfType<ObjectGrabSystem>();
        }
        
        // Настраиваем UI элементы
        SetupUI();
    }
    
    void SetupUI()
    {
        if (throwUIObject == null)
        {
            // Создаем UI объект если не назначен
            CreateThrowUI();
        }
        
        // Настраиваем компоненты
        if (throwForceImage != null)
        {
            throwForceImage.type = Image.Type.Filled;
            throwForceImage.fillMethod = Image.FillMethod.Horizontal;
            throwForceImage.fillAmount = 0f;
        }
        
        if (throwForceText != null)
        {
            throwForceText.text = "Сила броска: 0.0";
            throwForceText.fontSize = 24;
            throwForceText.color = Color.white;
        }
    }
    
    void CreateThrowUI()
    {
        // Создаем панель для UI броска
        GameObject uiPanel = new GameObject("ThrowUIPanel");
        uiPanel.transform.SetParent(transform);
        
        RectTransform panelRect = uiPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.8f);
        panelRect.anchorMax = new Vector2(0.5f, 0.8f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(300, 100);
        
        // Добавляем фон
        Image panelImage = uiPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.5f);
        
        // Создаем текст
        GameObject textObj = new GameObject("ThrowForceText");
        textObj.transform.SetParent(uiPanel.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
        throwForceText = textObj.AddComponent<Text>();
        throwForceText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        throwForceText.fontSize = 24;
        throwForceText.color = Color.white;
        throwForceText.alignment = TextAnchor.MiddleCenter;
        throwForceText.text = "Сила броска: 0.0";
        
        // Создаем полосу прогресса
        GameObject progressObj = new GameObject("ThrowForceProgress");
        progressObj.transform.SetParent(uiPanel.transform);
        
        RectTransform progressRect = progressObj.AddComponent<RectTransform>();
        progressRect.anchorMin = new Vector2(0.1f, 0.1f);
        progressRect.anchorMax = new Vector2(0.9f, 0.3f);
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = Vector2.zero;
        
        throwForceImage = progressObj.AddComponent<Image>();
        throwForceImage.color = Color.green;
        throwForceImage.type = Image.Type.Filled;
        throwForceImage.fillMethod = Image.FillMethod.Horizontal;
        throwForceImage.fillAmount = 0f;
        
        throwUIObject = uiPanel;
        
        Debug.Log("UI для системы броска создан автоматически!");
    }
    
    void Update()
    {
        // Обновляем UI в реальном времени
        if (grabSystem != null && grabSystem.IsHoldingObject())
        {
            if (throwForceText != null)
            {
                throwForceText.text = $"Сила броска: {grabSystem.GetCurrentThrowForce():F1}";
            }
            
            if (throwForceImage != null)
            {
                throwForceImage.fillAmount = grabSystem.GetThrowChargeProgress();
                
                // Меняем цвет полосы в зависимости от прогресса
                if (grabSystem.GetThrowChargeProgress() > 0.8f)
                {
                    throwForceImage.color = Color.red;
                }
                else if (grabSystem.GetThrowChargeProgress() > 0.5f)
                {
                    throwForceImage.color = Color.yellow;
                }
                else
                {
                    throwForceImage.color = Color.green;
                }
            }
        }
    }
}
