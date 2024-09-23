using System.Collections.Generic;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        [ContextMenu("Spawn")]
        void TestSpawnActions()
        {
            if (!id.HasValue)
                return;
            
            var hierarchy = networkManager.GetModule<HierarchyModule>(isServer);

            if (hierarchy.TryGetHierarchy(sceneId, out var sceneHierarchy))
            {
                var actions = sceneHierarchy.GetActionsToSpawnTarget(new List<NetworkIdentity> {this});
                var actionsAsStr = HierarchyScene.GetActionsAsString(actions);
                Debug.Log(actionsAsStr);
            }
        }
    }
}
