using GasStation.Components;
using GasStation.Mono.Scenery;
using UnityEngine;

namespace GasStation.Mono.Build
{
    /// <summary>
    /// Builds the look of every prop: the art-pack model from PropModelSet when there is one, primitives
    /// (PrimitiveArt) otherwise. Used for placed props and for the build ghost (which gets no colliders).
    /// </summary>
    public static class PropVisuals
    {
        public static GameObject Create(PropType type, Transform parent, bool ghost = false)
        {
            var root = new GameObject($"Prop_{type}");
            root.transform.SetParent(parent, false);
            var t = root.transform;

            var model = PropModelSet.ModelFor(type);
            if (model != null)
            {
                var instance = Object.Instantiate(model, t, false);
                if (ghost)
                {
                    foreach (var collider in instance.GetComponentsInChildren<Collider>())
                        Object.Destroy(collider);
                }

                return root;
            }

            switch (type)
            {
                case PropType.TrashBin:
                    PrimitiveArt.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), new Color(0.2f, 0.45f, 0.25f));
                    PrimitiveArt.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.93f, 0f), new Vector3(0.66f, 0.04f, 0.66f), new Color(0.15f, 0.15f, 0.15f));
                    break;
                case PropType.Bench:
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), new Color(0.55f, 0.35f, 0.2f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0.22f), new Vector3(1.8f, 0.5f, 0.06f), new Color(0.55f, 0.35f, 0.2f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), new Color(0.2f, 0.2f, 0.22f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), new Color(0.2f, 0.2f, 0.22f));
                    break;
                case PropType.Planter:
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 1.2f), new Color(0.72f, 0.4f, 0.25f));
                    PrimitiveArt.Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0f), new Vector3(1.1f, 0.6f, 1.1f), new Color(0.3f, 0.6f, 0.25f));
                    PrimitiveArt.Part(t, PrimitiveType.Sphere, new Vector3(0.2f, 0.95f, 0.1f), new Vector3(0.25f, 0.2f, 0.25f), new Color(0.95f, 0.35f, 0.5f));
                    break;
                case PropType.Lamp:
                    PrimitiveArt.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(0.15f, 2f, 0.15f), new Color(0.3f, 0.3f, 0.33f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 4.05f, 0f), new Vector3(0.6f, 0.15f, 0.6f), new Color(0.95f, 0.9f, 0.7f));
                    break;
                case PropType.RoadSign:
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(-1f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f), new Color(0.6f, 0.6f, 0.6f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(1f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f), new Color(0.6f, 0.6f, 0.6f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 2.3f, 0f), new Vector3(2.6f, 1.2f, 0.1f), new Color(0.1f, 0.3f, 0.7f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 2.3f, -0.06f), new Vector3(2.2f, 0.2f, 0.02f), Color.white);
                    break;
                case PropType.AirPump:
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 0.4f), new Color(0.8f, 0.15f, 0.12f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.21f), new Vector3(0.35f, 0.25f, 0.02f), new Color(0.9f, 0.9f, 0.85f));
                    PrimitiveArt.Part(t, PrimitiveType.Cylinder, new Vector3(0.3f, 0.5f, 0f), new Vector3(0.06f, 0.4f, 0.06f), new Color(0.1f, 0.1f, 0.1f));
                    break;
                case PropType.WaterMachine:
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.9f, 0.8f), new Color(0.15f, 0.45f, 0.85f));
                    PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(-0.15f, 1.15f, -0.41f), new Vector3(0.55f, 1.1f, 0.02f), new Color(0.75f, 0.9f, 1f));
                    break;
                default:
                    PrimitiveArt.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 1.5f, 0.12f), new Color(0.3f, 0.3f, 0.33f));
                    var cam = PrimitiveArt.Part(t, PrimitiveType.Cube, new Vector3(0f, 3f, -0.25f), new Vector3(0.25f, 0.2f, 0.5f), new Color(0.9f, 0.9f, 0.9f));
                    cam.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
                    PrimitiveArt.Part(t, PrimitiveType.Sphere, new Vector3(0f, 2.9f, -0.52f), new Vector3(0.1f, 0.1f, 0.1f), new Color(0.9f, 0.1f, 0.1f));
                    break;
            }

            return root;
        }

        /// <summary>Height of the lamp head, where the light goes.</summary>
        public const float LampHeight = 3.9f;
    }
}
