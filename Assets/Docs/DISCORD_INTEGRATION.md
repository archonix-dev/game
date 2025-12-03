# Интеграция Discord Rich Presence

## Описание

Система Discord Rich Presence интегрирована в проект и автоматически отображает статус игры в Discord. Система отслеживает текущую сцену, количество игроков в лобби и текущую локацию игрока.

## Что отображается в Discord

### На сцене Menu (главное меню):
- **Details**: `localhost`
- **State**: `$sudo players [текущее количество] / [максимальное количество]`

### На сцене Lobby (лобби):
- **Details**: `$sudo players [текущее количество] / [максимальное количество]`
- **State**: `$sudo location main`

### На сцене Main (основная игра):
- **Details**: `$sudo players [текущее количество] / [максимальное количество]`
- **State**: `$sudo location [название локации]` (динамически обновляется при входе в зоны LocationZone)

## Настройка в Unity

### Шаг 1: Добавление DiscordRichPresenceManager на сцену

1. Откройте сцену **Start** (или любую сцену, которая загружается первой)
2. Создайте пустой GameObject:
   - В Hierarchy нажмите правой кнопкой мыши → **Create Empty**
   - Назовите его `DiscordRichPresenceManager`
3. Добавьте компонент `DiscordRichPresenceManager`:
   - Выберите созданный GameObject
   - В Inspector нажмите **Add Component**
   - Найдите и добавьте `DiscordRichPresenceManager`

### Шаг 2: Настройка параметров

В Inspector компонента `DiscordRichPresenceManager` настройте следующие параметры:

#### Discord Settings
- **Client Id**: `1445531932019527690` (уже установлено по умолчанию)
  - Это ваш Application Client ID из Discord Developer Portal
  - Если нужно изменить, получите новый ID на https://discord.com/developers/applications

#### Scene Names
- **Menu Scene Name**: `Menu` (по умолчанию)
- **Lobby Scene Name**: `Lobby` (по умолчанию)
- **Main Scene Name**: `Main` (по умолчанию)
  - Убедитесь, что названия сцен совпадают с реальными именами сцен в проекте

#### Update Settings
- **Update Interval**: `1` (секунды, по умолчанию)
  - Интервал обновления Rich Presence
  - Рекомендуется оставить значение 1 секунда

### Шаг 3: Проверка LocationZone

Убедитесь, что на сцене **Main** есть объекты с компонентом `LocationZone`:

1. Для каждой локации создайте GameObject с:
   - **Collider** (любой тип, например Box Collider)
   - Компонент `LocationZone`
   - В поле `Location Name` укажите название локации (например: "Forest", "City", "Dungeon")

2. Важно:
   - Collider должен быть помечен как **Is Trigger**
   - Collider должен быть достаточно большим, чтобы игрок мог в него войти
   - Название локации должно быть уникальным и понятным

### Шаг 4: Проверка работы

1. Запустите игру в Unity Editor или в Build
2. Убедитесь, что Discord запущен
3. Проверьте свой статус в Discord:
   - Откройте Discord
   - Посмотрите на свой профиль - должен отображаться статус игры
   - При переходе между сценами статус должен обновляться
   - При входе в зоны LocationZone на сцене Main статус должен обновляться

## Технические детали

### Как это работает

1. **Инициализация**: При старте игры `DiscordRichPresenceManager` инициализирует Discord SDK с указанным Client ID
2. **Отслеживание сцен**: Система подписывается на события загрузки сцен и обновляет Rich Presence при смене сцены
3. **Отслеживание игроков**: Система периодически получает количество игроков из `NetworkManager.numPlayers` и максимальное количество из `LobbyManager` или `LobbyNetworkManager`
4. **Отслеживание локаций**: Система подписывается на события `LocationZone.OnLocalPlayerEnterZone` и обновляет локацию при входе игрока в зону

### Структура кода

- **DiscordRichPresenceManager.cs**: Основной менеджер, управляющий Rich Presence
  - Расположен в `Assets/Scripts/Discord/DiscordRichPresenceManager.cs`
  - Использует Discord Game SDK из `Assets/Discord-Game-SDK-master/`

- **LocationZone.cs**: Компонент для определения зон локаций
  - Расположен в `Assets/Scripts/SceneManagement/LocationZone.cs`
  - Генерирует события при входе локального игрока в зону

### Требования

- Discord должен быть запущен на компьютере пользователя
- Игра должна быть запущена (не в режиме редактора, если требуется полная функциональность)
- Discord Game SDK должен быть правильно установлен в проекте

## Устранение неполадок

### Rich Presence не отображается

1. **Проверьте, запущен ли Discord**
   - Discord должен быть запущен и авторизован
   - Перезапустите Discord, если он был запущен до запуска игры

2. **Проверьте Client ID**
   - Убедитесь, что Client ID правильный
   - Проверьте в Discord Developer Portal, что приложение создано и Rich Presence включен

3. **Проверьте логи Unity**
   - Откройте Console в Unity
   - Ищите сообщения от `[DiscordRichPresenceManager]`
   - Если есть ошибки инициализации, проверьте, что Discord SDK правильно установлен

4. **Проверьте, что компонент добавлен на сцену**
   - Убедитесь, что `DiscordRichPresenceManager` добавлен на сцену Start или другую сцену, которая загружается первой
   - Компонент должен иметь `DontDestroyOnLoad`, чтобы сохраняться между сценами

### Количество игроков не обновляется

1. **Проверьте NetworkManager**
   - Убедитесь, что `LobbyNetworkManager` или `NetworkManager` присутствует на сцене
   - Проверьте, что сеть активна (`NetworkServer.active` или `NetworkClient.active`)

2. **Проверьте LobbyManager**
   - Убедитесь, что `LobbyManager` присутствует на сцене
   - Проверьте, что `maxPlayers` установлен правильно

### Локация не обновляется

1. **Проверьте LocationZone**
   - Убедитесь, что на сцене Main есть объекты с компонентом `LocationZone`
   - Проверьте, что Collider помечен как Trigger
   - Проверьте, что `Location Name` заполнен

2. **Проверьте, что игрок имеет NetworkIdentity**
   - Игрок должен иметь компонент `NetworkIdentity`
   - `NetworkIdentity.isLocalPlayer` должен быть `true` для локального игрока

## Дополнительная информация

- Discord Game SDK документация: https://discord.com/developers/docs/game-sdk/sdk-starter-guide
- Discord Developer Portal: https://discord.com/developers/applications

