using System.Numerics;

namespace via
{
    public struct Cone
    {
        public Vector3 p0;
        public float r0;
        public Vector3 p1;
        public float r1;

        public Cone(Vector3 p0, float r0, Vector3 p1, float r1)
        {
            this.p0 = p0;
            this.r0 = r0;
            this.p1 = p1;
            this.r1 = r1;
        }

        public readonly override string ToString() => $"Cone({p0} ({r0}), {p1} ({r1}))";
    }
}
