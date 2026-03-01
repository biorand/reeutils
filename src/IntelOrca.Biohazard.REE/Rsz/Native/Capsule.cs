using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct Capsule
    {
        [FieldOffset(0)]
        public Vector3 Start;
        [FieldOffset(16)]
        public Vector3 End;
        [FieldOffset(32)]
        public float Radius;
    }
}
