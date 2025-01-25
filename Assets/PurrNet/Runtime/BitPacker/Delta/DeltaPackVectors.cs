using UnityEngine;

namespace PurrNet.Packing
{
    public static class DeltaPackVectors
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            DeltaPacker<Vector2>.Register(WriteVector2, ReadVector2);
            DeltaPacker<Vector3>.Register(WriteVector3, ReadVector3);
            DeltaPacker<Vector4>.Register(WriteVector4, ReadVector4);
            DeltaPacker<Quaternion>.Register(WriteQuaternion, ReadQuaternion);
        }
        
        private static void WriteVector2(BitPacker packer, Vector2 oldvalue, Vector2 newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Write(packer, oldvalue.x, newvalue.x);
                DeltaPacker<float>.Write(packer, oldvalue.y, newvalue.y);
            }
        }
        
        private static void ReadVector2(BitPacker packer, Vector2 oldvalue, ref Vector2 value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Read(packer, oldvalue.x, ref value.x);
                DeltaPacker<float>.Read(packer, oldvalue.y, ref value.y);
            }
        }
        
        private static void WriteVector3(BitPacker packer, Vector3 oldvalue, Vector3 newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Write(packer, oldvalue.x, newvalue.x);
                DeltaPacker<float>.Write(packer, oldvalue.y, newvalue.y);
                DeltaPacker<float>.Write(packer, oldvalue.z, newvalue.z);
            }
        }
        
        private static void ReadVector3(BitPacker packer, Vector3 oldvalue, ref Vector3 value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Read(packer, oldvalue.x, ref value.x);
                DeltaPacker<float>.Read(packer, oldvalue.y, ref value.y);
                DeltaPacker<float>.Read(packer, oldvalue.z, ref value.z);
            }
        }
        
        private static void WriteVector4(BitPacker packer, Vector4 oldvalue, Vector4 newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Write(packer, oldvalue.x, newvalue.x);
                DeltaPacker<float>.Write(packer, oldvalue.y, newvalue.y);
                DeltaPacker<float>.Write(packer, oldvalue.z, newvalue.z);
                DeltaPacker<float>.Write(packer, oldvalue.w, newvalue.w);
            }
        }
        
        private static void ReadVector4(BitPacker packer, Vector4 oldvalue, ref Vector4 value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Read(packer, oldvalue.x, ref value.x);
                DeltaPacker<float>.Read(packer, oldvalue.y, ref value.y);
                DeltaPacker<float>.Read(packer, oldvalue.z, ref value.z);
                DeltaPacker<float>.Read(packer, oldvalue.w, ref value.w);
            }
        }
        
        private static void WriteQuaternion(BitPacker packer, Quaternion oldvalue, Quaternion newvalue)
        {
            bool hasChanged = oldvalue != newvalue;
            Packer<bool>.Write(packer, hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Write(packer, oldvalue.x, newvalue.x);
                DeltaPacker<float>.Write(packer, oldvalue.y, newvalue.y);
                DeltaPacker<float>.Write(packer, oldvalue.z, newvalue.z);
                DeltaPacker<float>.Write(packer, oldvalue.w, newvalue.w);
            }
        }
        
        private static void ReadQuaternion(BitPacker packer, Quaternion oldvalue, ref Quaternion value)
        {
            bool hasChanged = default;
            Packer<bool>.Read(packer, ref hasChanged);

            if (hasChanged)
            {
                DeltaPacker<float>.Read(packer, oldvalue.x, ref value.x);
                DeltaPacker<float>.Read(packer, oldvalue.y, ref value.y);
                DeltaPacker<float>.Read(packer, oldvalue.z, ref value.z);
                DeltaPacker<float>.Read(packer, oldvalue.w, ref value.w);
            }
        }
    }
}
