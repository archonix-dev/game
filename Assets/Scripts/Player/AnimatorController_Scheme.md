# Схема Animator Controller для PlayerController

## Параметры Animator

```
Stance (Integer):
  0 = Standing (Стоя)
  1 = Crouching (Присед)
  2 = Prone (Лежа)

IsMoving (Bool):
  true = Игрок двигается
  false = Игрок стоит на месте

IsRunning (Bool):
  true = Игрок бежит (только для Standing)
  false = Игрок идет или стоит
```

## Структура состояний

```
┌─────────────────────────────────────────────────────────┐
│                    ENTRY (Вход)                         │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
        ┌─────────────────────────┐
        │   Standing Idle         │ ◄─── (Stance == 0 && IsMoving == false)
        │   (Стоя на месте)       │
        └────────┬────────────────┘
                 │
        ┌────────┴────────┐
        │                 │
        ▼                 ▼
┌───────────────┐  ┌───────────────┐
│ Standing Walk │  │ Standing Run  │
│ (Стоя идёт)   │  │ (Стоя бежит)  │
│               │  │               │
│ Stance == 0   │  │ Stance == 0   │
│ IsMoving      │  │ IsMoving      │
│ !IsRunning    │  │ IsRunning     │
└───────┬───────┘  └───────┬───────┘
        │                  │
        └────────┬─────────┘
                 │
                 ▼
        ┌─────────────────────────┐
        │   Crouching Idle        │ ◄─── (Stance == 1 && IsMoving == false)
        │   (Присед на месте)     │
        └────────┬────────────────┘
                 │
                 ▼
        ┌─────────────────────────┐
        │   Crouching Walk        │
        │   (Присед идёт)         │
        │                         │
        │ Stance == 1             │
        │ IsMoving                │
        └───────┬─────────────────┘
                │
                ▼
        ┌─────────────────────────┐
        │   Prone Idle            │ ◄─── (Stance == 2 && IsMoving == false)
        │   (Лежа на месте)       │
        └────────┬────────────────┘
                 │
                 ▼
        ┌─────────────────────────┐
        │   Prone Crawl           │
        │   (Лежа ползёт)         │
        │                         │
        │ Stance == 2             │
        │ IsMoving                │
        └─────────────────────────┘
```

## Детальная схема переходов

### 1. Standing States (Stance == 0)

```
Standing Idle
    │
    ├─→ Standing Walk (Условие: IsMoving == true && IsRunning == false)
    │       │
    │       └─→ Standing Idle (Условие: IsMoving == false)
    │
    └─→ Standing Run (Условие: IsMoving == true && IsRunning == true)
            │
            └─→ Standing Walk (Условие: IsRunning == false)
            │
            └─→ Standing Idle (Условие: IsMoving == false)
```

### 2. Crouching States (Stance == 1)

```
Crouching Idle
    │
    ├─→ Crouching Walk (Условие: IsMoving == true)
    │       │
    │       └─→ Crouching Idle (Условие: IsMoving == false)
    │
    └─→ Standing Idle (Условие: Stance == 0)
```

### 3. Prone States (Stance == 2)

```
Prone Idle
    │
    ├─→ Prone Crawl (Условие: IsMoving == true)
    │       │
    │       └─→ Prone Idle (Условие: IsMoving == false)
    │
    └─→ Standing Idle (Условие: Stance == 0)
```

## Условия переходов

### Из любого Standing состояния:
- **→ Crouching Idle**: `Stance == 1`
- **→ Prone Idle**: `Stance == 2`

### Из любого Crouching состояния:
- **→ Standing Idle**: `Stance == 0`
- **→ Prone Idle**: `Stance == 2`

### Из любого Prone состояния:
- **→ Standing Idle**: `Stance == 0`
- **→ Crouching Idle**: `Stance == 1`

### Внутренние переходы Standing:
- **Idle → Walk**: `IsMoving == true && IsRunning == false`
- **Idle → Run**: `IsMoving == true && IsRunning == true`
- **Walk → Run**: `IsRunning == true`
- **Run → Walk**: `IsRunning == false`
- **Walk → Idle**: `IsMoving == false`
- **Run → Idle**: `IsMoving == false`

### Внутренние переходы Crouching:
- **Idle → Walk**: `IsMoving == true`
- **Walk → Idle**: `IsMoving == false`

### Внутренние переходы Prone:
- **Idle → Crawl**: `IsMoving == true`
- **Crawl → Idle**: `IsMoving == false`

## Настройка в Unity Animator

### Шаги настройки:

1. **Создайте параметры:**
   - `Stance` (Int): 0-2
   - `IsMoving` (Bool)
   - `IsRunning` (Bool)

2. **Создайте состояния:**
   - Standing Idle
   - Standing Walk
   - Standing Run
   - Crouching Idle
   - Crouching Walk
   - Prone Idle
   - Prone Crawl

3. **Назначьте анимации:**
   - Присвойте соответствующие Animation Clips каждому состоянию

4. **Создайте переходы:**
   - Используйте условия, указанные выше
   - Рекомендуется: Exit Time = false, Transition Duration = 0.1-0.2

5. **Настройте переходы между стойками:**
   - Добавьте переходы от любого состояния одной стойки к Idle другой стойки
   - Условие: только изменение `Stance`

## Пример настройки переходов

```
Standing Idle ──[Stance == 1]──> Crouching Idle
Standing Walk ──[Stance == 1]──> Crouching Idle
Standing Run ──[Stance == 1]──> Crouching Idle

Crouching Idle ──[Stance == 0]──> Standing Idle
Crouching Walk ──[Stance == 0]──> Standing Idle
Crouching Idle ──[Stance == 2]──> Prone Idle
Crouching Walk ──[Stance == 2]──> Prone Idle

Prone Idle ──[Stance == 0]──> Standing Idle
Prone Crawl ──[Stance == 0]──> Standing Idle
Prone Idle ──[Stance == 1]──> Crouching Idle
Prone Crawl ──[Stance == 1]──> Crouching Idle
```

## Важные замечания

1. **Exit Time**: Рекомендуется отключить для всех переходов, чтобы анимации переключались мгновенно при изменении параметров
2. **Transition Duration**: 0.1-0.2 секунды для плавности
3. **Has Exit Time**: Отключить для всех переходов
4. **Interruption Source**: None (чтобы переходы происходили сразу)
5. **Transition Offset**: 0 (не использовать смещение)

