using UnityEngine;

/// <summary>
/// Интерфейс для объектов, которые можно подобрать
/// </summary>
public interface IPickupable
{
    /// <summary>
    /// Возвращает данные предмета для инвентаря
    /// </summary>
    InventoryItem GetInventoryItem();
    
    /// <summary>
    /// Вызывается когда предмет подбирается
    /// </summary>
    void OnPickedUp();
    
    /// <summary>
    /// Проверяет, можно ли подобрать предмет
    /// </summary>
    bool CanBePickedUp();
}
