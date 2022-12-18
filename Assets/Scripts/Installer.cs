using UnityEngine;
using Zenject;

public class Installer : MonoInstaller
{
    [SerializeField] private Camera camera;

    public override void InstallBindings()
    {
        Container.Bind<Camera>().FromInstance(camera);
    }
}
