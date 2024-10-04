using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;

namespace PurrNet
{
    internal partial struct NetAnimatorActionBatch : IAutoNetworkedData
    {
        public List<NetAnimatorRPC> actions;
    }
    
    public partial class NetworkAnimator
    {
        readonly List<NetAnimatorRPC> _dirty = new ();

        protected override void OnObserverAdded(PlayerID player)
        {
            PurrLogger.Log("TODO: Implement OnObserverAdded, send current state to new observer.");
        }

        protected override void OnTick(float delta)
        {
            if (!IsController(isController))
            {
                if (_dirty.Count > 0)
                    _dirty.Clear();
                return;
            }
            
            if (_dirty.Count <= 0)
                return;
            
            SendDirtyActions();
            _dirty.Clear();
        }
        
        private void SendDirtyActions()
        {
            var batch = new NetAnimatorActionBatch
            {
                actions = _dirty
            };
            
            if (isServer)
            {
                ApplyActionsOnObservers(batch);
            }
            else
            {
                ForwardThroughServer(batch);
            }
        }
        
        [ServerRPC]
        private void ForwardThroughServer(NetAnimatorActionBatch actions)
        {
            if (_ownerAuth)
                ApplyActionsOnObservers(actions);
        }
        
        [ObserversRPC]
        private void ApplyActionsOnObservers(NetAnimatorActionBatch actions)
        {
            if (IsController(_ownerAuth))
                return;
            
            ExecuteBatch(actions);
        }

        private void ExecuteBatch(NetAnimatorActionBatch actions)
        {
            if (!_animator)
            {
                PurrLogger.LogError($"Animator is null, can't apply actions, dismissing {actions.actions.Count} actions.");
                return;
            }
            
            if (actions.actions == null)
                return;
            
            for (var i = 0; i < actions.actions.Count; i++)
                actions.actions[i].Apply(_animator);
        }
    }
}