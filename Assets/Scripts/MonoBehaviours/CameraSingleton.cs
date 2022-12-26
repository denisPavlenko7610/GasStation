using UnityEngine;

namespace GasStation.MonoBehaviours
{
    public class CameraSingleton : MonoBehaviour
    {
       public static Camera Camerainstance { get; private set; }
       public static Vector3 CameraOffset { get; private set; }

        private void Awake()
        {
            Camerainstance = Camera.main;
            CameraOffset = transform.position;
        }
    }
}