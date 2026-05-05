using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Ray
    {
        [FieldOffset(0)]
        public Vector3 from;
        [FieldOffset(16)]
        public Vector3 dir;

        public readonly override string ToString() => $"Ray({from} -> {dir})";
    }
}
