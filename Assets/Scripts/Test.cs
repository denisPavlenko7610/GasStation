using UnityEngine;
using Zenject;

public class Test : MonoBehaviour
{
    private Camera camera;

    [Inject]
    private void Construct(Camera camera)
    {
        this.camera = camera;
    }
    
    
    void Start()
    {
        if (camera != null)
        {
            print(1);
        }   
    }
}
