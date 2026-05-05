using System.Numerics;

namespace via
{
    public struct Torus
    {
        public Vector3 pos;
        public float r;
        public Vector3 axis;
        public float cr;

        public readonly override string ToString() => $"Torus(pos={pos}, r={r}, axis={axis}, cr={cr})";
    }
}
