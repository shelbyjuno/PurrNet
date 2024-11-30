using System;
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
        
        public int tickRate { get; private set; }

        public readonly float tickDelta;
        public readonly double tickDeltaDouble;
        
        public event Action onPreTick, onTick, onPostTick;
        
        public TickManager(int tickRate)
        {
            tickDelta = 1f / tickRate;
            tickDeltaDouble = 1d / tickRate;
            this.tickRate = tickRate;
        }

        public void Enable(bool asServer) { }

        public void Disable(bool asServer)
        {
            
        }

        public void FixedUpdate()
        {
            tick++;
            floatingPoint = 0;
            
            onPreTick?.Invoke();
            onTick?.Invoke();
            onPostTick?.Invoke();
        }

        public void Update()
        {
            floatingPoint += Time.unscaledDeltaTime * tickRate;
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
            return (uint)(time * tickRate);
        }
        
        /// <summary>
        /// Converts the input float time to precise ticks (double)
        /// </summary>
        /// <param name="time">And amount of time to convert</param>
        public double TimeToPreciseTick(float time)
        {
            return time * tickRate;
        }
    }
}
