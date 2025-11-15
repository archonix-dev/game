# Схема подключения анимаций захвата предметов

## Описание анимаций

1. **grabstart** - Анимация начала захвата предмета (проигрывается один раз при начале захвата)
2. **grabhold** - Цикличная анимация удержания предмета (проигрывается постоянно, пока предмет удерживается)
3. **grabrelease** - Анимация отпускания предмета (проигрывается один раз при отпускании)

## Схема подключения в Unity Animator Controller

```
┌─────────────────────────────────────────────────────────────────┐
│                         ANIMATOR CONTROLLER                      │
│                                                                  │
│  ┌──────────────┐                                               │
│  │   Entry      │                                               │
│  └──────┬───────┘                                               │
│         │                                                        │
│         ▼                                                        │
│  ┌──────────────┐                                               │
│  │    idle      │ ◄──────┐                                      │
│  └──────┬───────┘        │                                      │
│         │                 │                                      │
│         │ Trigger:        │                                      │
│         │ grabstart       │                                      │
│         │                 │                                      │
│         ▼                 │                                      │
│  ┌──────────────┐        │                                      │
│  │  grabstart   │        │                                      │
│  │  (NOT LOOP)  │        │                                      │
│  └──────┬───────┘        │                                      │
│         │                 │                                      │
│         │ Has Exit Time:  │                                      │
│         │ TRUE            │                                      │
│         │ Exit Time: 0.2  │                                      │
│         │                 │                                      │
│         ▼                 │                                      │
│  ┌──────────────┐        │                                      │
│  │  grabhold    │◄────────┘                                      │
│  │  (LOOP)      │        │                                      │
│  │  Bool: true  │        │                                      │
│  └──────┬───────┘        │                                      │
│         │                 │                                      │
│         │ Bool: false     │                                      │
│         │                 │                                      │
│         ▼                 │                                      │
│  ┌──────────────┐        │                                      │
│  │ grabrelease  │        │                                      │
│  │  (NOT LOOP)  │        │                                      │
│  └──────┬───────┘        │                                      │
│         │                 │                                      │
│         │ Has Exit Time:  │                                      │
│         │ TRUE            │                                      │
│         │                 │                                      │
│         ▼                 │                                      │
│  ┌──────────────┐        │                                      │
│  │    idle      │────────┘                                      │
│  └──────────────┘                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Пошаговая настройка в Unity

### Шаг 1: Создание параметров в Animator Controller

1. Откройте Animator Controller
2. В окне **Parameters** создайте следующие параметры:

| Тип | Имя | Значение по умолчанию |
|-----|-----|----------------------|
| **Trigger** | `grabstart` | - |
| **Bool** | `grabhold` | `false` |
| **Trigger** | `grabrelease` | - |

### Шаг 2: Создание состояний (States)

1. Создайте 3 состояния для анимаций:
   - **grabstart** (Any State → grabstart)
   - **grabhold** (новое состояние)
   - **grabrelease** (grabhold → grabrelease)

### Шаг 3: Настройка состояния `grabstart`

**Основные настройки:**
- **Motion**: Перетащите анимацию `grabstart`
- **Speed**: 1.0
- **Loop**: ❌ **ВЫКЛЮЧЕНО** (Has Exit Time должен быть включен)

**Переходы (Transitions):**

#### Переход: `idle` → `grabstart`
- **Conditions**: 
  - `grabstart` (Trigger)
- **Has Exit Time**: ❌ **ВЫКЛЮЧЕНО**
- **Transition Duration**: 0.0

#### Переход: `grabstart` → `grabhold`
- **Conditions**: 
  - `grabhold` (Bool) = `true`
- **Has Exit Time**: ✅ **ВКЛЮЧЕНО**
- **Exit Time**: `0.2` (или время, когда анимация должна перейти к циклу)
- **Transition Duration**: 0.1
- **Interruption Source**: None

### Шаг 4: Настройка состояния `grabhold`

**Основные настройки:**
- **Motion**: Перетащите анимацию `grabhold`
- **Speed**: 1.0
- **Loop**: ✅ **ВКЛЮЧЕНО**

**Переходы (Transitions):**

#### Переход: `grabstart` → `grabhold`
- **Conditions**: 
  - `grabhold` (Bool) = `true`
- **Has Exit Time**: ✅ **ВКЛЮЧЕНО**
- **Exit Time**: `0.2`
- **Transition Duration**: 0.1

#### Переход: `grabhold` → `grabrelease`
- **Conditions**: 
  - `grabhold` (Bool) = `false`
- **Has Exit Time**: ❌ **ВЫКЛЮЧЕНО**
- **Transition Duration**: 0.1

### Шаг 5: Настройка состояния `grabrelease`

**Основные настройки:**
- **Motion**: Перетащите анимацию `grabrelease`
- **Speed**: 1.0
- **Loop**: ❌ **ВЫКЛЮЧЕНО**

**Переходы (Transitions):**

#### Переход: `grabhold` → `grabrelease`
- **Conditions**: 
  - `grabhold` (Bool) = `false`
- **Has Exit Time**: ❌ **ВЫКЛЮЧЕНО**
- **Transition Duration**: 0.1

#### Переход: `grabrelease` → `idle`
- **Conditions**: 
  - (нет условий, только Has Exit Time)
- **Has Exit Time**: ✅ **ВКЛЮЧЕНО**
- **Exit Time**: `1.0` (или когда анимация завершится)
- **Transition Duration**: 0.2

## Альтернативная схема (через Any State)

Если нужно, чтобы переходы работали из любого состояния:

```
Any State → grabstart (Trigger: grabstart)
grabstart → grabhold (Bool: grabhold = true, Has Exit Time: 0.2)
grabhold → grabrelease (Bool: grabhold = false)
grabrelease → idle (Has Exit Time: 1.0)
```

## Важные моменты

1. **Порядок проверки условий**: Unity проверяет переходы сверху вниз. Убедитесь, что более специфичные условия идут первыми.

2. **Has Exit Time для grabstart**: Должен быть включен, чтобы анимация успела проиграться перед переходом к `grabhold`.

3. **Loop для grabhold**: Должна быть включена, чтобы анимация повторялась, пока предмет удерживается.

4. **Быстрый переход**: Если игрок быстро захватывает и отпускает предмет, убедитесь, что переходы настроены правильно.

5. **Приоритет переходов**: Если используется Any State, убедитесь, что переходы из конкретных состояний имеют более высокий приоритет.

## Проверка работы

1. Запустите игру
2. Захватите предмет - должна проиграться `grabstart`, затем через 0.2 сек перейти к `grabhold`
3. Удерживайте предмет - должна цикличная проигрываться `grabhold`
4. Отпустите предмет - должна проиграться `grabrelease`, затем вернуться к `idle`

## Пример настроек переходов (детально)

### Переход 1: idle → grabstart
```
Settings:
  - Has Exit Time: FALSE
  - Fixed Duration: TRUE
  - Transition Duration: 0.0
  - Transition Offset: 0.0
  
Conditions:
  - grabstart (Trigger)
```

### Переход 2: grabstart → grabhold
```
Settings:
  - Has Exit Time: TRUE
  - Exit Time: 0.2
  - Fixed Duration: TRUE
  - Transition Duration: 0.1
  - Transition Offset: 0.0
  
Conditions:
  - grabhold (Bool) = true
```

### Переход 3: grabhold → grabrelease
```
Settings:
  - Has Exit Time: FALSE
  - Fixed Duration: TRUE
  - Transition Duration: 0.1
  - Transition Offset: 0.0
  
Conditions:
  - grabhold (Bool) = false
```

### Переход 4: grabrelease → idle
```
Settings:
  - Has Exit Time: TRUE
  - Exit Time: 1.0
  - Fixed Duration: TRUE
  - Transition Duration: 0.2
  - Transition Offset: 0.0
  
Conditions:
  - (нет условий)
```

## Примечания

- Все анимации должны быть импортированы в проект
- Убедитесь, что анимации имеют правильную длительность
- В коде используются названия параметров по умолчанию: `"grabstart"`, `"grabhold"`, `"grabrelease"`
- Если вы изменили названия в коде, убедитесь, что они совпадают с названиями в Animator Controller

