using UnityEngine;
using Zenject;

namespace GasStation.Core
{
    public class Installer : MonoInstaller
    {
        [SerializeField] private Camera camera;

        public override void InstallBindings()
        {
            Container.Bind<Camera>().FromInstance(camera);
        }
    }
}
