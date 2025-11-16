# Инструкция по настройке синхронизации списка игроков

## Обзор

Для корректной работы синхронизации списка игроков между сервером и клиентами используется компонент `LobbyPlayerSync`, который является `NetworkBehaviour` и синхронизирует данные через Mirror RPC.

## Что нужно сделать

### 1. Настройка GameObject с LobbyManager

**Важно:** GameObject с `LobbyManager` должен иметь `NetworkIdentity` компонент для работы `LobbyPlayerSync`.

#### Автоматическая настройка (рекомендуется):
- Компонент `LobbyPlayerSync` автоматически добавляется в `LobbyManager.Start()`
- `NetworkIdentity` автоматически добавляется в `LobbyPlayerSync.Start()`
- Объект автоматически спавнится на сервере через `OnStartServer()`

#### Ручная настройка (если автоматическая не работает):

1. **В сцене Menu:**
   - Найдите GameObject с компонентом `LobbyManager`
   - Убедитесь, что на нем есть компонент `NetworkIdentity`
   - Если нет, добавьте его:
     - `Add Component` → `Network Identity`
     - Установите `Server Only` = `false` (чтобы был доступен на клиенте)

2. **Добавьте компонент LobbyPlayerSync:**
   - На том же GameObject добавьте компонент `LobbyPlayerSync`
   - Или он будет добавлен автоматически при запуске

### 2. Проверка спавна LobbyPlayerSync

`LobbyPlayerSync` должен быть заспавнен на сервере. Это происходит автоматически:

- При запуске сервера через `OnStartServer()`
- При вызове `OnMirrorServerStarted()` в `LobbyManager`

**Проверка в логах:**
- Должно появиться: `[LobbyPlayerSync] LobbyPlayerSync запущен на сервере`
- Должно появиться: `[LobbyPlayerSync] LobbyPlayerSync заспавнен на сервере`

### 3. Проверка работы синхронизации

#### На сервере (хост):
1. При подключении клиента должно появиться:
   - `[LobbyPlayerSync] Отправка списка игроков клиенту X. Игроков: Y`

#### На клиенте:
1. При подключении к серверу должно появиться:
   - `[LobbyPlayerSync] Получен список игроков от сервера. Игроков: X`
   - `[LobbyPlayerSync] ✓ Синхронизация списка игроков завершена. Создано PlayerLobbyItem: X`

### 4. Возможные проблемы и решения

#### Проблема: LobbyPlayerSync не спавнится на сервере

**Решение:**
1. Убедитесь, что `NetworkIdentity` добавлен на GameObject с `LobbyManager`
2. Проверьте, что `NetworkServer.active == true` при запуске сервера
3. Добавьте в `MirrorNetworkManager.OnStartServer()`:
   ```csharp
   LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
   if (lobbyManager != null && lobbyManager.playerSync != null)
   {
       NetworkIdentity netId = lobbyManager.playerSync.GetComponent<NetworkIdentity>();
       if (netId != null && netId.netId == 0)
       {
           NetworkServer.Spawn(lobbyManager.playerSync.gameObject);
       }
   }
   ```

#### Проблема: Клиент не получает список игроков

**Решение:**
1. Проверьте логи на сервере - должно быть сообщение об отправке списка
2. Проверьте логи на клиенте - должно быть сообщение о получении списка
3. Убедитесь, что `LobbyPlayerSync` заспавнен на сервере (`netId.netId != 0`)
4. Проверьте, что `TargetRpc` вызывается с правильным `NetworkConnection`

#### Проблема: Обновления данных игрока не синхронизируются

**Решение:**
1. Убедитесь, что `UpdatePlayerDataAndSync()` вызывается на сервере
2. Проверьте, что `playerSync.BroadcastPlayerUpdate()` вызывается
3. Проверьте логи - должно быть сообщение о вызове `ClientRpc`

### 5. Структура данных

`PlayerLobbyData` структура содержит:
- `connectionId` - ID подключения игрока
- `playerName` - Имя игрока (из Steam или PlayerPrefs)
- `playerColor` - Цвет игрока
- `isAdmin` - Статус админа (хост или нет)

### 6. Порядок работы

1. **Сервер запускается:**
   - `LobbyManager.OnMirrorServerStarted()` вызывается
   - `LobbyPlayerSync` спавнится на сервере
   - Хост создает `PlayerLobbyItem` для себя

2. **Клиент подключается:**
   - `LobbyManager.OnMirrorClientConnected()` вызывается на сервере
   - Сервер создает `PlayerLobbyItem` для нового клиента
   - Сервер отправляет список всех игроков новому клиенту через `TargetRpc`
   - Клиент получает список и создает `PlayerLobbyItem` для всех игроков

3. **Обновление данных игрока:**
   - Сервер обновляет локальный `PlayerLobbyItem`
   - Сервер отправляет обновление всем клиентам через `ClientRpc`
   - Клиенты обновляют свои `PlayerLobbyItem`

4. **Отключение игрока:**
   - Сервер удаляет локальный `PlayerLobbyItem`
   - Сервер отправляет уведомление всем клиентам через `ClientRpc`
   - Клиенты удаляют соответствующий `PlayerLobbyItem`

## Проверочный список

- [ ] GameObject с `LobbyManager` имеет `NetworkIdentity`
- [ ] Компонент `LobbyPlayerSync` добавлен на GameObject с `LobbyManager`
- [ ] В логах появляется сообщение о спавне `LobbyPlayerSync` на сервере
- [ ] При подключении клиента сервер отправляет список игроков
- [ ] Клиент получает список игроков и создает `PlayerLobbyItem`
- [ ] Обновления данных игрока синхронизируются между всеми клиентами
- [ ] При отключении игрока его `PlayerLobbyItem` удаляется у всех клиентов

## Дополнительные настройки

Если автоматическая настройка не работает, можно вручную:

1. **Создать префаб LobbyManager:**
   - Создайте префаб GameObject с компонентами:
     - `LobbyManager`
     - `LobbyPlayerSync`
     - `NetworkIdentity` (Server Only = false)
   - Добавьте этот префаб в `MirrorNetworkManager.spawnPrefabs`

2. **Спавнить вручную:**
   - В `MirrorNetworkManager.OnStartServer()` добавьте спавн `LobbyPlayerSync`

## Важные замечания

- `LobbyPlayerSync` должен быть на том же GameObject, что и `LobbyManager`
- `NetworkIdentity` должен быть установлен (`Server Only = false`)
- Объект должен быть заспавнен на сервере (`NetworkServer.Spawn()`)
- `TargetRpc` и `ClientRpc` работают только если объект заспавнен

