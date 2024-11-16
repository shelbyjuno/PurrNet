using JetBrains.Annotations;
using PurrNet.Modules;
using UnityEngine;

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
        
        [UsedByIL]
        public static void Write(this BitStream stream, Vector2 value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Vector2 value)
        {
            stream.Read(ref value.x);
            stream.Read(ref value.y);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Vector3 value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
            stream.Write(value.z);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Vector3 value)
        {
            stream.Read(ref value.x);
            stream.Read(ref value.y);
            stream.Read(ref value.z);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Vector4 value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
            stream.Write(value.z);
            stream.Write(value.w);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Vector4 value)
        {
            stream.Read(ref value.x);
            stream.Read(ref value.y);
            stream.Read(ref value.z);
            stream.Read(ref value.w);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Vector2Int value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Vector2Int value)
        {
            float x = default;
            float y = default;
            stream.Read(ref x);
            stream.Read(ref y);
            value = new Vector2Int((int)x, (int)y);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Vector3Int value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
            stream.Write(value.z);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Vector3Int value)
        {
            float x = default;
            float y = default;
            float z = default;
            stream.Read(ref x);
            stream.Read(ref y);
            stream.Read(ref z);
            value = new Vector3Int((int)x, (int)y, (int)z);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, HalfVector2 value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref HalfVector2 value)
        {
            stream.Read(ref value.x);
            stream.Read(ref value.y);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, HalfVector3 value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
            stream.Write(value.z);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref HalfVector3 value)
        {
            stream.Read(ref value.x);
            stream.Read(ref value.y);
            stream.Read(ref value.z);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, HalfVector4 value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
            stream.Write(value.z);
            stream.Write(value.w);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref HalfVector4 value)
        {
            stream.Read(ref value.x);
            stream.Read(ref value.y);
            stream.Read(ref value.z);
            stream.Read(ref value.w);
        }
   
        [UsedByIL]
        public static void Write(this BitStream stream, Quaternion value)
        {
            value.Normalize();
            
            stream.Write(PackHalf(value.x));
            stream.Write(PackHalf(value.y));
            stream.Write(PackHalf(value.z));
            
            stream.Write(value.w < 0);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Quaternion value)
        {
            ushort xs = default;
            ushort ys = default;
            ushort zs = default;
            
            stream.Read(ref xs);
            stream.Read(ref ys);
            stream.Read(ref zs);
            
            float x = UnpackHalf(xs);
            float y = UnpackHalf(ys);
            float z = UnpackHalf(zs);
            
            bool wSign = false;
            stream.Read(ref wSign);
            
            float w = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y - z * z));
            
            if (wSign)
                w = -w;
            
            value = new Quaternion(x, y, z, w);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Color32 value)
        {
            stream.Write(value.r);
            stream.Write(value.g);
            stream.Write(value.b);
            stream.Write(value.a);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Color32 value)
        {
            byte r = default;
            byte g = default;
            byte b = default;
            byte a = default;
            
            stream.Read(ref r);
            stream.Read(ref g);
            stream.Read(ref b);
            stream.Read(ref a);
            
            value = new Color32(r, g, b, a);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Color value)
        {
            Color32 color32 = value;
            stream.Write(color32);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Color value)
        {
            Color32 color32 = default;
            stream.Read(ref color32);
            value = color32;
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Rect value)
        {
            stream.Write(value.x);
            stream.Write(value.y);
            stream.Write(value.width);
            stream.Write(value.height);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Rect value)
        {
            float x = default;
            float y = default;
            float width = default;
            float height = default;
            
            stream.Read(ref x);
            stream.Read(ref y);
            stream.Read(ref width);
            stream.Read(ref height);
            
            value = new Rect(x, y, width, height);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, Bounds value)
        {
            stream.Write(value.center);
            stream.Write(value.size);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref Bounds value)
        {
            Vector3 center = default;
            Vector3 size = default;
            
            stream.Read(ref center);
            stream.Read(ref size);
            
            value = new Bounds(center, size);
        }
        
        [UsedByIL]
        public static void Write(this BitStream stream, BoundsInt value)
        {
            stream.Write(value.center);
            stream.Write(value.size);
        }
        
        [UsedByIL]
        public static void Read(this BitStream stream, ref BoundsInt value)
        {
            Vector3Int center = default;
            Vector3Int size = default;
            
            stream.Read(ref center);
            stream.Read(ref size);
            
            value = new BoundsInt(center, size);
        }
    }
}