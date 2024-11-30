using PurrNet;
using PurrNet.Modules;
using UnityEngine;

public class SomeBehaviour : PurrMonoBehaviour
{
    public override void Subscribe(NetworkManager manager, bool asServer)
    {
        var players = manager.GetModule<PlayersManager>(asServer);
        players.onPlayerJoined += OnPlayerJoined;
    }

    public override void Unsubscribe(NetworkManager manager, bool asServer)
    {
        var players = manager.GetModule<PlayersManager>(asServer);
        players.onPlayerJoined += OnPlayerJoined;
    }
    
    static void OnPlayerJoined(PlayerID player, bool isReconnect, bool asserver)
    {
        Debug.Log($"Player {player} joined. Reconnect: {isReconnect}. As server: {asserver}");
    }
}
