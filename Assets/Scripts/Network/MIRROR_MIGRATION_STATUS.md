# Статус миграции на Mirror + FizzySteamworks

## Выполнено ✅

1. **manifest.json** - добавлены пакеты Mirror и FizzySteamworks
2. **MirrorNetworkManager.cs** - создан новый NetworkManager на основе Mirror
3. **LobbyNetworkManager.cs** - обновлен для использования Mirror (SyncVar, Command, ClientRpc, TargetRpc)
4. **LobbyManager.cs** - обновлен для использования Mirror API (частично, требует доработки)

## Требуется обновление ⚠️

1. **NetworkPlayer.cs** - обновить для Mirror:
   - Заменить `Unity.Netcode.NetworkBehaviour` на `Mirror.NetworkBehaviour`
   - Заменить `NetworkVariable` на `SyncVar`
   - Заменить `ServerRpc` на `Command`
   - Заменить `OwnerClientId` на `connectionToClient?.connectionId ?? 0`
   - Заменить `IsOwner` на `hasAuthority`

2. **GameManager.cs** - обновить для Mirror:
   - Заменить Unity Netcode API на Mirror API
   - Заменить `NetworkObject` на `NetworkIdentity`
   - Заменить `SpawnAsPlayerObject` на `NetworkServer.Spawn` с назначением владельца

3. **SteamLobbyManager.cs** - обновить для интеграции с Mirror:
   - Заменить вызовы `NetworkManager.StartHost()` на `MirrorNetworkManager.StartHostGame()`
   - Заменить вызовы `NetworkManager.StartClient()` на `MirrorNetworkManager.ConnectToSteamId()`

4. **ConnectToLobbyPanel.cs** - обновить для Mirror:
   - Заменить проверки `NetworkManager.IsClient` на `NetworkClient.active`
   - Заменить проверки `NetworkManager.IsHost` на `NetworkServer.activeHost`

5. **ClientNetworkTransform.cs** - заменить на Mirror NetworkTransform или создать кастомный

6. **MultiplayerManager.cs** - обновить для Mirror (если используется)

## Удалить/Заменить 🗑️

1. **SteamNetworkingTransport.cs** - больше не нужен, заменен на FizzySteamworks

## Важные изменения в API

### Unity Netcode → Mirror

- `NetworkManager.Singleton` → `MirrorNetworkManager.Instance` или `NetworkManager.singleton`
- `ulong clientId` → `uint connectionId`
- `NetworkObject` → `NetworkIdentity`
- `NetworkBehaviour.IsServer` → `NetworkBehaviour.isServer`
- `NetworkBehaviour.IsClient` → `NetworkBehaviour.isClient`
- `NetworkBehaviour.IsOwner` → `NetworkBehaviour.hasAuthority`
- `NetworkVariable<T>` → `[SyncVar]` для простых типов
- `[ServerRpc]` → `[Command]`
- `[ClientRpc]` → `[ClientRpc]` (остается)
- `NetworkObject.Spawn()` → `NetworkServer.Spawn()`
- `NetworkObject.Despawn()` → `NetworkServer.Destroy()`

## Настройка в Unity Editor

1. Создать GameObject с компонентами:
   - `MirrorNetworkManager` (скрипт)
   - `FizzySteamworks` (компонент транспорта)

2. В MirrorNetworkManager:
   - Установить Player Prefab (если используется)
   - Убедиться, что транспорт установлен на FizzySteamworks

3. В FizzySteamworks:
   - Установить Steam App ID
   - Настроить порт (если требуется)

## Тестирование

1. Запустить Unity
2. Убедиться, что Mirror и FizzySteamworks импортировались
3. Проверить, что нет ошибок компиляции
4. Протестировать подключение через Steam

## Замечания

- FizzySteamworks требует запущенный Steam клиент
- Для тестирования нужно два разных Steam аккаунта (нельзя подключиться к себе)
- Некоторые методы в LobbyManager.cs могут требовать дополнительной доработки для полной совместимости с Mirror

