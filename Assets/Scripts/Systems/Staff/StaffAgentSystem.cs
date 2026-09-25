using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Living staff: every worker has a body on the lot that picks the nearest job of its role, walks there
    /// and does it. Attendants start fueling waiting cars, janitors pick up litter and clean the restroom,
    /// mechanics service worn pumps, cashiers stand at the shop counter. Off shift they go home.
    /// How fast they work depends on StaffMath.Efficiency (skill, energy, mood, trait).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInteractionSystem))]
    [UpdateBefore(typeof(FuelingSystem))]
    public partial struct StaffAgentSystem : ISystem
    {
        private const float IdleRecheck = 1.5f;

        private struct Job
        {
            public Entity Target;
            public float3 Point;
            public float Condition;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float hour = SystemAPI.GetSingleton<GameTime>().Hour;
            float deltaTime = SystemAPI.Time.DeltaTime;
            float3 home = Home(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var workers = new NativeHashMap<int, Worker>(8, Allocator.Temp);
            foreach (var worker in SystemAPI.Query<RefRO<Worker>>())
                workers.TryAdd(worker.ValueRO.Id, worker.ValueRO);

            // Bodies for new workers, none for those who left.
            var withBody = new NativeHashSet<int>(8, Allocator.Temp);
            foreach (var (agent, entity) in SystemAPI.Query<RefRO<StaffAgent>>().WithEntityAccess())
            {
                if (workers.ContainsKey(agent.ValueRO.WorkerId))
                    withBody.Add(agent.ValueRO.WorkerId);
                else
                    ecb.DestroyEntity(entity);
            }

            foreach (var worker in SystemAPI.Query<RefRO<Worker>>())
            {
                if (!withBody.Contains(worker.ValueRO.Id))
                    SpawnBody(ecb, worker.ValueRO, home);
            }

            // Everything that needs doing, minus what someone already took.
            var claimed = new NativeHashSet<Entity>(8, Allocator.Temp);
            bool restroomTaken = false;
            foreach (var agent in SystemAPI.Query<RefRO<StaffAgent>>())
            {
                if (agent.ValueRO.Target != Entity.Null)
                    claimed.Add(agent.ValueRO.Target);
                restroomTaken |= agent.ValueRO.Job == AgentJob.CleanRestroom;
            }

            var cars = new NativeList<Job>(Allocator.Temp);
            foreach (var (car, entity) in SystemAPI.Query<RefRO<Car>>().WithEntityAccess())
            {
                var pump = car.ValueRO.Pump;
                if (car.ValueRO.State == CarState.WaitingForService && pump != Entity.Null && SystemAPI.HasComponent<Pump>(pump) &&
                    !claimed.Contains(entity))
                    cars.Add(new Job { Target = entity, Point = SystemAPI.GetComponent<Pump>(pump).InteractionPoint });
            }

            var trash = new NativeList<Job>(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Trash>().WithEntityAccess())
            {
                if (!claimed.Contains(entity))
                    trash.Add(new Job { Target = entity, Point = transform.ValueRO.Position });
            }

            var pumps = new NativeList<Job>(Allocator.Temp);
            foreach (var (pump, entity) in SystemAPI.Query<RefRO<Pump>>().WithEntityAccess())
            {
                if (pump.ValueRO.Condition < StaffMath.PumpWornForMechanic && !claimed.Contains(entity))
                    pumps.Add(new Job { Target = entity, Point = pump.ValueRO.InteractionPoint, Condition = pump.ValueRO.Condition });
            }

            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            foreach (var (agent, movement, path, transform) in SystemAPI
                         .Query<RefRW<StaffAgent>, RefRW<CarMovement>, DynamicBuffer<PathPoint>, RefRO<LocalTransform>>())
            {
                ref var a = ref agent.ValueRW;
                if (!workers.TryGetValue(a.WorkerId, out var worker))
                    continue;

                a.Efficiency = StaffMath.Efficiency(worker, hour);
                movement.ValueRW.Speed = StaffMath.AgentWalkSpeed * StaffMath.WalkFactor(worker.Trait) *
                                         (0.8f + 0.2f * StaffMath.EnergyFactor(worker.Energy));

                if (a.Efficiency <= 0f)
                {
                    if (a.Task != AgentTask.OffDuty)
                        GoHome(ref a, path, home);
                    continue;
                }

                if (a.Task == AgentTask.OffDuty)
                {
                    a.Task = AgentTask.Idle;
                    a.Timer = 0f;
                }

                float3 position = transform.ValueRO.Position;
                switch (a.Task)
                {
                    case AgentTask.Idle:
                        a.Timer -= deltaTime;
                        if (a.Timer > 0f)
                            break;
                        if (!PickJob(ref state, ref a, path, position, worker.Role, cars, trash, pumps, ref restroomTaken))
                            a.Timer = IdleRecheck;
                        break;

                    case AgentTask.Walking:
                        if (!StillNeeded(ref state, a))
                        {
                            Drop(ref a, path);
                            break;
                        }

                        if (path.IsEmpty)
                        {
                            a.Task = AgentTask.Working;
                            a.Timer = JobTime(a.Job) / math.max(0.1f, a.Efficiency);
                        }
                        break;

                    case AgentTask.Working:
                        Work(ref state, ref a, path, worker, ecb, events, deltaTime);
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            workers.Dispose();
            withBody.Dispose();
            claimed.Dispose();
            cars.Dispose();
            trash.Dispose();
            pumps.Dispose();
        }

        private float3 Home(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<StaffSettings>())
                return SystemAPI.GetSingleton<StaffSettings>().Home;
            if (SystemAPI.HasSingleton<Shop>())
                return SystemAPI.GetSingleton<Shop>().Door;
            return float3.zero;
        }

        private static void SpawnBody(EntityCommandBuffer ecb, in Worker worker, float3 home)
        {
            var body = ecb.CreateEntity();
            ecb.AddComponent(body, LocalTransform.FromPosition(home));
            ecb.AddComponent(body, new StaffAgent { WorkerId = worker.Id, Role = worker.Role, Task = AgentTask.OffDuty });
            ecb.AddComponent(body, new CarMovement { Speed = StaffMath.AgentWalkSpeed, TurnSpeed = 8f });
            ecb.AddBuffer<PathPoint>(body);
        }

        private static void GoHome(ref StaffAgent agent, DynamicBuffer<PathPoint> path, float3 home)
        {
            agent.Task = AgentTask.OffDuty;
            agent.Job = AgentJob.None;
            agent.Target = Entity.Null;
            path.Clear();
            path.Add(new PathPoint { Position = home });
        }

        private static void Drop(ref StaffAgent agent, DynamicBuffer<PathPoint> path)
        {
            agent.Task = AgentTask.Idle;
            agent.Job = AgentJob.None;
            agent.Target = Entity.Null;
            agent.Timer = 0f;
            path.Clear();
        }

        private static void Go(ref StaffAgent agent, DynamicBuffer<PathPoint> path, AgentJob job, Entity target, float3 point)
        {
            agent.Task = AgentTask.Walking;
            agent.Job = job;
            agent.Target = target;
            path.Clear();
            path.Add(new PathPoint { Position = point });
        }

        private bool PickJob(ref SystemState state, ref StaffAgent agent, DynamicBuffer<PathPoint> path, float3 position,
            StaffRole role, NativeList<Job> cars, NativeList<Job> trash, NativeList<Job> pumps, ref bool restroomTaken)
        {
            switch (role)
            {
                case StaffRole.Attendant:
                    return TakeNearest(ref agent, path, position, cars, AgentJob.FuelCar);

                case StaffRole.Janitor:
                    if (TakeNearest(ref agent, path, position, trash, AgentJob.PickTrash))
                        return true;
                    if (restroomTaken || !SystemAPI.HasSingleton<Restroom>())
                        return false;
                    var restroom = SystemAPI.GetSingleton<Restroom>();
                    if (restroom.Dirt < StaffMath.RestroomDirtyForJanitor)
                        return false;
                    restroomTaken = true;
                    Go(ref agent, path, AgentJob.CleanRestroom, Entity.Null, restroom.Door);
                    return true;

                case StaffRole.Mechanic:
                    // The most worn pump first, not the nearest.
                    int worst = -1;
                    for (int i = 0; i < pumps.Length; i++)
                    {
                        if (worst < 0 || pumps[i].Condition < pumps[worst].Condition)
                            worst = i;
                    }

                    if (worst < 0)
                        return false;
                    Go(ref agent, path, AgentJob.RepairPump, pumps[worst].Target, pumps[worst].Point);
                    pumps.RemoveAtSwapBack(worst);
                    return true;

                case StaffRole.Cook:
                    if (agent.Job == AgentJob.Grill || !SystemAPI.HasSingleton<Diner>())
                        return false;
                    Go(ref agent, path, AgentJob.Grill, Entity.Null, SystemAPI.GetSingleton<Diner>().Grill);
                    return true;

                default:
                    if (agent.Job == AgentJob.Counter || !SystemAPI.HasSingleton<Shop>())
                        return false;
                    Go(ref agent, path, AgentJob.Counter, Entity.Null, SystemAPI.GetSingleton<Shop>().Door + new float3(0f, 0f, 1.5f));
                    return true;
            }
        }

        private static bool TakeNearest(ref StaffAgent agent, DynamicBuffer<PathPoint> path, float3 position, NativeList<Job> jobs, AgentJob kind)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < jobs.Length; i++)
            {
                float distance = math.distancesq(position.xz, jobs[i].Point.xz);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            if (best < 0)
                return false;

            Go(ref agent, path, kind, jobs[best].Target, jobs[best].Point);
            jobs.RemoveAtSwapBack(best);
            return true;
        }

        private bool StillNeeded(ref SystemState state, in StaffAgent agent)
        {
            switch (agent.Job)
            {
                case AgentJob.FuelCar:
                    return SystemAPI.Exists(agent.Target) && SystemAPI.HasComponent<Car>(agent.Target) &&
                           SystemAPI.GetComponent<Car>(agent.Target).State == CarState.WaitingForService;
                case AgentJob.PickTrash:
                    return SystemAPI.Exists(agent.Target) && SystemAPI.HasComponent<Trash>(agent.Target);
                case AgentJob.RepairPump:
                    return SystemAPI.Exists(agent.Target) && SystemAPI.HasComponent<Pump>(agent.Target);
                case AgentJob.CleanRestroom:
                    return SystemAPI.HasSingleton<Restroom>();
                default:
                    return true;
            }
        }

        private static float JobTime(AgentJob job) => job switch
        {
            AgentJob.FuelCar => StaffMath.AttendantServiceTime,
            AgentJob.PickTrash => StaffMath.TrashPickupTime,
            _ => 0f
        };

        private void Work(ref SystemState state, ref StaffAgent agent, DynamicBuffer<PathPoint> path, in Worker worker,
            EntityCommandBuffer ecb, DynamicBuffer<StationEvent> events, float deltaTime)
        {
            if (!StillNeeded(ref state, agent))
            {
                Drop(ref agent, path);
                return;
            }

            float rate = agent.Efficiency * StaffMath.JobFactor(worker.Trait) * deltaTime;
            switch (agent.Job)
            {
                case AgentJob.FuelCar:
                {
                    agent.Timer -= deltaTime;
                    if (agent.Timer > 0f)
                        return;
                    var car = SystemAPI.GetComponentRW<Car>(agent.Target);
                    car.ValueRW.State = CarState.Fueling;
                    StationEvent.Push(events, StationEventType.FuelingStarted, car.ValueRO.FuelType);
                    // A chatty attendant makes the customer's day.
                    if (worker.Trait == StaffTrait.Chatty && SystemAPI.HasSingleton<Economy>())
                    {
                        ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
                        economy.Reputation = StationMath.ClampReputation(economy.Reputation + StaffMath.ChattyReputation);
                    }

                    Drop(ref agent, path);
                    return;
                }

                case AgentJob.PickTrash:
                    agent.Timer -= deltaTime;
                    if (agent.Timer > 0f)
                        return;
                    // Janitor work counts for cleanliness only, not for the player's cleanup quests.
                    ecb.DestroyEntity(agent.Target);
                    Drop(ref agent, path);
                    return;

                case AgentJob.CleanRestroom:
                {
                    ref var restroom = ref SystemAPI.GetSingletonRW<Restroom>().ValueRW;
                    restroom.Dirt = math.max(0f, restroom.Dirt - StaffMath.RestroomCleaningPerSecond * rate);
                    if (restroom.Dirt <= 0.02f)
                        Drop(ref agent, path);
                    return;
                }

                case AgentJob.RepairPump:
                {
                    ref var pump = ref SystemAPI.GetComponentRW<Pump>(agent.Target).ValueRW;
                    pump.Condition = math.min(1f, pump.Condition + StaffMath.MechanicRepairRate * rate);
                    if (pump.Condition < 1f)
                        return;
                    // Subject 1: repaired by staff, which does not count for the player's repair quests.
                    StationEvent.Push(events, StationEventType.PumpRepaired, default, pump.Number, 1);
                    Drop(ref agent, path);
                    return;
                }

                default:
                    // Cashier: stays at the counter until the shift ends.
                    return;
            }
        }
    }
}
