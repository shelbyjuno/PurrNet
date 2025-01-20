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
}
