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
        private TickManager _tickManager;

        [SerializeField]
        private T _value;

        private bool _isDirty;

        private readonly float _sendIntervalInSeconds;

        public event Action<T> onChanged;

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

                onChanged?.Invoke(value);
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
            float timeSinceLastSend = Time.time - _lastSendTime;

            if (timeSinceLastSend < _sendIntervalInSeconds)
                return;

            if (_isDirty)
            {
                SendValue(_value);
                _lastSendTime = Time.time;
                _isDirty = false;
            }
        }


        public SyncVar(T initialValue = default, float sendIntervalInSeconds = 0f)
        {
            _value = initialValue;
            _sendIntervalInSeconds = sendIntervalInSeconds;
        }

        [ObserversRpc(bufferLast: true)]
        private void SendValue(T newValue)
        {
            if (isServer)
                return;

            if (_value.Equals(newValue))
                return;

            _value = newValue;
            onChanged?.Invoke(value);
        }

        public static implicit operator T(SyncVar<T> syncVar)
        {
            return syncVar._value;
        }

        public override string ToString()
        {
            return value.ToString();
        }
    }
}