# Исправление ошибок компиляции Steamworks.NET

## Проблема

Ошибки `CS1028: Unexpected preprocessor directive` в файлах Steamworks.NET указывают на проблему с директивами препроцессора в библиотеке.

## Решения

### Решение 1: Использовать официальную версию через Package Manager (РЕКОМЕНДУЕТСЯ)

1. **Удалите текущую установку Steamworks.NET:**
   - Удалите папку `Assets/Steamworks.NET-master` (если она есть)
   - Удалите папку `Assets/Plugins/Steamworks.NET` (если она есть)

2. **Установите через Unity Package Manager:**
   - Откройте Unity
   - Window → Package Manager
   - Нажмите "+" → Add package from git URL
   - Введите: `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net`
   - Дождитесь установки

3. **Проверьте установку:**
   - Убедитесь, что пакет появился в Package Manager
   - Проверьте, что нет ошибок компиляции

### Решение 2: Исправить вручную установленную версию

Если вы установили Steamworks.NET вручную:

1. **Проверьте версию Unity:**
   - Steamworks.NET требует Unity 2019.4 или новее
   - Для Unity 2021.2+ рекомендуется использовать версию через Package Manager

2. **Обновите Steamworks.NET:**
   - Скачайте последнюю версию с [GitHub](https://github.com/rlabrecque/Steamworks.NET/releases)
   - Удалите старую версию
   - Установите новую версию

3. **Проверьте настройки скриптов:**
   - Edit → Project Settings → Player
   - Убедитесь, что "Scripting Backend" установлен правильно (Mono или IL2CPP)
   - Для Windows обычно используется Mono

### Решение 3: Временно отключить Steam (для тестирования)

Если нужно временно отключить Steam для тестирования других функций:

1. **Добавьте символ компиляции:**
   - Edit → Project Settings → Player
   - В разделе "Other Settings" найдите "Scripting Define Symbols"
   - Добавьте: `DISABLESTEAMWORKS`
   - Нажмите "Apply"

2. **Перекомпилируйте проект:**
   - Unity автоматически перекомпилирует проект
   - Ошибки Steamworks.NET должны исчезнуть

3. **Для включения Steam обратно:**
   - Удалите `DISABLESTEAMWORKS` из Scripting Define Symbols

### Решение 4: Исправить файлы Steamworks.NET вручную (НЕ РЕКОМЕНДУЕТСЯ)

⚠️ **Внимание:** Это решение требует редактирования файлов библиотеки. При обновлении Steamworks.NET изменения будут потеряны.

Если вы все же хотите исправить вручную:

1. Найдите файлы с ошибками (например, `gameserveritem_t.cs`)
2. Проверьте директивы препроцессора - они должны быть правильно закрыты
3. Убедитесь, что нет незакрытых `#if` или лишних `#endif`

**Пример проблемы:**
```csharp
#if SOMETHING
// код
// Отсутствует #endif
```

**Правильно:**
```csharp
#if SOMETHING
// код
#endif
```

## Проверка после исправления

1. **Очистите кэш Unity:**
   - Закройте Unity
   - Удалите папку `Library` в корне проекта
   - Откройте проект заново

2. **Проверьте компиляцию:**
   - Дождитесь завершения компиляции
   - Проверьте Console на наличие ошибок

3. **Проверьте работу Steam:**
   - Убедитесь, что Steam клиент запущен
   - Проверьте, что `steam_appid.txt` существует
   - Запустите игру и проверьте логи

## Дополнительная информация

### Версии Unity и совместимость

- **Unity 2019.4+**: Полная поддержка
- **Unity 2020.x**: Полная поддержка
- **Unity 2021.x**: Рекомендуется использовать Package Manager версию
- **Unity 2022.x+**: Только Package Manager версия

### Альтернативные решения

Если проблемы продолжаются:

1. **Используйте Facepunch.Steamworks:**
   - Альтернативная библиотека для Steam
   - Может быть более стабильной в некоторых случаях
   - [GitHub](https://github.com/Facepunch/Facepunch.Steamworks)

2. **Используйте Steamworks.NET через UPM:**
   - Убедитесь, что используете версию через Unity Package Manager
   - Это гарантирует совместимость с вашей версией Unity

## Контакты и поддержка

- [Steamworks.NET Issues](https://github.com/rlabrecque/Steamworks.NET/issues)
- [Steamworks.NET Documentation](https://steamworks.github.io/)
- [Unity Forums](https://forum.unity.com/)

