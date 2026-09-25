# GasStation

Симулятор заправки на Unity 6 (6000.0.23f1) + DOTS (Entities 1.3, Entities Graphics, Unity Physics, URP).

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
   - `Pump_1`, `Pump_2`: колонки. Игрок взаимодействует с позицией колонки, машина встаёт в
     `StopPoint` и смотрит вдоль его `forward`.
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

## Структура кода

```
Assets/Scripts/                 GasStation.Runtime.asmdef
  Components/  Movement, Player, Cars, Station, Traffic   — ECS-данные
  Authoring/   Movement, Player, Station, Traffic         — MonoBehaviour + Baker
  Systems/     Input, Player, Camera, Time, Traffic, Station, Bridge
  Logic/       StationMath, CarRoutes — чистые формулы, покрыты тестами
  Bridge/      HudModel (ECS → UI), StationCommands (UI → ECS)
  Mono/        HUD, смена дня и ночи, камера, Zenject-инсталлер
  Input/       сгенерированный PlayerInputAction
  Editor/      меню настройки станции (GasStation.Editor.asmdef)
Assets/Tests/EditMode/          EditMode-тесты (Window → General → Test Runner)
```

Цикл одной машины: `Arriving → Queued → DrivingToPump → WaitingForService → Fueling → Leaving`.
