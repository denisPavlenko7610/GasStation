using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Motel. This transform is the reception door (the player cleans rooms here); each room is a parking
    /// place in front of it. The Motel upgrade opens rooms two at a time, in order.
    /// </summary>
    public class MotelAuthoring : MonoBehaviour
    {
        public Transform entry;
        public Transform[] rooms;
        public float roomPrice = 40f;
        public uint seed = 17;
    }

    public class MotelBaker : Baker<MotelAuthoring>
    {
        public override void Bake(MotelAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var door = GetComponent<Transform>();
            var entry = authoring.entry != null ? authoring.entry : door;
            DependsOn(entry);

            AddComponent(entity, new Motel
            {
                Door = door.position,
                Entry = entry.position,
                RoomPrice = authoring.roomPrice,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var rooms = AddBuffer<MotelRoom>(entity);
            if (authoring.rooms == null)
                return;

            foreach (var room in authoring.rooms)
            {
                if (room == null)
                    continue;
                DependsOn(room);
                rooms.Add(new MotelRoom { Position = room.position, Rotation = room.rotation, Occupant = Entity.Null });
            }
        }
    }
}
