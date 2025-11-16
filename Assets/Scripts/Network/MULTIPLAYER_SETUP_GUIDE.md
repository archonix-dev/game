# Руководство по настройке мультиплеера

## Обзор системы

Мультиплеер использует:
- **Mirror Networking** для сетевой синхронизации
- **FizzySteamworks** транспорт для подключения через Steam P2P
- **Steam Lobby API** для создания и поиска лобби
- **Steam Friends API** для приглашений друзей

## Поток работы мультиплеера

### Создание лобби (Хост)
1. Игрок нажимает на 2-й объект в массиве `objectTargets` в `CameraMovementController`
2. Вызывается `CameraMovementController.CreateSteamLobby()`
3. `SteamLobbyManager.CreateLobby()` создает Steam лобби
4. При успешном создании вызывается `LobbyManager.CreateLobby()`
5. `MirrorNetworkManager.StartHostGame()` запускает хост
6. Создается `LobbyNetworkManager` для синхронизации данных лобби
7. Создается `PlayerLobbyItem` для хоста в `playersListContainer`

### Подключение друга (Клиент)
1. Друг получает приглашение через Steam оверлей
2. При принятии приглашения вызывается `SteamLobbyManager.OnGameLobbyJoinRequestedCallback()`
3. `LobbyManager.ConnectToLobby()` подключается к серверу через Steam ID
4. `MirrorNetworkManager.ConnectToSteamId()` устанавливает соединение
5. При подключении создается `PlayerLobbyItem` для клиента
6. Клиент отправляет свое Steam имя и цвет серверу через `LobbyNetworkManager.SendPlayerSteamNameCommand()`

### Начало игры
1. Хост нажимает кнопку "Начать игру" (`LobbyManager.startGameButton`)
2. `LobbyManager.OnStartGameButtonClicked()` вызывает `NetworkManager.ServerChangeScene("Lobby")`
3. Все клиенты синхронно загружают сцену "Lobby"
4. `GameManager.OnStartServer()` спавнит всех игроков через `SpawnAllPlayersAfterSceneLoad()`

## Настройка сцены Menu

### Обязательные компоненты

#### 1. MirrorNetworkManager (GameObject: "NetworkManager")
- **Компоненты:**
  - `MirrorNetworkManager` (скрипт)
  - `FizzySteamworks` (транспорт)
  - `NetworkManagerHUD` (опционально, для отладки)
- **Настройки MirrorNetworkManager:**
  - `steamAppId`: Ваш Steam App ID (по умолчанию 480 для тестирования)
  - `maxPlayers`: Максимальное количество игроков (по умолчанию 8)
  - `offlineScene`: "Menu" (или пусто, если не хотите автоматическую перезагрузку)
  - `onlineScene`: Пусто (устанавливается динамически при нажатии "Начать игру")
- **Настройки FizzySteamworks:**
  - `Steam App ID`: Должен совпадать с `steamAppId` в MirrorNetworkManager
- **Важно:** `DontDestroyOnLoad` устанавливается автоматически в `Awake()` (не нужно настраивать вручную)

#### 2. SteamManager (GameObject: "SteamManager")
- **Компоненты:**
  - `SteamManager` (скрипт)
- **Настройки:**
  - `steamAppId`: Ваш Steam App ID
  - `autoInitialize`: true
- **Важно:** `DontDestroyOnLoad` устанавливается автоматически в `Awake()` (не нужно настраивать вручную)

#### 3. SteamInitializer (GameObject: "SteamInitializer")
- **Компоненты:**
  - `SteamInitializer` (скрипт)
- **Настройки:**
  - Должен инициализировать Steam перед запуском игры
- **Важно:** Должен запускаться первым (проверьте порядок выполнения в Project Settings)

#### 4. SteamLobbyManager (GameObject: "SteamLobbyManager")
- **Компоненты:**
  - `SteamLobbyManager` (скрипт)
- **Настройки:**
  - `maxLobbyMembers`: Максимальное количество игроков (по умолчанию 8)
  - `lobbyType`: Тип лобби (FriendsOnly, Public, Private, Invisible)

#### 5. LobbyManager (GameObject: "LobbyManager")
- **Компоненты:**
  - `LobbyManager` (скрипт)
- **Настройки:**
  - `playButton`: Кнопка "Играть" (создает лобби)
  - `lobbySettingsButton`: Кнопка "Настройки лобби" (только для хоста)
  - `startGameButton`: Кнопка "Начать игру" (только для хоста)
  - `connectToLobbyButton`: Кнопка "Подключиться к другому лобби"
  - `colorSelectionButton`: Кнопка "Выбор цвета"
  - `lobbySettingsPanel`: Панель настроек лобби
  - `connectToLobbyPanel`: Панель подключения к лобби
  - `colorSelectionPanel`: Панель выбора цвета
  - `playersListContainer`: Transform контейнер для списка игроков (обычно VerticalLayoutGroup или GridLayoutGroup)
  - `playerLobbyPrefab`: Префаб `PlayerLobbyItem` для отображения игрока в лобби
  - `defaultPort`: Порт по умолчанию (не используется для FizzySteamworks)
  - `maxPlayers`: Максимальное количество игроков
  - `gameSceneName`: "Lobby" (имя сцены для загрузки при нажатии "Начать игру")

#### 6. CameraMovementController (GameObject: "CameraMovementController")
- **Компоненты:**
  - `CameraMovementController` (скрипт)
- **Настройки:**
  - `targetCamera`: Главная камера
  - `initialCameraPosition`: Начальная позиция камеры
  - `objectTargets`: Массив пар объект-точка подлета
    - **Элемент 0:** Первый объект (любой)
    - **Элемент 1 (lobbyCreationIndex):** Объект для создания лобби
      - `hoverObject`: Объект с `ObjectHoverEffect`
      - `targetPoint`: Точка подлета камеры
      - `objectCanvas`: Canvas с UI лобби
      - `objectToHide`: Объект, который скрывается при клике
  - `lobbyCreationIndex`: 1 (индекс элемента для создания лобби)
  - `connectMenuIndex`: 1 (индекс элемента для меню подключения)
  - `lobbyManager`: Ссылка на `LobbyManager`
  - `escapeButtons`: Массив кнопок для возврата камеры (ESC)

### UI Элементы

#### Префаб PlayerLobbyItem
- **Компоненты:**
  - `PlayerLobbyItem` (скрипт)
  - UI элементы:
    - `playerNameText`: Text с именем игрока
    - `pingText`: Text с пингом
    - `colorPlayerImages`: Массив Image для отображения цвета игрока
    - `adminIndicator`: GameObject для отображения статуса админа
- **Важно:** Префаб НЕ должен иметь `NetworkIdentity` (создается локально на каждом клиенте)

#### Панель выбора цвета (ColorSelectionPanel)
- Должна иметь компонент `ColorSelectionPanel` (скрипт)
- Позволяет игроку выбрать цвет для отображения в лобби

#### Панель настроек лобби (LobbySettingsPanel)
- Должна иметь компонент `LobbySettingsPanel` (скрипт)
- Позволяет хосту настроить параметры лобби (пароль, максимальное количество игроков и т.д.)

#### Панель подключения к лобби (ConnectToLobbyPanel)
- Должна иметь компонент `ConnectToLobbyPanel` (скрипт)
- Отображает статус подключения

## Настройка сцены Lobby

### Обязательные компоненты

#### 1. MirrorNetworkManager
- **Важно:** Должен быть настроен как `DontDestroyOnLoad` (сохраняется из сцены Menu)
- **Настройки:**
  - `playerPrefab`: Префаб игрока для спавна
  - `spawnPrefabs`: Массив префабов для спавна (должен включать `playerPrefab`)

#### 2. GameManager (GameObject: "GameManager")
- **Компоненты:**
  - `GameManager` (скрипт)
  - `NetworkIdentity` (обязательно!)
- **Настройки:**
  - `gameSceneName`: "Test" (не используется, оставьте по умолчанию)
  - `menuSceneName`: "Menu"
  - `currentGameSceneName`: "Lobby"
  - `playerPrefab`: Префаб игрока (должен совпадать с `MirrorNetworkManager.playerPrefab`)
  - `spawnPoint`: Transform точки спавна игроков
- **Важно:** GameObject должен быть заспавнен на сервере (`NetworkServer.Spawn()`)

#### 3. Player Prefab
- **Компоненты:**
  - `NetworkIdentity` (обязательно!)
  - `NetworkTransform` (для синхронизации позиции)
  - `NetworkPlayer` (скрипт для управления игроком)
  - Другие компоненты игрока (PlayerController, PlayerHealthStamina и т.д.)
- **Настройки NetworkIdentity:**
  - `Server Only`: false
  - `Local Player Authority`: true (для управления локальным игроком)

### Порядок инициализации на сцене Lobby

1. Сцена загружается синхронно для всех клиентов через `NetworkManager.ServerChangeScene()`
2. `GameManager.OnStartServer()` вызывается на сервере
3. `GameManager.SpawnAllPlayersAfterSceneLoad()` спавнит всех подключенных игроков
4. Каждый клиент получает своего игрока через `NetworkServer.Spawn()`
5. Локальный игрок управляется через `NetworkIdentity.localPlayerAuthority`

## Проверка настройки

### Сцена Menu

✅ **Проверьте наличие всех компонентов:**
- [ ] MirrorNetworkManager с FizzySteamworks транспортом
- [ ] SteamManager
- [ ] SteamInitializer
- [ ] SteamLobbyManager
- [ ] LobbyManager (со всеми назначенными кнопками и панелями)
- [ ] CameraMovementController (с настроенным массивом objectTargets)
- [ ] Префаб PlayerLobbyItem
- [ ] UI панели (ColorSelectionPanel, LobbySettingsPanel, ConnectToLobbyPanel)

✅ **Проверьте настройки:**
- [ ] `LobbyManager.gameSceneName` = "Lobby"
- [ ] `CameraMovementController.lobbyCreationIndex` = 1
- [ ] `CameraMovementController.lobbyManager` назначен
- [ ] Все кнопки в `LobbyManager` назначены
- [ ] `playersListContainer` назначен и настроен (Layout Group)
- [ ] `playerLobbyPrefab` назначен

### Сцена Lobby

✅ **Проверьте наличие всех компонентов:**
- [ ] GameManager с NetworkIdentity
- [ ] Player Prefab в MirrorNetworkManager.spawnPrefabs
- [ ] Spawn Point назначен в GameManager

✅ **Проверьте настройки:**
- [ ] `GameManager.currentGameSceneName` = "Lobby"
- [ ] `GameManager.playerPrefab` совпадает с `MirrorNetworkManager.playerPrefab`
- [ ] `GameManager.spawnPoint` назначен

## Известные проблемы и решения

### Проблема: "Scene change is already in progress"
**Причина:** Двойная загрузка сцены (LobbyManager и LoadMansionScene вызывают ServerChangeScene одновременно)
**Решение:** Исправлено - добавлена проверка в `LoadMansionScene` и `LobbyManager` для предотвращения дублирующих вызовов

### Проблема: "Steamworks is not initialized" при закрытии
**Причина:** Steam завершается до того, как транспорт успевает закрыть сокеты
**Решение:** Исправлено - добавлена проверка и правильный порядок остановки (сначала сеть, затем Steam)

### Проблема: Игроки не спавнятся на сцене Lobby
**Причина:** GameManager не заспавнен или playerPrefab не назначен
**Решение:** Убедитесь, что GameManager имеет NetworkIdentity и заспавнен на сервере, а playerPrefab назначен в GameManager и MirrorNetworkManager

### Проблема: PlayerLobbyItem не отображается
**Причина:** playersListContainer не назначен или неактивен
**Решение:** Убедитесь, что playersListContainer назначен в LobbyManager и активен в иерархии

## Тестирование

### Локальное тестирование (один компьютер)
1. Запустите игру в редакторе Unity
2. Нажмите на 2-й объект в objectTargets для создания лобби
3. Проверьте, что в playersListContainer появился PlayerLobbyItem для хоста
4. Нажмите "Начать игру"
5. Проверьте, что сцена Lobby загрузилась и игрок заспавнился

### Тестирование с другом
1. Запустите игру на двух компьютерах
2. На первом компьютере создайте лобби (нажмите на 2-й объект)
3. На втором компьютере нажмите "Подключиться к другому лобби" и выберите друга через Steam оверлей
4. Проверьте, что оба игрока видят друг друга в playersListContainer
5. Хост нажимает "Начать игру"
6. Проверьте, что оба игрока загрузили сцену Lobby и заспавнились

## Дополнительные заметки

- **DontDestroyOnLoad:** MirrorNetworkManager и SteamManager должны сохраняться между сценами (LobbyManager больше не использует DontDestroyOnLoad)
- **Singleton Pattern:** Все менеджеры используют Singleton pattern для предотвращения дубликатов
- **Steam App ID:** Убедитесь, что используете правильный Steam App ID для вашей игры
- **Порядок инициализации:** SteamInitializer должен инициализировать Steam перед запуском других компонентов
- **NetworkIdentity:** Все объекты, которые должны синхронизироваться, должны иметь NetworkIdentity
- **Local Player Authority:** Player Prefab должен иметь Local Player Authority для управления локальным игроком

