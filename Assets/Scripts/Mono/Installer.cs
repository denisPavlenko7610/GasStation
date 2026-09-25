using UnityEngine;
using Zenject;

namespace GasStation.Mono
{
    /// <summary>Zenject bindings for the MonoBehaviour layer. ECS systems talk to it through the Bridge classes.</summary>
    public class Installer : MonoInstaller
    {
        [SerializeField] private Camera camera;

        public override void InstallBindings()
        {
            Container.Bind<Camera>().FromInstance(camera);
        }
    }
}
