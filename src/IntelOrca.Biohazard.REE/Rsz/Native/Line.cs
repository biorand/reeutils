using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Line
    {
        [FieldOffset(0)]
        public Vector3 from;
        [FieldOffset(16)]
        public Vector3 dir;

        public Line(Vector3 from, Vector3 dir)
        {
            this.from = from;
            this.dir = dir;
        }

        public readonly override string ToString() => $"Line({from} -> {dir})";
    }
}
