using UnityEngine;

/// <summary>
/// Базовый класс для предметов инвентаря
/// </summary>
[System.Serializable]
public class InventoryItem
{
    [Header("Основные свойства")]
    public string itemName;
    public string description;
    public Sprite icon;
    public GameObject itemPrefab; // Префаб для отображения в руке
    public int maxStackSize = 1;
    
    [Header("Категория предмета")]
    public ItemCategory category = ItemCategory.Tool;
    
    [Header("Физические свойства")]
    public float weight = 1f;
    public bool isBreakable = false; // Может ли предмет разбиться при падении
    
    [Header("Использование")]
    public bool canBeUsed = false;
    public float useCooldown = 1f;
    
    [Header("Тег и слой")]
    public string itemTag = "Untagged";
    public int itemLayer = 0;
    
    public InventoryItem(string name, string desc, Sprite itemIcon, GameObject prefab)
    {
        itemName = name;
        description = desc;
        icon = itemIcon;
        itemPrefab = prefab;
    }
    
    public InventoryItem()
    {
        itemName = "Unknown Item";
        description = "No description available";
    }
}

/// <summary>
/// Категории предметов в инвентаре
/// </summary>
public enum ItemCategory
{
    Tool,       // Инструменты
    Weapon,     // Оружие
    Consumable, // Расходники
    Material,   // Материалы
    Misc        // Разное
}
