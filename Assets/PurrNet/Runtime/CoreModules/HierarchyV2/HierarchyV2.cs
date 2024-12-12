using UnityEngine;

namespace PurrNet.Modules
{
    public class HierarchyV2
    {
        private readonly SceneID _scene;
        private readonly ScenePlayersModule _players;
        private readonly VisilityV2 _visibility;
        private readonly HierarchyPool _pool = new ();
        
        
        public HierarchyV2(SceneID scene, ScenePlayersModule players, bool asServer)
        {
            _scene = scene;
            _players = players;
            _visibility = new VisilityV2();
        }
        
        public void Enable()
        {
            _visibility.Enable();
        }

        public void Disable()
        {
            _visibility.Disable();
        }

        public void Spawn(GameObject gameObject)
        {
        }
        
        public void PreNetworkMessages()
        {
            _visibility.EvaluateAll();
        }

        public void PostNetworkMessages()
        {
            
        }
    }
}