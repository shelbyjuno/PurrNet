using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;

namespace PurrNet
{
    public class SyncVar<T> : NetworkModule where T : struct
    {
        const int REDUNDANCY_TICKS = 10;

        private TickManager _tickManager;

        private T _value;

        private bool _isDirty;

        private int _ticksToSync;
        
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

        private void OnTick()
        {
            if (_isDirty)
            {
                _ticksToSync = REDUNDANCY_TICKS;
                _isDirty = false;
            }

            if (_ticksToSync > 0)
            {
            SendValue(_value);
                _ticksToSync--;
            }
        }


        public SyncVar(T initialValue = default)
        {
            _value = initialValue;
        }

        [ObserversRPC(Channel.UnreliableSequenced)]
        private void SendValue(T newValue)
        {
            _value = newValue;
        }

        public static implicit operator T(SyncVar<T> syncVar)
        {
            return syncVar._value;
        }
    }
}