using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct Transform
    {
        [FieldOffset(0)]
        public Vector3 pos;
        [FieldOffset(16)]
        public Quaternion rot;
        [FieldOffset(32)]
        public Vector3 scale;

        public Transform(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            this.pos = pos;
            this.rot = rot;
            this.scale = scale;
        }

        public override string ToString() => $"[T: {pos}] [R: {rot}] [S: {scale}]";
    }
}
