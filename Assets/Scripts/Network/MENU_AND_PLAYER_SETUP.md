# Инструкция по настройке Меню и Префаба Игрока

Эта инструкция поможет настроить меню и префаб игрока для полной функциональности мультиплеера.

## 1. Настройка Префаба Игрока

### Требования к префабу игрока:

1. **NetworkIdentity компонент**
   - Префаб игрока ДОЛЖЕН иметь компонент `NetworkIdentity`
   - В инспекторе NetworkIdentity:
     - `Server Only` = **отключено** (unchecked)
     - `Local Player Authority` = **включено** (checked) - для управления владельцем

2. **NetworkPlayer компонент**
   - Добавьте компонент `NetworkPlayer` к префабу
   - В инспекторе NetworkPlayer настройте:
     - `Player Controller` - ссылка на компонент `PlayerController`
     - `Network Transform` - ссылка на компонент `ClientNetworkTransform`
     - `Player Camera` - ссылка на Camera (обычно в дочернем объекте)
     - `Audio Listener` - ссылка на AudioListener (обычно в дочернем объекте)
     - `Player Name` - имя игрока по умолчанию (будет перезаписано из Steam/PlayerPrefs)
     - `Player Color` - цвет игрока по умолчанию
     - `Player Model Objects` - массив GameObject, которые представляют модель игрока (голова, тело и т.д.)
       - Эти объекты будут скрыты для владельца игрока в игре
     - `Lobby Scene Name` - имя сцены лобби (по умолчанию "Lobby")

3. **ClientNetworkTransform компонент**
   - Добавьте компонент `ClientNetworkTransform` для синхронизации позиции
   - Компонент необходим для сетевой синхронизации движения игрока

4. **PlayerController компонент**
   - Убедитесь, что компонент `PlayerController` присутствует
   - Этот компонент будет включен только для владельца игрока

5. **Camera и AudioListener**
   - Camera должна быть в дочернем объекте
   - AudioListener должен быть в дочернем объекте
   - Они будут включены только для владельца игрока

### Пример структуры префаба:
```
PlayerPrefab (GameObject)
├── NetworkIdentity (компонент)
├── NetworkPlayer (компонент)
├── ClientNetworkTransform (компонент)
├── PlayerController (компонент)
├── [Модель игрока - объекты для скрытия/показа]
│   ├── Head (GameObject)
│   ├── Body (GameObject)
│   └── ...
└── Camera (GameObject)
    ├── Camera (компонент)
    └── AudioListener (компонент)
```

## 2. Настройка MirrorNetworkManager

### В сцене меню (Menu):

1. **Создайте GameObject с MirrorNetworkManager**
   - Добавьте компонент `MirrorNetworkManager` (наследуется от Mirror.NetworkManager)
   - Настройте в инспекторе:
     - `Max Connections` = **8** (или нужное вам максимальное количество игроков)
     - `Offline Scene` = **Menu** (имя сцены меню)
     - `Online Scene` = **Lobby** (имя сцены лобби, где создаются игроки)

2. **Настройка Player Prefab (ОПЦИОНАЛЬНО)**
   - `Player Prefab` в MirrorNetworkManager можно оставить **пустым**
   - Спавн игроков управляется через `GameManager` в игровой сцене
   - Если хотите использовать автоматический спавн Mirror, назначьте префаб игрока здесь

3. **Добавьте FizzySteamworks транспорт**
   - Убедитесь, что на объекте с MirrorNetworkManager есть компонент `FizzySteamworks`
   - Этот компонент должен быть настроен для работы со Steam

4. **Настройте Steam App ID**
   - В MirrorNetworkManager настройте `Steam App ID`
   - По умолчанию используется 480 (Spacewar для тестирования)
   - Замените на свой App ID в продакшене

## 3. Настройка GameManager в игровой сцене

### В игровой сцене (Lobby или другая игровая сцена):

1. **Создайте GameObject с GameManager**
   - Добавьте компонент `GameManager`
   - Добавьте компонент `NetworkIdentity`
   - В инспекторе GameManager настройте:
     - `Game Scene Name` = **"Lobby"** (или имя вашей игровой сцены)
     - `Menu Scene Name` = **"Menu"** (имя сцены меню)
     - `Player Prefab` = **ссылка на ваш префаб игрока**
     - `Current Game Scene Name` = **"Lobby"** (имя текущей игровой сцены)
     - `Spawn Point` = **Transform** точки спавна игроков (создайте пустой GameObject и разместите его в нужном месте)

2. **GameManager должен быть заспавнен на сервере**
   - MirrorNetworkManager автоматически заспавнит объекты с NetworkIdentity при загрузке сцены
   - Или вы можете заспавнить его вручную в коде

## 4. Настройка LobbyManager в сцене меню

### В сцене меню (Menu):

1. **Создайте GameObject с LobbyManager**
   - Добавьте компонент `LobbyManager`
   - В инспекторе настройте все необходимые ссылки:

2. **Настройка кнопок:**
   - `Play Button` - кнопка "Играть" (создает лобби)
   - `Lobby Settings Button` - кнопка настроек лобби (только для хоста)
   - `Start Game Button` - кнопка "Начать игру" (только для хоста)
   - `Connect To Lobby Button` - кнопка подключения к лобби
   - `Color Selection Button` - кнопка выбора цвета игрока

3. **Настройка UI панелей:**
   - `Lobby Settings Panel` - панель настроек лобби
   - `Connect To Lobby Panel` - панель подключения к лобби
   - `Color Selection Panel` - панель выбора цвета

4. **Настройка отображения игроков:**
   - `Players List Container` - Transform контейнера для списка игроков в лобби
   - `Player Lobby Prefab` - префаб UI элемента игрока в лобби (должен иметь компонент `PlayerLobbyItem`)

5. **Настройки сети:**
   - `Default Port` = **7777** (для FizzySteamworks не используется, но можно оставить)
   - `Max Players` = **8** (максимальное количество игроков)

6. **Настройка загрузки сцены:**
   - `Scene Loader` - ссылка на компонент `AsyncSceneLoaderWithAnimation` (опционально)
   - `Game Scene Name` = **"Lobby"** (или имя вашей игровой сцены)

## 5. Настройка сцен

### Сцена Menu (меню):

1. **Не забудьте добавить:**
   - MirrorNetworkManager (GameObject с компонентом MirrorNetworkManager)
   - LobbyManager (GameObject с компонентом LobbyManager)
   - SteamInitializer (если используется Steam)
   - Все необходимые UI элементы

2. **Сцена должна быть в Build Settings**

### Сцена Lobby (или другая игровая сцена):

1. **Не забудьте добавить:**
   - GameManager (GameObject с компонентами GameManager и NetworkIdentity)
   - Spawn Point (GameObject для точки спавна игроков)
   - Все игровые объекты

2. **ВАЖНО: Сохранение сцены**
   - После добавления GameManager в сцену, **ОБЯЗАТЕЛЬНО откройте сцену Lobby в Unity редакторе и сохраните её**
   - Unity покажет предупреждение: "Scene Assets/Lobby.unity needs to be opened and resaved"
   - Это необходимо для того, чтобы Mirror правильно идентифицировал GameManager как сетевой объект
   - Откройте сцену: `File > Open Scene` → выберите `Lobby.unity`
   - Сохраните сцену: `File > Save` или `Ctrl+S`

3. **Сцена должна быть в Build Settings**

4. **В Build Settings порядок сцен:**
   - Menu должна быть сценой 0 (первая загружаемая сцена)
   - Lobby (игровая сцена) должна быть добавлена после Menu

## 6. Проверка настройки

### Проверьте следующее:

1. **Префаб игрока:**
   - ✅ Имеет компонент NetworkIdentity с включенным Local Player Authority
   - ✅ Имеет компонент NetworkPlayer
   - ✅ Имеет компонент ClientNetworkTransform
   - ✅ Имеет компонент PlayerController
   - ✅ Имеет Camera и AudioListener в дочерних объектах
   - ✅ Player Model Objects назначены правильно

2. **MirrorNetworkManager:**
   - ✅ Настроен в сцене Menu
   - ✅ Имеет компонент FizzySteamworks
   - ✅ Steam App ID настроен (если используется Steam)

3. **GameManager:**
   - ✅ Настроен в игровой сцене
   - ✅ Имеет компонент NetworkIdentity
   - ✅ Player Prefab назначен
   - ✅ Spawn Point назначен

4. **LobbyManager:**
   - ✅ Настроен в сцене Menu
   - ✅ Все кнопки назначены
   - ✅ Все панели назначены
   - ✅ Player Lobby Prefab назначен
   - ✅ Players List Container назначен

## 7. Важные замечания

1. **Спавн игроков:**
   - Игроки НЕ спавнятся в сцене Menu
   - Игроки спавнятся только в игровой сцене (Lobby) через GameManager
   - Когда хост нажимает "Начать игру", сцена загружается для всех игроков, и затем GameManager спавнит игроков

2. **NetworkIdentity:**
   - Все объекты, которые должны быть синхронизированы по сети, должны иметь NetworkIdentity
   - Префаб игрока ОБЯЗАТЕЛЬНО должен иметь NetworkIdentity

3. **Steam интеграция:**
   - Убедитесь, что Steam инициализирован до создания лобби
   - SteamInitializer должен быть настроен и запущен перед использованием мультиплеера

4. **Синхронизация данных:**
   - Имя и цвет игрока синхронизируются через NetworkPlayer компонент
   - Данные сохраняются в PlayerPrefs и загружаются при спавне

## 8. Типичные проблемы и решения

### Проблема: Игроки не спавнятся
**Решение:**
- Проверьте, что Player Prefab назначен в GameManager
- Проверьте, что GameManager имеет NetworkIdentity
- Проверьте, что вы находитесь в игровой сцене (не в меню)
- Проверьте логи на наличие ошибок

### Проблема: Игрок видит свою модель
**Решение:**
- Убедитесь, что Player Model Objects назначены в NetworkPlayer
- Проверьте, что модель правильно скрывается для владельца в методе UpdatePlayerModelVisibility

### Проблема: Камера не работает
**Решение:**
- Проверьте, что Camera назначена в NetworkPlayer
- Убедитесь, что Camera находится в дочернем объекте
- Проверьте, что SetupOwnerPlayer вызывается для владельца

### Проблема: Управление не работает
**Решение:**
- Проверьте, что PlayerController назначен в NetworkPlayer
- Убедитесь, что PlayerController включен только для владельца (isOwned)

### Проблема: Игроки не видят друг друга
**Решение:**
- Проверьте, что NetworkIdentity присутствует на префабе игрока
- Убедитесь, что ClientNetworkTransform настроен правильно
- Проверьте, что игроки заспавнены через NetworkServer.Spawn

### Проблема: "State comes from an incompatible keyword space" ошибка
**Причина:**
- Эта ошибка возникает когда материалы используют несовместимые шейдеры
- Например, материал с шейдером `Custom/AdvancedOutline` и материал с `Universal Render Pipeline/Lit` имеют разные keyword spaces
- Это может происходить в DestructibleObject при получении материалов

**Решение:**
- Код уже исправлен для безопасной обработки несовместимых материалов
- Если ошибка все еще возникает, убедитесь, что все материалы на объектах используют совместимые шейдеры
- Проверьте, что `outlineMaterial` и `normalMaterial` в DestructibleObject используют совместимые шейдеры с основными материалами

### Проблема: "Steamworks is not initialized" при выключении
**Причина:**
- Эта ошибка возникает при выключении приложения, когда FizzySteamworks пытается закрыть сокеты, но Steam уже не инициализирован
- Происходит в `OnApplicationQuit`

**Решение:**
- Код уже исправлен в MirrorNetworkManager для безопасной обработки выключения
- Теперь проверяется, что Steam инициализирован перед попыткой закрыть соединения
- Если Steam не инициализирован, соединения закрываются без вызова Shutdown транспорта

### Проблема: "Scene needs to be opened and resaved"
**Решение:**
- Откройте сцену Lobby в Unity редакторе: `File > Open Scene` → выберите `Lobby.unity`
- Сохраните сцену: `File > Save` или `Ctrl+S`
- Это необходимо для правильной работы Mirror с сетевыми объектами в сцене

## 9. Дополнительная информация

Для более подробной информации о настройке Mirror Networking см.:
- `MIRROR_MIGRATION_STATUS.md` - статус миграции на Mirror
- `STEAM_INTEGRATION_INSTRUCTIONS.md` - инструкции по интеграции Steam
- `TESTING_MULTIPLAYER_LOCAL.md` - инструкции по локальному тестированию

