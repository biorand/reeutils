using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PaddedVec3
    {
        [FieldOffset(0)] public float x;
        [FieldOffset(4)] public float y;
        [FieldOffset(8)] public float z;

        public PaddedVec3(Vector3 vec)
        {
            x = vec.X;
            y = vec.Y;
            z = vec.Z;
        }

        public PaddedVec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 Vector3 => new Vector3(x, y, z);
        public override string ToString() => $"{x}, {y}, {z}";
    }
}
