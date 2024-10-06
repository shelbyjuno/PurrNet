using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Logging;

namespace PurrNet
{
    public partial class NetworkAnimator
    {
        readonly List<NetAnimatorRPC> _dirty = new ();

        protected override void OnObserverAdded(PlayerID player)
        {
            Reconcile(player);
        }

        protected override void OnTick(float delta)
        {
            if (!IsController(isController))
            {
                if (_dirty.Count > 0)
                    _dirty.Clear();
                return;
            }
            
            SendDirtyActions();
        }
        
        /// <summary>
        /// Sends the current state of the animator to the observers.
        /// This is useful when you need to ensure that the observers are in sync with the controller.
        /// </summary>
        public void Reconcile()
        {
            if (!IsController(isController))
                return;
            
            var data = NetAnimatorActionBatch.CreateReconcile(_animator);
            
            if (isServer)
            {
                ApplyActionsOnObservers(data);
            }
            else
            {
                ForwardThroughServer(data);
            }
        }
        
        /// <summary>
        /// Sends the current state of the animator to the target player.
        /// This is useful when a new player joins the scene.
        /// Or when you need to ensure that the player is in sync with the controller.
        /// </summary>
        public void Reconcile(PlayerID target)
        {
            if (!IsController(isController))
                return;
            
            var data = NetAnimatorActionBatch.CreateReconcile(_animator);
            
            if (isServer)
            {
                ReconcileState(target, data);
            }
            else
            {
                ForwardThroughServerToTarget(target, data);
            }
        }
        
        private void SendDirtyActions()
        {
            if (_dirty.Count <= 0)
                return;
            
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
            
            _dirty.Clear();
        }
        
        [TargetRPC]
        private void ReconcileState([UsedImplicitly] PlayerID player, NetAnimatorActionBatch actions)
        {
            if (IsController(_ownerAuth))
                return;
            
            ExecuteBatch(actions);
        }
        
        [ServerRPC]
        private void ForwardThroughServerToTarget(PlayerID target, NetAnimatorActionBatch actions)
        {
            if (_ownerAuth)
                ReconcileState(target, actions);
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