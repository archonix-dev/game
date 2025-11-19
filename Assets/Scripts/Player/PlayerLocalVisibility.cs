using UnityEngine;
using Mirror;

/// <summary>
/// Локальное управление видимостью объектов на игроке.
/// Скрипт должен быть на объекте игрока с NetworkIdentity/NetworkBehaviour.
/// </summary>
public class PlayerLocalVisibility : NetworkBehaviour
{
    [Header("Объекты, скрытые только у локального игрока")]
    [Tooltip("Эти объекты будут скрыты только у владельца (isOwned == true), но видны для всех остальных клиентов.")]
    [SerializeField] private GameObject[] hideForLocalOnly;

    [Header("Объект, показываемый при открытии меню")]
    [Tooltip("Этот объект показывается у локального игрока при открытом меню и скрывается при закрытом.")]
    [SerializeField] private GameObject menuLocalObject;

    // Локальный флаг состояния меню для владельца
    private bool isMenuOpenLocal = false;

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyLocalVisibility();
        // Меню-объект по умолчанию всегда скрыт
        if (menuLocalObject != null)
        {
            menuLocalObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        // На всякий случай постоянно поддерживаем нужное состояние для локального игрока,
        // т.к. другие скрипты (например, PlayerController) могут менять SetActive.
        if (!isOwned)
            return;

        if (hideForLocalOnly != null && hideForLocalOnly.Length > 0)
        {
            foreach (var go in hideForLocalOnly)
            {
                if (go == null) continue;
                if (go.activeSelf)
                    go.SetActive(false);
            }
        }

        // Поддерживаем правильное состояние menuLocalObject:
        // видно только у локального игрока и только когда открыто меню.
        if (menuLocalObject != null)
        {
            bool shouldBeVisible = isMenuOpenLocal; // мы уже знаем, что isOwned == true
            if (menuLocalObject.activeSelf != shouldBeVisible)
            {
                menuLocalObject.SetActive(shouldBeVisible);
            }
        }
    }

    /// <summary>
    /// Применяет логику локальной видимости для массива объектов.
    /// </summary>
    private void ApplyLocalVisibility()
    {
        if (hideForLocalOnly == null || hideForLocalOnly.Length == 0)
            return;

        // Для владельца скрываем объекты, для остальных клиентов — показываем
        bool shouldBeActiveForLocal = false;

        foreach (var go in hideForLocalOnly)
        {
            // Не трогаем объект, который используется как menuLocalObject,
            // его видимость контролируется через OnMenuStateChanged.
            if (go == null || go == menuLocalObject)
                continue;

            if (isOwned)
            {
                // Локальный игрок — скрываем
                go.SetActive(shouldBeActiveForLocal);
            }
            else
            {
                // Удаленные клиенты — всегда показываем
                if (!go.activeSelf)
                    go.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Вызывается контроллером меню при изменении состояния меню.
    /// Меняет видимость menuLocalObject только у локального игрока.
    /// </summary>
    /// <param name="open">true, если меню открыто</param>
    public void OnMenuStateChanged(bool open)
    {
        // Запоминаем локальное состояние меню
        isMenuOpenLocal = open;

        if (!isOwned)
            return;

        if (menuLocalObject != null)
        {
            menuLocalObject.SetActive(open);
        }
    }
}


