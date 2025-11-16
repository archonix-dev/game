# Настройка SteamNetworkingSockets Transport

## Обзор

`SteamNetworkingTransport.cs` - это кастомный transport для Unity Netcode, который использует SteamNetworkingSockets для прямого P2P соединения между игроками через Steam.

## Преимущества

1. **Прямое P2P соединение** - игроки подключаются напрямую через Steam, без необходимости знать IP адреса
2. **NAT Traversal** - Steam автоматически решает проблемы с NAT
3. **Релейные серверы** - Steam предоставляет релейные серверы для игроков, которые не могут установить прямое соединение
4. **Низкая задержка** - прямое соединение обеспечивает минимальную задержку

## Установка

### Шаг 1: Убедитесь, что Steamworks.NET установлен

Следуйте инструкциям в `STEAM_INTEGRATION_INSTRUCTIONS.md` для установки Steamworks.NET.

### Шаг 2: Настройка NetworkManager

1. Откройте сцену с NetworkManager
2. Выберите GameObject с компонентом NetworkManager
3. В инспекторе найдите раздел "Network Transport"
4. Удалите или отключите текущий UnityTransport компонент
5. Добавьте компонент `SteamNetworkingTransport`
6. Настройте параметры:
   - **Max Packet Size**: 1200 (рекомендуется для Steam)
   - **Connection Timeout**: 10 секунд

### Шаг 3: Обновление LobbyManager

`LobbyManager` автоматически будет использовать `SteamNetworkingTransport`, если он установлен на NetworkManager.

## Использование

### Создание лобби (Хост)

1. Игрок нажимает "Играть"
2. `SteamLobbyManager` создает Steam лобби
3. `LobbyManager` создает Unity Netcode лобби
4. `SteamNetworkingTransport` запускает сервер через `StartServer()`
5. Сервер начинает слушать P2P соединения через Steam

### Подключение к лобби (Клиент)

1. Игрок нажимает "Присоединиться к другу" в Steam оверлее
2. `SteamLobbyManager` подключается к Steam лобби
3. `SteamNetworkingTransport` запускает клиент через `StartClient()`
4. Клиент получает Steam ID хоста из `SteamLobbyManager`
5. Клиент подключается к хосту через `ConnectP2P()`

## Важные замечания

### Обработка новых подключений

Текущая реализация `ProcessNewConnections()` упрощена. Для полноценной работы нужно:

1. **Подписаться на callback события:**
```csharp
SteamNetworkingSockets.OnConnectionStatusChanged += OnConnectionStatusChanged;
```

2. **Обработать события подключения:**
```csharp
void OnConnectionStatusChanged(SteamNetworkingConnectionStatusChangedCallback_t callback)
{
    if (callback.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
    {
        // Новый клиент подключен
        ulong clientId = callback.m_info.m_identityRemote.GetSteamID64();
        clientConnections[clientId] = callback.m_hConn;
    }
}
```

### Релейные серверы

SteamNetworkingSockets автоматически использует релейные серверы, если прямое соединение невозможно. Убедитесь, что:

1. `SteamNetworkingUtils.InitRelayNetworkAccess()` вызывается при инициализации
2. Игроки имеют активный Steam аккаунт
3. Игра правильно настроена в Steamworks Partner Portal

### Ограничения

1. **Максимальный размер пакета**: SteamNetworkingSockets имеет ограничение на размер пакета (обычно 1200 байт)
2. **Только P2P**: Этот transport работает только для P2P соединений, не для выделенных серверов
3. **Требуется Steam**: Все игроки должны иметь запущенный Steam клиент

## Расширенная настройка

### Настройка качества соединения

Можно настроить параметры соединения через `SteamNetworkingConfigValue_t`:

```csharp
SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[]
{
    new SteamNetworkingConfigValue_t
    {
        m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize,
        m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
        m_int32 = 1024 * 1024 // 1 MB
    }
};
```

### Логирование и отладка

Для отладки можно включить логирование SteamNetworkingSockets:

```csharp
SteamNetworkingUtils.SetDebugOutputFunction(
    ESteamNetworkingSocketsDebugOutputType.k_ESteamNetworkingSocketsDebugOutputType_Verbose,
    OnDebugOutput);
```

## Решение проблем

### Клиент не может подключиться

1. Убедитесь, что Steam клиент запущен
2. Проверьте, что хост создал Steam лобби
3. Проверьте логи на наличие ошибок SteamNetworkingSockets

### Высокая задержка

1. Проверьте, используется ли релейный сервер (может увеличить задержку)
2. Убедитесь, что игроки находятся в одной сети (для прямого соединения)
3. Проверьте настройки брандмауэра

### Соединение разрывается

1. Проверьте стабильность интернет-соединения
2. Убедитесь, что Steam клиент не закрывается
3. Проверьте логи на наличие ошибок

## Дополнительные ресурсы

- [SteamNetworkingSockets Documentation](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets)
- [SteamNetworkingSockets Examples](https://github.com/ValveSoftware/GameNetworkingSockets)
- [Unity Netcode Transport Documentation](https://docs-multiplayer.unity3d.com/netcode/current/learn/transport/)

