using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LineSegment
    {
        [FieldOffset(0)]
        public Vector3 start;
        [FieldOffset(16)]
        public Vector3 end;

        public LineSegment(Vector3 start, Vector3 end)
        {
            this.start = start;
            this.end = end;
        }

        public readonly override string ToString() => $"LineSegment({start} -> {end})";
    }
}
