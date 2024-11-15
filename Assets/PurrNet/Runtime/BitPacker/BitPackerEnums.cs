using System;
using System.Collections.Generic;

namespace PurrNet.Packing
{
    public partial class BitStream
    {
        internal struct EnumCachedData
        {
            public ulong min;
            public ulong max;
        }
        
        static readonly Dictionary<Type, EnumCachedData> _enumData = new ();
        
        public void PackEnum<T>(ref T data) where T : Enum
        {
            var range = GetEnumRange<T>();
            var type = Enum.GetUnderlyingType(typeof(T));
            var rawdata = Convert.ChangeType(data, type);
            ulong d = Convert.ToUInt64(rawdata);
            
            Pack(ref d, range.min, range.max);
            
            if (_isReading)
                data = (T)Enum.ToObject(typeof(T), d);
        }

        private static EnumCachedData GetEnumRange<T>() where T : Enum
        {
            if (!_enumData.TryGetValue(typeof(T), out var cachedData))
            {
                ulong min = ulong.MaxValue;
                ulong max = ulong.MinValue;

                var values = Enum.GetValues(typeof(T));

                if (values.Length == 0)
                {
                    min = 0;
                    max = 0;
                }
                else
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        ulong val = Convert.ToUInt64(Convert.ChangeType(values.GetValue(i), typeof(ulong)));

                        if (val < min)
                            min = val;

                        if (val > max)
                            max = val;
                    }
                }

                cachedData = new EnumCachedData
                {
                    min = min,
                    max = max
                };

                _enumData[typeof(T)] = cachedData;
            }
            
            return cachedData;
        }
    }
}
