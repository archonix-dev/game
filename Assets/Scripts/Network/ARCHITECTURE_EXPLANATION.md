# Объяснение архитектуры мультиплеера

## Префабы и их назначение

### 1. **LobbyPlayer** (НОВЫЙ префаб - нужно создать!)
**Назначение:** Легковесный объект для синхронизации данных игрока на сцене Menu

**Что содержит:**
- Компонент `NetworkIdentity` (обязательно!)
- Компонент `LobbyPlayer` (NetworkBehaviour)

**Когда спавнится:**
- Автоматически при создании/присоединении к лобби на сцене Menu
- Спавнится через `NetworkManager.playerPrefab` (когда `autoCreatePlayer = true`)

**Где используется:**
- На сцене **Menu** для синхронизации данных игрока (имя, цвет, Steam ID, пинг)
- UI элемент `playerlist` читает данные из `LobbyPlayer` и отображает их

**Как создать:**
1. Создайте пустой GameObject в сцене
2. Назовите его "LobbyPlayer"
3. Добавьте компонент `NetworkIdentity`
4. Добавьте компонент `LobbyPlayer` (скрипт)
5. Настройте `NetworkIdentity`:
   - Server Only: **false**
   - Visibility: **Default**
   - Spawn On Start: **false**
6. Сохраните как префаб (перетащите в папку Prefabs)
7. **Назначьте этот префаб в `LobbyNetworkManager.playerPrefab`**

---

### 2. **Player** (ваш существующий игровой префаб)
**Назначение:** Полноценный игровой объект для сцены Lobby

**Что содержит:**
- Контроллер игрока
- Камера
- Модель игрока
- Все игровые компоненты
- Компонент `NetworkIdentity` (обязательно!)

**Когда спавнится:**
- На сцене **Lobby** через `LobbyPlayerSpawner.SpawnPlayer()`
- НЕ спавнится на сцене Menu!

**Где используется:**
- В игре на сцене Lobby
- Это объект, которым игрок управляет

**Настройка:**
- Назначьте в `LobbyPlayerSpawner.playerPrefab` (НЕ в NetworkManager!)

---

### 3. **playerlist** (UI префаб)
**Назначение:** UI элемент для отображения игрока в списке лобби

**Что содержит:**
- UI элементы (Text, Image и т.д.)
- Компонент `PlayerListUI` (скрипт)

**Когда спавнится:**
- Динамически в `LobbyManager.playerListParent` при обновлении списка
- Создается для каждого `LobbyPlayer` в лобби

**Где используется:**
- На сцене Menu в списке игроков лобби
- Отображает данные из `LobbyPlayer` (имя, цвет, пинг и т.д.)

---

### 4. **lobbylist** (UI префаб)
**Назначение:** UI элемент для отображения лобби в списке

**Что содержит:**
- UI элементы (Text, Image, InputField, Button)
- Компонент `LobbyListUI` (скрипт)

**Когда спавнится:**
- Динамически в `LobbyManager.lobbyListParent` при поиске лобби
- Создается для каждого найденного лобби друзей

**Где используется:**
- На сцене Menu в панели "Join Other Lobby"

---

## Как это работает вместе

### Сцена Menu:
1. Игрок создает/присоединяется к лобби
2. `NetworkManager` автоматически спавнит `LobbyPlayer` (если `autoCreatePlayer = true`)
3. `LobbyPlayer` синхронизирует данные игрока через Mirror
4. `LobbyManager.UpdatePlayerList()` находит все `LobbyPlayer` в сцене
5. Для каждого `LobbyPlayer` создается UI элемент `playerlist`
6. `PlayerListUI.SetupPlayer()` заполняет UI данными из `LobbyPlayer`

### Сцена Lobby:
1. Игрок нажимает "Start Game"
2. Все игроки переходят на сцену Lobby
3. `LobbyPlayerSpawner` спавнит `Player` (игровой префаб) для каждого подключения
4. Игроки могут играть

---

## Важно!

**`LobbyNetworkManager.playerPrefab` должен быть префабом `LobbyPlayer` (легковесный), НЕ `Player` (игровой)!**

**`LobbyPlayerSpawner.playerPrefab` должен быть префабом `Player` (игровой)!**

Это два разных префаба для разных сцен и разных целей!

