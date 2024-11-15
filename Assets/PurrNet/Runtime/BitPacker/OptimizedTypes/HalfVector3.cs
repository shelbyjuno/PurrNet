using UnityEngine;

namespace PurrNet.Packing
{
    public struct HalfVector3
    {
        public Half x;
        public Half y;
        public Half z;
        
        public static implicit operator Vector3(HalfVector3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
        
        public static implicit operator HalfVector3(Vector3 value)
        {
            return new HalfVector3
            {
                x = new Half(value.x),
                y = new Half(value.y),
                z = new Half(value.z)
            };
        }
    }
}