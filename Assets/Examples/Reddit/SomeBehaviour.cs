using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;

public class SomeBehaviour : MonoBehaviour
{
    [SerializeField] string lobbySceneName = "Lobby";    
    NetworkManager networkManager;

    void Awake()
    {
        networkManager = InstanceHandler.NetworkManager;
        InstanceHandler.NetworkManager.onClientConnectionState += OnClientConnectionState;
    }

    private void OnClientConnectionState(ConnectionState state)
    {
        if (state != ConnectionState.Connected || !networkManager.isServer)
            return;

        PurrLogger.Log($"Client connected to server. Loading scene {lobbySceneName}");
        networkManager.sceneModule.LoadSceneAsync(lobbySceneName);
    }

    public void StartHost()
    {
        networkManager.StartServer();
        networkManager.StartClient();
    }
    public void StartClient()
    {
        networkManager.StartClient();
    }
}
