using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public delegate T LerpFunction<T>(T from, T to, float t);
    
    public class Interpolated<T>
    {
        private LerpFunction<T> _lerp;
        
        public int maxBufferSize { get; set; }
        
        public float tickDelta { get; set; }
        
        private readonly List<T> _buffer;
        
        private T _lastValue;

        private float _timer;
        
        public Interpolated(LerpFunction<T> lerp, float tickDelta, T initialValue = default, int maxBufferSize = 5)
        {
            _lerp = lerp;
            _lastValue = initialValue;
            
            this.maxBufferSize = maxBufferSize;
            this.tickDelta = tickDelta;
            
            _buffer = new List<T>(maxBufferSize);
        }
        
        public void Add(T value)
        {
            _buffer.Add(value);
        }

        public T Advance(float deltaTime)
        {
            if (_buffer.Count <= 0)
                return _lastValue;
        
            float lerp = Mathf.Clamp01(_timer / deltaTime);

            _timer += deltaTime;
        
            var prev = _lastValue;
            var next = _buffer.Count > 0 ?  _buffer[0] : prev;
            var lerped = _lerp(prev, next, lerp);
        
            if (_timer >= Time.fixedDeltaTime)
            {
                if (_buffer.Count > maxBufferSize)
                     _buffer.RemoveRange(0, _buffer.Count - maxBufferSize);
                else _buffer.RemoveAt(0);
            
                _lastValue = lerped;
                _timer -= Time.fixedDeltaTime;
            }
            
            return lerped;
        }
    }
}
