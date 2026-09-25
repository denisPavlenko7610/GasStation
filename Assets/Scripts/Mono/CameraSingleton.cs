using UnityEngine;

namespace GasStation.Mono
{
    public class CameraSingleton : MonoBehaviour
    {
        [SerializeField] private float followDistance = 5f;

        public static Camera Instance { get; private set; }
        public static Vector3 Offset { get; private set; }

        private void Awake()
        {
            Instance = Camera.main;
            Offset = new Vector3(0f, transform.position.y, -followDistance);
        }
    }
}
