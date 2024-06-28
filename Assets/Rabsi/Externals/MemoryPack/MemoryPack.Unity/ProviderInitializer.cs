#nullable enable

using MemoryPack.Formatters;
using UnityEngine;

namespace MemoryPack
{
    public static class MemoryPackUnityFormatterProviderInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void RegisterInitialFormatters()
        {
            // struct
            UnityRegister<Vector2>();
            UnityRegister<Vector3>();
            UnityRegister<Vector4>();
            UnityRegister<Quaternion>();
            UnityRegister<Color>();
            UnityRegister<Bounds>();
            UnityRegister<Rect>();
            UnityRegister<Keyframe>();
            MemoryPackFormatterProvider.Register(new UnmanagedFormatter<WrapMode>());
            UnityRegister<Matrix4x4>();
            UnityRegister<GradientColorKey>();
            UnityRegister<GradientAlphaKey>();
            MemoryPackFormatterProvider.Register(new UnmanagedFormatter<GradientMode>());
            UnityRegister<Color32>();
            UnityRegister<LayerMask>();
            UnityRegister<Vector2Int>();
            UnityRegister<Vector3Int>();
            UnityRegister<RangeInt>();
            UnityRegister<RectInt>();
            UnityRegister<BoundsInt>();
            
            // class
            if (!MemoryPackFormatterProvider.IsRegistered<AnimationCurve>())
            {
                MemoryPackFormatterProvider.Register(new AnimationCurveFormatter());
                MemoryPackFormatterProvider.Register(new ArrayFormatter<AnimationCurve>());
                MemoryPackFormatterProvider.Register(new ListFormatter<AnimationCurve>());
            }
            if (!MemoryPackFormatterProvider.IsRegistered<Gradient>())
            {
                MemoryPackFormatterProvider.Register(new GradientFormatter());
                MemoryPackFormatterProvider.Register(new ArrayFormatter<Gradient>());
                MemoryPackFormatterProvider.Register(new ListFormatter<Gradient>());
            }
            if (!MemoryPackFormatterProvider.IsRegistered<RectOffset>())
            {
                MemoryPackFormatterProvider.Register(new RectOffsetFormatter());
                MemoryPackFormatterProvider.Register(new ArrayFormatter<RectOffset>());
                MemoryPackFormatterProvider.Register(new ListFormatter<RectOffset>());
            }
        }

        static void UnityRegister<T>()
            where T : unmanaged
        {
            MemoryPackFormatterProvider.Register(new UnmanagedFormatter<T>());
            MemoryPackFormatterProvider.Register(new UnmanagedArrayFormatter<T>());
            MemoryPackFormatterProvider.Register(new ListFormatter<T>());
            MemoryPackFormatterProvider.Register(new NullableFormatter<T>());
        }
    }
}
