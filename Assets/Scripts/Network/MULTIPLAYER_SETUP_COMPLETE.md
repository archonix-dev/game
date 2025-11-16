# Полное руководство по настройке мультиплеера

## Обзор системы

Система мультиплеера использует:
- **Mirror Networking** - для сетевой синхронизации
- **Steam Networking** (FizzySteamworks) - для подключения через Steam
- **Steam API** - для получения никнеймов и управления лобби

## Содержание

1. [Настройка сцены Menu](#настройка-сцены-menu)
2. [Настройка сцены Lobby](#настройка-сцены-lobby)
3. [Настройка префабов](#настройка-префабов)
4. [Настройка UI элементов](#настройка-ui-элементов)
5. [Настройка CameraMovementController](#настройка-cameramovementcontroller)
6. [Проверка и тестирование](#проверка-и-тестирование)

---

## Настройка сцены Menu

### 1. Создание основных объектов

#### 1.1. MirrorNetworkManager
**Путь:** Создайте пустой GameObject с именем `MirrorNetworkManager`

**Компоненты:**
- `MirrorNetworkManager` (скрипт)
- `FizzySteamworks` (транспорт для Steam)

**Настройки MirrorNetworkManager:**
- `offlineScene`: `Menu`
- `onlineScene`: `Lobby` (или оставьте пустым, если переход будет через кнопку)
- `playerPrefab`: Префаб игрока (см. раздел "Настройка префабов")
- `maxConnections`: `8` (или нужное количество игроков)

**Настройки FizzySteamworks:**
- Оставьте настройки по умолчанию
- Транспорт автоматически использует Steam для подключения

#### 1.2. SteamManager
**Путь:** Создайте пустой GameObject с именем `SteamManager`

**Компоненты:**
- `SteamManager` (скрипт)

**Настройки:**
- `steamAppId`: Ваш Steam App ID (например, `480` для тестирования)
- `autoInitialize`: `true`

**Важно:** Убедитесь, что файл `steam_appid.txt` существует в корне проекта с вашим App ID

#### 1.3. SteamLobbyManager
**Путь:** Создайте пустой GameObject с именем `SteamLobbyManager`

**Компоненты:**
- `SteamLobbyManager` (скрипт)

**Настройки:** Не требуются (все настройки по умолчанию)

#### 1.4. LobbyManager
**Путь:** Создайте пустой GameObject с именем `LobbyManager`

**Компоненты:**
- `LobbyManager` (скрипт)

**Настройки LobbyManager:**

**Кнопки:**
- `lobbySettingsButton`: Кнопка "Настройки лобби" (видна только хосту)
- `startGameButton`: Кнопка "Начать игру" (видна только хосту)
- `connectToLobbyButton`: Кнопка "Подключиться к лобби"
- `colorSelectionButton`: Кнопка для открытия панели выбора цвета

**UI Панели:**
- `lobbySettingsPanel`: GameObject панели настроек лобби
- `connectToLobbyPanel`: GameObject панели подключения к лобби
- `colorSelectionPanel`: GameObject панели выбора цвета

**Отображение игроков:**
- `playersListContainer`: Transform контейнера для списка игроков (обычно VerticalLayoutGroup или GridLayoutGroup)
- `playerLobbyPrefab`: Префаб элемента игрока в лобби (см. раздел "Настройка префабов")

**Настройки сети:**
- `defaultPort`: `7777` (не используется для Steam, но можно оставить)
- `maxPlayers`: `8` (максимальное количество игроков)

**Загрузка сцены:**
- `sceneLoader`: Компонент `AsyncSceneLoaderWithAnimation` (опционально)
- `gameSceneName`: `Lobby` (имя сцены для игры)

#### 1.5. ColorSelectionPanel
**Путь:** Создайте UI панель с именем `ColorSelectionPanel`

**Компоненты:**
- `ColorSelectionPanel` (скрипт)

**Настройки:**

**Кнопки цветов:**
- `redColorButton`: Кнопка красного цвета
- `blueColorButton`: Кнопка синего цвета
- `whiteColorButton`: Кнопка белого цвета
- `greenColorButton`: Кнопка зеленого цвета
- `violetColorButton`: Кнопка фиолетового цвета
- `pinkColorButton`: Кнопка розового цвета
- `lightBlueColorButton`: Кнопка голубого цвета
- `limeColorButton`: Кнопка салатового цвета

**Настройки цветов:**
- `redColor`: `Color.red`
- `blueColor`: `Color.blue`
- `whiteColor`: `Color.white`
- `greenColor`: `Color.green`
- `violetColor`: `new Color(0.5f, 0f, 1f, 1f)`
- `pinkColor`: `new Color(1f, 0.75f, 0.8f, 1f)`
- `lightBlueColor`: `new Color(0.5f, 0.8f, 1f, 1f)`
- `limeColor`: `new Color(0.5f, 1f, 0f, 1f)`

**Важно:** Панель должна быть неактивна по умолчанию (`SetActive(false)`)

#### 1.6. ConnectToLobbyPanel
**Путь:** Создайте UI панель с именем `ConnectToLobbyPanel`

**Компоненты:**
- `ConnectToLobbyPanel` (скрипт)

**Настройки:**

**UI Элементы:**
- `joinFriendButton`: Кнопка "Присоединиться к другу"
- `backButton`: Кнопка "Назад"
- `statusText`: Текст для отображения статуса подключения (опционально)

**Важно:** Панель должна быть неактивна по умолчанию (`SetActive(false)`)

#### 1.7. LobbySettingsPanel
**Путь:** Создайте UI панель с именем `LobbySettingsPanel`

**Компоненты:**
- `LobbySettingsPanel` (скрипт)

**Настройки:**

**UI Элементы:**
- `maxPlayersInput`: InputField для максимального количества игроков
- `cheatsToggle`: Toggle для включения/выключения читов
- `passwordInput`: InputField для пароля лобби
- `ipAddressText`: Text для отображения IP адреса сервера
- `copyIpButton`: Кнопка для копирования IP адреса
- `resetButton`: Кнопка "Сбросить"
- `applyButton`: Кнопка "Применить"
- `closeButton`: Кнопка закрытия панели (опционально)

**Настройки:**
- `defaultMaxPlayers`: `8`

**Важно:** Панель должна быть неактивна по умолчанию (`SetActive(false)`)

#### 1.8. CameraMovementController
**Путь:** Найдите объект с камерой или создайте пустой GameObject с именем `CameraMovementController`

**Компоненты:**
- `CameraMovementController` (скрипт)

**Настройки:**

**Camera Settings:**
- `targetCamera`: Главная камера (если не указана, используется `Camera.main`)
- `initialCameraPosition`: Transform начальной позиции камеры

**Object Targets:**
- `objectTargets`: Массив пар объектов и точек подлета
  - **Элемент с индексом 0**: Первый объект (любой)
  - **Элемент с индексом 1**: Второй объект (любой)
  - **Элемент с индексом 2**: **Третий объект - для создания лобби** ⚠️ ВАЖНО

**Network Settings:**
- `lobbyManager`: Ссылка на `LobbyManager` компонент
- `lobbyCreationIndex`: `2` (индекс элемента для создания лобби)
- `connectMenuIndex`: `1` (индекс элемента для меню подключения)

**Movement Settings:**
- `movementSpeed`: `2f` (скорость движения камеры)
- `rotationSpeed`: `2f` (скорость поворота камеры)

**UI Buttons:**
- `escapeButtons`: Массив кнопок для возврата (ESC функционал)

**Важно:** 
- Элемент `objectTargets[2]` должен иметь:
  - `hoverObject`: GameObject с компонентом `ObjectHoverEffect`
  - `targetPoint`: Transform точки подлета камеры
  - `objectCanvas`: Canvas для отображения UI (опционально)
  - `objectToHide`: GameObject, который скрывается при клике (опционально)

---

## Настройка сцены Lobby

### 1. Создание основных объектов

#### 1.1. LobbyManager (если не переносится из Menu)
Если `LobbyManager` настроен с `DontDestroyOnLoad`, он автоматически перенесется из Menu.
Если нет, создайте его так же, как в сцене Menu.

#### 1.2. Spawn Points для игроков
**Путь:** Создайте пустые GameObject с именем `SpawnPoints` (родитель)

**Создайте дочерние объекты:**
- `SpawnPoint_1`, `SpawnPoint_2`, `SpawnPoint_3`, и т.д.

**Настройки:**
- Каждый SpawnPoint должен иметь Transform с позицией и поворотом
- Добавьте их в `MirrorNetworkManager.playerSpawnPositions` (массив Transform)

**Важно:** SpawnPoints должны быть в сцене Lobby, а не в Menu

---

## Настройка префабов

### 1. Префаб игрока (Player)

**Путь:** `Assets/Resources/Player.prefab` (или путь, указанный в MirrorNetworkManager)

**Компоненты:**

#### NetworkIdentity
- `Server Only`: `false`
- `Local Player Authority`: `true`

#### NetworkPlayer
- `playerController`: Ссылка на `PlayerController` компонент
- `networkTransform`: Ссылка на `ClientNetworkTransform` компонент
- `playerCamera`: Ссылка на камеру игрока
- `audioListener`: Ссылка на AudioListener
- `playerModelObjects`: Массив GameObject модели игрока (для скрытия у владельца)
- `lobbySceneName`: `Lobby`

#### PlayerController
- Настройки движения игрока

#### ClientNetworkTransform
- Для синхронизации позиции и поворота

#### VoiceWaveVisualizer
- Для визуализации голоса
- Настройки микрофона и визуализации

#### ChatSystem
- `chatRoot`: GameObject корня чата
- `chatInputField`: InputField для ввода сообщений
- `playerController`: Ссылка на PlayerController
- `mouseLook`: Ссылка на MouseLook
- `audioSource`: AudioSource для звуков набора
- `letterSounds`: Массив звуков для букв A-Z (26 звуков)
- `russianLetterSounds`: Массив звуков для русских букв (31 звук)
- `numberSounds`: Массив звуков для цифр 0-9 (10 звуков)
- `russianNumberSounds`: Массив звуков для русских цифр 0-9 (10 звуков)
- `defaultSymbolSound`: Звук для знаков препинания
- `spaceDelay`: `0.1f` (задержка для пробелов)
- `messageSpawnParent`: Transform для спавна сообщений чата
- `chatMessagePrefab`: Префаб сообщения чата

**Важно:**
- Модель игрока должна быть в `playerModelObjects` для скрытия у владельца
- Камера и AudioListener должны быть дочерними объектами игрока

### 2. Префаб элемента игрока в лобби (PlayerLobbyItem)

**Путь:** Префаб, который будет назначен в `LobbyManager.playerLobbyPrefab`

**Структура UI:**
```
PlayerLobbyItem (GameObject)
├── PlayerNameText (Text) - имя игрока
├── PingText (Text) - пинг игрока
├── ColorPlayerImage_1 (Image) - изображение цвета игрока
├── ColorPlayerImage_2 (Image) - изображение цвета игрока (опционально)
├── AdminIndicator (GameObject) - индикатор админа (неактивен по умолчанию)
```

**Компоненты:**
- `PlayerLobbyItem` (скрипт)

**Настройки PlayerLobbyItem:**
- `playerNameText`: Ссылка на Text компонент с именем
- `pingText`: Ссылка на Text компонент с пингом
- `colorPlayerImages`: Массив Image компонентов для цвета (минимум 1)
- `adminIndicator`: GameObject индикатора админа

**Важно:**
- `adminIndicator` должен быть неактивен по умолчанию
- `colorPlayerImages` должны быть настроены для отображения цвета игрока

### 3. Префаб сообщения чата (ChatMessageItem)

**Путь:** Префаб, который будет назначен в `ChatSystem.chatMessagePrefab`

**Структура UI:**
```
ChatMessageItem (GameObject)
├── PlayerNameText (Text) - имя отправителя
├── MessageText (Text) - текст сообщения
├── AdminIndicator (GameObject) - индикатор админа (опционально)
└── ColorIndicator (Image) - индикатор цвета игрока (опционально)
```

**Компоненты:**
- `ChatMessageItem` (скрипт) - должен иметь метод `Initialize(string message, string playerName, Color playerColor, uint senderId, bool isAdmin)`

---

## Настройка UI элементов

### 1. Canvas для Menu сцены

**Структура:**
```
Canvas (Menu)
├── LobbyManager UI
│   ├── LobbySettingsButton (Button) - виден только хосту
│   ├── StartGameButton (Button) - виден только хосту
│   ├── ConnectToLobbyButton (Button)
│   ├── ColorSelectionButton (Button)
│   └── PlayersListContainer (VerticalLayoutGroup или GridLayoutGroup)
│       └── (здесь будут создаваться PlayerLobbyItem префабы)
├── LobbySettingsPanel (GameObject) - неактивна по умолчанию
├── ConnectToLobbyPanel (GameObject) - неактивна по умолчанию
└── ColorSelectionPanel (GameObject) - неактивна по умолчанию
```

### 2. Canvas для Lobby сцены

**Структура:**
```
Canvas (Lobby)
├── ChatSystem UI
│   ├── ChatRoot (GameObject)
│   │   ├── ChatInputField (InputField)
│   │   └── MessageSpawnParent (Transform)
│   │       └── (здесь будут создаваться ChatMessageItem префабы)
```

---

## Настройка CameraMovementController

### Детальная настройка objectTargets[2] (для создания лобби)

**Элемент с индексом 2 должен быть настроен так:**

1. **hoverObject:**
   - GameObject с компонентом `ObjectHoverEffect`
   - Должен иметь Collider для обнаружения наведения мыши
   - При клике на этот объект будет создано лобби

2. **targetPoint:**
   - Transform с позицией и поворотом для подлета камеры
   - Камера плавно переместится к этой точке при клике

3. **objectCanvas:**
   - Canvas (World Space) для отображения UI после подлета
   - Может содержать информацию о лобби или кнопки

4. **objectToHide:**
   - GameObject, который скрывается при клике на объект
   - Показывается обратно при нажатии ESC

**Пример настройки:**
```
objectTargets[2]:
  hoverObject: GameObject "CreateLobbyButton" (с ObjectHoverEffect и Collider)
  targetPoint: Transform "CameraTarget_Lobby" (позиция для камеры)
  objectCanvas: Canvas "LobbyInfoCanvas" (World Space)
  objectToHide: GameObject "MainMenuObject" (скрывается при клике)
```

---

## Порядок настройки (пошагово)

### Шаг 1: Базовая настройка Steam
1. Убедитесь, что Steam запущен
2. Создайте файл `steam_appid.txt` в корне проекта с вашим App ID
3. Настройте `SteamManager` с правильным `steamAppId`

### Шаг 2: Настройка MirrorNetworkManager
1. Создайте GameObject `MirrorNetworkManager`
2. Добавьте компонент `MirrorNetworkManager`
3. Добавьте компонент `FizzySteamworks`
4. Настройте `playerPrefab` (префаб игрока)
5. Настройте `offlineScene` и `onlineScene`
6. Настройте `maxConnections`

### Шаг 3: Настройка LobbyManager
1. Создайте GameObject `LobbyManager`
2. Добавьте компонент `LobbyManager`
3. Настройте все кнопки и панели
4. Настройте `playerLobbyPrefab`
5. Настройте `playersListContainer`
6. Убедитесь, что `gameSceneName` = `"Lobby"`

### Шаг 4: Настройка UI
1. Создайте все панели (LobbySettingsPanel, ConnectToLobbyPanel, ColorSelectionPanel)
2. Настройте все кнопки и поля ввода
3. Убедитесь, что панели неактивны по умолчанию
4. Настройте `playersListContainer` (VerticalLayoutGroup или GridLayoutGroup)

### Шаг 5: Настройка CameraMovementController
1. Найдите или создайте объект с `CameraMovementController`
2. Настройте `objectTargets` массив
3. **ВАЖНО:** Настройте элемент с индексом 2 для создания лобби
4. Назначьте `lobbyManager` ссылку
5. Установите `lobbyCreationIndex = 2`

### Шаг 6: Настройка префабов
1. Создайте/настройте префаб игрока с всеми компонентами
2. Создайте префаб `PlayerLobbyItem` с UI элементами
3. Создайте префаб `ChatMessageItem` с UI элементами
4. Назначьте префабы в соответствующие скрипты

### Шаг 7: Настройка сцены Lobby
1. Создайте SpawnPoints для игроков
2. Добавьте SpawnPoints в `MirrorNetworkManager.playerSpawnPositions`
3. Настройте UI для чата (если нужно)

---

## Важные моменты и проверки

### ✅ Проверочный список

#### Перед тестированием убедитесь:

1. **Steam:**
   - [ ] Steam запущен
   - [ ] Вы вошли в аккаунт Steam
   - [ ] `steam_appid.txt` существует и содержит правильный App ID
   - [ ] `SteamManager.steamAppId` совпадает с файлом

2. **MirrorNetworkManager:**
   - [ ] Компонент `FizzySteamworks` добавлен
   - [ ] `playerPrefab` назначен
   - [ ] `offlineScene` = `"Menu"`
   - [ ] `onlineScene` = `"Lobby"` (или пусто)
   - [ ] `maxConnections` установлен

3. **LobbyManager:**
   - [ ] Все кнопки назначены
   - [ ] Все панели назначены
   - [ ] `playerLobbyPrefab` назначен
   - [ ] `playersListContainer` назначен
   - [ ] `gameSceneName` = `"Lobby"`

4. **CameraMovementController:**
   - [ ] `objectTargets` массив заполнен
   - [ ] `objectTargets[2]` настроен для создания лобби
   - [ ] `lobbyManager` назначен
   - [ ] `lobbyCreationIndex = 2`

5. **Префаб игрока:**
   - [ ] `NetworkIdentity` добавлен
   - [ ] `NetworkPlayer` добавлен и настроен
   - [ ] `playerModelObjects` заполнен
   - [ ] Камера и AudioListener настроены
   - [ ] `VoiceWaveVisualizer` настроен
   - [ ] `ChatSystem` настроен

6. **Префаб PlayerLobbyItem:**
   - [ ] `playerNameText` назначен
   - [ ] `pingText` назначен
   - [ ] `colorPlayerImages` заполнен (минимум 1 элемент)
   - [ ] `adminIndicator` назначен и неактивен по умолчанию

7. **UI:**
   - [ ] Все панели неактивны по умолчанию
   - [ ] `playersListContainer` активен
   - [ ] Кнопки подключены к соответствующим скриптам

---

## Процесс работы системы

### Создание лобби (Хост):

1. Игрок наводит мышь на объект в `objectTargets[2]`
2. Игрок кликает ЛКМ на объект
3. `CameraMovementController` вызывает `CreateSteamLobby()`
4. `SteamLobbyManager` создает Steam лобби
5. После создания Steam лобби запускается Mirror хост
6. `LobbyManager` создает `PlayerLobbyItem` для хоста
7. Хост видит себя в списке игроков с `adminIndicator`

### Подключение к лобби (Клиент):

1. Игрок нажимает кнопку "Подключиться к лобби"
2. Открывается `ConnectToLobbyPanel`
3. Открывается Steam оверлей (`SteamFriends.ActivateGameOverlay("friends")`)
4. Игрок выбирает друга или принимает приглашение
5. `SteamLobbyManager` получает событие `OnGameLobbyJoinRequested`
6. Игрок присоединяется к Steam лобби
7. `SteamLobbyManager` получает Steam ID хоста
8. Подключается к Mirror серверу через `ConnectToSteamId()`
9. `LobbyManager` синхронизирует список игроков через `NetworkPlayer`

### Синхронизация данных:

1. **Никнеймы:**
   - Получаются из Steam API через `SteamManager.GetSteamName()`
   - Синхронизируются через `NetworkPlayer.networkPlayerName` (SyncVar)
   - Отображаются в `PlayerLobbyItem.playerNameText`

2. **Цвета:**
   - Выбираются в `ColorSelectionPanel`
   - Сохраняются в `PlayerPrefs`
   - Синхронизируются через `NetworkPlayer.networkPlayerColor` (SyncVar)
   - Отображаются в `PlayerLobbyItem.colorPlayerImages`

3. **Пинг:**
   - У хоста всегда = `5`
   - У клиентов получается из `NetworkConnection.rtt`
   - Обновляется каждые 5 секунд
   - Отображается в `PlayerLobbyItem.pingText`

4. **Статус админа:**
   - Хост всегда админ (`connectionId == 0` или `localClientId` если хост)
   - Отображается через `PlayerLobbyItem.adminIndicator`

### Переход в игру:

1. Хост нажимает `lobbySettingsButton`
2. Происходит переход на сцену `Lobby` через `ServerChangeScene()`
3. Все игроки автоматически переходят на сцену Lobby
4. Игроки спавнятся на SpawnPoints
5. Хост видит всех игроков, кроме себя
6. Клиенты видят всех игроков, включая хоста

---

## Решение проблем

### Проблема: Steam не инициализируется
**Решение:**
- Убедитесь, что Steam запущен
- Проверьте `steam_appid.txt` файл
- Проверьте `SteamManager.steamAppId`

### Проблема: Лобби не создается
**Решение:**
- Проверьте, что `objectTargets[2]` настроен правильно
- Проверьте, что `lobbyCreationIndex = 2`
- Проверьте, что `lobbyManager` назначен в `CameraMovementController`
- Проверьте логи Unity на ошибки

### Проблема: Игроки не видят друг друга в лобби
**Решение:**
- Проверьте, что `playerLobbyPrefab` назначен
- Проверьте, что `playersListContainer` активен
- Проверьте, что `NetworkPlayer` правильно синхронизирует данные
- Проверьте логи на ошибки синхронизации

### Проблема: Никнеймы не отображаются
**Решение:**
- Проверьте, что Steam инициализирован
- Проверьте, что `SteamManager.GetSteamName()` возвращает имя
- Проверьте, что `NetworkPlayer.SetPlayerNameCommand()` вызывается
- Проверьте, что `PlayerLobbyItem.playerNameText` назначен

### Проблема: Цвета не синхронизируются
**Решение:**
- Проверьте, что `NetworkPlayer.SetPlayerColorCommand()` вызывается
- Проверьте, что `ColorSelectionPanel` находит локального `NetworkPlayer`
- Проверьте, что `PlayerLobbyItem.colorPlayerImages` заполнен

### Проблема: Пинг не отображается
**Решение:**
- У хоста пинг всегда = 5 (проверьте логику в `PlayerLobbyItem`)
- У клиентов проверьте, что RTT получается из `NetworkConnection`
- Проверьте, что `UpdateRTTPeriodically()` запускается на сервере

### Проблема: Кнопка "Настройки лобби" не видна
**Решение:**
- Проверьте, что `lobbySettingsButton` назначен
- Проверьте, что `IsHost()` возвращает `true` для хоста
- Проверьте, что `UpdateUI()` вызывается после создания лобби

### Проблема: Переход на сцену Lobby не работает
**Решение:**
- Проверьте, что `gameSceneName` = `"Lobby"`
- Проверьте, что сцена `Lobby` добавлена в Build Settings
- Проверьте, что `NetworkManager.singleton.onlineScene` установлен
- Проверьте логи на ошибки загрузки сцены

---

## Дополнительные настройки

### Настройка Steam App ID

1. Получите свой Steam App ID из Steamworks
2. Создайте файл `steam_appid.txt` в корне проекта
3. Запишите в файл только число (например, `480`)
4. Установите `SteamManager.steamAppId` = ваш App ID

### Настройка максимального количества игроков

1. В `LobbyManager`: установите `maxPlayers`
2. В `MirrorNetworkManager`: установите `maxConnections` = `maxPlayers`
3. В `SteamLobbyManager`: при создании лобби используется `networkManager.maxConnections`

### Настройка цветов игроков

1. В `ColorSelectionPanel` настройте цвета в инспекторе
2. Цвета сохраняются в `PlayerPrefs` с ключами:
   - `PlayerColor_R`
   - `PlayerColor_G`
   - `PlayerColor_B`
   - `PlayerColor_A`

### Настройка звуков чата

1. В `ChatSystem` назначьте массивы звуков:
   - `letterSounds`: 26 звуков для A-Z
   - `russianLetterSounds`: 31 звук для русских букв
   - `numberSounds`: 10 звуков для 0-9
   - `russianNumberSounds`: 10 звуков для русских цифр
   - `defaultSymbolSound`: звук для знаков препинания

---

## Тестирование

### Локальное тестирование (один компьютер):

1. Запустите игру в редакторе Unity
2. Создайте лобби (клик на `objectTargets[2]`)
3. Откройте второй экземпляр игры (через ParrelSync или вручную)
4. Подключитесь к лобби через Steam оверлей
5. Проверьте синхронизацию никнеймов и цветов
6. Проверьте отображение пинга
7. Проверьте переход в сцену Lobby

### Тестирование с несколькими компьютерами:

1. Запустите игру на первом компьютере
2. Создайте лобби
3. Запустите игру на втором компьютере
4. Подключитесь через Steam оверлей или приглашение
5. Проверьте все функции мультиплеера

---

## Заключение

После выполнения всех шагов система мультиплеера должна работать корректно:
- ✅ Создание лобби через клик на объект
- ✅ Подключение через Steam оверлей
- ✅ Синхронизация никнеймов из Steam
- ✅ Синхронизация цветов
- ✅ Отображение пинга (у хоста = 5)
- ✅ Отображение статуса админа
- ✅ Переход в сцену Lobby
- ✅ Спавн игроков
- ✅ Чат с озвучкой
- ✅ Голосовой чат с визуализацией

Если возникнут проблемы, проверьте логи Unity и убедитесь, что все компоненты настроены правильно.

