using System;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono.Build
{
    /// <summary>Art-pack models for build-mode props, loaded from Resources/PropModels. Props without a model fall back to primitives.</summary>
    [CreateAssetMenu(menuName = "GasStation/Prop Models", fileName = "PropModels")]
    public class PropModelSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public PropType type;
            public GameObject model;
        }

        public Entry[] entries = Array.Empty<Entry>();

        private static PropModelSet _instance;
        private static bool _loaded;

        public static GameObject ModelFor(PropType type)
        {
            if (!_loaded)
            {
                _instance = Resources.Load<PropModelSet>("PropModels");
                _loaded = true;
            }

            if (_instance == null)
                return null;

            foreach (var entry in _instance.entries)
            {
                if (entry.type == type)
                    return entry.model;
            }

            return null;
        }
    }
}
