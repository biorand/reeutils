using System.Numerics;

namespace via
{
    public struct RayY
    {
        public Vector3 from;
        public float dir;

        public readonly override string ToString() => $"RayY({from}, Dir={dir})";
    }
}
