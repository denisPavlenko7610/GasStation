# Архитектура

Короткая карта кода: где что лежит, как текут данные и как добавить новую систему, не ломая остальное.

## Слои

```
Authoring (MonoBehaviour + Baker)      → сцена и SubScene: что есть на уровне
Components (IComponentData, буферы)    → данные ECS, без логики
Logic (*Math, каталоги)                → чистые функции и константы баланса, покрыты тестами
Systems (ISystem / SystemBase)         → симуляция в StationSystemGroup
Bridge (HudModel, StationCommands, …)  → граница между ECS и интерфейсом
Mono (HUD, меню, ноутбук, presenters)  → всё, что видит и нажимает игрок
Save (SaveData, SaveService)           → JSON-сохранение
Localization (LocTable, Loc)           → тексты RU/EN
Editor                                 → меню Unity: строитель уровня, настройка локализации
```

Правило зависимостей: `Logic` не знает про системы и UI; системы не знают про Mono. Интерфейс не
пишет в ECS напрямую — только через `StationCommands`.

## Поток данных за кадр

1. `PlayerInputSystem` (первым) читает ввод, если не открыт интерфейс (`GamePause`, `BuildMode`,
   `PhotoMode`, `LaptopState`).
2. Системы `StationSystemGroup` двигают симуляцию и пишут события в буфер `StationEvent` на станции.
   Полночь — событие `DayEnded` от `GameTimeSystem`; все ночные расчёты ловят его через
   `StationEvent.Contains(events, StationEventType.DayEnded)`.
3. Системы с `OrderLast` (задания, финансы, контракты, клиенты, коллекции, достижения) читают события
   этого кадра. Последним идёт `AutoSaveSystem`, поэтому сохранение видит уже всю ночную обработку.
4. `HudBridgeSystem` (PresentationSystemGroup) копирует состояние в статический `HudModel`, превращает
   события в сообщения и очищает буфер.
5. Mono-классы в `Update` читают `HudModel` и отправляют действия игрока в `StationCommands`;
   `StationCommandSystem` применяет их в следующем кадре.

## Договорённости

- **Состояние станции** — синглтоны на сущности станции (`StationAuthoring`): экономика, финансы,
  конкурент, клиенты, контракты, навыки, сезон… Доступ: `SystemAPI.GetSingleton…` после
  `RequireForUpdate` или `HasSingleton`.
- **Баланс** — только в `Logic/*Math.cs` (константы и формулы). В системах чисел баланса нет.
- **Burst** — все `ISystem` с `[BurstCompile]`; управляемые данные (строки, `static readonly` массивы
  классов, `Loc`, `HudModel`) — только в `SystemBase` (`StationCommandSystem`, `VisitorSystem`,
  `HudBridgeSystem`, `QuestSystem`, `AutoSaveSystem`).
- **События** — `StationEvent { Type, Fuel, Value, Subject }`: `Value` — число (деньги, звёзды),
  `Subject` — о ком (id постоянника, контракта, штата). Смысл полей описан в комментарии к типу события.
- **Большие классы** разбиты на `partial`-файлы по темам: `StationCommandSystem.*.cs`,
  `HudBridgeSystem.*.cs`, `StationHud.*.cs`, `LaptopController.*.cs`, `SaveService.*.cs`.
- **Графика-заглушка** — `PrimitiveArt` (примитивы и материалы). Объекты, персонал и кот рисуются
  Mono-презентерами по данным из `HudModel`; ECS хранит только данные. Замена на модели не трогает
  геймплей.
- **Сохранения** — каждое изменение формата поднимает `SaveData.CurrentVersion`, новые поля получают
  значения «как в старой игре» (null, -1 или явный инициализатор), а `SaveService.Apply` восстанавливает
  их после старых. Перед релизом версии стоит «схлопнуть» (см. NEXT_PLAN).

## Как добавить систему (пример: «автомойка самообслуживания»)

1. **Компонент** в `Components/…` — данные и короткий комментарий, что это.
2. **Логика** в `Logic/…Math.cs` — формулы и константы + EditMode-тест в `Tests/EditMode`.
3. **Authoring/Baker**, если нужна точка в сцене, или добавить компонент в `StationBaker`.
4. **Система** в `Systems/…`: `[BurstCompile]`, `RequireForUpdate` на всё, что читает, явный порядок
   (`UpdateAfter`/`UpdateBefore`) только там, где он важен; события — через `StationEvent.Push`.
5. **Команды игрока** — метод в `StationCommands` + обработчик в нужном `StationCommandSystem.*.cs`.
6. **Мост и UI** — поле в `HudModel`, копирование в `HudBridgeSystem`, сообщение в
   `HudBridgeSystem.Messages.cs`, панель в `StationHud.Panels.cs` или приложение ноутбука.
7. **Тексты** — ключи в `LocTable` (оба языка, одинаковые `{0}`), затем меню
   `GasStation → Localization → Setup`.
8. **Сохранение** — поля в `SaveData` (+1 к версии), `Capture…`/`Restore…` в `SaveService.Systems.cs`.
9. **Уровень** — точки и заглушки в `DesertLevelBuilder`, при необходимости запретная зона строительства.
