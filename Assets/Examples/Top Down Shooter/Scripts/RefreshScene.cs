using PurrNet;
using UnityEngine;

public class RefreshScene : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && InstanceHandler.NetworkManager.isServer)
            InstanceHandler.NetworkManager.sceneModule.LoadSceneAsync("TopDownShooter_Scene");
    }
}
