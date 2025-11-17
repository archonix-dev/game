# Система Steam Приглашений

## Описание
Система Steam приглашений позволяет игрокам приглашать друзей в активное лобби через встроенный Steam оверлей.

## Основные возможности
- ✅ Открытие Steam оверлея по нажатию клавиши **I**
- ✅ Приглашение друзей через Steam интерфейс
- ✅ Автоматическое присоединение по приглашению
- ✅ Синхронизация с существующим LobbyManager
- ✅ Поддержка FizzySteam транспорта

## Компоненты

### SteamLobbyManager
Основной компонент для управления Steam приглашениями.

**Настройки:**
- `lobbyType` - тип лобби Steam (по умолчанию: FriendsOnly)
- `inviteKey` - клавиша для открытия оверлея (по умолчанию: I)

**Методы:**
- `InviteFriendToLobby()` - открывает диалог приглашения Steam
- `InviteSpecificFriend(CSteamID)` - отправляет приглашение конкретному другу
- `GetLobbyPlayerCount()` - возвращает количество игроков в лобби
- `LeaveLobby()` - выход из лобби

## Установка

### 1. Добавление компонента в сцену

Добавьте компонент `SteamLobbyManager` на любой GameObject в сцене Menu (или создайте новый):

```
1. Создайте пустой GameObject: Hierarchy → Create Empty
2. Переименуйте в "SteamLobbyManager"
3. Добавьте компонент: Add Component → SteamLobbyManager
```

### 2. Настройка

В Inspector настройте параметры:

- **Lobby Type**: `FriendsOnly` (рекомендуется)
- **Invite Key**: `I` (или любая другая клавиша по вашему выбору)

### 3. Интеграция с существующим кодом

Система автоматически интегрируется с `LobbyManager`. При создании или присоединении к лобби, `SteamLobbyManager` автоматически синхронизируется.

## Использование

### Для игрока-хоста (создавшего лобби):

1. Создайте лобби через стандартный интерфейс
2. После создания лобби нажмите клавишу **I**
3. Откроется Steam оверлей с списком друзей
4. Выберите друзей для приглашения
5. Друзья получат уведомление в Steam

### Для приглашённого игрока:

1. Получите уведомление от Steam о приглашении
2. Нажмите "Принять" в уведомлении Steam
3. Игра автоматически подключится к лобби

## Технические детали

### Callbacks Steam

Система использует следующие Steam callbacks:

- `LobbyCreated_t` - создание лобби
- `GameLobbyJoinRequested_t` - запрос на присоединение через оверлей
- `LobbyEnter_t` - вход в лобби

### Синхронизация с LobbyManager

При следующих событиях происходит синхронизация:

- **OnLobbyCreated**: Устанавливается currentLobbyID в SteamLobbyManager
- **OnLobbyEntered**: Обновляется currentLobbyID при присоединении
- **LeaveLobby**: Очищается currentLobbyID в обоих менеджерах

### Проверки безопасности

Перед открытием оверлея система проверяет:
1. Steam запущен (`SteamAPI.IsSteamRunning()`)
2. Активное лобби существует (`currentLobbyID.IsValid()`)
3. LobbyManager инициализирован

Если проверки не пройдены, в консоль выводится предупреждение.

## Отладка

### Логи

Все действия системы логируются с префиксом `[SteamLobbyManager]`:

```
[SteamLobbyManager] Steam callbacks инициализированы
[SteamLobbyManager] Открытие диалога приглашения через Steam оверлей для лобби 123456789
[SteamLobbyManager] Установлено текущее лобби: 123456789
```

### Частые проблемы

**Оверлей не открывается:**
- Убедитесь, что Steam запущен
- Проверьте, что лобби создано
- Проверьте, что Steam оверлей включен в настройках Steam

**Друзья не получают приглашения:**
- Убедитесь, что тип лобби не `Private`
- Проверьте, что друзья находятся в вашем списке друзей Steam
- Убедитесь, что у них установлена игра

**Приглашения не работают:**
- Проверьте, что используется FizzySteam транспорт
- Убедитесь, что NetworkManager правильно настроен
- Проверьте консоль Unity на наличие ошибок

## Примеры кода

### Программное открытие диалога приглашений

```csharp
// Из любого места кода
if (SteamLobbyManager.Instance != null)
{
    SteamLobbyManager.Instance.InviteFriendToLobby();
}
```

### Приглашение конкретного друга

```csharp
CSteamID friendID = new CSteamID(123456789); // Steam ID друга
if (SteamLobbyManager.Instance != null)
{
    SteamLobbyManager.Instance.InviteSpecificFriend(friendID);
}
```

### Получение количества игроков

```csharp
if (SteamLobbyManager.Instance != null)
{
    int playerCount = SteamLobbyManager.Instance.GetLobbyPlayerCount();
    Debug.Log($"Игроков в лобби: {playerCount}");
}
```

## UI Интеграция

### Добавление кнопки приглашения в UI

Если вы хотите добавить кнопку в UI для приглашения друзей:

```csharp
using UnityEngine;
using UnityEngine.UI;

public class InviteButton : MonoBehaviour
{
    private Button button;
    
    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnInviteButtonClicked);
    }
    
    void OnInviteButtonClicked()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.InviteFriendToLobby();
        }
    }
}
```

### Индикатор доступности приглашений

Показывайте кнопку только когда лобби активно:

```csharp
void Update()
{
    bool canInvite = SteamLobbyManager.Instance != null && 
                     SteamLobbyManager.Instance.GetCurrentLobby().IsValid();
    
    button.interactable = canInvite;
}
```

## Требования

- ✅ Unity 2020.3 или новее
- ✅ Mirror Networking
- ✅ Steamworks.NET
- ✅ FizzySteam Transport
- ✅ Steam Client запущен на компьютере

## Дополнительная информация

### Типы лобби Steam

- `k_ELobbyTypePrivate` - только по приглашению, не видно в поиске
- `k_ELobbyTypeFriendsOnly` - видно только друзьям (рекомендуется)
- `k_ELobbyTypePublic` - видно всем, доступно в поиске
- `k_ELobbyTypeInvisible` - невидимое лобби, только прямое подключение

### Ограничения

- Максимальное количество игроков определяется в `LobbyManager.maxPlayers`
- Steam лобби поддерживает до 250 участников
- Приглашения работают только между друзьями Steam

## Лицензия

Используйте в соответствии с лицензией проекта.

