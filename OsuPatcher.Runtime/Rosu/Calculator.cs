using OsuPatcher.Runtime.Rosu.FFI;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OsuPatcher.Runtime.Rosu
{
    public sealed class Calculator
    {
        [DllImport("refx_ffi", CallingConvention = CallingConvention.Cdecl)]
        private static extern CalculateResult calculate_akatsuki_101_from_bytes(
            Sliceu8 beatmap_bytes,
            uint mode,
            uint mods,
            uint max_combo,
            float accuracy,
            uint miss_count,
            Optionu32 passed_objects);

        [DllImport("refx_ffi", CallingConvention = CallingConvention.Cdecl)]
        private static extern CalculateResult calculate_akatsuki_112_from_bytes(
            Sliceu8 beatmap_bytes,
            uint mode,
            uint mods,
            uint max_combo,
            float accuracy,
            uint miss_count,
            Optionu32 passed_objects);

        [DllImport("refx_ffi", CallingConvention = CallingConvention.Cdecl)]
        private static extern CalculateResult calculate_refx_from_bytes(
            Sliceu8 beatmap_bytes,
            uint mode,
            uint mods,
            uint max_combo,
            float accuracy,
            uint miss_count,
            long legacy_score,
            Optionu32 passed_objects);

        private static MethodInfo _getBeatmapStream;
        private readonly object _beatmap;
        private readonly object _mods;

        private byte[] _cachedBeatmap;
        private GCHandle _pinnedHandle;
        private Sliceu8 _beatmapSlice;
        private bool _disposed;

        private static readonly ConcurrentDictionary<Type, FieldInfo[]> _ushortFieldsCache =
            new ConcurrentDictionary<Type, FieldInfo[]>();

        public Calculator(object beatmap, MethodInfo getBeatmapStream, object mods)
        {
            _beatmap = beatmap;
            _getBeatmapStream = getBeatmapStream;
            _mods = mods;
            InitializeBeatmapData();
        }

        private void InitializeBeatmapData()
        {
            if (_beatmap == null || _getBeatmapStream == null)
                return;

            using (Stream stream = (Stream)_getBeatmapStream.Invoke(_beatmap, null))
            {
                if (stream == null)
                    return;

                if (stream.CanSeek)
                {
                    int length = (int)stream.Length;
                    _cachedBeatmap = new byte[length];

                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(_cachedBeatmap, totalRead, length - totalRead);
                        if (read == 0)
                            break;

                        totalRead += read;
                    }
                }
                else
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        _cachedBeatmap = memoryStream.ToArray();
                    }
                }
            }

            if (_cachedBeatmap != null && _cachedBeatmap.Length != 0)
            {
                _pinnedHandle = GCHandle.Alloc(_cachedBeatmap, GCHandleType.Pinned);
                _beatmapSlice = new Sliceu8(_pinnedHandle.AddrOfPinnedObject(), (ulong)((long)_cachedBeatmap.Length));
            }
        }

        private static FieldInfo[] GetUshortFields(Type scoreType)
        {
            FieldInfo[] cached;
            if (_ushortFieldsCache.TryGetValue(scoreType, out cached))
                return cached;

            FieldInfo[] fields = scoreType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(ushort))
                .ToArray();

            _ushortFieldsCache[scoreType] = fields;
            return fields;
        }

        public double CalculateScore(object score, float accuracy, int legacyScore, int maxCombo, int playMode)
        {
            if (_disposed)
                throw new ObjectDisposedException("Calculator");

            if (_cachedBeatmap == null || _cachedBeatmap.Length == 0)
                return 0.0;

            var ushortFields = GetUshortFields(score.GetType());

            if (ushortFields.Length <= 5)
                return 0.0;

            ushort count300 = (ushort)ushortFields[0].GetValue(score);
            ushort count100 = (ushort)ushortFields[1].GetValue(score);
            ushort count50 = (ushort)ushortFields[2].GetValue(score);
            ushort countMiss = (ushort)ushortFields[5].GetValue(score);
            uint passedObjectCount = (uint)(count300 + count100 + count50 + countMiss);

            Optionu32 passedObjects = new Optionu32(passedObjectCount);
            uint mods = (uint)Convert.ToInt32(_mods);

            if (Main.IsRefx)
            {
                return calculate_refx_from_bytes(
                    _beatmapSlice,
                    (uint)playMode,
                    mods,
                    (uint)maxCombo,
                    accuracy,
                    (uint)Convert.ToInt32(countMiss),
                    legacyScore,
                    passedObjects).pp;
            }
            else if (Main.IsAkat)
            {
                // free will
                return calculate_akatsuki_112_from_bytes(
                    _beatmapSlice,
                    (uint)playMode,
                    mods,
                    (uint)maxCombo,
                    accuracy,
                    (uint)Convert.ToInt32(countMiss),
                    passedObjects).pp;
            }

            return calculate_akatsuki_101_from_bytes(
                _beatmapSlice,
                (uint)playMode,
                mods,
                (uint)maxCombo,
                accuracy,
                (uint)Convert.ToInt32(countMiss),
                passedObjects).pp;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_pinnedHandle.IsAllocated)
                _pinnedHandle.Free();

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~Calculator()
        {
            Dispose();
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CalculateResult
    {
        public double pp;
        public double stars;
    }
}
