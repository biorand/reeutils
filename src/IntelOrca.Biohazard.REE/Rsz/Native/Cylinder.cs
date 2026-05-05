using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct Cylinder
    {
        [FieldOffset(0)]
        public Vector3 p0;
        [FieldOffset(16)]
        public Vector3 p1;
        [FieldOffset(32)]
        public float r;

        public Cylinder()
        {
            r = 1;
        }

        public Cylinder(Vector3 p0, Vector3 p1, float r)
        {
            this.p0 = p0;
            this.p1 = p1;
            this.r = r;
        }

        public readonly override string ToString() => $"Cylinder(P1={p0}, P2={p1}, R={r})";
    }
}
