using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PurrNet.Modules
{
    public class TickManager : INetworkModule, IFixedUpdate, IUpdate
    {
        /// <summary>
        /// Tracks local ticks starting from client connection to the server for synchronization.
        /// </summary>
        public uint tick { get; private set; }
        
        /// <summary>
        /// Tracks the ticks aligned with the servers ticks for synchronization.
        /// </summary>
        public uint syncedTick {
            get
            {
                if(_networkManager.isServer)
                    return tick;
                return _syncedTick;
            }
            private set
            {
                _syncedTick = value;
            }
        }
        
        /// <summary>
        /// This is the round trip time. Local time it takes for the client to get a response from the server
        /// </summary>
        public double rtt { get; private set; }
        
        /// <summary>
        /// Uses floating point values for ticks to allow fractional updates, allowing to get precise tick timing within update
        /// </summary>
        public double floatingPoint { get; private set; }

        /// <summary>
        /// Gives the exact step of the tick, including the floating point.
        /// </summary>
        public double preciseTick
        {
            get => tick + floatingPoint;
            private set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                preciseTick = value;
            }
        }
        
        /// <summary>
        /// The amount of ticks per second
        /// </summary>
        public int tickRate { get; private set; }

        /// <summary>
        /// Time between each tick as a float
        /// </summary>
        public readonly float tickDelta;
        
        /// <summary>
        /// Time between each tick as a double
        /// </summary>
        public readonly double tickDeltaDouble;
        
        public event Action onPreTick, onTick, onPostTick;

        private uint _syncedTick;
        private NetworkManager _networkManager;
        private float _lastSyncTime = -99;
        
        public TickManager(int tickRate, NetworkManager nm)
        {
            _networkManager = nm;
            tickDelta = 1f / tickRate;
            tickDeltaDouble = 1d / tickRate;
            this.tickRate = tickRate;
        }

        public void Enable(bool asServer) { }

        public void Disable(bool asServer) { }

        public void FixedUpdate()
        {
            tick++;
            syncedTick++;
            floatingPoint = 0;
            
            onPreTick?.Invoke();
            onTick?.Invoke();
            onPostTick?.Invoke();
        }

        public void Update()
        {
            floatingPoint += Time.unscaledDeltaTime * tickRate;

            if (_networkManager.isServer || !_networkManager.isClient)
                return;
            if(_lastSyncTime + _networkManager.networkRules. < Time.unscaledTime)
            {
                _lastSyncTime = Time.unscaledTime;
                HandleTickSync();
            }
        }

        /// <summary>
        /// Converts the input tick to float time
        /// </summary>
        /// <param name="tick">The amount of ticks to convert to time</param>
        public float TickToTime(uint tick)
        {
            return tick / (float)tickRate;
        }

        /// <summary>
        /// Converts the precise input tick to float time
        /// </summary>
        /// <param name="preciseTick">The precise tick to convert</param>
        public float PreciseTickToTime(double preciseTick)
        {
            return (float)(preciseTick / tickRate);
        }
        
        /// <summary>
        /// Converts the input float time to ticks
        /// </summary>
        /// <param name="time">The amount of time to convert</param>
        public uint TimeToTick(float time)
        {
            return (uint)Math.Round(time * tickRate);
        }
        
        /// <summary>
        /// Converts the input float time to precise ticks (double)
        /// </summary>
        /// <param name="time">And amount of time to convert</param>
        public double TimeToPreciseTick(float time)
        {
            return time * tickRate;
        }

        private async void HandleTickSync()
        {
            float requestSendTime = Time.unscaledTime;
            var rawServerTick = await RPCClass.RequestServerTick();
            rtt = Time.unscaledTime - requestSendTime;
            float halfRTT = (float)rtt / 2;
            syncedTick = rawServerTick + TimeToTick(halfRTT);
        }

        private class RPCClass
        {
            [ServerRpc(requireOwnership: false)]
            public static Task<uint> RequestServerTick()
            {
                return Task.FromResult(NetworkManager.main.tickModule.tick);
            }
        }
    }
}
