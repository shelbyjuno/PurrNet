namespace PurrNet
{
    public interface IMath<T>
    {
        public T Add(T a, T b) => default;

        public T Negate(T a) => default;

        public T Scale(T a, float b) => default;
    }

    public struct SimpleCCState : IMath<SimpleCCState>
    {
        public UnityEngine.Vector3 position;
        public UnityEngine.Vector3 velocity;
    }
}
