using System.Collections.Generic;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono.Build
{
    /// <summary>Builds the look of every prop from primitives. Used for placed props and for the build ghost.</summary>
    public static class PropVisuals
    {
        private static readonly Dictionary<Color, Material> Materials = new();

        public static GameObject Create(PropType type, Transform parent)
        {
            var root = new GameObject($"Prop_{type}");
            root.transform.SetParent(parent, false);
            var t = root.transform;

            switch (type)
            {
                case PropType.TrashBin:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), new Color(0.2f, 0.45f, 0.25f));
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.93f, 0f), new Vector3(0.66f, 0.04f, 0.66f), new Color(0.15f, 0.15f, 0.15f));
                    break;
                case PropType.Bench:
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), new Color(0.55f, 0.35f, 0.2f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0.22f), new Vector3(1.8f, 0.5f, 0.06f), new Color(0.55f, 0.35f, 0.2f));
                    Part(t, PrimitiveType.Cube, new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), new Color(0.2f, 0.2f, 0.22f));
                    Part(t, PrimitiveType.Cube, new Vector3(0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), new Color(0.2f, 0.2f, 0.22f));
                    break;
                case PropType.Planter:
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 1.2f), new Color(0.72f, 0.4f, 0.25f));
                    Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0f), new Vector3(1.1f, 0.6f, 1.1f), new Color(0.3f, 0.6f, 0.25f));
                    Part(t, PrimitiveType.Sphere, new Vector3(0.2f, 0.95f, 0.1f), new Vector3(0.25f, 0.2f, 0.25f), new Color(0.95f, 0.35f, 0.5f));
                    break;
                case PropType.Lamp:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(0.15f, 2f, 0.15f), new Color(0.3f, 0.3f, 0.33f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 4.05f, 0f), new Vector3(0.6f, 0.15f, 0.6f), new Color(0.95f, 0.9f, 0.7f));
                    break;
                case PropType.RoadSign:
                    Part(t, PrimitiveType.Cube, new Vector3(-1f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f), new Color(0.6f, 0.6f, 0.6f));
                    Part(t, PrimitiveType.Cube, new Vector3(1f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f), new Color(0.6f, 0.6f, 0.6f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 2.3f, 0f), new Vector3(2.6f, 1.2f, 0.1f), new Color(0.1f, 0.3f, 0.7f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 2.3f, -0.06f), new Vector3(2.2f, 0.2f, 0.02f), Color.white);
                    break;
                case PropType.AirPump:
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 0.4f), new Color(0.8f, 0.15f, 0.12f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.21f), new Vector3(0.35f, 0.25f, 0.02f), new Color(0.9f, 0.9f, 0.85f));
                    Part(t, PrimitiveType.Cylinder, new Vector3(0.3f, 0.5f, 0f), new Vector3(0.06f, 0.4f, 0.06f), new Color(0.1f, 0.1f, 0.1f));
                    break;
                case PropType.WaterMachine:
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.9f, 0.8f), new Color(0.15f, 0.45f, 0.85f));
                    Part(t, PrimitiveType.Cube, new Vector3(-0.15f, 1.15f, -0.41f), new Vector3(0.55f, 1.1f, 0.02f), new Color(0.75f, 0.9f, 1f));
                    break;
                default:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 1.5f, 0.12f), new Color(0.3f, 0.3f, 0.33f));
                    var cam = Part(t, PrimitiveType.Cube, new Vector3(0f, 3f, -0.25f), new Vector3(0.25f, 0.2f, 0.5f), new Color(0.9f, 0.9f, 0.9f));
                    cam.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
                    Part(t, PrimitiveType.Sphere, new Vector3(0f, 2.9f, -0.52f), new Vector3(0.1f, 0.1f, 0.1f), new Color(0.9f, 0.1f, 0.1f));
                    break;
            }

            return root;
        }

        /// <summary>Height of the lamp head, where the light goes.</summary>
        public const float LampHeight = 3.9f;

        private static GameObject Part(Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(primitive);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        public static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out var material) && material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            Materials[color] = material;
            return material;
        }

        /// <summary>Paints every renderer of the ghost in one color (green = can place, red = cannot).</summary>
        public static void Tint(GameObject root, Color color)
        {
            var material = MaterialFor(color);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }
    }
}
