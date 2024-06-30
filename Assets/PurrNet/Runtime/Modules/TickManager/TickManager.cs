using System;
using UnityEngine;

namespace PurrNet.Modules
{
    public class TickManager : INetworkModule, IFixedUpdate, IUpdate
    {
        /// <summary>
        /// Tracks local ticks starting from client connection to the server for synchronization.
        /// </summary>
        public uint Tick { get; private set; }
        
        /// <summary>
        /// Uses floating point values for ticks to allow fractional updates, allowing to get precise tick timing within update
        /// </summary>
        public double FloatingPoint { get; private set; }

        /// <summary>
        /// Gives the exact step of the tick, including the floating point.
        /// </summary>
        public double PreciseTick
        {
            get => Tick + FloatingPoint;
            private set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                PreciseTick = value;
            }
        }
        
        public int TickRate { get; private set; }
        
        public Action OnPreTick, OnTick, OnPostTick;

        public TickManager(int tickRate)
        {
            TickRate = tickRate;
        }

        public void Enable(bool asServer)
        {
            
        }

        public void Disable(bool asServer)
        {
            
        }

        public void FixedUpdate()
        {
            Tick++;
            
            OnPreTick?.Invoke();
            OnTick?.Invoke();
            OnPostTick?.Invoke();
        }

        public void Update()
        {
            double elapsedTimeSinceLastFixedUpdate = Time.time - Time.fixedTime;
            FloatingPoint = elapsedTimeSinceLastFixedUpdate * TickRate;
        }
    }
}
