using System.Runtime.InteropServices;

namespace OsuPatcher.Runtime.Rosu.FFI
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Optionu32
    {
        private uint value;
        private byte isSome;

        public Optionu32(uint value)
        {
            this.value = value;
            isSome = 1;
        }
    }
}
