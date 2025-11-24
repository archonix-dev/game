using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TerminalShopUI : MonoBehaviour
{
    [SerializeField] private Transform itemsRoot;
    [SerializeField] private GameObject itemEntryPrefab;
    [SerializeField] private Text balanceText;
    [SerializeField] private Text feedbackText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private float feedbackVisibleTime = 3f;
    
    private LobbyTerminalController controller;
    private readonly List<TerminalShopItemUI> itemEntries = new List<TerminalShopItemUI>();
    private float feedbackTimer;
    private bool feedbackActive;
    private bool initialized;
    
    public void Initialize(LobbyTerminalController owner)
    {
        if (initialized)
            return;
        
        controller = owner;
        initialized = true;
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
        
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        
        BuildCatalog();
    }
    
    void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }
        
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
        }
    }
    
    void Update()
    {
        if (!feedbackActive)
            return;
        
        feedbackTimer -= Time.unscaledDeltaTime;
        if (feedbackTimer <= 0f)
        {
            feedbackActive = false;
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }
    }
    
    public void Show()
    {
        feedbackActive = false;
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }
    
    public void Hide()
    {
        feedbackActive = false;
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }
    
    void BuildCatalog()
    {
        if (itemsRoot == null || itemEntryPrefab == null || controller == null || controller.ShopItems == null)
            return;
        
        for (int i = itemsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsRoot.GetChild(i).gameObject);
        }
        itemEntries.Clear();
        
        var definitions = controller.ShopItems;
        for (int i = 0; i < definitions.Length; i++)
        {
            GameObject entryGO = Instantiate(itemEntryPrefab, itemsRoot);
            if (entryGO == null)
                continue;
            
            TerminalShopItemUI entry = entryGO.GetComponent<TerminalShopItemUI>();
            if (entry == null)
                continue;
            
            entry.Setup(i, definitions[i], controller);
            itemEntries.Add(entry);
        }
    }
    
    public void UpdateBalance(int coins)
    {
        if (balanceText != null)
        {
            balanceText.text = CurrencyFormatter.FormatBits(coins);
        }
    }
    
    public void UpdateItemAffordability(int coins)
    {
        foreach (var entry in itemEntries)
        {
            entry?.RefreshAffordability(coins);
        }
    }
    
    public void ShowPurchaseResult(bool success, string message)
    {
        if (feedbackText == null)
            return;
        
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
        feedbackText.color = success ? Color.green : Color.red;
        feedbackActive = true;
        feedbackTimer = feedbackVisibleTime;
    }
    
    public void SetStartButtonState(bool visible, bool inProgress)
    {
        if (startGameButton == null)
            return;
        
        startGameButton.gameObject.SetActive(visible);
        startGameButton.interactable = visible && !inProgress;
    }
    
    void OnCloseClicked()
    {
        controller?.CloseTerminal();
    }
    
    void OnStartGameClicked()
    {
        controller?.RequestMainGameStart();
    }
}

