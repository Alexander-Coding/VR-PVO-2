=== VR-персонаж (камера + руки, OpenXR) ===

В сцене SampleScene настроено:

• XR Origin (Hands) — камера (голова) + две руки (hand tracking), без тела.
  Позиция: точка появления (27.87, 0.28, 25.69).

• Перемещение:
  - Телепортация: указать лучом на зелёную зону (Teleportation Area) и нажать кнопку.
  - Непрерывное движение: стики контроллеров (Dynamic Move Provider).
  - Поворот: Snap Turn / Continuous Turn (стики).
  - Подъём: Climb (хват за рейлинги).

• Управление: контроллеры и/или руки (OpenXR через XRI Default Input Actions).

• Main Camera отключена — используется только камера XR Origin.

ЧТО ПРОВЕРИТЬ ВРУЧНУЮ:

1. Edit → Project Settings → XR Plug-in Management:
   - Для платформы "PC, Mac & Linux Standalone" включите "OpenXR".
   - В разделе OpenXR выберите нужный Interaction Profile (например Microsoft Motion Controller).

2. Edit → Project Settings → Player → Other Settings:
   - Active Input Handling = "Input System Package (New)" (для нового Input System).

3. Если телепорт не срабатывает: выберите в сцене "Teleportation Area" и в Inspector в компоненте Teleportation Area при необходимости укажите "Teleportation Provider" = объект XR Origin/Locomotion/Teleportation.

4. Запуск: нажмите Play в редакторе (при подключённом VR-шлеме) или соберите билд под Windows.
