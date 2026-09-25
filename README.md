# GasStation

Симулятор заправки на Unity 6 + DOTS (Entities, Entities Graphics, Unity Physics, URP).
Целевая версия — **Unity 6.6**. Проект пока сохранён в 6000.0.23f1, см. раздел «Переход на Unity 6.6».

План развития: [docs/ROADMAP.md](docs/ROADMAP.md).

## Как запустить геймплей

1. Открой сцену `Assets/Scenes/Day.unity`.
2. В иерархии включи галку у SubScene `Entities`, чтобы открыть её для редактирования, и сделай её
   активной сценой (ПКМ → *Set Active Scene*).
3. Наведи Scene View на место, где должна стоять станция, и выбери меню
   **GasStation → Создать объекты станции**. Появится объект `GasStationSetup`, в нём:
   - `Station`: деньги, время, резервуары и цены (`StationAuthoring`);
   - `CarSpawner`: откуда приезжают машины, маршрут въезда, голова очереди (`QueueHead`,
     очередь растёт назад по его `-forward`) и маршрут выезда (последняя точка удаляет машину).
     Список машин заполняется автоматически из `Prefabs/Environment/Interactable/Cars` и
     `Models/Interactable/Cars`;
   - `Pump_1`…`Pump_4`: колонки. Игрок взаимодействует с позицией колонки, машина встаёт в
     `StopPoint` и смотрит вдоль его `forward`. Колонки 3 и 4 закрыты, пока не куплено улучшение
     «Новая колонка» (поле `requiredUpgradeLevel`).
4. Расставь точки под окружение, закрой SubScene и нажми Play.

Колонки можно вешать и прямо на модели колонок (`PumpAuthoring`). Машины едут вдоль своей оси +Z:
если модель повёрнута иначе, заверни её в префаб с поправленным дочерним объектом.

## Управление

| Клавиша | Действие |
|---|---|
| WASD / стрелки | ходить |
| E / ЛКМ | начать заправку машины у ближайшей колонки |
| 1 / 2 / 3 | выбрать топливо (АИ-92 / АИ-95 / ДТ) |
| + / − | поднять / снизить цену выбранного топлива |
| O | заказать 500 л выбранного топлива |
| Tab | открыть / закрыть улучшения, в открытом окне цифры 1–6 покупают |
| F5 / F9 | сохранить / загрузить |
| F10 дважды | новая игра (удаляет сохранение) |

Игра сама загружает сохранение при старте и сохраняется в конце каждого игрового дня
(`Application.persistentDataPath/savegame.json`). Машины на дороге не сохраняются, оплаченные
бензовозы считаются доставленными.

## Улучшения

| Улучшение | Эффект | Уровней |
|---|---|---|
| Быстрые насосы | +50% скорости заправки за уровень | 3 |
| Большие резервуары | +1000 л к каждому резервуару | 3 |
| Навес и кофе | клиенты ждут на 20% дольше | 3 |
| Реклама | +25% клиентов | 3 |
| Заправщик | сам начинает заправку через 8/уровень с, $80 в день | 3 |
| Новая колонка | открывает колонки 3 и 4 | 2 |

Цена растёт в 1,8 раза с каждым уровнем.

## Звук

`StationAudio` синтезирует звуки при запуске: касса, гудок недовольного клиента, пистолет, бензовоз,
тревога «топливо закончилось», гул насоса. Чтобы заменить их настоящими, добавь `StationAudio` на
объект в сцене и назначь клипы в инспекторе.

## Переход на Unity 6.6

1. Открой проект в Unity 6.6 и согласись на апгрейд.
2. В Package Manager обнови Entities, Entities Graphics, Unity Physics, URP и Input System до версий,
   которые предлагает 6.6. Начиная с Unity 6.4 номера версий DOTS-пакетов совпадают с версией редактора (6.6.x).
3. Пересобери SubScene (закрой и открой её или нажми *Reimport*).

Код уже рассчитан на 6.6:
- нет `IAspect` и `Entities.ForEach`, только `ISystem`, `IJobEntity` и `SystemAPI.Query`;
- нет managed-компонентов, поэтому переход на `CompanionComponent<T>` в 6.6 не затрагивает код;
- нет `FindFirstObjectByType` и перегрузок с `FindObjectsSortMode`, которые устарели при переходе
  с InstanceID на EntityId; используются `FindAnyObjectByType` и `Resources.FindObjectsOfTypeAll`;
- `ComponentLookup.GetRefRWOptional/GetRefROOptional` не используются.

## Структура кода

```
Assets/Scripts/                 GasStation.Runtime.asmdef
  Components/  Movement, Player, Cars, Station, Traffic   — ECS-данные
  Authoring/   Movement, Player, Station, Traffic         — MonoBehaviour + Baker
  Systems/     Input, Player, Camera, Time, Traffic, Station, Bridge
  Logic/       StationMath, CarRoutes — чистые формулы, покрыты тестами
  Bridge/      HudModel (ECS → UI), StationCommands (UI → ECS), GameTexts
  Save/        SaveData (JSON) и SaveService
  Mono/        HUD, звук, смена дня и ночи, камера, Zenject-инсталлер
  Input/       сгенерированный PlayerInputAction
  Editor/      меню настройки станции (GasStation.Editor.asmdef)
Assets/Tests/EditMode/          EditMode-тесты (Window → General → Test Runner)
```

Цикл одной машины: `Arriving → Queued → DrivingToPump → WaitingForService → Fueling → Leaving`.

Игровые события (оплата, ушедший клиент, закончилось топливо, бензовоз и т. д.) Burst-системы пишут в
буфер `StationEvent` на сущности станции. `HudBridgeSystem` раз в кадр забирает их для HUD и звука.
