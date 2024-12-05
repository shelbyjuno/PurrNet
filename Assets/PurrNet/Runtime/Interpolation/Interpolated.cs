using System.Collections.Generic;
using UnityEngine;

namespace PurrNet
{
    public delegate T LerpFunction<T>(T from, T to, float t);
    
    public class Interpolated<T>
    {
        private readonly LerpFunction<T> _lerp;
        
        public int maxBufferSize { get; set; }
        
        public float tickDelta { get; set; }
        
        private readonly List<T> _buffer;
        
        private T _lastValue;

        private float _timer;
        
        public void Teleport(T value)
        {
            _lastValue = value;
            _buffer.Clear();
            _timer = 0f;
        }
        
        public Interpolated(LerpFunction<T> lerp, float tickDelta, T initialValue = default, int maxBufferSize = 2)
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
            while (true)
            {
                if (_buffer.Count <= 0)
                {
                    _timer = 0f;
                    return _lastValue;
                }

                float lerp = Mathf.Clamp01(_timer / tickDelta);

                _timer += deltaTime;

                var prev = _lastValue;
                var next = _buffer.Count > 0 ? _buffer[0] : prev;
                var lerped = _lerp(prev, next, lerp);

                if (_timer >= tickDelta)
                {
                    if (_buffer.Count > maxBufferSize)
                        _buffer.RemoveRange(0, _buffer.Count - maxBufferSize);
                    else
                        _buffer.RemoveAt(0);

                    _lastValue = lerped;
                    _timer -= tickDelta;

                    if (_timer >= tickDelta)
                    {
                        deltaTime = 0f;
                        continue;
                    }
                }

                return lerped;
            }
        }
    }
}
