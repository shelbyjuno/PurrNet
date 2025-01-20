using UnityEngine;

namespace PurrNet
{
    public interface IMath<T>
    {
        public T Add(T a, T b) => default;

        public T Multiply(T a, T b) => default;
        
        public T Divide(T a, T b) => default;

        public T Negate(T a) => default;

        public T Scale(T a, float b) => default;
    }
    
    public struct Test : IMath<Test>
    {
        private float test;
        private Vector3 vec;
        
        // ignored since math doesn't make sense here
        private bool boolean;
    }
}
