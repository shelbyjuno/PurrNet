using UnityEngine;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;
using System;

namespace PurrNet
{
    [Serializable]
    public class SyncVar<T> : NetworkModule where T : struct
    {
        const int REDUNDANCY_TICKS = 10;

        private TickManager _tickManager;

        [SerializeField]
        private T _value;

        private bool _isDirty;

        private int _ticksToSync;

        private readonly float _sendIntervalInSeconds;

        public T value
        {
            get => _value;
            set
            {
                if (!isServer)
                {
                    PurrLogger.LogError("Only server can change the value of SyncVar.");
                    return;
                }
                
                if (value.Equals(_value))
                    return;

                _value = value;
                _isDirty = true;
            }
        }

        public override void OnSpawn()
        {
            _tickManager = networkManager.GetModule<TickManager>(isServer);
            _tickManager.onTick += OnTick;
        }

        public override void OnDespawned()
        {
            _tickManager.onTick -= OnTick;
        }

        private float _lastSendTime;

        private void OnTick()
        {
            if (_isDirty)
            {
                _ticksToSync = REDUNDANCY_TICKS;
                _isDirty = false;
            }

            float timeSinceLastSend = Time.time - _lastSendTime;

            if (timeSinceLastSend < _sendIntervalInSeconds)
                return;

            if (_ticksToSync > 0)
            {
                SendValue(_value);
                _lastSendTime = Time.time;
                _ticksToSync--;
            }
        }


        public SyncVar(T initialValue = default, float sendIntervalInSeconds = 0f)
        {
            _value = initialValue;
            _sendIntervalInSeconds = sendIntervalInSeconds;
        }

        [ObserversRPC(Channel.UnreliableSequenced, bufferLast: true)]
        private void SendValue(T newValue)
        {
            if (isServer)
                return;
            _value = newValue;
        }

        public static implicit operator T(SyncVar<T> syncVar)
        {
            return syncVar._value;
        }
    }
}