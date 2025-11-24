using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TerminalShopItemUI : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text priceText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyButton;
    
    private LobbyTerminalController controller;
    private LobbyTerminalController.ShopItemDefinition definition;
    private int itemIndex;
    private int cachedPrice;
    
    public void Setup(int index, LobbyTerminalController.ShopItemDefinition itemDefinition, LobbyTerminalController owner)
    {
        itemIndex = index;
        definition = itemDefinition;
        controller = owner;
        
        cachedPrice = controller.ResolveItemPrice(itemIndex);
        
        if (nameText != null && definition.itemData != null)
        {
            nameText.text = definition.itemData.itemName;
        }
        
        if (descriptionText != null && definition.itemData != null)
        {
            descriptionText.text = definition.itemData.description;
        }
        
        if (iconImage != null)
        {
            iconImage.sprite = definition.itemData != null ? definition.itemData.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }
        
        if (priceText != null)
        {
            priceText.text = CurrencyFormatter.FormatBits(cachedPrice);
        }
        
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnBuyClicked);
            buyButton.onClick.AddListener(OnBuyClicked);
        }
    }
    
    void OnDestroy()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnBuyClicked);
        }
    }
    
    public void RefreshAffordability(int coins)
    {
        if (buyButton != null)
        {
            buyButton.interactable = coins >= cachedPrice;
        }
    }
    
    void OnBuyClicked()
    {
        controller?.RequestPurchase(itemIndex);
    }
}

