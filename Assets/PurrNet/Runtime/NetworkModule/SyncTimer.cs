using System;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    public class SyncTimer : NetworkModule
    {
        private bool _ownerAuth;
        private float _reconcileInterval;

        private bool _isRunning;
        private float _remaining;
        private TickManager _tickManager;
        private float _lastReconcile;
        
        public float Remaining => _remaining;
        public bool IsRunning => _isRunning;
        public int RemainingInt => Mathf.CeilToInt(_remaining);
        public Action OnTimerEnd, OnTimerStart, OnTimerSecondTick;
        
        public SyncTimer(bool ownerAuth = false, float reconcileInterval = 3) : base()
        {
            _ownerAuth = ownerAuth;
            _reconcileInterval = reconcileInterval;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            
            networkManager.GetModule<ScenePlayersModule>(isServer).onPlayerJoinedScene += OnPlayerJoinedScene;
            _tickManager = networkManager.GetModule<TickManager>(isServer);
            networkManager.GetModule<TickManager>(isServer).onTick += OnTick;
        }

        private void OnTick()
        {
            if (!_isRunning) return;

            int lastSecond = RemainingInt;
            _remaining -= _tickManager.tickDelta;
            if(lastSecond != RemainingInt)
                OnTimerSecondTick?.Invoke();
            
            if (_ownerAuth && isOwner || !_ownerAuth && isServer)
            {
                if(_remaining <= 0)
                    StopTimer();
                
                if (_lastReconcile + _reconcileInterval < Time.unscaledTime)
                {
                    if (isServer)
                        SendStartTimerToAll(_remaining, localPlayer);
                    else
                        SendStartTimerToServer(_remaining);
                    _lastReconcile = Time.unscaledTime;
                }
            }
        }

        #region Buffering

        private void OnPlayerJoinedScene(PlayerID player, SceneID scene, bool asServer)
        {
            if(_ownerAuth && !isOwner || !_ownerAuth && !isServer) return;
            
            if(isServer)
                BufferPlayer(player, _remaining, _isRunning);
            else
                BufferPlayerServer(player, _remaining, _isRunning);
        }
        
        [ServerRpc(Channel.ReliableOrdered, requireOwnership: true)]
        private void BufferPlayerServer(PlayerID player, float timeRemaining, bool isRunning)
        {
            BufferPlayer(player, timeRemaining, isRunning);
        }

        [TargetRpc]
        private void BufferPlayer(PlayerID player, float timeRemaining, bool isRunning)
        {
            _remaining = timeRemaining;
            _isRunning = isRunning;
        }

        #endregion

        #region StartTimer

        public void StartTimer(float duration)
        {
            if (!isOwner && _ownerAuth || !_ownerAuth && !isServer) return;
            
            _remaining = duration;
            _isRunning = true;
            _lastReconcile = Time.unscaledTime;
            OnTimerStart?.Invoke();

            if (isServer)
            {
                SendStartTimerToAll(_remaining, localPlayer);
            }
            else
                SendStartTimerToServer(_remaining);
        }
        
        [ServerRpc(Channel.ReliableOrdered, requireOwnership:true)]
        private void SendStartTimerToServer(float duration, RPCInfo info = default)
        {
            _isRunning = true;
            SendStartTimerToAll(duration, info.sender);
        }
        
        [ObserversRpc(Channel.ReliableOrdered)]
        private void SendStartTimerToAll(float duration, PlayerID? toIgnore)
        {
            if(toIgnore.HasValue && localPlayer.HasValue && toIgnore.Value.id == localPlayer.Value.id)
                return;
            
            OnTimerStart?.Invoke();
            _remaining = duration;
            _isRunning = true;
        }

        #endregion

        #region StopTimer

        public void StopTimer(bool syncRemaining = false)
        {
            if (!isOwner && _ownerAuth || !_ownerAuth && !isServer) return;
            
            _isRunning = false;
            OnTimerEnd?.Invoke();
            
            var remaining = syncRemaining ? _remaining : -1;
            _remaining = 0;
            
            if (isServer)
                SendStopTimerToAll(remaining);
            else
                SendStopTimerToServer(remaining);
        }

        [ServerRpc(Channel.ReliableOrdered, requireOwnership: true)]
        private void SendStopTimerToServer(float remaining, RPCInfo info = default)
        {
            if (remaining > -1)
                _remaining = remaining;
            _isRunning = false;
            
            SendStopTimerToAll(remaining, info.sender);
        }

        [ObserversRpc(Channel.ReliableOrdered)]
        private void SendStopTimerToAll(float remaining, PlayerID? toIgnore = null)
        {
            if(toIgnore.HasValue && localPlayer.HasValue && toIgnore.Value.id == localPlayer.Value.id)
                return;
            
            if (remaining > -1)
                _remaining = remaining;
            
            _isRunning = false;
            OnTimerEnd?.Invoke();
        }

        #endregion
        
        #region ResumeTimer
        
        public void ResumeTimer()
        {
            if (!isOwner && _ownerAuth || !_ownerAuth && !isServer) return;

            _isRunning = true;
            _lastReconcile = Time.unscaledTime;
            
            if (isServer)
                SendResumeTimerToAll();
            else
                SendResumeTimerToServer();
        }
        
        [ServerRpc(Channel.ReliableOrdered, requireOwnership:true)]
        private void SendResumeTimerToServer()
        {
            _isRunning = true;
            SendResumeTimerToAll();
        }
        
        [ObserversRpc(Channel.ReliableOrdered)]
        private void SendResumeTimerToAll()
        {
            _isRunning = true;
        }
        
        #endregion
        
    }
}
