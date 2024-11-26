using PurrNet;
using UnityEngine;

public class NTChangeOwner : NetworkBehaviour
{
    [ContextMenu("Take Ownership")]
    private void TakeOwnershipOfController()
    {
        GiveOwnership(localPlayer);
    }
}
