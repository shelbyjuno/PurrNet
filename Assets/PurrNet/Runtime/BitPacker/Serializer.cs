using JetBrains.Annotations;
using UnityEngine;

namespace PurrNet.Packing
{
    public struct HalfVector2
    {
        public Half x;
        public Half y;
        
        public static implicit operator Vector2(HalfVector2 value)
        {
            return new Vector2(value.x, value.y);
        }
        
        public static implicit operator HalfVector2(Vector2 value)
        {
            return new HalfVector2
            {
                x = new Half(value.x),
                y = new Half(value.y)
            };
        }
    }
    
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
    
    public struct HalfVector4
    {
        public Half x;
        public Half y;
        public Half z;
        public Half w;
        
        public static implicit operator Vector4(HalfVector4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }
        
        public static implicit operator HalfVector4(Vector4 value)
        {
            return new HalfVector4
            {
                x = new Half(value.x),
                y = new Half(value.y),
                z = new Half(value.z),
                w = new Half(value.w)
            };
        }
    }
}

namespace PurrNet.Packing
{
    [UsedImplicitly]
    public static class BitPackerUnityExtensions
    {
        static ushort PackHalf(float value)
        {
            value = value switch
            {
                // clamp to -1 to 1
                < -1f => -1f,
                > 1f => 1f,
                _ => value
            };
            
            // map -1 to 1 to 0 to 1 and then to 0 to 65535
            return (ushort)((value * 0.5f + 0.5f) * 65535);
        }
        
        static float UnpackHalf(ushort value)
        {
            return value / 65535f * 2f - 1f;
        }
        
        public static void Write(this BitPacker packer, object value)
        {
            throw new System.NotImplementedException();
        }
        
        public static void Read(this BitPacker packer, ref object value)
        {
            throw new System.NotImplementedException();
        }
        
        public static void Pack(this BitPacker packer, ref object value)
        {
            throw new System.NotImplementedException();
        }
        
        public static void Pack<P, T>(this P packer, ref T value) where P : IPack<T>
        {
            if (packer.packer.isReading)
                 packer.Read(ref value);
            else packer.Write(value);
        }
        
        public static void Write<P, T>(this P packer, T value) where P : IPack<T>
        {
            packer.Write(value);
        }
        
        public static void Read<P, T>(this P packer, ref T value) where P : IPack<T>
        {
            packer.Read(ref value);
        }
        
        public static void Write(this BitPacker packer, Vector2 value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
        }
        
        public static void Read(this BitPacker packer, ref Vector2 value)
        {
            packer.Read(ref value.x);
            packer.Read(ref value.y);
        }
        
        public static void Write(this BitPacker packer, Vector3 value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
            packer.Write(value.z);
        }
        
        public static void Read(this BitPacker packer, ref Vector3 value)
        {
            packer.Read(ref value.x);
            packer.Read(ref value.y);
            packer.Read(ref value.z);
        }
        
        public static void Write(this BitPacker packer, Vector4 value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
            packer.Write(value.z);
            packer.Write(value.w);
        }
        
        public static void Read(this BitPacker packer, ref Vector4 value)
        {
            packer.Read(ref value.x);
            packer.Read(ref value.y);
            packer.Read(ref value.z);
            packer.Read(ref value.w);
        }
        
        public static void Write(this BitPacker packer, Vector2Int value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
        }
        
        public static void Read(this BitPacker packer, ref Vector2Int value)
        {
            float x = default;
            float y = default;
            packer.Read(ref x);
            packer.Read(ref y);
            value = new Vector2Int((int)x, (int)y);
        }
        
        public static void Write(this BitPacker packer, Vector3Int value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
            packer.Write(value.z);
        }
        
        public static void Read(this BitPacker packer, ref Vector3Int value)
        {
            float x = default;
            float y = default;
            float z = default;
            packer.Read(ref x);
            packer.Read(ref y);
            packer.Read(ref z);
            value = new Vector3Int((int)x, (int)y, (int)z);
        }
        
        public static void Write(this BitPacker packer, HalfVector2 value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
        }
        
        public static void Read(this BitPacker packer, ref HalfVector2 value)
        {
            packer.Read(ref value.x);
            packer.Read(ref value.y);
        }
        
        public static void Write(this BitPacker packer, HalfVector3 value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
            packer.Write(value.z);
        }
        
        public static void Read(this BitPacker packer, ref HalfVector3 value)
        {
            packer.Read(ref value.x);
            packer.Read(ref value.y);
            packer.Read(ref value.z);
        }
        
        public static void Write(this BitPacker packer, HalfVector4 value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
            packer.Write(value.z);
            packer.Write(value.w);
        }
        
        public static void Read(this BitPacker packer, ref HalfVector4 value)
        {
            packer.Read(ref value.x);
            packer.Read(ref value.y);
            packer.Read(ref value.z);
            packer.Read(ref value.w);
        }
   
        public static void Write(this BitPacker packer, Quaternion value)
        {
            packer.Write(PackHalf(value.x));
            packer.Write(PackHalf(value.y));
            packer.Write(PackHalf(value.z));
        }
        
        public static void Read(this BitPacker packer, ref Quaternion value)
        {
            ushort xs = default;
            ushort ys = default;
            ushort zs = default;
            
            packer.Read(ref xs);
            packer.Read(ref ys);
            packer.Read(ref zs);
            
            float x = UnpackHalf(xs);
            float y = UnpackHalf(ys);
            float z = UnpackHalf(zs);
            float w = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y - z * z));
            
            value = new Quaternion(x, y, z, w);
        }
        
        public static void Write(this BitPacker packer, Color32 value)
        {
            packer.Write(value.r);
            packer.Write(value.g);
            packer.Write(value.b);
            packer.Write(value.a);
        }
        
        public static void Read(this BitPacker packer, ref Color32 value)
        {
            byte r = default;
            byte g = default;
            byte b = default;
            byte a = default;
            
            packer.Read(ref r);
            packer.Read(ref g);
            packer.Read(ref b);
            packer.Read(ref a);
            
            value = new Color32(r, g, b, a);
        }
        
        public static void Write(this BitPacker packer, Color value)
        {
            Color32 color32 = value;
            packer.Write(color32);
        }
        
        public static void Read(this BitPacker packer, ref Color value)
        {
            Color32 color32 = default;
            packer.Read(ref color32);
            value = color32;
        }
        
        public static void Write(this BitPacker packer, Rect value)
        {
            packer.Write(value.x);
            packer.Write(value.y);
            packer.Write(value.width);
            packer.Write(value.height);
        }
        
        public static void Read(this BitPacker packer, ref Rect value)
        {
            float x = default;
            float y = default;
            float width = default;
            float height = default;
            
            packer.Read(ref x);
            packer.Read(ref y);
            packer.Read(ref width);
            packer.Read(ref height);
            
            value = new Rect(x, y, width, height);
        }
        
        public static void Write(this BitPacker packer, Bounds value)
        {
            packer.Write(value.center);
            packer.Write(value.size);
        }
        
        public static void Read(this BitPacker packer, ref Bounds value)
        {
            Vector3 center = default;
            Vector3 size = default;
            
            packer.Read(ref center);
            packer.Read(ref size);
            
            value = new Bounds(center, size);
        }
        
        public static void Write(this BitPacker packer, BoundsInt value)
        {
            packer.Write(value.center);
            packer.Write(value.size);
        }
        
        public static void Read(this BitPacker packer, ref BoundsInt value)
        {
            Vector3Int center = default;
            Vector3Int size = default;
            
            packer.Read(ref center);
            packer.Read(ref size);
            
            value = new BoundsInt(center, size);
        }
    }
}
