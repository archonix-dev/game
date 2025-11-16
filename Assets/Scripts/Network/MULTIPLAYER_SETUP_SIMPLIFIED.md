# Упрощенная настройка мультиплеера

## Обзор системы

Проект использует **Mirror Networking** с **FizzySteamworks** транспортом для мультиплеера через Steam.

## Основной поток работы

### 1. Создание лобби (Хост)

1. В сцене `Menu` игрок нажимает на 2-й элемент (индекс 1) массива `objectTargets` в `CameraMovementController`
2. Вызывается `CreateSteamLobby()` → создается Steam лобби
3. После создания Steam лобби автоматически создается Mirror лобби через `LobbyManager.CreateLobby()`
4. Сервер запускается через `MirrorNetworkManager.StartHostGame()`
5. Создается `LobbyNetworkManager` (NetworkIdentity) для синхронизации данных
6. Создается `PlayerLobbyItem` для хоста с `adminIndicator` = true

### 2. Подключение к лобби (Клиент)

1. Игрок нажимает кнопку "Подключиться к другому лобби" в `LobbyManager`
2. Открывается Steam оверлей (`SteamFriends.ActivateGameOverlay("friends")`)
3. Игрок выбирает друга или принимает приглашение
4. Подключается к Steam лобби → автоматически подключается к Mirror лобби
5. Сервер отправляет клиенту список всех игроков через `LobbyNetworkManager`
6. Клиент создает локальные `PlayerLobbyItem` для всех игроков

### 3. Синхронизация данных

- **Никнеймы**: Получаются из Steam API, синхронизируются через `LobbyNetworkManager.SendPlayerSteamNameCommand()`
- **Цвета**: Выбираются в `ColorSelectionPanel`, синхронизируются через `LobbyNetworkManager.RequestPlayerColorUpdateCommand()`
- **Пинг**: У хоста всегда 5, у других игроков фиксированное значение (можно заменить на реальный RTT)

### 4. Переход в игру

1. Хост нажимает кнопку "Начать игру" (`startGameButton`)
2. Вызывается `NetworkManager.ServerChangeScene(gameSceneName)` для синхронизированной загрузки сцены
3. Все игроки переходят в сцену `Lobby` (или другую указанную в `gameSceneName`)

## Настройка компонентов

### CameraMovementController

**Настройки:**
- `lobbyCreationIndex = 1` (2-й элемент массива, индекс начинается с 0)
- `connectMenuIndex = 1` (индекс меню подключения)
- `objectTargets[]` - массив пар объект-точка подлета

**Логика:**
- При клике на объект с индексом `lobbyCreationIndex` создается лобби
- При получении приглашения открывается меню подключения через `OpenConnectMenu()`

### LobbyManager

**Настройки:**
- `playersListContainer` - Transform контейнер для списка игроков
- `playerLobbyPrefab` - префаб `PlayerLobbyItem`
- `lobbySettingsButton` - кнопка настроек (видна только хосту)
- `startGameButton` - кнопка начала игры (видна только хосту)
- `connectToLobbyButton` - кнопка подключения (видна всем)
- `gameSceneName = "Lobby"` - имя сцены для загрузки

**Логика:**
- Создает `PlayerLobbyItem` для каждого игрока
- Синхронизирует данные через `LobbyNetworkManager`
- Управляет видимостью кнопок (только хост видит `lobbySettingsButton` и `startGameButton`)

### PlayerLobbyItem

**Настройки:**
- `playerNameText` - Text для отображения никнейма из Steam
- `pingText` - Text для отображения пинга (у хоста всегда 5)
- `colorPlayerImages[]` - массив Image для отображения цвета игрока
- `adminIndicator` - GameObject индикатора админа (показывается только хосту)

**Логика:**
- Отображает никнейм из Steam
- Отображает пинг (хост = 5, другие = фиксированное значение)
- Применяет цвет к `colorPlayerImages`
- Показывает `adminIndicator` если `isAdmin = true`

### ColorSelectionPanel

**Настройки:**
- Кнопки для выбора цветов (красный, синий, белый, зеленый, фиолетовый, розовый, голубой, салатовый)
- Цвета настраиваются в инспекторе

**Логика:**
- При выборе цвета сохраняется в `PlayerPrefs`
- Цвет применяется локально к `PlayerLobbyItem`
- Цвет синхронизируется всем игрокам через `LobbyNetworkManager.RequestPlayerColorUpdateCommand()`

### LobbyNetworkManager

**Настройки:**
- Должен быть добавлен в сцену `Menu` как GameObject с компонентами:
  - `LobbyNetworkManager`
  - `NetworkIdentity` (Server Only = false для синхронизации с клиентами)

**Логика:**
- Синхронизирует данные лобби (макс игроков, пароль, читы)
- Отправляет данные игроков через `ClientRpc` и `TargetRpc`
- Обрабатывает команды от клиентов (`SendPlayerSteamNameCommand`, `RequestPlayerColorUpdateCommand`)

### SteamLobbyManager

**Настройки:**
- `maxLobbyMembers = 8` - максимальное количество игроков
- `lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly` - тип лобби

**Логика:**
- Создает Steam лобби через `SteamMatchmaking.CreateLobby()`
- Обрабатывает подключения через Steam оверлей
- Автоматически создает/подключается к Mirror лобби после Steam лобби

## Важные моменты

1. **Пинг хоста**: Всегда равен 5 (не рандомное значение)
2. **Кнопки хоста**: `lobbySettingsButton` и `startGameButton` видны только хосту
3. **Steam оверлей**: При подключении открывается встроенный Steam оверлей, а не дополнительная панель
4. **Синхронизация**: Все данные (никнеймы, цвета) синхронизируются автоматически через `LobbyNetworkManager`
5. **Модель игрока**: Владелец не видит свою модель, другие игроки видят
6. **Голос и чат**: Владелец не слышит себя, другие игроки слышат

## Порядок настройки в Unity

1. **Сцена Menu:**
   - Добавьте `MirrorNetworkManager` с компонентом `FizzySteamworks`
   - Добавьте `LobbyNetworkManager` GameObject с `NetworkIdentity`
   - Добавьте `LobbyManager` и настройте все ссылки
   - Добавьте `CameraMovementController` и настройте `objectTargets[]`
   - Настройте `SteamLobbyManager`

2. **Префаб PlayerLobbyItem:**
   - Создайте UI элемент с компонентом `PlayerLobbyItem`
   - Настройте ссылки на `playerNameText`, `pingText`, `colorPlayerImages[]`, `adminIndicator`

3. **Сцена Lobby:**
   - Убедитесь, что префаб игрока имеет компоненты `NetworkPlayer`, `PlayerController`, `VoiceWaveVisualizer`, `ChatSystem`
   - Настройте видимость модели игрока в `NetworkPlayer.playerModelObjects[]`

## Проверка работы

1. Запустите игру и нажмите на 2-й элемент в `objectTargets`
2. Проверьте, что создалось Steam лобби и Mirror сервер запустился
3. Проверьте, что появился `PlayerLobbyItem` для хоста с `adminIndicator`
4. Проверьте, что кнопки `lobbySettingsButton` и `startGameButton` видны только хосту
5. Подключите второго игрока через Steam оверлей
6. Проверьте, что у обоих игроков синхронизированы никнеймы и цвета
7. Проверьте, что пинг хоста = 5

