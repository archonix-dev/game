## UI Fisheye для URP через Shader Graph

Ниже — пошаговая инструкция, как собрать fisheye‑эффект для UI (Screen Space ‑ Overlay) с помощью **Shader Graph** в URP и подключить его к уже существующему скрипту `UIFisheyeEffect`.

---

### 1. Подготовка Shader Graph

1. В Unity в Project:
   - ПКМ → **Create → Shader → Universal Render Pipeline → Sprite Unlit Graph**  
     (можно и Unlit Graph, но для UI‑спрайтов удобен Sprite Unlit).
2. Назови граф, например, **`UI_Fisheye_Overlay_SG`**.
3. Дважды кликни по графу, чтобы открыть его в Shader Graph.

---

### 2. Настройка свойств (Properties)

Создай следующие **Properties** в Blackboard (слева):

1. **_MainTex**
   - Тип: **Texture2D**
   - Reference: `_MainTex` (так же, как у стандартных UI шейдеров)
   - Mode: Default

2. **_Color**
   - Тип: **Color**
   - Reference: `_Color`
   - Mode: Default
   - HDR опционально (обычно не обязательно)

3. **_Strength**
   - Тип: **Float**
   - Reference: `_Strength`
   - Default: `0.4`
   - Mode: **Slider**
   - Min: `0`, Max: `1`

4. **_Center**
   - Тип: **Vector2**
   - Reference: `_Center`
   - Default: `(0.5, 0.5)`  
     (центр экрана по умолчанию)

Эти же имена/Reference можно использовать в `UIFisheyeEffect`, если захочешь управлять параметрами через материал напрямую (скрипту от этого хуже не станет).

---

### 3. Узлы для чтения текстуры и базового цвета

1. **Sample Texture 2D**
   - Перетащи свойство `_MainTex` в граф и подключи его к входу **Texture**.
   - Для UV пока можно подключить **`Sprite UV`** (см. ниже) или просто `UV` из фрагментного пространства.

2. **Sprite Color / Vertex Color** (опционально)
   - Если граф создан как Sprite Unlit, у Master Stack уже есть вход **Color**.
   - Позже к нему мы подадим результат `Sample Texture 2D` × `_Color`.

Схема:

- `_MainTex` → **Sample Texture 2D (Texture)**  
- `Sample Texture 2D.RGBA` → (будет умножаться на `_Color`)

---

### 4. Получение экранных координат

Нам нужны нормализованные координаты **по экрану** (0..1), чтобы искажение было одинаковым для всех UI‑элементов (Screen Space ‑ Overlay).

1. **Screen Position Node**
   - Добавь узел **Screen Position**.
   - В настройках узла (Inspector):
     - Mode: **Default** или **Raw**.
   - Выход **Screen Position** имеет тип `Vector4`:
     - Если Mode = Default, XY уже в диапазоне 0..1 в зависимости от контекста.
   - Для универсальности:
     - Возьми **Screen Position** → Node `Split` (R,G,B,A) или просто `Screen Position.xy`.
     - Если координаты не нормализованы, можно разделить на `_ScreenParams.xy`, но в URP UI, как правило, `Screen Position` уже даёт нормализованные UV.  
       Для надёжности:

2. **Screen / Resolution (опционально)**
   - Можно получить размер экрана через **`Screen Position`** (Mode = Raw) и `_ScreenParams` через Custom Function, но в большинстве случаев UI‑граф корректно работает с Mode = Default как 0..1.

Итог:  
- Обозначим нормализованные экранные UV как `UV_Screen` = `ScreenPosition.xy` (0..1).

---

### 5. Вычисление смещения для fisheye

Нужные узлы и шаги:

1. **Center**  
   - Перетащи свойство `_Center` на граф, у него тип Vector2.
   - Это `Center` (0..1) — центр искажения.

2. **Delta = UV_Screen − Center**
   - Добавь узел **Subtract**:
     - A: `UV_Screen` (из `Screen Position.xy`)
     - B: `_Center`
     - Результат → `Delta` (Vector2).

3. **r = length(Delta)**
   - Узел **Length**:
     - Input: `Delta`
     - Output: `r` (Float).

4. **Нормализованное направление `dir = Delta / max(r, ε)`**
   - Узел **Divide**:
     - A: `Delta`
     - B: `r` (подключить как Float), НО:
       - Перед делением добавь узел **Max**:  
         - A: `r`, B: очень маленькое значение, например `0.0001` (узел **Float**).
         - Output Max → B узла Divide.
   - Результат Divide → `dir` (Vector2).

5. **Коэффициент искажения**
   - Перетащи `_Strength` в граф (Float).
   - Узел **Saturate** для `_Strength` (ограничение 0..1).
   - Узел **Multiply**:
     - A: `Saturate(_Strength)`
     - B: константа `0.75` (Float)
     - Output → `k`.

6. **Новый радиус `nr = pow(r, 1 + k*2)`**
   - Узел **Multiply**:
     - A: `k`, B: константа `2.0` → `k2`.
   - Узел **Add**:
     - A: константа `1.0`, B: `k2` → `exp` (степень).
   - Узел **Power**:
     - A: `r`, B: `exp` → `nr`.

7. **Искажённые координаты `distorted = Center + dir * nr`**
   - Узел **Multiply**:
     - A: `dir` (Vector2)
     - B: `nr` (Float)
     - Output → `offsetPolar` (Vector2).
   - Узел **Add**:
     - A: `_Center`
     - B: `offsetPolar`
     - Output → `distorted` (Vector2).

8. **Усмирение за пределами экрана (опционально)**
   - Можно добавить **Clamp** к `distorted`:
     - Min: `(0, 0)`
     - Max: `(1, 1)`
   - Это предотвратит выборку за пределами текстуры.

---

### 6. Использование искажённых координат для выборки текстуры

У нас есть:

- Базовые UV спрайта: `UV_Sprite` (можно взять из:
  - узла **UV** Master Stack, или
  - `Sprite Texture / Sprite UV` если граф тип `Sprite Unlit Graph`).
- Экранные UV: `UV_Screen`.
- Искажённые экранные UV: `distorted`.

Чтобы fisheye был одинаковым по экрану, мы можем сделать:

1. **uvOffset = distorted − UV_Screen**
   - Узел **Subtract**:
     - A: `distorted`
     - B: `UV_Screen`
     - Output → `uvOffset` (Vector2).

2. **finalUV = UV_Sprite + uvOffset**
   - Узел **Add**:
     - A: `UV_Sprite`
     - B: `uvOffset`
     - Output → `finalUV`.

3. Подключи `finalUV` в узел **Sample Texture 2D**:
   - Input UV: `finalUV`.

4. Результат `Sample Texture 2D` умножь на `_Color`:
   - Узел **Multiply**:
     - A: `SampleTextureColor` (RGBA)
     - B: `_Color`
     - Output → `finalColor`.

5. Подключи:
   - `finalColor` → **Color** вход в Master Stack (Sprite Unlit Master / Unlit Master).
   - Alpha уже содержится в `finalColor.a`, можно отдельно подключить в **Alpha** если есть отдельный вход.

---

### 7. Подключение к Master Stack

Для **Sprite Unlit Graph**:

- В Master Stack (Sprite Unlit Master):
  - `Base Color` или `Color` → `finalColor` (RGBA).
  - `Alpha` → `finalColor.a` (если есть отдельный вход).

Для обычного **Unlit Graph**:

- В Master Stack (Unlit Master):
  - `Color` → `finalColor`.
  - При необходимости: включи Alpha Clipping/Transparent в Graph Settings и настрой Surface Type = Transparent.

Не забудь в **Graph Settings**:

- **Surface Type**: Transparent.
- **Blending**: Alpha.
- **Two Sided**: включать/выключать по желанию (обычно Off для UI).

---

### 8. Создание материала и подключение к UIFisheyeEffect

1. Сохрани Shader Graph (CTRL+S).
2. В Project:
   - ПКМ → **Create → Material**.
   - Назови, например, `Mat_UI_Fisheye_SG`.
   - В Inspector у материала:
     - Shader → выбери созданный Shader Graph `UI_Fisheye_Overlay_SG`.
3. Настрой значения по умолчанию:
   - `_Strength` (например, 0.3–0.5).
   - `_Center` = (0.5, 0.5).

4. Открой объект с компонентом `UIFisheyeEffect`:
   - В поле `Fisheye Material` перетащи `Mat_UI_Fisheye_SG`.
   - В поле `Target Canvas` укажи нужный Canvas (Screen Space ‑ Overlay).

Компонент будет подменять материалы `Graphic` на инстансы этого материала и сможет менять `_Strength` / `_Center`, если ты захочешь управлять ими из кода через `MaterialPropertyBlock` или напрямую через сам материал.

---

### 9. Проверка

1. Убедись, что:
   - Canvas = **Screen Space ‑ Overlay**.
   - На каком‑нибудь `Image`/`Text` под этим Canvas стоит любой материал (компонент `UIFisheyeEffect` всё равно его перезапишет).
2. Запусти сцену:
   - Если `UIFisheyeEffect` включён, UI должен слегка «выгибаться» по краям.
   - Изменяй `Strength` в инспекторе у `UIFisheyeEffect` или у материала — и проверь силу эффекта.

Если что‑то не работает (чёрный/фиолетовый цвет):

- Проверь, что материал действительно использует Shader Graph `UI_Fisheye_Overlay_SG`.
- Убедись, что в Graph Settings выбран **Universal Render Pipeline** (при создании Sprite Unlit Graph в URP это обычно делается автоматически).
- Пересобери шейдер (Reimport) при необходимости.


