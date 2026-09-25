using System.Collections.Generic;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// Placeholder art built from Unity primitives (props, staff, the cat): one shared material per color and
    /// collider-free parts. Replace with real models later without touching the gameplay code.
    /// </summary>
    public static class PrimitiveArt
    {
        private static readonly Dictionary<Color, Material> Materials = new();

        public static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out var material) && material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            Materials[color] = material;
            return material;
        }

        public static GameObject Part(Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color) =>
            Part(parent, primitive, position, scale, color, Quaternion.identity);

        public static GameObject Part(Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color,
            Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(primitive);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        /// <summary>Paints every renderer under root in one color (the build ghost).</summary>
        public static void Tint(GameObject root, Color color)
        {
            var material = MaterialFor(color);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }
    }
}
