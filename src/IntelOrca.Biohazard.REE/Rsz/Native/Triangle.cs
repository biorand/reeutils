using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct Triangle
    {
        [FieldOffset(0)]
        public Vector3 p0;
        [FieldOffset(16)]
        public Vector3 p1;
        [FieldOffset(32)]
        public Vector3 p2;

        public readonly override string ToString() => $"Triangle({p1} {p1} {p2})";
    }
}
