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
            FloatingPoint = 0;
            
            OnPreTick?.Invoke();
            OnTick?.Invoke();
            OnPostTick?.Invoke();
        }

        public void Update()
        {
            FloatingPoint += Time.unscaledDeltaTime * TickRate;
        }

        /// <summary>
        /// Converts the input tick to float time
        /// </summary>
        /// <param name="tick">The amount of ticks to convert to time</param>
        public float TickToTime(uint tick)
        {
            return tick / (float)TickRate;
        }

        /// <summary>
        /// Converts the precise input tick to float time
        /// </summary>
        /// <param name="preciseTick">The precise tick to convert</param>
        public float PreciseTickToTime(double preciseTick)
        {
            return (float)(preciseTick / TickRate);
        }
        
        /// <summary>
        /// Converts the input float time to ticks
        /// </summary>
        /// <param name="time">The amount of time to convert</param>
        public uint TimeToTick(float time)
        {
            return (uint)(time * TickRate);
        }
        
        /// <summary>
        /// Converts the input float time to precise ticks (double)
        /// </summary>
        /// <param name="time">And amount of time to convert</param>
        public double TimeToPreciseTick(float time)
        {
            return time * TickRate;
        }
    }
}
