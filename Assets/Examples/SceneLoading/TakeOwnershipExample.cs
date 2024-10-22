using PurrNet;
using PurrNet.Modules;
using UnityEngine;

public class TakeOwnershipExample : MonoBehaviour
{
    [SerializeField] NetworkIdentity _identity;
    [SerializeField] NetworkManager _manager;
    
    [ContextMenu("Request Ownership")]
    public void GiveOwnership()
    {
        if (_manager.TryGetModule<PlayersManager>(false, out var players) && players.localPlayerId.HasValue)
            _identity.GiveOwnership(players.localPlayerId.Value);
    }
    
    [ContextMenu("Remove Ownership")]
    public void RemoveOwnership()
    {
        _identity.RemoveOwnership();
    }
}
