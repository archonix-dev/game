# Инструкция по настройке мультиплеера

## Требования

1. **FizzySteamworks** - должен быть установлен в проекте
2. **Mirror Networking** - уже установлен
3. **Steamworks.NET** - должен быть установлен для работы со Steam API

## Настройка сцены Menu

### 1. Добавьте SteamInitializer
- Создайте пустой GameObject на сцене Menu (например, назовите его "SteamInitializer")
- Добавьте компонент `SteamInitializer`
- Этот компонент инициализирует Steam в `Awake()`
- **Важно:** Компонент автоматически использует `DontDestroyOnLoad`, чтобы Steam оставался активным при смене сцен
- Steam будет выключен только при выходе из приложения (`OnApplicationQuit`)

### 2. Настройте LobbyNetworkManager
- Создайте пустой GameObject с именем "NetworkManager" **на сцене Menu**
- **ВАЖНО:** `LobbyNetworkManager` должен быть **только на сцене Menu**, НЕ на сцене Lobby
- `NetworkManager` использует `dontDestroyOnLoad = true` по умолчанию, поэтому он автоматически переносится между сценами
- Добавьте компонент `LobbyNetworkManager` (наследуется от Mirror NetworkManager)
- Добавьте компонент `FizzySteamworks` (транспорт для Mirror)
- В Inspector настройте:
  - `steamAppID` - ваш Steam App ID
  - `defaultMaxPlayers` - максимальное количество игроков (по умолчанию 4)
  - `lobbySceneName` - имя сцены лобби ("Lobby")
  - `menuSceneName` - имя сцены меню ("Menu")
  - `playerPrefab` - префаб LobbyPlayer (см. ниже)
  - `dontDestroyOnLoad` - должно быть включено (true) - это значение по умолчанию

### 3. Настройте LobbyManager
- Создайте пустой GameObject с именем "LobbyManager"
- Добавьте компонент `LobbyManager`
- В Inspector настройте:
  - `maxPlayers` - максимальное количество игроков
  - `playerListParent` - Transform для спавна префабов списка игроков
  - `playerListPrefab` - префаб PlayerListUI (см. ниже)
  - `lobbyListParent` - Transform для спавна префабов списка лобби
  - `lobbyListPrefab` - префаб LobbyListUI (см. ниже)

### 4. Настройте CameraMovementController
- Найдите существующий `CameraMovementController` на сцене
- Убедитесь, что второй элемент в массиве `objectTargets` (индекс 1) настроен правильно
- При клике на этот объект будет создаваться лобби

## Настройка UI для лобби

### 1. Canvas для лобби
- В Canvas, который открывается при клике на второй объект, добавьте компонент `LobbyMenuUI`
- Настройте все ссылки на UI элементы в Inspector:
  - **Player List Parent** - Transform (обычно пустой GameObject) для спавна префабов списка игроков
  - **Start Game Button** - кнопка "Начать игру" (видна только создателю лобби)
  - **Choose Color Button** - кнопка "Выбрать цвет"
  - **Lobby Settings Button** - кнопка "Настройки лобби" (видна только создателю лобби)
  - **Join Other Lobby Button** - кнопка "Присоединиться к другому лобби"
  - **Color Selection Panel** - GameObject с панелью выбора цвета (8 кнопок цветов)
  - **Lobby Settings Panel** - GameObject с панелью настроек лобби
  - **Max Players Input** - InputField для максимального количества игроков
  - **Password Input** - InputField для пароля лобби
  - **Cheats Toggle** - Toggle для включения читов
  - **Settings Back Button** - кнопка "Назад" в настройках
  - **Settings Apply Button** - кнопка "Применить" в настройках
  - **Join Lobby Panel** - GameObject с панелью присоединения к лобби
  - **Lobby Search Input** - InputField для поиска лобби по имени
  - **Lobby List Parent** - Transform для спавна префабов списка лобби
  - **Lobby Password Text** - Text для отображения пароля лобби (виден только создателю)
  - **Connection Status Text** - Text для отображения статуса соединения

### 2. Префаб PlayerListUI
Создайте префаб для элемента списка игроков со следующими компонентами:
- `PlayerListUI` скрипт
- UI элементы:
  - `Image isOwnerImage` - изображение для отображения создателя лобби (может быть скрыто по умолчанию)
  - `Text playerNameText` или `TextMeshProUGUI playerNameText` - текст имени игрока
  - `Text pingText` или `TextMeshProUGUI pingText` - текст пинга
  - `Image cursorFollowImage` - изображение, следующее за курсором (может быть скрыто по умолчанию)
  - `Image[] colorImages` - массив изображений для отображения цвета игрока (минимум 1 элемент)

**Примечание:** Скрипт поддерживает как стандартные UI компоненты Unity (`Text`, `InputField`), так и TextMeshPro (`TextMeshProUGUI`, `TMP_InputField`). Используйте те, которые у вас установлены в проекте.

### 3. Префаб LobbyListUI
Создайте префаб для элемента списка лобби со следующими компонентами:
- `LobbyListUI` скрипт
- UI элементы:
  - `Image cursorFollowImage` - изображение, следующее за курсором (может быть скрыто по умолчанию)
  - `Text playerNameText` или `TextMeshProUGUI playerNameText` - текст имени создателя лобби
  - `Text playerCountText` или `TextMeshProUGUI playerCountText` - текст количества игроков
  - `InputField passwordInput` или `TMP_InputField passwordInput` - поле для ввода пароля
  - `Button joinButton` - кнопка "Войти"

## Настройка сцены Lobby

### 1. Добавьте LobbyPlayerSpawner
- Создайте пустой GameObject с именем "PlayerSpawner" (или "LobbyPlayerSpawner")
- **ВАЖНО:** Добавьте компонент `NetworkIdentity` на этот GameObject
  - `NetworkIdentity` необходим, так как `LobbyPlayerSpawner` наследуется от `NetworkBehaviour`
  - Настройте `NetworkIdentity`:
    - **Server Only** - оставьте выключенным (false)
    - **Visibility** - выберите "Default"
    - **Spawn On Start** - оставьте выключенным (false)
- Добавьте компонент `LobbyPlayerSpawner`
- В Inspector настройте:
  - `spawnPoints` - массив Transform точек спавна игроков (создайте пустые GameObject'ы и назначьте их в массив)
  - `playerPrefab` - префаб игрока для спавна на сцене Lobby (это должен быть ваш игровой префаб с контроллером, камерой и моделью, НЕ LobbyPlayer!)
  
**Примечание:** Если вы видите ошибку "LobbyPlayerSpawner requires a NetworkIdentity", это означает, что вы забыли добавить компонент `NetworkIdentity` на GameObject с `LobbyPlayerSpawner`.

### 2. Настройте LobbyNetworkManager
- Убедитесь, что `LobbyNetworkManager` настроен с правильными параметрами
- В Inspector компонента `LobbyNetworkManager` настройте следующие параметры:

**Основные настройки:**
  - **Steam App ID** (`steamAppID`) - ваш Steam App ID (по умолчанию 480 для тестирования)
  - **Default Max Players** (`defaultMaxPlayers`) - максимальное количество игроков (по умолчанию 4)
  - **Lobby Scene Name** (`lobbySceneName`) - имя сцены лобби ("Lobby")
  - **Menu Scene Name** (`menuSceneName`) - имя сцены меню ("Menu")

**Настройки сцен (Scenes):**
  - **Offline Scene** (`offlineScene`) - оставьте пустым или укажите сцену Menu (если нужно)
  - **Online Scene** (`onlineScene`) - оставьте пустым (сцена будет загружаться через `ServerChangeScene`)
  - **Offline Scene Load Delay** (`offlineSceneLoadDelay`) - задержка загрузки офлайн сцены (можно оставить по умолчанию)
  
  **Важно:** Убедитесь, что сцена Lobby добавлена в Build Settings:
  1. Откройте File → Build Settings
  2. Если сцена Lobby не в списке, нажмите "Add Open Scenes" или перетащите сцену Lobby в список
  3. Сцена Lobby должна быть в списке сцен для сборки

**Настройки игрока (Player):**
  - **Player Prefab** (`playerPrefab`) - перетащите префаб LobbyPlayer (см. раздел "Префабы")
  - **Auto Create Player** (`autoCreatePlayer`) - **должно быть включено (true)**, так как LobbyPlayer должен спавниться автоматически при подключении к лобби на сцене Menu для синхронизации данных игрока (имя, цвет, пинг) и отображения в списке игроков
  - **Player Spawn Method** (`playerSpawnMethod`) - выберите подходящий метод (например, Random)
  
  **Важно:** `LobbyPlayerSpawner` спавнит другой префаб (игровой) на сцене Lobby, поэтому конфликта с автоматическим спавном LobbyPlayer не будет. LobbyPlayer нужен для лобби на сцене Menu, а игровой префаб - для игры на сцене Lobby.

**Настройки сети (Network):**
  - **Max Connections** (`maxConnections`) - максимальное количество подключений (должно совпадать с `maxPlayers` в LobbyManager)
  - **Disconnect Inactive Connections** (`disconnectInactiveConnections`) - рекомендуется включить (true)
  - **Disconnect Inactive Timeout** (`disconnectInactiveTimeout`) - таймаут в секундах (например, 60)

**Дополнительные настройки:**
  - **Run In Background** (`runInBackground`) - рекомендуется включить (true)
  - **Don't Destroy On Load** (`dontDestroyOnLoad`) - должно быть включено (true) по умолчанию
  - **Transport** (`transport`) - должен быть установлен компонент FizzySteamworks (см. шаг 2 в разделе "Настройка сцены Menu")
  - **Network Address** (`networkAddress`) - адрес для подключения (устанавливается автоматически при присоединении к лобби)
  - **Headless Start Mode** (`headlessStartMode`) - режим запуска без графики (для серверов)
  - **Editor Auto Start** (`editorAutoStart`) - автоматический старт в редакторе (можно выключить)
  - **Send Rate** (`sendRate`) - частота отправки данных (по умолчанию 60 Hz)
  - **Unreliable Baseline Rate** (`unreliableBaselineRate`) - частота базовой линии для ненадежных сообщений
  - **Unreliable Redundancy** (`unreliableRedundancy`) - избыточность для ненадежных сообщений (можно выключить)
  - **Exceptions Disconnect** (`exceptionsDisconnect`) - отключать при исключениях (рекомендуется включить)
  - **Authenticator** (`authenticator`) - аутентификатор (можно оставить пустым)

**Registered Spawnable Prefabs:**
  - Этот список используется для регистрации префабов, которые могут быть заспавнены через сеть
  - Префаб LobbyPlayer будет автоматически добавлен в этот список, если он назначен в Player Prefab
  - Если у вас есть другие сетевые префабы, добавьте их в этот список

## Префабы

### 1. LobbyPlayer Prefab
- Создайте пустой GameObject (например, назовите его "LobbyPlayer")
- Добавьте компонент `NetworkIdentity`
- Добавьте компонент `LobbyPlayer`
- Настройте `NetworkIdentity`:
  - **Server Only** - оставьте выключенным (false), так как игрок должен быть виден всем
  - **Visibility** - выберите "Default" (объект будет виден всем подключенным клиентам)
  - **Spawn On Start** - оставьте выключенным (false), спавн будет происходить автоматически
  - **Local Player Authority** - эта опция больше не существует в новых версиях Mirror
- Сохраните как префаб:
  1. Перетащите GameObject в папку Prefabs в Project window
  2. Или используйте меню: GameObject → Save As Prefab
- Назначьте префаб в `LobbyNetworkManager`:
  1. Выберите GameObject с компонентом `LobbyNetworkManager` на сцене
  2. В Inspector найдите поле **"Player Prefab"**
  3. Перетащите созданный префаб LobbyPlayer в это поле

### 2. Player Prefab (для сцены Lobby)
- Это ваш обычный префаб игрока (с контроллером, камерой, моделью и т.д.)
- Должен иметь `NetworkIdentity` (обязательно!)
- Настройте `NetworkIdentity`:
  - **Server Only** - оставьте выключенным (false)
  - **Visibility** - выберите "Default"
  - **Spawn On Start** - оставьте выключенным (false)
- **КРИТИЧЕСКИ ВАЖНО:** Убедитесь, что на префабе Player **НЕТ** следующих компонентов-синглтонов:
  - `SimpleDestructionManager` - должен быть на отдельном объекте на сцене, НЕ на игроке
  - `LocalizationManager` - должен быть на отдельном объекте на сцене, НЕ на игроке
  
  **ВАЖНО:** `CoinManager` теперь **ДОЛЖЕН** быть на префабе Player! Он был переделан из синглтона в `NetworkBehaviour`, и теперь каждый игрок имеет свой собственный `CoinManager` с синхронизацией через сеть. Монеты каждого игрока хранятся отдельно.
  
  **Почему другие синглтоны нужно убрать?** Эти компоненты используют паттерн Singleton и уничтожают GameObject, если Instance уже существует. Если они на префабе Player, то при спавне игрока они видят, что Instance уже существует (на другом объекте на сцене), и уничтожают весь GameObject Player!
  
  **Решение:** 
  - `CoinManager` - оставьте на префабе Player (он больше не синглтон)
  - `SimpleDestructionManager` и `LocalizationManager` - удалите с префаба Player и разместите их на отдельных объектах на сцене Lobby (или Menu, если они нужны там). Например, создайте GameObject "Managers" на сцене и добавьте на него эти компоненты.
- Назначьте в `LobbyPlayerSpawner.playerPrefab` на сцене Lobby
- **НЕ назначайте этот префаб в `LobbyNetworkManager.playerPrefab`!** Там должен быть префаб `LobbyPlayer`

## Цвета игроков

Цвета определены в `LobbyPlayer.PlayerColors`:
0. Белый
1. Красный
2. Розовый
3. Фиолетовый
4. Синий
5. Голубой
6. Зеленый
7. Салатовый

## Важные замечания

1. **Steam должен быть запущен** перед запуском игры
2. **FizzySteamworks** должен быть правильно настроен с вашим Steam App ID
3. На сцене **Menu игроки не спавнятся** - только на сцене Lobby
   - На сцене Menu спавнится только `LobbyPlayer` (легковесный объект для синхронизации данных)
   - `LobbyPlayer` автоматически уничтожается перед переходом на сцену Lobby
   - На сцене Lobby спавнится игровой префаб `Player` через `LobbyPlayerSpawner`
4. **Пароль лобби** генерируется автоматически при создании (6 цифр)
5. **Только создатель лобби** может начать игру и изменять настройки
6. **SteamInitializer** использует `DontDestroyOnLoad`, поэтому Steam остается активным при смене сцен
7. **LobbyPlayerSpawner** должен иметь компонент `NetworkIdentity` на GameObject

## Порядок работы

1. Игрок запускает игру на сцене Menu
2. Steam инициализируется через `SteamInitializer` (SteamInitializer остается активным между сценами благодаря `DontDestroyOnLoad`)
3. Игрок кликает на второй объект в `objectTargets` (индекс 1)
4. Создается лобби через Steam API (`SteamMatchmaking.CreateLobby()`)
5. Запускается Mirror сервер через `LobbyNetworkManager.StartHost()`
6. Автоматически спавнится `LobbyPlayer` для создателя лобби (если `autoCreatePlayer = true`)
7. Камера перемещается к объекту, открывается Canvas с UI лобби
8. `LobbyManager.UpdatePlayerList()` находит все `LobbyPlayer` и создает UI элементы `playerListPrefab` для каждого
9. Другие игроки могут присоединиться через список лобби друзей (кнопка "Join Other Lobby")
10. При присоединении клиента также спавнится `LobbyPlayer` для синхронизации данных
11. Создатель лобби нажимает "Начать игру"
12. **Все `LobbyPlayer` уничтожаются** перед переходом на сцену Lobby (они нужны только на Menu)
13. Все игроки переходят на сцену Lobby через `ServerChangeScene()`
14. `LobbyPlayerSpawner` спавнит игровой префаб `Player` для каждого подключения на сцене Lobby
15. Игроки могут играть

## Архитектура системы

### Префабы и их назначение

**LobbyPlayer (префаб для сцены Menu):**
- Легковесный объект для синхронизации данных игрока (имя, цвет, Steam ID, пинг)
- Спавнится автоматически при создании/присоединении к лобби на сцене Menu
- Назначается в `LobbyNetworkManager.playerPrefab`
- Уничтожается перед переходом на сцену Lobby
- Используется только для отображения списка игроков в лобби

**Player (префаб для сцены Lobby):**
- Полноценный игровой объект с контроллером, камерой, моделью
- Спавнится через `LobbyPlayerSpawner` на сцене Lobby
- Назначается в `LobbyPlayerSpawner.playerPrefab`
- Это объект, которым игрок управляет в игре

**playerListPrefab (UI префаб):**
- UI элемент для отображения игрока в списке лобби
- Создается динамически для каждого `LobbyPlayer` на сцене Menu
- Отображает имя, цвет, пинг игрока

**lobbylistPrefab (UI префаб):**
- UI элемент для отображения лобби в списке
- Создается динамически для каждого найденного лобби друзей

## Возможные проблемы и решения

### Проблема: "LobbyPlayerSpawner requires a NetworkIdentity"
**Решение:** Добавьте компонент `NetworkIdentity` на GameObject с `LobbyPlayerSpawner` на сцене Lobby.

### Проблема: Steam ID = 0 и имя "Player X" вместо Steam ника
**Решение:** 
- Убедитесь, что Steam запущен перед запуском игры
- Проверьте, что `SteamInitializer` использует `DontDestroyOnLoad` (это уже реализовано в коде)
- Убедитесь, что Steam API инициализирован правильно

### Проблема: LobbyPlayer переносится на сцену Lobby
**Решение:** Это исправлено в коде - `LobbyPlayer` автоматически уничтожается перед переходом на сцену Lobby через `LobbyManager.DestroyAllLobbyPlayers()`.

### Проблема: Префаб Player моментально удаляется при спавне или при размещении на сцене

**Причина:** На префабе Player есть компоненты-синглтоны (`SimpleDestructionManager`, `LocalizationManager`), которые уничтожают GameObject, если Instance уже существует.

**Решение:**
1. Откройте префаб Player в редакторе
2. Удалите следующие компоненты:
   - `SimpleDestructionManager` - должен быть на отдельном объекте на сцене, НЕ на игроке
   - `LocalizationManager` - должен быть на отдельном объекте на сцене, НЕ на игроке
3. **ВАЖНО:** `CoinManager` теперь **ДОЛЖЕН** остаться на префабе Player! Он был переделан из синглтона в `NetworkBehaviour`, и теперь каждый игрок имеет свой собственный `CoinManager` с синхронизацией через сеть.
4. Разместите `SimpleDestructionManager` и `LocalizationManager` на отдельных объектах на сцене Lobby (или Menu, если они нужны там)
5. Убедитесь, что на сцене есть только один экземпляр каждого из этих компонентов

**Почему это происходит?** Синглтоны проверяют в `Awake()`, существует ли уже Instance. Если да, они уничтожают GameObject. Когда Player спавнится, эти компоненты видят, что Instance уже существует (на другом объекте на сцене), и уничтожают весь GameObject Player.

**Важно:** 
- `CoinManager` теперь на каждом игроке отдельно (не синглтон)
- `SimpleDestructionManager` и `LocalizationManager` должны быть на отдельных объектах на сцене, а не на каждом игроке. Например:
  - Создайте GameObject "Managers" на сцене Lobby
  - Добавьте на него `SimpleDestructionManager`, `LocalizationManager`
  - Убедитесь, что эти компоненты удалены с префаба Player

### Проблема: Игровой объект Player не спавнится на сцене Lobby
**Решение:**
- Убедитесь, что `LobbyPlayerSpawner` имеет компонент `NetworkIdentity`
- Проверьте, что `LobbyPlayerSpawner.playerPrefab` назначен (игровой префаб, не LobbyPlayer!)
- Проверьте, что `LobbyPlayerSpawner.spawnPoints` назначены
- Проверьте логи - должны быть сообщения о спавне игроков
- **Убедитесь, что на префабе Player нет синглтонов (см. проблему выше)**

### Проблема: Steam выключается при смене сцены
**Решение:** Это исправлено в коде - `SteamInitializer` использует `DontDestroyOnLoad` и не выключает Steam в `OnDestroy()`.

### Проблема: LobbyPlayer не появляется в списке игроков
**Решение:**
- Убедитесь, что `autoCreatePlayer = true` в `LobbyNetworkManager`
- Проверьте, что `LobbyPlayer` префаб назначен в `LobbyNetworkManager.playerPrefab`
- Проверьте, что `LobbyManager.playerListParent` и `LobbyManager.playerListPrefab` назначены
- Проверьте логи - должны быть сообщения о найденных `LobbyPlayer`

### Проблема: Ошибка компиляции "NetworkIdentity does not contain a definition for..."
**Решение:** Убедитесь, что вы используете актуальную версию Mirror. Некоторые свойства могли измениться в новых версиях.

## Дополнительные настройки

### Настройка точек спавна
- Создайте пустые GameObject'ы на сцене Lobby в нужных позициях
- Назначьте их Transform'ы в массив `LobbyPlayerSpawner.spawnPoints`
- Игроки будут спавниться по индексу подключения: `spawnIndex = connectionId % spawnPoints.Length`

### Настройка цветов игроков
Цвета определены в `LobbyPlayer.PlayerColors` и могут быть изменены в коде:
```csharp
public static readonly Color[] PlayerColors = new Color[]
{
    Color.white,      // 0 - Белый
    Color.red,        // 1 - Красный
    new Color(1f, 0.4f, 0.8f), // 2 - Розовый
    // ... и т.д.
};
```

### Настройка максимального количества игроков
- В `LobbyManager.maxPlayers` - максимальное количество игроков в лобби
- В `LobbyNetworkManager.maxConnections` - максимальное количество подключений (должно совпадать с `maxPlayers`)
- В `LobbyNetworkManager.defaultMaxPlayers` - значение по умолчанию

## Отладка

### Включение подробных логов
Все компоненты используют `Debug.Log` для отладки. Проверьте Console в Unity для диагностики проблем.

### Ключевые логи для проверки:
- `[SteamInitializer] Steam успешно инициализирован` - Steam работает
- `[LobbyManager] Лобби создано успешно!` - лобби создано
- `[LobbyPlayer] OnStartServer вызван` - LobbyPlayer спавнится
- `[LobbyManager] Найдено LobbyPlayer: X` - LobbyPlayer найдены
- `[LobbyNetworkManager] Сервер перешел на сцену: Lobby` - переход на сцену Lobby
- `[LobbyPlayerSpawner] Игрок заспавнен для подключения X` - игровой объект спавнится

