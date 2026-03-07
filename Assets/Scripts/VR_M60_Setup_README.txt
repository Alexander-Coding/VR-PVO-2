=== VR + OpenXR: GunPlatform, M60 захват и стрельба ===

  ★ Если не можете взаимодействовать с пушками контроллерами — откройте чек-лист:
    Assets/Scripts/VR_Interaction_Checklist.txt
    Там по пунктам: что обязательно должно быть и где это проверить (Hierarchy / Inspector).

0) XR INTERACTION MANAGER — КАК И КУДА ДОБАВИТЬ
   • XR Interaction Manager нужен, чтобы контроллеры (лучи/руки) и объекты (XR Grab Interactable) могли взаимодействовать. Без него захват и триггер не работают.
   • КУДА: в Hierarchy должен быть один объект с компонентом XR Interaction Manager. Обычно его делают отдельным объектом в корне сцены (рядом с XR Origin, камерой и т.д.).
   • КАК ДОБАВИТЬ:
     1) В Hierarchy: ПКМ → Create Empty. Назовите объект, например, "XR Interaction Manager".
     2) Выберите этот объект, в Inspector нажмите Add Component.
     3) В поиске введите "XR Interaction Manager" и выберите компонент (пакет XR Interaction Toolkit).
   • Контроллеры в XR Origin обычно сами находят менеджер в сцене (поле Interaction Manager может быть пустым — тогда поиск по сцене). Если у вас несколько сцен или нестандартная иерархия, в компонентах контроллеров (Left/Right Controller) можно вручную указать этот XR Interaction Manager в поле Interaction Manager.

1) GUNPLATFORM И ПОСАДКА ПЕРСОНАЖА
   • В сцене создайте пустой объект (ПКМ в Hierarchy → Create Empty) и назовите его "GunPlatform".
   • Поставьте GunPlatform туда, где должен стоять игрок при старте (например, на платформе с пушкой).
   • На любой объект сцены (например, на XR Origin или на пустой "GameSetup") добавьте компонент VRSetupManager.
   • При старте игры XR Origin будет перемещён в позицию и поворот GunPlatform.

2) СТАРТОВЫЙ ЗВУК (Good Morning Vietnam)
   • В VRSetupManager в поле Startup Clip перетащите клип Assets/Sound/Good_Morning_Vietnam_budilnik.mp3
     ИЛИ создайте папку Assets/Resources/Sound/ и положите туда копию файла с тем же именем (без пути Assets/) — тогда он подхватится по пути "Sound/Good_Morning_Vietnam_budilnik".

3) ЗАХВАТ M60 И M60-1 КОНТРОЛЛЕРАМИ (OpenXR)
   • Убедитесь, что в сцене есть XR Origin (XR Rig) с контроллерами (Left/Right Controller) и XR Interaction Manager.
   • На объект "m60" (корень пушки):
     - Добавьте Rigidbody (если ещё нет): Use Gravity можно включить; скрипт GunStableSpawn при старте сделает пушку кинематической, чтобы не падала.
     - Добавьте компонент Gun Stable Spawn — пушки не будут падать и проваливаться под землю при старте; после отпускания из рук снова будут подчиняться физике.
     - Добавьте компонент XR Grab Interactable (Component → XR → XR Grab Interactable).
     - Добавьте компонент M60 VR Shoot.
     - Если m60-1 — отдельный объект (не дочерний к m60), на m60-1 тоже добавьте Rigidbody, Gun Stable Spawn, XR Grab Interactable и M60 VR Shoot.
   • Коллайдеры: на m60 (и m60-1) должны быть коллайдеры, иначе луч контроллера не попадёт и при падении пушка может провалиться. У пола/земли тоже должен быть коллайдер (Mesh Collider или Box Collider). При необходимости добавьте на пушку Box Collider или объедините дочерние коллайдеры в списке Colliders у XR Grab Interactable.
   • M60ShellEjection должен висеть на корне m60 (как в M60_Setup_README.txt). M60 VR Shoot сам найдёт его на этом объекте или у родителя.

4) СТРЕЛЬБА, ГИЛЬЗЫ И ЗВУК (КНОПКА — ТРИГГЕР)
   • Стрельба привязана к кнопке активации (Activate) XR Grab Interactable. По умолчанию в OpenXR это триггер контроллера. Пока держите m60, при нажатии триггера вызываются:
     - M60ShellEjection.Fire() — выброс гильз из обоих стволов;
     - случайный звук из tush_net_1..4.
   • Звуки выстрела: в M60 VR Shoot перетащите в Shoot Clips элементы tush_net_1..4 из Assets/Sound/
     ИЛИ положите эти файлы в Assets/Resources/Sound/ (tush_net_1.mp3 … tush_net_4.mp3) — скрипт подгрузит их по имени.

5) ТОЧКА ОПОРЫ (m60 receiver) И РЕЖИМ «ТУРЕЛЬ»
   • Точка опоры и захвата по умолчанию — дочерний объект "m60 receiver". M60 VR Shoot при старте подставляет его в Pivot Point и в Attach Transform у XR Grab Interactable.
   • Чтобы при захвате точка опоры оставалась неподвижной в мире (режим закреплённой турели — только наведение вверх/вниз и в стороны), на m60 добавлен компонент Gun Pivot Lock Grab Transformer и он указан в XR Grab Interactable → Starting Single Grab Transformers. Pivot Transform в трансформере можно оставить пустым — тогда используется pivot из M60 VR Shoot (m60 receiver).
   • Вариант без турели: не добавляйте Gun Pivot Lock Grab Transformer; оружие будет следовать за контроллером как обычно.

6) КРАТКО
   • GunPlatform — пустой объект с именем "GunPlatform"; VRSetupManager ставит на него XR Origin и играет стартовый звук.
   • m60 (и при необходимости m60-1): XR Grab Interactable + M60 VR Shoot + Gun Pivot Lock Grab Transformer (для режима турели); коллайдеры обязательны.
   • Стрельба: кнопка активации (триггер) в VR → гильзы (M60ShellEjection.Fire) + случайный tush_net.
   • Точка опоры: m60 receiver (подставляется автоматически); при использовании Gun Pivot Lock Grab Transformer остаётся неподвижной в мире при захвате.

7) ЕСЛИ НЕ РАБОТАЕТ ВЗАИМОДЕЙСТВИЕ С ПУШКАМИ
   • Откройте чек-лист: Assets/Scripts/VR_Interaction_Checklist.txt
   • Обязательно проверьте: XR Interaction Manager в сцене; на контроллерах — XR Ray Interactor (или Near Far); на m60 — XR Grab Interactable + Collider + Rigidbody; Interaction Layer Mask на пушке и интеракторе должны пересекаться (например, оба Default или пушка Everything); слой m60 не Ignore Raycast.
