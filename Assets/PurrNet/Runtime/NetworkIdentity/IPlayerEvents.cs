namespace PurrNet
{
    public interface IPlayerEvents
    {
        void OnPlayerConnected(PlayerID playerId, bool isReconnect, bool asServer);
        
        void OnPlayerDisconnected(PlayerID playerId, bool asServer);
    }
    
    public interface IServerSceneEvents
    {
        void OnPlayerJoinedScene(PlayerID playerId);
        
        void OnPlayerLeftScene(PlayerID playerId);
    }
}