using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Ellipsoid
    {
        [FieldOffset(0)]
        public Vector3 pos;
        [FieldOffset(16)]
        public Vector3 r;

        public readonly override string ToString() => $"Ellipsoid(Pos={pos}, R={r})";
    }
}
