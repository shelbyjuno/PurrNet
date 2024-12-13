using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet.Modules
{
    public class HierarchyV2
    {
        private readonly SceneID _sceneId;
        private readonly Scene _scene;
        private readonly ScenePlayersModule _players;
        private readonly VisilityV2 _visibility;
        private readonly HierarchyPool _pool;
        
        public HierarchyV2(SceneID sceneId, Scene scene, ScenePlayersModule players, IPrefabProvider prefabs, bool asServer)
        {
            _sceneId = sceneId;
            _scene = scene;
            _players = players;
            _visibility = new VisilityV2();
            
            var poolParent = new GameObject("PurrNetPool");
            SceneManager.MoveGameObjectToScene(poolParent, scene);

            _pool = new HierarchyPool(poolParent.transform, prefabs);
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