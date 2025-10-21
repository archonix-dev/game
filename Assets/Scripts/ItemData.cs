using UnityEngine;

/// <summary>
/// Enum для типов предметов
/// </summary>
public enum ItemType
{
    Normal,         // Обычный предмет (не расходуется)
    Health,         // Предмет который лечит
    MaxHealth,      // Предмет который увеличивает максимальный запас здоровья
    MaxStamina      // Предмет который увеличивает максимальный запас стамины
}

/// <summary>
/// ScriptableObject для данных предметов
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Основная информация")]
    public string itemName;
    public string description;
    public Sprite icon;
    public ItemType itemType = ItemType.Normal;
    
    [Header("Настройки предмета")]
    public float weight = 1f;
    public bool isBreakable = false;
    public string itemTag = "Untagged";
    public int itemLayer = 0;
    
    [Header("Эффекты предмета")]
    [Tooltip("Количество здоровья для восстановления (только для Health)")]
    public float healthAmount = 0f;
    
    [Tooltip("Количество максимального здоровья для увеличения (только для MaxHealth)")]
    public float maxHealthAmount = 0f;
    
    [Tooltip("Количество максимальной стамины для увеличения (только для MaxStamina)")]
    public float maxStaminaAmount = 0f;
    
    [Header("Визуальные настройки")]
    public GameObject itemPrefab;
    
    /// <summary>
    /// Проверяет, является ли предмет расходуемым
    /// </summary>
    public bool IsConsumable()
    {
        return itemType != ItemType.Normal;
    }
    
    /// <summary>
    /// Получает описание эффекта предмета
    /// </summary>
    public string GetEffectDescription()
    {
        switch (itemType)
        {
            case ItemType.Health:
                return $"Восстанавливает {healthAmount} здоровья";
            case ItemType.MaxHealth:
                return $"Увеличивает максимальное здоровье на {maxHealthAmount}";
            case ItemType.MaxStamina:
                return $"Увеличивает максимальную стамину на {maxStaminaAmount}";
            default:
                return "Обычный предмет";
        }
    }
}
