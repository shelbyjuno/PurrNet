using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using MemoryPack;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine.Scripting;

namespace PurrNet.Packets
{
    [UsedImplicitly]
    public interface IAutoNetworkedData { }
    
    public interface INetworkedData
    {
        void Serialize(NetworkStream packer);
    }
    
    public readonly struct NetworkStream
    {
        readonly ByteBuffer _stream;
        
        public readonly bool isReading;
        
        public ByteBuffer buffer => _stream;
        
        public int pointer => _stream.pointer;
        
        public NetworkStream(ByteBuffer stream, bool isReading)
        {
            _stream = stream;
            this.isReading = isReading;
        }
        
        public ByteData Read(int length)
        {
            return _stream.Read(length);
        }
        
        public void Write(ByteData data)
        {
            _stream.Write(data);
        }
        
        public void Serialize(ref INetworkedData data)
        {
            data.Serialize(this);
        }
        
        public void Serialize(ref uint data)
        {
            if (isReading)
                 data = (uint)ReadUnsignedPackedWhole();
            else WriteUnsignedPackedWhole(data);
        }
        
        public void Serialize(ref uint data, bool packed)
        {
            if (packed)
                 Serialize(ref data);
            else Serialize<uint>(ref data);
        }
        
        public void Serialize(ref int data)
        {
            if (isReading)
                data = (int)ReadSignedPackedWhole();
            else WriteSignedPackedWhole(data);
        }
        
        public void Serialize(ref int data, bool packed)
        {
            if (packed)
                 Serialize(ref data);
            else Serialize<int>(ref data);
        }
        
        public void Serialize(ref ushort data)
        {
            if (isReading)
                data = (ushort)ReadUnsignedPackedWhole();
            else WriteUnsignedPackedWhole(data);
        }
        
        public void Serialize(ref ushort data, bool packed)
        {
            if (packed)
                Serialize(ref data);
            else Serialize<ushort>(ref data);
        }
        
        public void Serialize(ref short data)
        {
            if (isReading)
                data = (short)ReadSignedPackedWhole();
            else WriteSignedPackedWhole(data);
        }
                
        public void Serialize(ref short data, bool packed)
        {
            if (packed)
                Serialize(ref data);
            else Serialize<short>(ref data);
        }
        
        public void Serialize<T>(ref T data)
        {
            if (isReading)
            {
                var span = _stream.GetSpan();
                int consumed = MemoryPackSerializer.Deserialize(span, ref data);
                _stream.Advance(consumed);
            }
            else
            {
                MemoryPackSerializer.Serialize(_stream, data);
            }
        }
        
        public void Serialize(Type type, [CanBeNull] ref object data)
        {
            if (isReading)
            {
                var span = _stream.GetSpan();
                int consumed = MemoryPackSerializer.Deserialize(type, span, ref data);
                _stream.Advance(consumed);
            }
            else MemoryPackSerializer.Serialize(_stream, data);
        }
        
        public void SerializeWithType(Type type, [CanBeNull] ref object data)
        {
            if (isReading)
            {
                var span = _stream.GetSpan();
                int consumed = MemoryPackSerializer.Deserialize(type, span, ref data);
                _stream.Advance(consumed);
            }
            else MemoryPackSerializer.Serialize(_stream, data);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void WriteSignedPackedWhole(long value) => WriteUnsignedPackedWhole(ZigZagEncode((ulong)value));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long ReadSignedPackedWhole() => (long)ZigZagDecode(ReadUnsignedPackedWhole());
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void WriteUnsignedPackedWhole(ulong value)
        {
            switch (value)
            {
                case < 0x80UL:
                    _stream.Write((byte)(value & 0x7F));
                    break;
                case < 0x4000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)((value >> 7) & 0x7F));
                    break;
                case < 0x200000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)((value >> 14) & 0x7F));
                    break;
                case < 0x10000000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 14) & 0x7F)));
                    _stream.Write((byte)((value >> 21) & 0x7F));
                    break;
                case < 0x100000000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 14) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 21) & 0x7F)));
                    _stream.Write((byte)((value >> 28) & 0x0F));
                    break;
                case < 0x10000000000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 14) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 21) & 0x7F)));
                    _stream.Write((byte)(0x10 | ((value >> 28) & 0x0F)));
                    _stream.Write((byte)((value >> 32) & 0xFF));
                    break;
                case < 0x1000000000000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 14) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 21) & 0x7F)));
                    _stream.Write((byte)(0x20 | ((value >> 28) & 0x0F)));
                    _stream.Write((byte)((value >> 32) & 0xFF));
                    _stream.Write((byte)((value >> 40) & 0xFF));
                    break;
                case < 0x100000000000000UL:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 14) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 21) & 0x7F)));
                    _stream.Write((byte)(0x30 | ((value >> 28) & 0x0F)));
                    _stream.Write((byte)((value >> 32) & 0xFF));
                    _stream.Write((byte)((value >> 40) & 0xFF));
                    _stream.Write((byte)((value >> 48) & 0xFF));
                    break;
                default:
                    _stream.Write((byte)(0x80 | (value & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 7) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 14) & 0x7F)));
                    _stream.Write((byte)(0x80 | ((value >> 21) & 0x7F)));
                    _stream.Write((byte)(0x40 | ((value >> 28) & 0x0F)));
                    _stream.Write((byte)((value >> 32) & 0xFF));
                    _stream.Write((byte)((value >> 40) & 0xFF));
                    _stream.Write((byte)((value >> 48) & 0xFF));
                    _stream.Write((byte)((value >> 56) & 0xFF));
                    break;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong ReadUnsignedPackedWhole()
        {
            var span = _stream.GetSpan();
            int index = 0;
            
            byte data = span[index++];
            ulong result = (ulong)(data & 0x7F);
            if ((data & 0x80) == 0)
            {
                _stream.Advance(index);
                return result;
            }

            data = span[index++];
            result |= (ulong)(data & 0x7F) << 7;
            if ((data & 0x80) == 0)
            {
                _stream.Advance(index);
                return result;
            }

            data = span[index++];
            result |= (ulong)(data & 0x7F) << 14;
            if ((data & 0x80) == 0)
            {
                _stream.Advance(index);
                return result;
            }

            data = span[index++];
            result |= (ulong)(data & 0x7F) << 21;
            if ((data & 0x80) == 0)
            {
                _stream.Advance(index);
                return result;
            }

            data = span[index++];
            result |= (ulong)(data & 0x0F) << 28;
            int extraBytes = data >> 4;

            switch (extraBytes)
            {
                case 0:
                    break;
                case 1:
                    result |= (ulong)span[index++] << 32;
                    break;
                case 2:
                    result |= (ulong)span[index++] << 32;
                    result |= (ulong)span[index++] << 40;
                    break;
                case 3:
                    result |= (ulong)span[index++] << 32;
                    result |= (ulong)span[index++] << 40;
                    result |= (ulong)span[index++] << 48;
                    break;
                case 4:
                    result |= (ulong)span[index++] << 32;
                    result |= (ulong)span[index++] << 40;
                    result |= (ulong)span[index++] << 48;
                    result |= (ulong)span[index++] << 56;
                    break;
            }

            _stream.Advance(index);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong ZigZagDecode(ulong value)
        {
            ulong sign = value << 63;
            if (sign > 0)
                return ~(value >> 1) | sign;
            return value >> 1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong ZigZagEncode(ulong value)
        {
            if (value >> 63 > 0)
                return ~(value << 1) | 1;
            return value << 1;
        }

        public void ResetPointer()
        {
            _stream.ResetPointer();
        }
    }

    public class NetworkRegister
    {
        [UsedByIL]
        public static void Register<T>() where T : NetworkIdentity
        {
            MemoryPackFormatterProvider.Register(new NetworkIdentityFormatter<T>());
        }
    }

#pragma warning disable CS9074
    
    [Preserve]
    internal class NetworkedDataFormatter<T> : MemoryPackFormatter<T> where T : INetworkedData, new()
    {
        static bool IsNullable(T obj)
        {
            if (obj == null) return true; // obvious
            var type = typeof(T);
            if (!type.IsValueType) return true; // ref-type
            return Nullable.GetUnderlyingType(type) != null; // Nullable<T>
        }
        
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref T value)
        {
            if (IsNullable(value))
            {
                bool isNull = value == null;
                writer.WriteUnmanaged(isNull);
                if (isNull) return;
            }
            
            var dataStream = ByteBufferPool.Alloc();
            var stream = new NetworkStream(dataStream, false);
            value.Serialize(stream);
            writer.WriteUnmanagedSpan(dataStream.ToByteData().span);
            ByteBufferPool.Free(dataStream);
        }

        public override void Deserialize(ref MemoryPackReader reader, ref T value)
        {
            if (IsNullable(value))
            {
                if (reader.ReadUnmanaged<bool>())
                {
                    value = default;
                    return;
                }

                value ??= new T();
            }
            else
            {
                value = new T();
            }
            
            var dataStream = ByteBufferPool.Alloc();
            var stream = new NetworkStream(dataStream, true);

            Span<byte> data = default;
            reader.ReadUnmanagedSpan(ref data);
            dataStream.Write(data);
            dataStream.ResetPointer();
            value.Serialize(stream);
            ByteBufferPool.Free(dataStream);
        }
    }
    
    [Preserve]
    public class NetworkIdentityFormatter<T> : MemoryPackFormatter<T> where T : NetworkIdentity
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref T value)
        {
            bool isNull = value == null;
            writer.WriteUnmanaged(isNull);
            
            if (isNull) return;
        
            var id = value.id;
            bool hasId = id.HasValue;
            
            writer.WriteUnmanaged(hasId);

            if (hasId)
            {
                var idValue = id!.Value;
                writer.WriteUnmanaged(idValue);
                writer.WriteUnmanaged(value.sceneId);
            }
        }

        public override void Deserialize(ref MemoryPackReader reader, ref T value)
        {
            value = null;

            if (reader.ReadUnmanaged<bool>())
                return;
        
            bool hasId = reader.ReadUnmanaged<bool>();
            
            if (!hasId)
                return;
            
            var networkId = reader.ReadUnmanaged<NetworkID>();
            var sceneId = reader.ReadUnmanaged<SceneID>();
            
            var networkManager = NetworkManager.main;

            if (!networkManager.TryGetModule<HierarchyModule>(true, out var module) ||
                !module.TryGetIdentity(sceneId, networkId, out var result) || 
                result is not T castedResult) return;
            
            value = castedResult;
        }
    }
    
    [Preserve]
    internal sealed class UnmanagedFormatterUnsage<T> : MemoryPackFormatter<T>
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(
            ref MemoryPackWriter<TBufferWriter> writer,
            ref T value)
        {
            Unsafe.WriteUnaligned(ref writer.GetSpanReference(Unsafe.SizeOf<T>()), value);
            writer.Advance(Unsafe.SizeOf<T>());
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, ref T value)
        {
            value = Unsafe.ReadUnaligned<T>(ref reader.GetSpanReference(Unsafe.SizeOf<T>()));
            reader.Advance(Unsafe.SizeOf<T>());
        }
    }
#pragma warning restore CS9074
}
