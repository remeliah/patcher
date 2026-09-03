using System;
using System.Runtime.InteropServices;

namespace OsuPatcher.Runtime.Rosu.FFI
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Sliceu8
    {
        private IntPtr data;
        private ulong length;

        public Sliceu8(IntPtr handle, ulong count)
        {
            data = handle;
            length = count;
        }
    }
}
