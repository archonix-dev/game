# Подробная сводка по настройке сцен Menu и Lobby

## Обзор
Этот документ описывает все необходимые компоненты и настройки для правильной работы мультиплеера на сценах **Menu** и **Lobby**.

---

## Сцена Menu

### 1. Обязательные объекты в сцене

#### 1.1. MirrorNetworkManager (Singleton)
**Компоненты:**
- `MirrorNetworkManager` (скрипт) - **наследуется от NetworkManager (Mirror)**
- `FizzySteamworks` (Transport) - для Steam P2P подключений

**Важно:**
- `MirrorNetworkManager` уже является `NetworkManager`, поэтому **НЕ нужно** добавлять компонент `NetworkManager` отдельно
- Unity автоматически распознает `MirrorNetworkManager` как `NetworkManager` благодаря наследованию

**Настройки MirrorNetworkManager:**
- **Offline Scene**: `Menu` (или пусто)
- **Online Scene**: `Lobby`
- **Player Prefab**: Префаб игрока (должен быть в папке Resources)
- **Auto Create Player**: `false` (создаем вручную через GameManager)

**Настройки FizzySteamworks:**
- **Channels**: Массив типов отправки (по умолчанию: Reliable и UnreliableNoDelay)
- **Timeout**: Таймаут подключения в секундах (по умолчанию: 25)
- **Allow Steam Relay**: `true` (рекомендуется) - разрешает ретрансляцию через серверы Steam, если прямое подключение невозможно
- **Use Next Gen Steam Networking**: `true` (рекомендуется) - использовать SteamSockets вместо устаревшего SteamNetworking

**Важно:**
- **Steam App ID** не настраивается в FizzySteamworks - он берется автоматически из Steam API
- Steam App ID должен быть установлен в файле `steam_appid.txt` в корне проекта или через SteamManager

#### 1.2. LobbyManager (Singleton)
**Компоненты:**
- `LobbyManager` (скрипт)

**Поля в инспекторе:**
- **Play Button**: Кнопка "Играть" (создание лобби)
- **Lobby Settings Button**: Кнопка настроек лобби (только для хоста)
- **Start Game Button**: Кнопка "Начать игру" (загрузка сцены Lobby)
- **Connect To Lobby Button**: Кнопка подключения к другому лобби
- **Color Selection Button**: Кнопка выбора цвета игрока

**UI Панели:**
- **Lobby Settings Panel**: Панель настроек лобби
- **Connect To Lobby Panel**: Панель подключения к лобби
- **Color Selection Panel**: Панель выбора цвета

**Отображение игроков:**
- **Players List Container**: Transform контейнер для списка игроков (обычно VerticalLayoutGroup или GridLayoutGroup)
- **Player Lobby Prefab**: Префаб элемента игрока в лобби

**Настройки сети:**
- **Default Port**: `7777` (не используется для FizzySteamworks)
- **Max Players**: `8` (максимальное количество игроков)

**Загрузка сцены:**
- **Scene Loader**: AsyncSceneLoaderWithAnimation (опционально)
- **Game Scene Name**: `Lobby`

#### 1.3. SteamManager (Singleton)
**Компоненты:**
- `SteamManager` (скрипт) - MonoBehaviour, добавляется как компонент к GameObject

**Настройки в инспекторе:**
- **Steam App ID**: ID вашего приложения Steam (по умолчанию: 480 - Spacewar для тестирования)
- **Auto Initialize**: `true` (рекомендуется) - автоматически инициализировать Steam при старте

**Проверки:**
- Должен быть инициализирован до создания лобби
- Steam должен быть запущен на машине игрока
- Скрипт автоматически создаст файл `steam_appid.txt` в корне проекта, если его нет

**Важно:**
- SteamManager должен быть единственным экземпляром в сцене (Singleton)
- Объект не уничтожается при смене сцены (DontDestroyOnLoad)
- Скрипт автоматически вызывает `SteamAPI.RunCallbacks()` в Update() для обработки событий Steam

#### 1.4. SteamLobbyManager (Singleton)
**Компоненты:**
- `SteamLobbyManager` (скрипт)

**Функции:**
- Создание Steam лобби
- Подключение к Steam лобби
- Управление Steam лобби (приглашения, список игроков)

#### 1.5. LobbyNetworkManager (NetworkIdentity)
**Автоматическое создание:**
- Создается автоматически при старте сервера
- НЕ нужно добавлять вручную в сцену

**Компоненты (создаются автоматически):**
- `LobbyNetworkManager` (скрипт)
- `NetworkIdentity` (Mirror)

**Важно:**
- Этот объект создается динамически при запуске сервера
- Должен быть заспавнен через `NetworkServer.Spawn()`

#### 1.6. UI Canvas
**Структура:**
- **Canvas** (Canvas с Screen Space - Overlay)
  - **Players List Container** (VerticalLayoutGroup или GridLayoutGroup)
    - Здесь будут создаваться префабы `PlayerLobbyItem`
  - **Lobby Settings Panel** (панель настроек)
  - **Connect To Lobby Panel** (панель подключения)
  - **Color Selection Panel** (панель выбора цвета)

**Важно:**
- `Players List Container` должен быть назначен в `LobbyManager.playersListContainer`
- Все панели должны быть неактивны по умолчанию

#### 1.7. Префаб PlayerLobbyItem
**Местоположение:** Может быть любым (назначен в LobbyManager)

**Компоненты:**
- `PlayerLobbyItem` (скрипт)

**UI Элементы:**
- **Player Name Text**: Text компонент с именем игрока
- **Ping Text**: Text компонент с пингом
- **Color Player Images**: Массив Image компонентов для отображения цвета
- **Admin Indicator**: GameObject для индикатора админа

**Важно:**
- ЭТО ЛОКАЛЬНЫЙ UI ЭЛЕМЕНТ - не NetworkIdentity
- Создается каждым клиентом отдельно
- Синхронизируется через ClientRpc/TargetRpc

---

## Сцена Lobby

### 1. Обязательные объекты в сцене

#### 1.1. GameManager (NetworkIdentity)
**Компоненты:**
- `GameManager` (скрипт)
- `NetworkIdentity` (Mirror) - обязательно!

**Настройки NetworkIdentity:**
- **Server Only**: `false` (должен быть доступен клиентам)

**Важно:**
- **Local Player Authority** - это не параметр NetworkIdentity
- Authority (владение объектом) устанавливается автоматически при спавне через `NetworkServer.Spawn(object, connection)`
- Для игроков authority устанавливается автоматически - каждый клиент владеет своим игроком
- Можно проверить authority через свойство `isOwned` в NetworkBehaviour компонентах

**Поля в инспекторе GameManager:**
- **Game Scene Name**: `Test` (или другое имя игровой сцены)
- **Menu Scene Name**: `Menu`
- **Current Game Scene Name**: `Lobby`
- **Player Prefab**: Префаб игрока (должен быть в папке Resources)
- **Spawn Point**: Transform точки спавна игроков

**Важно:**
- GameManager должен быть заспавнен через `NetworkServer.Spawn()`
- Обычно создается в сцене и помечается как DontDestroyOnLoad (опционально)

#### 1.2. Префаб игрока (Player Prefab)
**Местоположение:** Папка Resources

**Компоненты:**
- `NetworkPlayer` (скрипт)
- `NetworkIdentity` (Mirror) - обязательно!
- `PlayerController` (управление)
- `NetworkTransform` или `ClientNetworkTransform` (синхронизация позиции)
- Модель игрока (MeshRenderer, Collider и т.д.)

**Настройки NetworkIdentity:**
- **Server Only**: `false` (должен быть доступен клиентам)

**Важно:**
- **Local Player Authority** - это не параметр NetworkIdentity
- Authority устанавливается автоматически при спавне через `NetworkServer.Spawn(playerPrefab, connection)`
- Когда игрок спавнится с указанием `connection`, он автоматически получает authority
- Можно проверить authority через свойство `isOwned` в NetworkBehaviour компонентах

**Настройки NetworkTransform (если используется):**
- **Sync Position**: `true`
- **Sync Rotation**: `true` (если нужно)
- **Client Authority**: `true` (для управления игроком клиентом)

**Важно:**
- Если используется `ClientNetworkTransform` вместо `NetworkTransform`, то Client Authority управляется через этот компонент
- ClientNetworkTransform автоматически дает authority клиенту для синхронизации позиции

#### 1.3. Spawn Point
**Компоненты:**
- Transform с позицией спавна

**Настройки:**
- Назначен в `GameManager.spawnPoint`
- Может быть один или несколько (выбирается случайно)

#### 1.4. MirrorNetworkManager (DontDestroyOnLoad)
**Важно:**
- Обычно создается в сцене Menu и не уничтожается при переходе
- Настройки остаются те же, что в сцене Menu

---

## Процесс подключения и синхронизации

### 1. Создание лобби (Хост)

1. **Хост нажимает "Играть"** или взаимодействует с объектом для создания лобби
2. **SteamLobbyManager** создает Steam лобби
3. **LobbyManager.CreateMirrorLobbyAfterSteamLobby()** вызывается
4. **MirrorNetworkManager.StartHostGame()** запускает хост
5. **LobbyNetworkManager** создается автоматически на сервере
6. **LobbyManager.OnMirrorServerStarted()** создает UI для хоста
7. **LobbyManager.CreatePlayerLobbyItem()** создает PlayerLobbyItem для хоста

### 2. Подключение клиента

1. **Клиент получает приглашение** через Steam оверлей
2. **SteamLobbyManager** подключается к Steam лобби
3. **SteamLobbyManager.JoinMirrorLobbyAfterSteamLobby()** подключает клиента к Mirror
4. **MirrorNetworkManager.ConnectToSteamId()** подключает клиента к серверу
5. **OnMirrorClientConnected()** вызывается на сервере и клиенте

**На сервере:**
- Создается PlayerLobbyItem для нового клиента
- **SendAllPlayersToNewClient()** отправляет список всех игроков новому клиенту через TargetRpc

**На клиенте:**
- Клиент отправляет свое Steam имя через `SendPlayerSteamNameCommand`
- Клиент получает данные всех игроков через TargetRpc
- Создаются локальные PlayerLobbyItem для всех игроков

### 3. Синхронизация списка игроков

**Механизм синхронизации:**
1. Все PlayerLobbyItem создаются **локально** на каждом клиенте
2. Сервер хранит словарь `playerLobbyItems` с данными всех игроков
3. При подключении нового клиента сервер отправляет ему все данные через `TargetRpc`
4. При изменении данных (имя, цвет) сервер отправляет `ClientRpc` всем клиентам

**Важно:**
- PlayerLobbyItem **НЕ является** NetworkIdentity
- Данные синхронизируются вручную через RPC
- Каждый клиент создает свои локальные копии

### 4. Переход в игровую сцену (Lobby)

1. **Хост нажимает "Начать игру"**
2. **LobbyManager.OnStartGameButtonClicked()** вызывает `NetworkManager.ServerChangeScene("Lobby")`
3. Mirror автоматически загружает сцену Lobby для всех клиентов
4. **GameManager.OnStartServer()** вызывается на сервере
5. **GameManager.SpawnAllPlayersAfterSceneLoad()** спавнит всех игроков
6. Каждый клиент получает своего игрока через NetworkServer.Spawn()

---

## Проверочный список

### Сцена Menu

- [ ] MirrorNetworkManager присутствует и настроен
- [ ] FizzySteamworks Transport настроен с правильным Steam App ID
- [ ] LobbyManager присутствует и все поля назначены
- [ ] SteamManager присутствует и инициализирован
- [ ] SteamLobbyManager присутствует
- [ ] UI Canvas с Players List Container настроен
- [ ] Префаб PlayerLobbyItem создан и назначен в LobbyManager
- [ ] Все кнопки и панели назначены в LobbyManager

### Сцена Lobby

- [ ] GameManager присутствует с NetworkIdentity
- [ ] Player Prefab находится в папке Resources
- [ ] Player Prefab имеет NetworkIdentity и NetworkPlayer
- [ ] Spawn Point назначен в GameManager
- [ ] GameManager.spawnPoint не null
- [ ] GameManager.playerPrefab не null

### Префабы

- [ ] PlayerLobbyItem имеет все UI элементы назначены
- [ ] Player Prefab имеет NetworkIdentity с Local Player Authority = true
- [ ] Player Prefab имеет NetworkTransform или ClientNetworkTransform

### Сеть

- [ ] Steam запущен
- [ ] Steam App ID правильный
- [ ] FizzySteamworks настроен правильно
- [ ] NetworkManager использует FizzySteamworks как Transport

---

## Частые проблемы и решения

### Проблема: Игроки не отображаются в списке лобби

**Причины:**
1. Players List Container не назначен в LobbyManager
2. PlayerLobbyItem Prefab не назначен
3. LobbyNetworkManager не создан (проверьте логи)
4. Клиент не получает данные через RPC (проверьте логи)

**Решение:**
1. Проверьте назначение всех полей в LobbyManager
2. Проверьте логи - должны быть сообщения о создании PlayerLobbyItem
3. Убедитесь, что LobbyNetworkManager создается автоматически (логи при старте сервера)

### Проблема: Игроки спавнятся дважды

**Причина:**
- `SpawnAllPlayersAfterSceneLoad()` вызывается несколько раз

**Решение:**
- Используется флаг `spawnCoroutineStarted` для предотвращения дублирования (уже исправлено)

### Проблема: Игроки не спавнятся в сцене Lobby

**Причины:**
1. GameManager не заспавнен
2. Player Prefab не назначен
3. Spawn Point не назначен
4. Сцена Lobby не загружена правильно

**Решение:**
1. Проверьте логи GameManager - должны быть сообщения о спавне
2. Убедитесь, что GameManager имеет NetworkIdentity и заспавнен
3. Проверьте назначение playerPrefab и spawnPoint

### Проблема: Клиент не видит других игроков при подключении

**Причина:**
- Данные о других игроках не отправляются новому клиенту

**Решение:**
- Используется метод `SendAllPlayersToNewClient()` который автоматически отправляет все данные (уже исправлено)

---

## Логи для отладки

### Создание лобби
- `[LobbyManager] ✓ Сервер создан через FizzySteamworks!`
- `[LobbyManager] Создание PlayerLobbyItem для connectionId=...`

### Подключение клиента
- `[MirrorNetworkManager] ✓ Клиент подключен к серверу`
- `[LobbyManager] ✓ Клиент подключен: ID=...`
- `[LobbyManager] Отправляем новому клиенту данные о X игроках`

### Синхронизация игроков
- `[LobbyNetworkManager] CreatePlayerLobbyItemClientRpc получен: ...`
- `[LobbyManager] ✓ PlayerLobbyItem для игрока X создан локально`

### Спавн игроков
- `[GameManager] ✓ GameManager запущен на сервере!`
- `[GameManager] Начинаем спавн игроков в сцене Lobby`
- `[GameManager] ✓ Игрок X успешно заспавнен!`

---

## Дополнительные замечания

1. **Важно:** Все синхронизируемые данные должны проходить через сервер
2. **Важно:** PlayerLobbyItem создается локально, но данные синхронизируются через RPC
3. **Важно:** GameManager должен быть заспавнен перед спавном игроков
4. **Важно:** Не создавайте NetworkIdentity вручную для PlayerLobbyItem - это локальные UI элементы

---

---

## Тестирование мультиплеера

### Тестирование через Editor + Build

**Да, это нормально и работает!** Вы можете запускать одну игру через Unity Editor, а другую через собранную версию (Build). Это стандартный способ тестирования мультиплеера.

**Преимущества:**
- Не требует установки ParrelSync
- Позволяет тестировать Build в реальных условиях
- Можно тестировать на разных компьютерах в локальной сети

**Требования:**
- Обе версии (Editor и Build) должны использовать **одинаковый Steam App ID**
- Steam должен быть запущен на обеих машинах
- Для тестирования через Steam оверлей - используйте тот же Steam аккаунт или друзей в Steam

**Процесс тестирования:**
1. Запустите хост в Unity Editor
2. Создайте Steam лобби
3. Запустите Build на другом устройстве (или через другой Steam аккаунт)
4. Подключитесь к лобби через Steam оверлей

**Важно:**
- При тестировании Editor + Build через Steam могут быть небольшие различия в производительности
- Editor версия обычно работает медленнее из-за отладки
- Это нормально и не влияет на функциональность

### Тестирование через два Build'а

Также можно тестировать, запуская два Build'а одновременно:
- На одном компьютере (запустите экземпляр дважды)
- На двух компьютерах в локальной сети

---

## Версия
Версия документа: 1.0
Последнее обновление: 2024
Автор: AI Assistant

