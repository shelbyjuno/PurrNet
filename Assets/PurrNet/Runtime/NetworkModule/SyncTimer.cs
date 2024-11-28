using System;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    public class SyncTimer : NetworkModule, ITick
    {
        private readonly bool _ownerAuth;
        private readonly float _reconcileInterval;

        private bool _isRunning;
        private float _remaining;
        private float _lastReconcile;
        
        public float remaining => _remaining;
        public bool isRunning => _isRunning;

        public int remainingInt => Mathf.CeilToInt(_remaining);
        
        public Action onTimerEnd, onTimerStart, onTimerSecondTick;
        
        public SyncTimer(bool ownerAuth = false, float reconcileInterval = 3)
        {
            _ownerAuth = ownerAuth;
            _reconcileInterval = reconcileInterval;
        }

        private void LogValue(int obj)
        {
            PurrLogger.Log($"Remaining: {obj}");
        }

        public void OnTick(float delta)
        {
            if (!_isRunning) return;

            int lastSecond = remainingInt;
            _remaining -= delta;
            if(lastSecond != remainingInt)
                onTimerSecondTick?.Invoke();
            
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

        public override void OnObserverAdded(PlayerID player)
        {
            BufferPlayer(player, _remaining, _isRunning);
        }

        [TargetRpc]
        private void BufferPlayer([UsedImplicitly] PlayerID player, float timeRemaining, bool running)
        {
            _remaining = timeRemaining;
            _isRunning = running;
        }

        #endregion

        #region StartTimer

        public void StartTimer(float duration)
        {
            if (!IsController(_ownerAuth)) return;
            
            _remaining = duration;
            _isRunning = true;
            _lastReconcile = Time.unscaledTime;
            onTimerStart?.Invoke();

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
            
            onTimerStart?.Invoke();
            _remaining = duration;
            _isRunning = true;
        }

        #endregion

        #region StopTimer

        public void StopTimer(bool syncRemaining = false)
        {
            if (!isOwner && _ownerAuth || !_ownerAuth && !isServer) return;
            
            _isRunning = false;
            onTimerEnd?.Invoke();
            
            var remainingTime = syncRemaining ? _remaining : -1;
            _remaining = 0;
            
            if (isServer)
                SendStopTimerToAll(remainingTime, localPlayer);
            else
                SendStopTimerToServer(remainingTime);
        }

        [ServerRpc(Channel.ReliableOrdered, requireOwnership: true)]
        private void SendStopTimerToServer(float remainingTime, RPCInfo info = default)
        {
            if (remainingTime > -1)
                _remaining = remainingTime;
            _isRunning = false;
            
            SendStopTimerToAll(remainingTime, info.sender);
        }

        [ObserversRpc(Channel.ReliableOrdered)]
        private void SendStopTimerToAll(float remainingTime, PlayerID? toIgnore = null)
        {
            if(toIgnore.HasValue && localPlayer.HasValue && toIgnore.Value.id == localPlayer.Value.id)
                return;
            
            if (remainingTime > -1)
                _remaining = remainingTime;
            
            _isRunning = false;
            onTimerEnd?.Invoke();
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
