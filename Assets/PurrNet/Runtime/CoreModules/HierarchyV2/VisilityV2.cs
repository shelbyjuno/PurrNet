using JetBrains.Annotations;

namespace PurrNet.Modules
{
    internal class VisilityV2
    {
        public delegate void VisibilityDelegate(NetworkIdentity identity, PlayerID player);
        public delegate void VisibilityChangedDelegate(NetworkIdentity identity, PlayerID player, bool visible);
        
        [UsedImplicitly]
        public event VisibilityDelegate onRootVisibilityAdded;
        
        [UsedImplicitly]
        public event VisibilityDelegate onRootVisibilityRemoved;
        
        [UsedImplicitly]
        public event VisibilityChangedDelegate onVisibilityChanged;

        public void Enable()
        {
            
        }
        
        public void Disable()
        {
            
        }
        
        public void EvaluateRoot(NetworkIdentity root)
        {
            
        }
        
        public void EvaluateAll()
        {
            
        }
    }
}