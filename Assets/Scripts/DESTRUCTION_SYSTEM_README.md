# Система Разрушения Объектов

Полная система разрушения объектов с выпадением монет и интеграцией с MeshSlicer.

## 📁 Созданные Файлы

1. **DestructibleObjectData.cs** - ScriptableObject для настройки параметров разрушаемых объектов
2. **DestructibleObject.cs** - Компонент для объектов которые можно разрушить
3. **CoinManager.cs** - Singleton менеджер для управления монетами
4. **CoinUI.cs** - UI компонент для отображения количества монет
5. **GrabbableObject.cs** - Обновлен для детекции ударов по разрушаемым объектам

## 🎮 Как Использовать

### Шаг 1: Создание ScriptableObject с параметрами объекта

1. В Unity Editor: `ПКМ → Create → Game → Destructible Object Data`
2. Настройте параметры:
   - **Hits To Destroy** - количество ударов до разрушения
   - **Minimum Impact Force** - минимальная сила удара
   - **Min/Max Coins** - диапазон выпадаемых монет
   - **Material Type** - тип материала для MeshSlicer

### Шаг 2: Настройка разрушаемого объекта

1. Выберите объект в сцене
2. Добавьте компонент `DestructibleObject`
3. Перетащите созданный ScriptableObject в поле `Object Data`
4. Убедитесь что у объекта есть:
   - **Rigidbody** (добавится автоматически)
   - **Collider**
   - **MeshFilter** и **MeshRenderer**

### Шаг 3: Настройка объекта для ударов (оружие/инструмент)

1. Выберите объект который будет использоваться для ударов
2. Убедитесь что у него есть компонент `GrabbableObject`
3. Настройте вес объекта (влияет на силу удара)
4. Убедитесь что у объекта есть **Collider** и **Rigidbody**

### Шаг 4: Настройка UI монет

#### Вариант A: Использование стандартного UI Text

1. Создайте UI Canvas: `ПКМ → UI → Canvas`
2. Создайте Text: `ПКМ на Canvas → UI → Text`
3. Создайте пустой GameObject в сцене
4. Добавьте компонент `CoinManager`
5. Перетащите Text в поле `Coins Text`
6. Добавьте на Text компонент `CoinUI`
7. Перетащите Text в поле `Coins Text` в CoinUI

#### Вариант B: Использование TextMeshPro (рекомендуется)

1. Создайте UI Canvas
2. Создайте TextMeshPro: `ПКМ на Canvas → UI → Text - TextMeshPro`
3. В CoinManager перетащите TMP текст в поле `Coins TMP Text`
4. В CoinUI также перетащите TMP текст в поле `Coins TMP Text`

### Шаг 5: Настройка MeshCutterManager

1. Создайте пустой GameObject в сцене: `GameObject → Create Empty`
2. Назовите его "MeshCutterManager"
3. Добавьте компонент `MeshCutterManager`
4. Создайте пустой GameObject: "ObjectMaterialManager"
5. Добавьте компонент `ObjectMaterialManager`

**ВАЖНО:** Для работы ObjectMaterialManager нужен JSON файл с материалами:
- Путь: `StreamingAssets/material_types.json`

Пример `material_types.json`:
```json
{
  "materials": [
    {
      "type": "BEDROCK",
      "can_shatter": false,
      "strength": 999,
      "density": 100
    },
    {
      "type": "GLASS",
      "can_shatter": true,
      "strength": 10,
      "density": 20
    },
    {
      "type": "CONCRETE",
      "can_shatter": true,
      "strength": 50,
      "density": 80
    }
  ]
}
```

## ⚙️ Параметры DestructibleObjectData

### Настройки Разрушения
- **Hits To Destroy** - сколько раз нужно ударить объект
- **Minimum Impact Force** - минимальная сила удара для засчитывания
- **Use Realistic Destruction** - использовать MeshSlicer для реалистичного разрушения
- **Shatter Amount** - количество разрезов при разрушении

### Награды
- **Min Coins** - минимум монет
- **Max Coins** - максимум монет

### Визуальные Эффекты
- **Hit Effect Prefab** - эффект при ударе
- **Destroy Effect Prefab** - эффект при разрушении

### Звуки
- **Hit Sound** - звук удара
- **Destroy Sound** - звук разрушения

### Материал
- **Material Type** - тип материала (GLASS, CONCRETE, BEDROCK)

## 🎯 Как Это Работает

### Механика Ударов

1. Игрок берет предмет (`GrabbableObject`)
2. Бросает/бьет им по разрушаемому объекту (`DestructibleObject`)
3. Система вычисляет силу удара: `сила = скорость × масса`
4. Если сила >= `MinimumImpactForce`, удар засчитывается
5. После `HitsToDestroy` ударов объект разрушается

### Механика Разрушения

**С MeshSlicer (реалистичное):**
- Объект разрезается на множество кусков
- Куски разлетаются в стороны
- Применяется физика

**Без MeshSlicer (простое):**
- Создаются простые кубические осколки
- Осколки имитируют разрушение

### Система Монет

1. При разрушении объект выдает случайное количество монет
2. `CoinManager` обновляет счетчик
3. `CoinUI` отображает новое количество с анимацией
4. Монеты сохраняются в PlayerPrefs

## 🔧 Программный Доступ

### DestructibleObject

```csharp
// Нанести удар вручную
DestructibleObject obj = GetComponent<DestructibleObject>();
obj.TakeHit(force: 50f, impactPoint: Vector3.zero, impactDirection: Vector3.down);

// Получить информацию
int hits = obj.GetCurrentHits();
int remaining = obj.GetRemainingHits();

// Сбросить счетчик
obj.ResetHits();

// Показать/скрыть health bar
obj.SetHealthBarVisibility(true);
```

### CoinManager

```csharp
// Добавить монеты
CoinManager.Instance.AddCoins(10);

// Потратить монеты
bool success = CoinManager.Instance.SpendCoins(5);

// Получить количество
int coins = CoinManager.Instance.GetCoins();

// Проверить наличие
bool hasEnough = CoinManager.Instance.HasEnoughCoins(100);

// Установить количество
CoinManager.Instance.SetCoins(50);

// Сбросить
CoinManager.Instance.ResetCoins();
```

### CoinUI

```csharp
// Изменить префикс/суффикс
CoinUI ui = GetComponent<CoinUI>();
ui.SetPrefix("Золото: ");
ui.SetSuffix(" шт");
```

## 📊 Пример Настройки

### Деревянный Ящик (легко разрушаемый)
- Hits To Destroy: **2**
- Minimum Impact Force: **10**
- Min Coins: **1**
- Max Coins: **3**
- Material Type: **CONCRETE**

### Каменная Стена (прочная)
- Hits To Destroy: **8**
- Minimum Impact Force: **30**
- Min Coins: **5**
- Max Coins: **15**
- Material Type: **CONCRETE**

### Стеклянная Ваза (хрупкая)
- Hits To Destroy: **1**
- Minimum Impact Force: **5**
- Min Coins: **2**
- Max Coins: **5**
- Material Type: **GLASS**

## 🐛 Отладка

### Объект не получает урон
- Проверьте что у оружия есть `GrabbableObject` с `Rigidbody`
- Проверьте что сила удара >= `MinimumImpactForce`
- Проверьте Console для сообщений о силе удара

### MeshSlicer не работает
- Убедитесь что `MeshCutterManager` в сцене
- Убедитесь что у объекта есть `MeshFilter`
- Проверьте что `UseRealisticDestruction = true`
- Проверьте наличие `material_types.json`

### Монеты не отображаются
- Убедитесь что `CoinManager` в сцене
- Проверьте что UI Text привязан
- Проверьте Console на ошибки

### Объект не разрушается после нужного количества ударов
- Проверьте `HitsToDestroy` в ScriptableObject
- Проверьте Console: выводится количество ударов
- Используйте `obj.GetCurrentHits()` для отладки

## 💡 Советы

1. **Используйте разные ScriptableObject** для разных типов объектов
2. **Тестируйте силу ударов** через Console logs
3. **Добавьте визуальные эффекты** (частицы, звуки) для лучшей обратной связи
4. **Используйте Health Bar** для важных объектов
5. **Настройте веса объектов** для реалистичной физики

## 🎨 Улучшения (опционально)

- Добавьте визуальные повреждения (трещины) после каждого удара
- Создайте разные звуки для разных материалов
- Добавьте систему прочности инструментов
- Реализуйте критические удары
- Добавьте комбо-систему для последовательных ударов

---

**Автор:** AI Assistant  
**Дата:** 2025  
**Версия:** 1.0

